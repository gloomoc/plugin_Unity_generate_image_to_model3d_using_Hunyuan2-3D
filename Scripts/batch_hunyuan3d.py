#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import sys
import argparse
import time
import uuid
import shutil
from pathlib import Path
from glob import glob
import torch
import torch._dynamo
torch._dynamo.config.suppress_errors = True
import trimesh
from PIL import Image
from tqdm import tqdm
import json

# Add the project directory to the path
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

def setup_imports():
    """Imports the necessary modules from Hunyuan3D"""
    try:
        # Main imports from Hunyuan3D
        from hy3dgen.shapegen import (
            FaceReducer, 
            FloaterRemover, 
            DegenerateFaceRemover, 
            Hunyuan3DDiTFlowMatchingPipeline
        )
        from hy3dgen.shapegen.pipelines import export_to_trimesh
        from hy3dgen.rembg import BackgroundRemover
        
        # Optional imports for texturing
        try:
            from hy3dgen.texgen import Hunyuan3DPaintPipeline
            HAS_TEXTUREGEN = True
        except Exception:
            print("Warning: Texture generation not available. Install requirements per README.md")
            HAS_TEXTUREGEN = False
            Hunyuan3DPaintPipeline = None
        
        # Optional imports for text-to-image
        try:
            from hy3dgen.text2image import HunyuanDiTPipeline
            HAS_T2I = True
        except Exception:
            print("Warning: Text-to-image not available.")
            HAS_T2I = False
            HunyuanDiTPipeline = None
            
        return {
            'FaceReducer': FaceReducer,
            'FloaterRemover': FloaterRemover,
            'DegenerateFaceRemover': DegenerateFaceRemover,
            'Hunyuan3DDiTFlowMatchingPipeline': Hunyuan3DDiTFlowMatchingPipeline,
            'export_to_trimesh': export_to_trimesh,
            'BackgroundRemover': BackgroundRemover,
            'Hunyuan3DPaintPipeline': Hunyuan3DPaintPipeline,
            'HunyuanDiTPipeline': HunyuanDiTPipeline,
            'HAS_TEXTUREGEN': HAS_TEXTUREGEN,
            'HAS_T2I': HAS_T2I
        }
    except ImportError as e:
        print(f"Error importing Hunyuan3D modules: {e}")
        print("Make sure you are running the script from the Hunyuan3D-2 repository directory")
        sys.exit(1)

def is_image_file(file_path):
    """Checks if a file is a supported image"""
    supported_formats = ['.jpg', '.jpeg', '.png', '.bmp', '.webp', '.tiff']
    return any(str(file_path).lower().endswith(fmt) for fmt in supported_formats)

class HunyuanBatchProcessor:
    def __init__(self, args):
        """
        Initializes the processor following exactly the pattern of gradio_app.py
        """
        self.args = args
        self.device = args.device
        
        # Configure output directory
        self.output_dir = args.output
        os.makedirs(self.output_dir, exist_ok=True)
        
        print("Initializing Hunyuan3D Batch Processor...")
        print(f"Model: {args.model_path}/{args.subfolder}")
        print(f"Device: {args.device}")
        print(f"Output format: {args.file_type.upper()}")
        
        # Verify FBX support if necessary
        if args.file_type.lower() == 'fbx':
            if not self._check_fbx_dependencies():
                print("Warning: FBX dependencies not available. Conversion will be done via intermediate formats.")
        
        # Import modules
        self.modules = setup_imports()
        
        # Initialize workers exactly like gradio_app.py
        self._init_workers()
        
        print("Models loaded successfully!\n")
    
    def _check_fbx_dependencies(self):
        """Checks the necessary dependencies for FBX"""
        fbx_methods = []
        
        print("  Verifying FBX dependencies...")
        
        # Method 1: bpy (Blender Python API) - with timeout
        print("    Trying Blender Python API (bpy)...")
        try:
            import importlib.util
            import sys
            import signal
            
            def timeout_handler(signum, frame):
                raise TimeoutError("Import bpy took too long")
            
            # Only on Unix/Linux systems
            if hasattr(signal, 'SIGALRM'):
                signal.signal(signal.SIGALRM, timeout_handler)
                signal.alarm(5)  # 5 seconds timeout
            
            try:
                # Verify if bpy exists without importing it completely
                spec = importlib.util.find_spec("bpy")
                if spec is not None:
                    # Try a quick import
                    import bpy
                    fbx_methods.append('bpy')
                    print("      ✓ Blender Python API (bpy) available")
                else:
                    print("      ✗ bpy not found")
            finally:
                if hasattr(signal, 'SIGALRM'):
                    signal.alarm(0)  # Cancel timeout
                    
        except (ImportError, TimeoutError, Exception) as e:
            print(f"      ✗ bpy not available: {str(e)[:50]}...")
        
        # Method 2: pymeshlab - faster
        print("    Trying PyMeshLab...")
        try:
            import pymeshlab
            fbx_methods.append('pymeshlab')
            print("      ✓ PyMeshLab available")
        except ImportError:
            print("      ✗ PyMeshLab not available")
        
        # Method 3: Open3D
        print("    Trying Open3D...")
        try:
            import open3d as o3d
            fbx_methods.append('open3d')
            print("      ✓ Open3D available")
        except ImportError:
            print("      ✗ Open3D not available")
        
        self.fbx_methods = fbx_methods
        
        if not fbx_methods:
            print("      ⚠️ No FBX method available")
            print("         Recommendation: pip install pymeshlab open3d")
            print("         For bpy: pip install bpy (can take a long time)")
            print("         The script will continue with OBJ format as a fallback")
        else:
            print(f"      ✓ Available FBX methods: {', '.join(fbx_methods)}")
        
        return len(fbx_methods) > 0
    
    def _init_workers(self):
        """Initializes all workers following gradio_app.py"""
        
        # Background remover
        print("Loading Background Remover...")
        self.rmbg_worker = self.modules['BackgroundRemover']()
        
        # Shape generation pipeline
        print(f"Loading 3D generation pipeline...")
        self.i23d_worker = self.modules['Hunyuan3DDiTFlowMatchingPipeline'].from_pretrained(
            self.args.model_path,
            subfolder=self.args.subfolder,
            use_safetensors=True,
            device=self.args.device,
        )
        
        # Activate optimizations if available
        if self.args.enable_flashvdm:
            mc_algo = 'mc' if self.args.device in ['cpu', 'mps'] else self.args.mc_algo
            self.i23d_worker.enable_flashvdm(mc_algo=mc_algo)
        
        if self.args.compile:
            print("Compiling model...")
            self.i23d_worker.compile()
        
        # Post-processing workers
        self.floater_remove_worker = self.modules['FloaterRemover']()
        self.degenerate_face_remove_worker = self.modules['DegenerateFaceRemover']()
        self.face_reduce_worker = self.modules['FaceReducer']()
        
        # Texture generation (optional)
        if not self.args.disable_tex and self.modules['HAS_TEXTUREGEN']:
            print("Loading texturing pipeline...")
            self.texgen_worker = self.modules['Hunyuan3DPaintPipeline'].from_pretrained(
                self.args.texgen_model_path
            )
            if self.args.low_vram_mode:
                self.texgen_worker.enable_model_cpu_offload()
        else:
            self.texgen_worker = None
        
        # Text-to-image (optional)
        if self.args.enable_t23d and self.modules['HAS_T2I']:
            print("Loading text-to-image pipeline...")
            self.t2i_worker = self.modules['HunyuanDiTPipeline'](
                'Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled', 
                device=self.args.device
            )
        else:
            self.t2i_worker = None
    
    def gen_save_folder(self, base_name):
        """Generates a unique output folder for each image"""
        folder_name = f"{base_name}_{uuid.uuid4().hex[:8]}"
        save_folder = os.path.join(self.output_dir, folder_name)
        os.makedirs(save_folder, exist_ok=True)
        return save_folder
    
    def _export_to_fbx_bpy(self, input_path, output_path):
        """Exports to FBX using Blender Python API (bpy)"""
        try:
            import bpy
            import bmesh
            
            # Clean scene completely
            bpy.ops.wm.read_factory_settings(use_empty=True)
            
            # Delete default objects if they exist
            if bpy.context.selected_objects:
                bpy.ops.object.delete()
            
            # Import according to the input format
            try:
                if input_path.endswith('.obj'):
                    bpy.ops.wm.obj_import(filepath=input_path)
                elif input_path.endswith(('.glb', '.gltf')):
                    bpy.ops.import_scene.gltf(filepath=input_path)
                elif input_path.endswith('.ply'):
                    bpy.ops.wm.ply_import(filepath=input_path)
                else:
                    # Fallback for other formats
                    bpy.ops.wm.obj_import(filepath=input_path)
                    
            except AttributeError:
                # For older versions of Blender
                if input_path.endswith('.obj'):
                    bpy.ops.import_scene.obj(filepath=input_path)
                elif input_path.endswith(('.glb', '.gltf')):
                    bpy.ops.import_scene.gltf(filepath=input_path)
                else:
                    bpy.ops.import_scene.obj(filepath=input_path)
            
            # Verify that something has been imported
            if not bpy.context.selected_objects and not bpy.data.objects:
                print(f"        ✗ No objects were imported from {input_path}")
                return False
            
            # Select all mesh objects
            mesh_objects = [obj for obj in bpy.data.objects if obj.type == 'MESH']
            
            if not mesh_objects:
                print(f"        ✗ No meshes found in {input_path}")
                return False
            
            # Select all mesh objects
            bpy.ops.object.select_all(action='DESELECT')
            for obj in mesh_objects:
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
            
            # Apply transformations
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            
            # Mesh optimizations
            for obj in mesh_objects:
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.mode_set(mode='EDIT')
                
                # Remove duplicates
                bpy.ops.mesh.select_all(action='SELECT')
                bpy.ops.mesh.remove_doubles(threshold=0.0001)
                
                # Recalculate normals
                bpy.ops.mesh.normals_make_consistent(inside=False)
                
                bpy.ops.object.mode_set(mode='OBJECT')
            
            # Export to FBX with optimized settings
            bpy.ops.export_scene.fbx(
                filepath=output_path,
                use_selection=True,
                use_active_collection=False,
                
                # Objects to export
                object_types={'MESH'},
                
                # Mesh settings
                use_mesh_modifiers=True,
                use_mesh_modifiers_render=True,
                mesh_smooth_type='FACE',
                use_subsurf=False,
                use_mesh_edges=False,
                use_tspace=True,
                
                # Materials and textures
                use_custom_props=False,
                path_mode='AUTO',
                
                # Transformations
                bake_space_transform=False,
                
                # Armatures (bones)
                add_leaf_bones=True,
                primary_bone_axis='Y',
                secondary_bone_axis='X',
                
                # Animations (disabled)
                bake_anim=False,
                bake_anim_use_all_bones=False,
                bake_anim_use_nla_strips=False,
                bake_anim_use_all_actions=False,
                
                # Metadata
                use_metadata=True,
                
                # FBX version
                #version='BIN7400',  # FBX 2020
                
                # Axis settings
                axis_forward='-Z',
                axis_up='Y'
            )
            
            print(f"        ✓ FBX exported successfully")
            return True
            
        except Exception as e:
            print(f"        ✗ Error with bpy: {e}")
            return False
    
    def _export_to_fbx_pymeshlab(self, input_path, output_path):
        """Exports to FBX using PyMeshLab"""
        try:
            import pymeshlab as ml
            
            # Create mesh set
            ms = ml.MeshSet()
            
            # Load mesh
            ms.load_new_mesh(input_path)
            
            # Apply cleaning and optimization filters
            try:
                # Basic cleaning
                ms.apply_filter('meshing_remove_duplicate_vertices')
                ms.apply_filter('meshing_remove_null_faces')
                ms.apply_filter('meshing_repair_non_manifold_edges')
                
                # Optimizations
                ms.apply_filter('compute_normals_for_point_sets')
                ms.apply_filter('meshing_remove_connected_component_by_face_number', mincomponentsize=10)
                
            except Exception as filter_error:
                print(f"        ⚠ Some filters failed: {filter_error}")
            
            # Try to export to FBX
            # Note: PyMeshLab may not have direct support for FBX in all versions
            try:
                ms.save_current_mesh(output_path)
                return True
            except Exception:
                # If FBX is not supported, export as OBJ and return False
                obj_path = output_path.replace('.fbx', '_pymeshlab.obj')
                ms.save_current_mesh(obj_path)
                print(f"        ⚠ PyMeshLab does not support FBX, saved as: {obj_path}")
                return False
            
        except Exception as e:
            print(f"        ✗ Error with PyMeshLab: {e}")
            return False
    
    def _export_to_fbx_open3d(self, input_path, output_path):
        """Exports to FBX using Open3D as a preprocessor"""
        try:
            import open3d as o3d
            
            # Load mesh
            if input_path.endswith('.obj'):
                mesh = o3d.io.read_triangle_mesh(input_path)
            elif input_path.endswith('.ply'):
                mesh = o3d.io.read_triangle_mesh(input_path)
            else:
                print(f"        ✗ Format not supported by Open3D: {input_path}")
                return False
            
            if len(mesh.vertices) == 0:
                print(f"        ✗ Empty mesh loaded by Open3D")
                return False
            
            # Apply cleaning and optimization
            mesh.remove_duplicated_vertices()
            mesh.remove_degenerate_triangles()
            mesh.remove_unreferenced_vertices()
            mesh.remove_non_manifold_edges()
            
            # Calculate normals if they don't exist
            if not mesh.has_vertex_normals():
                mesh.compute_vertex_normals()
            
            # Open3D does not support FBX directly, so we save as a temporary OBJ
            # and then use bpy to convert
            temp_obj = output_path.replace('.fbx', '_temp_o3d.obj')
            
            success = o3d.io.write_triangle_mesh(temp_obj, mesh)
            
            if success and 'bpy' in self.fbx_methods:
                # Use bpy to convert the cleaned OBJ to FBX
                fbx_success = self._export_to_fbx_bpy(temp_obj, output_path)
                
                # Clean up temporary file
                if os.path.exists(temp_obj):
                    os.remove(temp_obj)
                
                return fbx_success
            else:
                print(f"        ⚠ Open3D processed but cannot convert to FBX")
                return False
            
        except Exception as e:
            print(f"        ✗ Error with Open3D: {e}")
            return False
    
    def _convert_to_fbx(self, input_path, output_path):
        """Converts any supported format to FBX"""
        print(f"      Converting to FBX: {os.path.basename(output_path)}")
        
        # Try different methods in order of preference
        methods = []
        
        if 'bpy' in self.fbx_methods:
            methods.append(('Blender Python API (bpy)', self._export_to_fbx_bpy))
        if 'pymeshlab' in self.fbx_methods:
            methods.append(('PyMeshLab', self._export_to_fbx_pymeshlab))
        if 'open3d' in self.fbx_methods:
            methods.append(('Open3D + bpy', self._export_to_fbx_open3d))
        
        for method_name, method_func in methods:
            try:
                print(f"        Trying {method_name}...")
                if method_func(input_path, output_path):
                    print(f"        ✓ Successful conversion with {method_name}")
                    return True
                else:
                    print(f"        ✗ Failed with {method_name}")
            except Exception as e:
                print(f"        ✗ Error with {method_name}: {e}")
        
        print(f"        ✗ Could not convert to FBX")
        return False
    
    def export_mesh(self, mesh, save_folder, textured=False, file_type='glb'):
        """Exports the mesh following the format of gradio_app.py with FBX support"""
        if textured:
            base_filename = 'textured_mesh'
        else:
            base_filename = 'white_mesh'
        
        # For FBX, we first export to an intermediate format (OBJ)
        if file_type.lower() == 'fbx':
            # Export to OBJ first
            temp_obj_path = os.path.join(save_folder, f'{base_filename}_temp.obj')
            mesh.export(temp_obj_path, include_normals=textured)
            
            # Convert to FBX
            final_path = os.path.join(save_folder, f'{base_filename}.fbx')
            
            if self._convert_to_fbx(temp_obj_path, final_path):
                # Clean up temporary file
                if os.path.exists(temp_obj_path):
                    os.remove(temp_obj_path)
                return final_path
            else:
                # If conversion fails, keep the OBJ
                final_path = temp_obj_path.replace('_temp.obj', '.obj')
                if os.path.exists(temp_obj_path):
                    os.rename(temp_obj_path, final_path)
                print(f"        ⚠ FBX not available, saved as OBJ: {final_path}")
                return final_path
        else:
            # Normal formats (OBJ, GLB, PLY, etc.)
            path = os.path.join(save_folder, f'{base_filename}.{file_type}')
            
            if file_type not in ['glb', 'obj']:
                mesh.export(path)
            else:
                mesh.export(path, include_normals=textured)
            
            return path
    
    def _gen_shape(self, image=None, caption=None, save_folder=None, **kwargs):
        """
        3D shape generation following exactly the logic of gradio_app.py
        """
        if image is None and caption is None:
            raise ValueError("An image or a caption must be provided")
        
        # Configure default parameters
        steps = kwargs.get('steps', 30)
        guidance_scale = kwargs.get('guidance_scale', 7.5)
        seed = kwargs.get('seed', 1234)
        octree_resolution = kwargs.get('octree_resolution', 256)
        check_box_rembg = kwargs.get('check_box_rembg', True)
        num_chunks = kwargs.get('num_chunks', 200000)
        
        stats = {
            'model': {
                'shapegen': f'{self.args.model_path}/{self.args.subfolder}',
                'texgen': f'{self.args.texgen_model_path}' if self.texgen_worker else 'Unavailable',
            },
            'params': {
                'caption': caption,
                'steps': steps,
                'guidance_scale': guidance_scale,
                'seed': seed,
                'octree_resolution': octree_resolution,
                'check_box_rembg': check_box_rembg,
                'num_chunks': num_chunks,
            }
        }
        time_meta = {}
        
        # Text to image if necessary
        if image is None and caption is not None:
            if self.t2i_worker is None:
                raise ValueError("Text-to-image is not available")
            start_time = time.time()
            image = self.t2i_worker(caption)
            time_meta['text2image'] = time.time() - start_time
        
        # Save input image
        if save_folder:
            image.save(os.path.join(save_folder, 'input.png'))
        
        # Remove background if necessary
        if check_box_rembg or image.mode == "RGB":
            start_time = time.time()
            image = self.rmbg_worker(image.convert('RGB'))
            time_meta['remove_background'] = time.time() - start_time
            
            if save_folder:
                image.save(os.path.join(save_folder, 'rembg.png'))
        
        # 3D shape generation
        start_time = time.time()
        generator = torch.Generator()
        generator = generator.manual_seed(int(seed))
        
        outputs = self.i23d_worker(
            image=image,
            num_inference_steps=steps,
            guidance_scale=guidance_scale,
            generator=generator,
            octree_resolution=octree_resolution,
            num_chunks=num_chunks,
            output_type='mesh'
        )
        time_meta['shape_generation'] = time.time() - start_time
        
        # Export to trimesh
        tmp_start = time.time()
        mesh = self.modules['export_to_trimesh'](outputs)[0]
        time_meta['export_to_trimesh'] = time.time() - tmp_start
        
        # Statistics
        stats['number_of_faces'] = mesh.faces.shape[0]
        stats['number_of_vertices'] = mesh.vertices.shape[0]
        stats['time'] = time_meta
        
        return mesh, image, stats
    
    def process_single_image(self, image_path, file_type, **kwargs):
        """
        Processes a single image following the complete pipeline
        """
        image_name = Path(image_path).stem
        print(f"\nProcessing: {image_name}")
        
        save_folder = self.gen_save_folder(image_name)
        
        try:
            start_time_total = time.time()
            
            # Load image
            print("  1. Loading image...")
            image = Image.open(image_path).convert('RGBA')
            
            # Resize if necessary (Hunyuan3D expects 512x512)
            if image.size != (512, 512):
                image = image.resize((512, 512), Image.Resampling.LANCZOS)
            
            # Shape generation
            print("  2. Generating 3D shape...")
            mesh, processed_image, stats = self._gen_shape(
                image=image,
                save_folder=save_folder,
                **kwargs
            )
            
            # Export initial white mesh
            white_mesh_path = self.export_mesh(mesh, save_folder, textured=False, file_type=file_type)
            print(f"    ✓ Initial mesh: {white_mesh_path}")
            
            # Post-processing exactly like gradio_app.py
            print("  3. Post-processing...")
            tmp_time = time.time()
            
            # Commented in gradio_app.py, but we do it here to clean up
            mesh = self.floater_remove_worker(mesh)
            mesh = self.degenerate_face_remove_worker(mesh)
            
            # Face reduction
            mesh = self.face_reduce_worker(mesh)
            stats['time']['face_reduction'] = time.time() - tmp_time
            
            # Export cleaned mesh
            cleaned_mesh_path = self.export_mesh(mesh, save_folder, textured=False, file_type=file_type)
            print(f"    ✓ Cleaned mesh: {cleaned_mesh_path}")
            
            # Texturing if available
            textured_mesh_path = None
            if self.texgen_worker is not None:
                print("  4. Generating texture...")
                tmp_time = time.time()
                textured_mesh = self.texgen_worker(mesh, processed_image)
                stats['time']['texture_generation'] = time.time() - tmp_time
                
                textured_mesh_path = self.export_mesh(textured_mesh, save_folder, textured=True, file_type=file_type)
                print(f"    ✓ Textured mesh: {textured_mesh_path}")
            else:
                print("    ⚠ Texturing not available")
            
            # Total time
            stats['time']['total'] = time.time() - start_time_total
            
            # Save statistics
            stats_path = os.path.join(save_folder, 'stats.json')
            with open(stats_path, 'w') as f:
                json.dump(stats, f, indent=2)
            
            # Generate preview
            self._generate_preview(mesh, save_folder, image_name)
            
            # Clean VRAM if activated
            if self.args.low_vram_mode:
                torch.cuda.empty_cache()
            
            print(f"  ✓ Completed in {stats['time']['total']:.2f}s")
            return True, save_folder, stats
            
        except Exception as e:
            print(f"  ✗ Error processing {image_name}: {str(e)}")
            return False, None, None
    
    def _generate_preview(self, mesh, save_folder, name):
        """Generates preview images of the 3D model"""
        try:
            print("  5. Generating preview...")
            
            # Create scene
            scene = mesh.scene()
            
            # Predefined views
            views = {
                'front': [0, 0, 2],
                'side': [2, 0, 0],
                'top': [0, 2, 1]
            }
            
            for view_name, position in views.items():
                camera_transform = trimesh.transformations.translation_matrix(position)
                scene.camera_transform = camera_transform
                
                preview_path = os.path.join(save_folder, f"{name}_preview_{view_name}.png")
                png = scene.save_image(resolution=[512, 512])
                
                with open(preview_path, 'wb') as f:
                    f.write(png)
            
            print(f"    ✓ Previews saved")
            
        except Exception as e:
            print(f"    ⚠ Error generating preview: {str(e)}")
    
    def process_folder(self, input_folder, file_type):
        """
        Processes all images in a folder
        """
        # Supported formats
        supported_formats = ['.jpg', '.jpeg', '.png', '.bmp', '.webp', '.tiff']
        
        # Find all images
        image_files = []
        for fmt in supported_formats:
            image_files.extend(Path(input_folder).glob(f'*{fmt}'))
            image_files.extend(Path(input_folder).glob(f'*{fmt.upper()}'))
        
        if not image_files:
            print(f"No images found in: {input_folder}")
            return
        
        print(f"\nFound {len(image_files)} images to process")
        print("=" * 80)
        
        # Global statistics
        processed = 0
        errors = 0
        total_time = 0
        results = []
        
        # Configure generation parameters
        generation_params = {
            'steps': self.args.steps,
            'guidance_scale': self.args.guidance_scale,
            'seed': self.args.seed,
            'octree_resolution': self.args.octree_resolution,
            'check_box_rembg': True,
            'num_chunks': self.args.num_chunks
        }
        
        # Process each image
        for i, image_path in enumerate(tqdm(image_files, desc="Processing images")):
            success, save_folder, stats = self.process_single_image(
                str(image_path), 
                file_type,
                **generation_params
            )
            
            if success:
                processed += 1
                total_time += stats['time']['total']
                results.append({
                    'image': str(image_path),
                    'output_folder': save_folder,
                    'stats': stats
                })
            else:
                errors += 1
        
        # Final summary
        print("\n" + "=" * 80)
        print(f"\nProcessing summary:")
        print(f"  - Total images: {len(image_files)}")
        print(f"  - Processed successfully: {processed}")
        print(f"  - Errors: {errors}")
        print(f"  - Total time: {total_time:.2f}s")
        print(f"  - Average time per image: {total_time/max(processed, 1):.2f}s")
        print(f"  - Results saved to: {self.output_dir}")
        
        # Save global summary
        summary = {
            'total_images': len(image_files),
            'processed': processed,
            'errors': errors,
            'total_time': total_time,
            'average_time': total_time/max(processed, 1),
            'results': results,
            'settings': vars(self.args)
        }
        
        summary_path = os.path.join(self.output_dir, 'batch_summary.json')
        with open(summary_path, 'w') as f:
            json.dump(summary, f, indent=2)
        
        print(f"  - Detailed summary: {summary_path}")

def main():
    parser = argparse.ArgumentParser(
        description='Hunyuan3D-2 Processor - Generates 3D models with texture (single image or full folder)',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Usage examples:

  Process a single image:
    python script.py image.png
    python script.py image.jpg --file_type fbx
    python script.py image.png --disable_tex --steps 10

  Process an entire folder:
    python script.py /path/to/images/
    python script.py /path/to/images/ --file_type fbx --low_vram_mode

  Advanced options:
    python script.py image.png --octree_resolution 384 --steps 50
    python script.py /folder/ --enable_flashvdm --compile
        """
    )
    
    # Main argument - can be an image or a folder
    parser.add_argument('input', 
                       help='Single image (.jpg, .png, etc.) or folder with images')
    parser.add_argument('-o', '--output', default='output_hunyuan3d', 
                       help='Output folder (default: output_hunyuan3d)')
    
    # Model arguments (following gradio_app.py)
    parser.add_argument("--model_path", type=str, default='tencent/Hunyuan3D-2mini')
    parser.add_argument("--subfolder", type=str, default='hunyuan3d-dit-v2-mini-turbo')
    parser.add_argument("--texgen_model_path", type=str, default='tencent/Hunyuan3D-2')
    parser.add_argument('--device', type=str, default='cuda', 
                       help='Device (cuda, cpu, etc.)')
    parser.add_argument('--mc_algo', type=str, default='mc')
    
    # Optimizations
    parser.add_argument('--enable_t23d', action='store_true',
                       help='Enable text-to-3D')
    parser.add_argument('--disable_tex', action='store_true',
                       help='Disable texture generation')
    parser.add_argument('--enable_flashvdm', action='store_true',
                       help='Enable FlashVDM for acceleration')
    parser.add_argument('--compile', action='store_true',
                       help='Compile the model for acceleration')
    parser.add_argument('--low_vram_mode', action='store_true',
                       help='Low VRAM consumption mode')
    
    # Generation parameters
    parser.add_argument('--steps', type=int, default=30,
                       help='Inference steps')
    parser.add_argument('--guidance_scale', type=float, default=7.5,
                       help='Guidance scale')
    parser.add_argument('--seed', type=int, default=1234,
                       help='Seed for reproducibility')
    parser.add_argument('--octree_resolution', type=int, default=256,
                       help='Octree resolution')
    parser.add_argument('--num_chunks', type=int, default=200000,
                       help='Number of chunks')
    parser.add_argument('--file_type', type=str, default='obj', 
                       choices=['obj', 'glb', 'ply', 'stl', 'fbx'],
                       help='Output file type (obj, glb, ply, stl, fbx)')
    
    args = parser.parse_args()
    
    # Determine if the input is an image or a folder
    input_path = Path(args.input)
    
    if not input_path.exists():
        print(f"Error: '{args.input}' does not exist.")
        sys.exit(1)
    
    is_single_image = input_path.is_file() and is_image_file(input_path)
    is_folder = input_path.is_dir()
    
    if not is_single_image and not is_folder:
        print(f"Error: '{args.input}' is not a valid image or folder.")
        print("Supported formats: .jpg, .jpeg, .png, .bmp, .webp, .tiff")
        sys.exit(1)
    
    # System checks
    if 'cuda' in args.device and not torch.cuda.is_available():
        print("Error: CUDA is not available. Use --device cpu")
        sys.exit(1)
    
    # FBX check
    if args.file_type.lower() == 'fbx':
        print("Note: FBX format selected. Verifying dependencies...")
        try:
            import bpy
            print("  ✓ Blender Python API (bpy) available")
        except ImportError:
            print("  ✗ bpy not available. Install with: pip install bpy")
            print("     Or try with other dependencies: pip install pymeshlab open3d")
    
    # Initial information
    print("Hunyuan3D-2 Processor")
    print("=" * 50)
    if is_single_image:
        print(f"Mode: Single image")
        print(f"Input: {args.input}")
    else:
        print(f"Mode: Batch processing")
        print(f"Folder: {args.input}")
    
    print(f"Output: {args.output}")
    print(f"Format: {args.file_type.upper()}")
    print(f"Model: {args.model_path}/{args.subfolder}")
    print(f"Device: {args.device}")
    print(f"Texturing: {'Disabled' if args.disable_tex else 'Enabled'}")
    print(f"Low VRAM mode: {'Yes' if args.low_vram_mode else 'No'}")
    print()
    
    # Create and run processor
    processor = HunyuanBatchProcessor(args)
    
    if is_single_image:
        # Process single image
        print("Processing single image...")
        
        generation_params = {
            'steps': args.steps,
            'guidance_scale': args.guidance_scale,
            'seed': args.seed,
            'octree_resolution': args.octree_resolution,
            'check_box_rembg': True,
            'num_chunks': args.num_chunks
        }
        
        success, save_folder, stats = processor.process_single_image(
            str(input_path),
            args.file_type,
            **generation_params
        )
        
        if success:
            print(f"\n🎉 Image processed successfully!")
            print(f"📁 Results saved to: {save_folder}")
            print(f"⏱️  Total time: {stats['time']['total']:.2f}s")
            
            # Show generated files
            generated_files = list(Path(save_folder).glob('*'))
            print(f"\n📋 Generated files:")
            for file in sorted(generated_files):
                print(f"   - {file.name}")
        else:
            print(f"\n❌ Error processing the image: {args.input}")
            sys.exit(1)
    else:
        # Process folder
        processor.process_folder(str(input_path), args.file_type)

if __name__ == "__main__":
    main()