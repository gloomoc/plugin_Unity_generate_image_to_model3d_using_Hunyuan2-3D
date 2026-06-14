#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import os
import sys
import argparse

# Force UTF-8 encoding for Windows compatibility
if sys.platform.startswith('win'):
    # Set environment variables for UTF-8
    os.environ['PYTHONIOENCODING'] = 'utf-8'
    os.environ['PYTHONUTF8'] = '1'
    os.environ['PYTHONLEGACYWINDOWSSTDIO'] = 'utf-8'
    
    # Try to configure stdout/stderr for UTF-8 (Python 3.7+)
    try:
        if hasattr(sys.stdout, 'reconfigure'):
            sys.stdout.reconfigure(encoding='utf-8')
            sys.stderr.reconfigure(encoding='utf-8')
    except:
        pass
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


def _add_repo_path(path):
    if path and os.path.isdir(path) and path not in sys.path:
        sys.path.insert(0, path)


def _looks_like_v21_root(path):
    return os.path.isdir(os.path.join(path, 'hy3dshape')) and os.path.isdir(os.path.join(path, 'hy3dpaint'))


def _looks_like_legacy_root(path):
    return os.path.isdir(os.path.join(path, 'hy3dgen'))


def _find_hunyuan_root():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    candidates = [
        os.environ.get('HUNYUAN3D_ROOT'),
        script_dir,
        os.path.dirname(script_dir),
        os.path.dirname(os.path.dirname(script_dir)),
        os.path.join(os.path.dirname(script_dir), 'Hunyuan3D-2.1'),
        os.path.join(os.path.dirname(script_dir), 'Hunyuan3D-2'),
        os.path.join(Path.home(), 'AppData', 'Local', 'Temp', 'Hunyuan3D-2.1-for-windows'),
        os.path.join(Path.home(), 'AppData', 'Local', 'Temp', 'Hunyuan3D-2-for-windows'),
        os.path.join(Path.home(), 'AppData', 'Local', 'Temp', 'Hunyuan2-3D-for-windows'),
    ]

    for candidate in candidates:
        if not candidate:
            continue

        if _looks_like_v21_root(candidate) or _looks_like_legacy_root(candidate):
            return candidate

    return None


HUNYUAN_ROOT = _find_hunyuan_root()
if HUNYUAN_ROOT:
    _add_repo_path(HUNYUAN_ROOT)
    _add_repo_path(os.path.join(HUNYUAN_ROOT, 'hy3dshape'))
    _add_repo_path(os.path.join(HUNYUAN_ROOT, 'hy3dpaint'))

def setup_imports():
    """Imports the necessary modules from Hunyuan3D"""
    import_errors = []

    try:
        try:
            from hy3dshape import FaceReducer, FloaterRemover, DegenerateFaceRemover
        except ImportError:
            from hy3dshape.postprocessors import FaceReducer, FloaterRemover, DegenerateFaceRemover

        from hy3dshape.pipelines import Hunyuan3DDiTFlowMatchingPipeline, export_to_trimesh

        try:
            from hy3dshape.rembg import BackgroundRemover
        except ImportError:
            BackgroundRemover = None

        try:
            from textureGenPipeline import Hunyuan3DPaintPipeline, Hunyuan3DPaintConfig
            has_texturegen = True
        except Exception as texture_error:
            print(f"Warning: Hunyuan3D 2.1 texture generation not available: {texture_error}")
            Hunyuan3DPaintPipeline = None
            Hunyuan3DPaintConfig = None
            has_texturegen = False

        # Text-to-3D on 2.1: the legacy text2image module (hy3dgen.text2image) ships only with the 2.0
        # package, so we drive text-to-image through diffusers' own HunyuanDiTPipeline (the same model).
        try:
            from diffusers import HunyuanDiTPipeline as DiffusersHunyuanDiTPipeline
            has_t2i = True
        except Exception as t2i_error:
            print(f"Warning: text-to-3D unavailable (diffusers HunyuanDiTPipeline missing): {t2i_error}")
            DiffusersHunyuanDiTPipeline = None
            has_t2i = False

        return {
            'API_VERSION': '2.1',
            'FaceReducer': FaceReducer,
            'FloaterRemover': FloaterRemover,
            'DegenerateFaceRemover': DegenerateFaceRemover,
            'Hunyuan3DDiTFlowMatchingPipeline': Hunyuan3DDiTFlowMatchingPipeline,
            'export_to_trimesh': export_to_trimesh,
            'BackgroundRemover': BackgroundRemover,
            'Hunyuan3DPaintPipeline': Hunyuan3DPaintPipeline,
            'Hunyuan3DPaintConfig': Hunyuan3DPaintConfig,
            'HunyuanDiTPipeline': DiffusersHunyuanDiTPipeline,
            'HAS_TEXTUREGEN': has_texturegen,
            'HAS_T2I': has_t2i,
            'T2I_BACKEND': 'diffusers'
        }
    except ImportError as e:
        import_errors.append(f"Hunyuan3D 2.1 imports failed: {e}")

    try:
        from hy3dgen.shapegen import (
            FaceReducer,
            FloaterRemover,
            DegenerateFaceRemover,
            Hunyuan3DDiTFlowMatchingPipeline
        )
        from hy3dgen.shapegen.pipelines import export_to_trimesh
        from hy3dgen.rembg import BackgroundRemover

        try:
            from hy3dgen.texgen import Hunyuan3DPaintPipeline
            has_texturegen = True
        except Exception:
            print("Warning: Legacy texture generation not available. Install requirements per README.md")
            Hunyuan3DPaintPipeline = None
            has_texturegen = False

        try:
            from hy3dgen.text2image import HunyuanDiTPipeline
            has_t2i = True
        except Exception:
            print("Warning: Legacy text-to-image not available.")
            HunyuanDiTPipeline = None
            has_t2i = False

        return {
            'API_VERSION': '2.0',
            'FaceReducer': FaceReducer,
            'FloaterRemover': FloaterRemover,
            'DegenerateFaceRemover': DegenerateFaceRemover,
            'Hunyuan3DDiTFlowMatchingPipeline': Hunyuan3DDiTFlowMatchingPipeline,
            'export_to_trimesh': export_to_trimesh,
            'BackgroundRemover': BackgroundRemover,
            'Hunyuan3DPaintPipeline': Hunyuan3DPaintPipeline,
            'Hunyuan3DPaintConfig': None,
            'HunyuanDiTPipeline': HunyuanDiTPipeline,
            'HAS_TEXTUREGEN': has_texturegen,
            'HAS_T2I': has_t2i,
            'T2I_BACKEND': 'legacy'
        }
    except ImportError as e:
        import_errors.append(f"Legacy Hunyuan3D imports failed: {e}")

    print("Error importing Hunyuan3D modules.")
    if HUNYUAN_ROOT:
        print(f"Detected repository root: {HUNYUAN_ROOT}")
    else:
        print("No compatible Hunyuan3D repository was detected automatically.")

    for error in import_errors:
        print(f"  - {error}")

    print("Expected either Hunyuan3D-2.1 (hy3dshape + hy3dpaint) or Hunyuan3D-2 (hy3dgen).")
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
        if self.modules['BackgroundRemover'] is None:
            from rembg import remove
            from io import BytesIO

            def _fallback_rmbg(image):
                buffer = BytesIO()
                image.save(buffer, format='PNG')
                result = remove(buffer.getvalue())
                return Image.open(BytesIO(result)).convert('RGBA')

            print("Loading Background Remover via rembg fallback...")
            self.rmbg_worker = _fallback_rmbg
        else:
            print("Loading Background Remover...")
            self.rmbg_worker = self.modules['BackgroundRemover']()
        
        # Shape generation pipeline
        print(f"Loading 3D generation pipeline...")
        shape_attempts = [
            {'subfolder': self.args.subfolder, 'use_safetensors': True, 'device': self.args.device},
            {'subfolder': self.args.subfolder, 'use_safetensors': True},
            {'subfolder': self.args.subfolder},
            {}
        ]
        last_shape_error = None
        for attempt in shape_attempts:
            try:
                self.i23d_worker = self.modules['Hunyuan3DDiTFlowMatchingPipeline'].from_pretrained(
                    self.args.model_path,
                    **attempt,
                )
                last_shape_error = None
                break
            except Exception as shape_error:
                last_shape_error = shape_error

        if last_shape_error is not None:
            raise last_shape_error
        
        # Activate optimizations if available
        if self.args.enable_flashvdm and hasattr(self.i23d_worker, 'enable_flashvdm'):
            mc_algo = 'mc' if self.args.device in ['cpu', 'mps'] else self.args.mc_algo
            try:
                self.i23d_worker.enable_flashvdm(mc_algo=mc_algo)
            except Exception as flash_error:
                print(f"Warning: FlashVDM could not be enabled: {flash_error}")
        
        if self.args.compile and hasattr(self.i23d_worker, 'compile'):
            print("Compiling model...")
            try:
                self.i23d_worker.compile()
            except Exception as compile_error:
                print(f"Warning: Model compilation not available: {compile_error}")
        
        # Post-processing workers
        self.floater_remove_worker = self.modules['FloaterRemover']()
        self.degenerate_face_remove_worker = self.modules['DegenerateFaceRemover']()
        self.face_reduce_worker = self.modules['FaceReducer']()
        
        # Texture generation (optional)
        if not self.args.disable_tex and self.modules['HAS_TEXTUREGEN']:
            print("Loading texturing pipeline...")
            if self.modules['API_VERSION'] == '2.1':
                # 2.1 API: Hunyuan3DPaintPipeline(Hunyuan3DPaintConfig(max_num_view, resolution)).
                # No model_path / positional / from_pretrained fallbacks here: those are 2.0-style and on 2.1
                # they only raise misleading errors (e.g. "Hunyuan3DPaintConfig() missing 2 positional
                # arguments") that mask the real failure. Report the real error + traceback instead.
                self.texgen_worker = None
                if self.modules.get('Hunyuan3DPaintConfig') is None:
                    print("Warning: Hunyuan3DPaintConfig unavailable; texture generation disabled.")
                else:
                    try:
                        paint_config = self.modules['Hunyuan3DPaintConfig'](max_num_view=6, resolution=512)
                        # Wire the "Texture Model Path": the paint (multiview) model repo is taken from
                        # --texgen_model_path so the dropdown actually selects the texture model on 2.1.
                        if getattr(self.args, 'texgen_model_path', None):
                            paint_config.multiview_pretrained_path = self.args.texgen_model_path
                        self.texgen_worker = self.modules['Hunyuan3DPaintPipeline'](paint_config)
                    except Exception as texture_error:
                        import traceback
                        print(f"Warning: Hunyuan3D 2.1 texture pipeline unavailable: {texture_error}")
                        traceback.print_exc()
                        self.texgen_worker = None
            else:
                self.texgen_worker = self.modules['Hunyuan3DPaintPipeline'].from_pretrained(
                    self.args.texgen_model_path
                )

            if self.args.low_vram_mode and self.texgen_worker is not None and hasattr(self.texgen_worker, 'enable_model_cpu_offload'):
                self.texgen_worker.enable_model_cpu_offload()
        else:
            self.texgen_worker = None
        
        # Text-to-image (optional). On 2.1 the backend is diffusers (from_pretrained + .images[0]);
        # on legacy 2.0 it is the hy3dgen wrapper (constructor + returns a PIL image directly).
        self.t2i_is_diffusers = False
        if self.args.enable_t23d and self.modules['HAS_T2I']:
            print("Loading text-to-image pipeline (first run downloads the HunyuanDiT model, ~8 GB)...")
            t2i_backend = self.modules.get('T2I_BACKEND', 'legacy')
            if t2i_backend == 'diffusers':
                t2i_dtype = torch.float16 if 'cuda' in self.args.device else torch.float32
                self.t2i_worker = self.modules['HunyuanDiTPipeline'].from_pretrained(
                    'Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled',
                    torch_dtype=t2i_dtype
                ).to(self.args.device)
                self.t2i_is_diffusers = True
            else:
                self.t2i_worker = self.modules['HunyuanDiTPipeline'](
                    'Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled',
                    device=self.args.device
                )
        elif self.args.enable_t23d and not self.modules['HAS_T2I']:
            print("Warning: --enable_t23d was set but text-to-image is unavailable; ignoring it.")
            self.t2i_worker = None
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
                # If conversion fails, keep the OBJ. Use os.replace (not os.rename) so it overwrites an
                # existing file on Windows — export_mesh is called twice for FBX (initial + cleaned mesh),
                # and os.rename raises WinError 183 when white_mesh.obj already exists.
                final_path = temp_obj_path.replace('_temp.obj', '.obj')
                if os.path.exists(temp_obj_path):
                    os.replace(temp_obj_path, final_path)
                print(f"        ⚠ FBX not available (needs Blender/bpy), saved as OBJ: {final_path}")
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
            'api_version': self.modules['API_VERSION'],
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
            t2i_output = self.t2i_worker(caption)
            # diffusers pipelines return an object exposing .images; the legacy wrapper returns a PIL image
            image = t2i_output.images[0] if getattr(self, 't2i_is_diffusers', False) else t2i_output
            if image.size != (512, 512):
                image = image.resize((512, 512), Image.Resampling.LANCZOS)
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
            if self.texgen_worker is not None:
                print("  4. Generating texture...")
                tmp_time = time.time()

                if self.modules['API_VERSION'] == '2.1':
                    texture_image_path = os.path.join(save_folder, 'rembg.png')
                    if not os.path.exists(texture_image_path):
                        texture_image_path = os.path.join(save_folder, 'input.png')

                    source_mesh_path = os.path.join(save_folder, 'white_mesh_for_texture.obj')
                    mesh.export(source_mesh_path)

                    requested_file_type = file_type.lower()
                    textured_output_path = os.path.join(
                        save_folder,
                        'textured_mesh.glb' if requested_file_type == 'glb' else 'textured_mesh.obj'
                    )

                    texture_attempts = [
                        {
                            'mesh_path': source_mesh_path,
                            'image_path': texture_image_path,
                            'output_mesh_path': textured_output_path,
                            'save_glb': requested_file_type == 'glb'
                        },
                        {
                            'mesh_path': source_mesh_path,
                            'image_path': texture_image_path,
                            'output_mesh_path': textured_output_path
                        },
                        {
                            'mesh_path': source_mesh_path,
                            'image_path': texture_image_path
                        }
                    ]

                    textured_result = None
                    last_texture_error = None
                    for attempt in texture_attempts:
                        try:
                            textured_result = self.texgen_worker(**attempt)
                            last_texture_error = None
                            break
                        except TypeError as texture_error:
                            last_texture_error = texture_error
                        except Exception as texture_error:
                            last_texture_error = texture_error

                    if textured_result is None and not os.path.exists(textured_output_path):
                        raise last_texture_error

                    textured_mesh_path = textured_result if isinstance(textured_result, str) else textured_output_path
                    if not os.path.exists(textured_mesh_path):
                        textured_mesh_path = textured_output_path

                    if requested_file_type == 'fbx' and os.path.exists(textured_mesh_path):
                        converted_fbx_path = os.path.join(save_folder, 'textured_mesh.fbx')
                        if self._convert_to_fbx(textured_mesh_path, converted_fbx_path):
                            textured_mesh_path = converted_fbx_path
                else:
                    textured_mesh = self.texgen_worker(mesh, processed_image)
                    textured_mesh_path = self.export_mesh(textured_mesh, save_folder, textured=True, file_type=file_type)

                stats['time']['texture_generation'] = time.time() - tmp_time
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
            if self.args.low_vram_mode and torch.cuda.is_available():
                torch.cuda.empty_cache()
            
            print(f"  ✓ Completed in {stats['time']['total']:.2f}s")
            return True, save_folder, stats
            
        except Exception as e:
            print(f"  ✗ Error processing {image_name}: {str(e)}")
            return False, None, None
    
    def process_single_text(self, caption, file_type, **kwargs):
        """
        Text-to-3D: generate an image from the prompt with the text-to-image model, then run the
        exact same image -> 3D -> texture pipeline used for image inputs (process_single_image).
        """
        if self.t2i_worker is None:
            print("  ✗ Text-to-image is not available (need --enable_t23d and a working HunyuanDiT).")
            return False, None, None

        print(f"\nProcessing text prompt: {caption}")
        print("  0. Generating image from text...")
        try:
            t2i_output = self.t2i_worker(caption)
            # diffusers pipelines return an object exposing .images; the legacy wrapper returns a PIL image
            image = t2i_output.images[0] if getattr(self, 't2i_is_diffusers', False) else t2i_output
            if image.size != (512, 512):
                image = image.resize((512, 512), Image.Resampling.LANCZOS)
            os.makedirs(self.output_dir, exist_ok=True)
            src_image_path = os.path.join(self.output_dir, f"{self._caption_to_name(caption)}.png")
            image.convert("RGB").save(src_image_path)
            # Free the text-to-image model before shape/texture generation to reduce VRAM pressure
            self.t2i_worker = None
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except Exception as e:
            print(f"  ✗ Error generating image from text: {str(e)}")
            return False, None, None

        try:
            return self.process_single_image(src_image_path, file_type, **kwargs)
        finally:
            # The generated source image is copied into the output folder as input.png by the
            # standard pipeline, so the temporary top-level copy can be removed.
            try:
                if os.path.exists(src_image_path):
                    os.remove(src_image_path)
            except Exception:
                pass

    @staticmethod
    def _caption_to_name(caption):
        """Builds a filesystem-safe folder/file name from a text prompt."""
        text = (caption or "")[:40]
        safe = "".join(c if (c.isalnum() or c in (' ', '_', '-')) else '_' for c in text).strip()
        safe = safe.replace(' ', '_')
        return safe or "text_prompt"

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
            'check_box_rembg': self.args.remove_background,
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
        description='Hunyuan3D Processor - Compatible with Hunyuan3D-2.1 and legacy Hunyuan3D-2 setups',
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
                       nargs='?', default=None,
                       help='Single image or folder with images (omit for text-to-3D with --caption)')
    parser.add_argument('-o', '--output', default='output_hunyuan3d', 
                       help='Output folder (default: output_hunyuan3d)')
    
    # Model arguments (following gradio_app.py)
    parser.add_argument("--model_path", type=str, default='tencent/Hunyuan3D-2.1')
    parser.add_argument("--subfolder", type=str, default='hunyuan3d-dit-v2-1')
    parser.add_argument("--texgen_model_path", type=str, default='tencent/Hunyuan3D-2.1')
    parser.add_argument('--device', type=str, default='cuda', 
                       help='Device (cuda, cpu, etc.)')
    parser.add_argument('--mc_algo', type=str, default='mc')
    
    # Optimizations
    parser.add_argument('--enable_t23d', action='store_true',
                       help='Enable text-to-3D')
    parser.add_argument('--caption', type=str, default=None,
                       help='Text prompt for text-to-3D (used together with --enable_t23d)')
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
    parser.set_defaults(remove_background=True)
    parser.add_argument('--remove_background', dest='remove_background', action='store_true',
                       help='Enable internal background removal')
    parser.add_argument('--skip_background_removal', dest='remove_background', action='store_false',
                       help='Skip internal background removal')
    
    args = parser.parse_args()
    
    # Determine the mode: text-to-3D (no positional input) or image/folder
    text_mode = args.input is None
    is_single_image = False
    is_folder = False

    if text_mode:
        if not (args.enable_t23d and args.caption):
            print("Error: no input image or folder was provided.")
            print("Provide an image/folder, or use --enable_t23d together with --caption \"your prompt\".")
            sys.exit(1)
        input_path = None
    else:
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
        print("Warning: CUDA is not available. Falling back to CPU.")
        args.device = 'cpu'
    
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
    print("Hunyuan3D Processor")
    print("=" * 50)
    if text_mode:
        print(f"Mode: Text-to-3D")
        print(f"Prompt: {args.caption}")
    elif is_single_image:
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
    print(f"Background removal: {'Enabled' if args.remove_background else 'Disabled'}")
    print(f"Low VRAM mode: {'Yes' if args.low_vram_mode else 'No'}")
    print()
    
    # Create and run processor
    processor = HunyuanBatchProcessor(args)

    generation_params = {
        'steps': args.steps,
        'guidance_scale': args.guidance_scale,
        'seed': args.seed,
        'octree_resolution': args.octree_resolution,
        'check_box_rembg': args.remove_background,
        'num_chunks': args.num_chunks
    }

    if is_folder:
        # Process a whole folder of images
        processor.process_folder(str(input_path), args.file_type)
    else:
        if text_mode:
            print("Processing text prompt (text-to-3D)...")
            success, save_folder, stats = processor.process_single_text(
                args.caption,
                args.file_type,
                **generation_params
            )
            error_label = f"text prompt: {args.caption}"
        else:
            print("Processing single image...")
            success, save_folder, stats = processor.process_single_image(
                str(input_path),
                args.file_type,
                **generation_params
            )
            error_label = args.input

        if success:
            print(f"\n🎉 Generation completed successfully!")
            print(f"📁 Results saved to: {save_folder}")
            print(f"⏱️  Total time: {stats['time']['total']:.2f}s")

            # Show generated files
            generated_files = list(Path(save_folder).glob('*'))
            print(f"\n📋 Generated files:")
            for file in sorted(generated_files):
                print(f"   - {file.name}")
        else:
            print(f"\n❌ Error processing {error_label}")
            sys.exit(1)

if __name__ == "__main__":
    main()
