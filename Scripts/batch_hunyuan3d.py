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
import trimesh
from PIL import Image
from tqdm import tqdm
import json

# Afegir el directori del projecte al path
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

def setup_imports():
    """Importa els mòduls necessaris de Hunyuan3D"""
    try:
        # Imports principals de Hunyuan3D
        from hy3dgen.shapegen import (
            FaceReducer, 
            FloaterRemover, 
            DegenerateFaceRemover, 
            Hunyuan3DDiTFlowMatchingPipeline
        )
        from hy3dgen.shapegen.pipelines import export_to_trimesh
        from hy3dgen.rembg import BackgroundRemover
        
        # Imports opcionals per texturització
        try:
            from hy3dgen.texgen import Hunyuan3DPaintPipeline
            HAS_TEXTUREGEN = True
        except Exception:
            print("Warning: Texture generation not available. Install requirements per README.md")
            HAS_TEXTUREGEN = False
            Hunyuan3DPaintPipeline = None
        
        # Imports opcionals per text-to-image
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
        print(f"Error important mòduls de Hunyuan3D: {e}")
        print("Assegura't que estàs executant l'script des del directori del repositori Hunyuan3D-2")
        sys.exit(1)

def is_image_file(file_path):
    """Comprova si un fitxer és una imatge suportada"""
    supported_formats = ['.jpg', '.jpeg', '.png', '.bmp', '.webp', '.tiff']
    return any(str(file_path).lower().endswith(fmt) for fmt in supported_formats)

class HunyuanBatchProcessor:
    def __init__(self, args):
        """
        Inicialitza el processador seguint exactament el patró de gradio_app.py
        """
        self.args = args
        self.device = args.device
        
        # Configurar directori de sortida
        self.output_dir = args.output
        os.makedirs(self.output_dir, exist_ok=True)
        
        print("Inicialitzant Hunyuan3D Batch Processor...")
        print(f"Model: {args.model_path}/{args.subfolder}")
        print(f"Dispositiu: {args.device}")
        print(f"Format de sortida: {args.file_type.upper()}")
        
        # Verificar suport FBX si és necessari
        if args.file_type.lower() == 'fbx':
            if not self._check_fbx_dependencies():
                print("Warning: Dependències FBX no disponibles. Es farà conversió via formats intermedis.")
        
        # Importar mòduls
        self.modules = setup_imports()
        
        # Inicialitzar workers exactament com gradio_app.py
        self._init_workers()
        
        print("Models carregats correctament!\n")
    
    def _check_fbx_dependencies(self):
        """Comprova les dependències necessàries per FBX"""
        fbx_methods = []
        
        print("  Verificant dependències FBX...")
        
        # Método 1: bpy (Blender Python API) - amb timeout
        print("    Provant Blender Python API (bpy)...")
        try:
            import importlib.util
            import sys
            import signal
            
            def timeout_handler(signum, frame):
                raise TimeoutError("Import bpy ha trigat massa")
            
            # Només en sistemes Unix/Linux
            if hasattr(signal, 'SIGALRM'):
                signal.signal(signal.SIGALRM, timeout_handler)
                signal.alarm(5)  # 5 segons timeout
            
            try:
                # Verificar si bpy existeix sense importar-lo completament
                spec = importlib.util.find_spec("bpy")
                if spec is not None:
                    # Intentar import ràpid
                    import bpy
                    fbx_methods.append('bpy')
                    print("      ✓ Blender Python API (bpy) disponible")
                else:
                    print("      ✗ bpy no trobat")
            finally:
                if hasattr(signal, 'SIGALRM'):
                    signal.alarm(0)  # Cancel·lar timeout
                    
        except (ImportError, TimeoutError, Exception) as e:
            print(f"      ✗ bpy no disponible: {str(e)[:50]}...")
        
        # Método 2: pymeshlab - més ràpid
        print("    Provant PyMeshLab...")
        try:
            import pymeshlab
            fbx_methods.append('pymeshlab')
            print("      ✓ PyMeshLab disponible")
        except ImportError:
            print("      ✗ PyMeshLab no disponible")
        
        # Método 3: Open3D
        print("    Provant Open3D...")
        try:
            import open3d as o3d
            fbx_methods.append('open3d')
            print("      ✓ Open3D disponible")
        except ImportError:
            print("      ✗ Open3D no disponible")
        
        self.fbx_methods = fbx_methods
        
        if not fbx_methods:
            print("      ⚠️ Cap mètode FBX disponible")
            print("         Recomanació: pip install pymeshlab open3d")
            print("         Per bpy: pip install bpy (pot trigar molt)")
            print("         El script continuarà amb format OBJ com a fallback")
        else:
            print(f"      ✓ Mètodes FBX disponibles: {', '.join(fbx_methods)}")
        
        return len(fbx_methods) > 0
    
    def _init_workers(self):
        """Inicialitza tots els workers seguint gradio_app.py"""
        
        # Background remover
        print("Carregant Background Remover...")
        self.rmbg_worker = self.modules['BackgroundRemover']()
        
        # Shape generation pipeline
        print(f"Carregant pipeline de generació 3D...")
        self.i23d_worker = self.modules['Hunyuan3DDiTFlowMatchingPipeline'].from_pretrained(
            self.args.model_path,
            subfolder=self.args.subfolder,
            use_safetensors=True,
            device=self.args.device,
        )
        
        # Activar optimitzacions si estan disponibles
        if self.args.enable_flashvdm:
            mc_algo = 'mc' if self.args.device in ['cpu', 'mps'] else self.args.mc_algo
            self.i23d_worker.enable_flashvdm(mc_algo=mc_algo)
        
        if self.args.compile:
            print("Compilant model...")
            self.i23d_worker.compile()
        
        # Post-processing workers
        self.floater_remove_worker = self.modules['FloaterRemover']()
        self.degenerate_face_remove_worker = self.modules['DegenerateFaceRemover']()
        self.face_reduce_worker = self.modules['FaceReducer']()
        
        # Texture generation (opcional)
        if not self.args.disable_tex and self.modules['HAS_TEXTUREGEN']:
            print("Carregant pipeline de texturització...")
            self.texgen_worker = self.modules['Hunyuan3DPaintPipeline'].from_pretrained(
                self.args.texgen_model_path
            )
            if self.args.low_vram_mode:
                self.texgen_worker.enable_model_cpu_offload()
        else:
            self.texgen_worker = None
        
        # Text-to-image (opcional)
        if self.args.enable_t23d and self.modules['HAS_T2I']:
            print("Carregant pipeline text-to-image...")
            self.t2i_worker = self.modules['HunyuanDiTPipeline'](
                'Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled', 
                device=self.args.device
            )
        else:
            self.t2i_worker = None
    
    def gen_save_folder(self, base_name):
        """Genera una carpeta de sortida única per cada imatge"""
        folder_name = f"{base_name}_{uuid.uuid4().hex[:8]}"
        save_folder = os.path.join(self.output_dir, folder_name)
        os.makedirs(save_folder, exist_ok=True)
        return save_folder
    
    def _export_to_fbx_bpy(self, input_path, output_path):
        """Exporta a FBX utilitzant Blender Python API (bpy)"""
        try:
            import bpy
            import bmesh
            
            # Netejar escena completament
            bpy.ops.wm.read_factory_settings(use_empty=True)
            
            # Eliminar objectes per defecte si existeixen
            if bpy.context.selected_objects:
                bpy.ops.object.delete()
            
            # Importar segons el format d'entrada
            try:
                if input_path.endswith('.obj'):
                    bpy.ops.wm.obj_import(filepath=input_path)
                elif input_path.endswith(('.glb', '.gltf')):
                    bpy.ops.import_scene.gltf(filepath=input_path)
                elif input_path.endswith('.ply'):
                    bpy.ops.wm.ply_import(filepath=input_path)
                else:
                    # Fallback per altres formats
                    bpy.ops.wm.obj_import(filepath=input_path)
                    
            except AttributeError:
                # Per versions més antigues de Blender
                if input_path.endswith('.obj'):
                    bpy.ops.import_scene.obj(filepath=input_path)
                elif input_path.endswith(('.glb', '.gltf')):
                    bpy.ops.import_scene.gltf(filepath=input_path)
                else:
                    bpy.ops.import_scene.obj(filepath=input_path)
            
            # Verificar que s'ha importat alguna cosa
            if not bpy.context.selected_objects and not bpy.data.objects:
                print(f"        ✗ No s'han importat objectes des de {input_path}")
                return False
            
            # Seleccionar tots els objectes de malla
            mesh_objects = [obj for obj in bpy.data.objects if obj.type == 'MESH']
            
            if not mesh_objects:
                print(f"        ✗ No s'han trobat malles a {input_path}")
                return False
            
            # Seleccionar tots els objectes de malla
            bpy.ops.object.select_all(action='DESELECT')
            for obj in mesh_objects:
                obj.select_set(True)
                bpy.context.view_layer.objects.active = obj
            
            # Aplicar transformacions
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            
            # Optimitzacions de malla
            for obj in mesh_objects:
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.mode_set(mode='EDIT')
                
                # Eliminar duplicats
                bpy.ops.mesh.select_all(action='SELECT')
                bpy.ops.mesh.remove_doubles(threshold=0.0001)
                
                # Recalcular normals
                bpy.ops.mesh.normals_make_consistent(inside=False)
                
                bpy.ops.object.mode_set(mode='OBJECT')
            
            # Exportar a FBX amb configuració optimitzada
            bpy.ops.export_scene.fbx(
                filepath=output_path,
                use_selection=True,
                use_active_collection=False,
                
                # Objectes a exportar
                object_types={'MESH'},
                
                # Configuració de malla
                use_mesh_modifiers=True,
                use_mesh_modifiers_render=True,
                mesh_smooth_type='FACE',
                use_subsurf=False,
                use_mesh_edges=False,
                use_tspace=True,
                
                # Materials i textures
                use_custom_props=False,
                path_mode='AUTO',
                
                # Transformacions
                bake_space_transform=False,
                
                # Armatures (bones)
                add_leaf_bones=True,
                primary_bone_axis='Y',
                secondary_bone_axis='X',
                
                # Animacions (desactivades)
                bake_anim=False,
                bake_anim_use_all_bones=False,
                bake_anim_use_nla_strips=False,
                bake_anim_use_all_actions=False,
                
                # Metadades
                use_metadata=True,
                
                # Versió FBX
                #version='BIN7400',  # FBX 2020
                
                # Configuració d'eixos
                axis_forward='-Z',
                axis_up='Y'
            )
            
            print(f"        ✓ FBX exportat correctament")
            return True
            
        except Exception as e:
            print(f"        ✗ Error amb bpy: {e}")
            return False
    
    def _export_to_fbx_pymeshlab(self, input_path, output_path):
        """Exporta a FBX utilitzant PyMeshLab"""
        try:
            import pymeshlab as ml
            
            # Crear conjunt de malles
            ms = ml.MeshSet()
            
            # Carregar malla
            ms.load_new_mesh(input_path)
            
            # Aplicar filtres de neteja i optimització
            try:
                # Neteja bàsica
                ms.apply_filter('meshing_remove_duplicate_vertices')
                ms.apply_filter('meshing_remove_null_faces')
                ms.apply_filter('meshing_repair_non_manifold_edges')
                
                # Optimitzacions
                ms.apply_filter('compute_normals_for_point_sets')
                ms.apply_filter('meshing_remove_connected_component_by_face_number', mincomponentsize=10)
                
            except Exception as filter_error:
                print(f"        ⚠ Alguns filtres han fallat: {filter_error}")
            
            # Intentar exportar a FBX
            # Nota: PyMeshLab pot no tenir suport directe per FBX en totes les versions
            try:
                ms.save_current_mesh(output_path)
                return True
            except Exception:
                # Si FBX no està suportat, exportar com a OBJ i retornar False
                obj_path = output_path.replace('.fbx', '_pymeshlab.obj')
                ms.save_current_mesh(obj_path)
                print(f"        ⚠ PyMeshLab no suporta FBX, guardat com: {obj_path}")
                return False
            
        except Exception as e:
            print(f"        ✗ Error amb PyMeshLab: {e}")
            return False
    
    def _export_to_fbx_open3d(self, input_path, output_path):
        """Exporta a FBX utilitzant Open3D com a preprocessor"""
        try:
            import open3d as o3d
            
            # Carregar malla
            if input_path.endswith('.obj'):
                mesh = o3d.io.read_triangle_mesh(input_path)
            elif input_path.endswith('.ply'):
                mesh = o3d.io.read_triangle_mesh(input_path)
            else:
                print(f"        ✗ Format no suportat per Open3D: {input_path}")
                return False
            
            if len(mesh.vertices) == 0:
                print(f"        ✗ Malla buida carregada per Open3D")
                return False
            
            # Aplicar neteja i optimització
            mesh.remove_duplicated_vertices()
            mesh.remove_degenerate_triangles()
            mesh.remove_unreferenced_vertices()
            mesh.remove_non_manifold_edges()
            
            # Calcular normals si no existeixen
            if not mesh.has_vertex_normals():
                mesh.compute_vertex_normals()
            
            # Open3D no suporta FBX directament, així que guardem com a OBJ temporal
            # i després utilitzem bpy per convertir
            temp_obj = output_path.replace('.fbx', '_temp_o3d.obj')
            
            success = o3d.io.write_triangle_mesh(temp_obj, mesh)
            
            if success and 'bpy' in self.fbx_methods:
                # Utilitzar bpy per convertir l'OBJ netejat a FBX
                fbx_success = self._export_to_fbx_bpy(temp_obj, output_path)
                
                # Netejar fitxer temporal
                if os.path.exists(temp_obj):
                    os.remove(temp_obj)
                
                return fbx_success
            else:
                print(f"        ⚠ Open3D ha processat però no pot convertir a FBX")
                return False
            
        except Exception as e:
            print(f"        ✗ Error amb Open3D: {e}")
            return False
    
    def _convert_to_fbx(self, input_path, output_path):
        """Converteix qualsevol format suportat a FBX"""
        print(f"      Convertint a FBX: {os.path.basename(output_path)}")
        
        # Provar diferents mètodes en ordre de preferència
        methods = []
        
        if 'bpy' in self.fbx_methods:
            methods.append(('Blender Python API (bpy)', self._export_to_fbx_bpy))
        if 'pymeshlab' in self.fbx_methods:
            methods.append(('PyMeshLab', self._export_to_fbx_pymeshlab))
        if 'open3d' in self.fbx_methods:
            methods.append(('Open3D + bpy', self._export_to_fbx_open3d))
        
        for method_name, method_func in methods:
            try:
                print(f"        Provant {method_name}...")
                if method_func(input_path, output_path):
                    print(f"        ✓ Conversió exitosa amb {method_name}")
                    return True
                else:
                    print(f"        ✗ Fallida amb {method_name}")
            except Exception as e:
                print(f"        ✗ Error amb {method_name}: {e}")
        
        print(f"        ✗ No s'ha pogut convertir a FBX")
        return False
    
    def export_mesh(self, mesh, save_folder, textured=False, file_type='glb'):
        """Exporta la malla seguint el format de gradio_app.py amb suport FBX"""
        if textured:
            base_filename = 'textured_mesh'
        else:
            base_filename = 'white_mesh'
        
        # Per FBX, primer exportem a un format intermedi (OBJ)
        if file_type.lower() == 'fbx':
            # Exportar primer a OBJ
            temp_obj_path = os.path.join(save_folder, f'{base_filename}_temp.obj')
            mesh.export(temp_obj_path, include_normals=textured)
            
            # Convertir a FBX
            final_path = os.path.join(save_folder, f'{base_filename}.fbx')
            
            if self._convert_to_fbx(temp_obj_path, final_path):
                # Netejar fitxer temporal
                if os.path.exists(temp_obj_path):
                    os.remove(temp_obj_path)
                return final_path
            else:
                # Si falla la conversió, mantenir l'OBJ
                final_path = temp_obj_path.replace('_temp.obj', '.obj')
                if os.path.exists(temp_obj_path):
                    os.rename(temp_obj_path, final_path)
                print(f"        ⚠ FBX no disponible, guardat com OBJ: {final_path}")
                return final_path
        else:
            # Formats normals (OBJ, GLB, PLY, etc.)
            path = os.path.join(save_folder, f'{base_filename}.{file_type}')
            
            if file_type not in ['glb', 'obj']:
                mesh.export(path)
            else:
                mesh.export(path, include_normals=textured)
            
            return path
    
    def _gen_shape(self, image=None, caption=None, save_folder=None, **kwargs):
        """
        Generació de forma 3D seguint exactament la lògica de gradio_app.py
        """
        if image is None and caption is None:
            raise ValueError("Cal proporcionar una imatge o un caption")
        
        # Configurar paràmetres per defecte
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
        
        # Text to image si és necessari
        if image is None and caption is not None:
            if self.t2i_worker is None:
                raise ValueError("Text-to-image no està disponible")
            start_time = time.time()
            image = self.t2i_worker(caption)
            time_meta['text2image'] = time.time() - start_time
        
        # Guardar imatge d'entrada
        if save_folder:
            image.save(os.path.join(save_folder, 'input.png'))
        
        # Remove background si és necessari
        if check_box_rembg or image.mode == "RGB":
            start_time = time.time()
            image = self.rmbg_worker(image.convert('RGB'))
            time_meta['remove_background'] = time.time() - start_time
            
            if save_folder:
                image.save(os.path.join(save_folder, 'rembg.png'))
        
        # Generació de forma 3D
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
        
        # Exportar a trimesh
        tmp_start = time.time()
        mesh = self.modules['export_to_trimesh'](outputs)[0]
        time_meta['export_to_trimesh'] = time.time() - tmp_start
        
        # Estadístiques
        stats['number_of_faces'] = mesh.faces.shape[0]
        stats['number_of_vertices'] = mesh.vertices.shape[0]
        stats['time'] = time_meta
        
        return mesh, image, stats
    
    def process_single_image(self, image_path, file_type, **kwargs):
        """
        Processa una sola imatge seguint el pipeline complet
        """
        image_name = Path(image_path).stem
        print(f"\nProcessant: {image_name}")
        
        save_folder = self.gen_save_folder(image_name)
        
        try:
            start_time_total = time.time()
            
            # Carregar imatge
            print("  1. Carregant imatge...")
            image = Image.open(image_path).convert('RGBA')
            
            # Redimensionar si és necessari (Hunyuan3D espera 512x512)
            if image.size != (512, 512):
                image = image.resize((512, 512), Image.Resampling.LANCZOS)
            
            # Generació de forma
            print("  2. Generant forma 3D...")
            mesh, processed_image, stats = self._gen_shape(
                image=image,
                save_folder=save_folder,
                **kwargs
            )
            
            # Exportar malla blanca inicial
            white_mesh_path = self.export_mesh(mesh, save_folder, textured=False, file_type=file_type)
            print(f"    ✓ Malla inicial: {white_mesh_path}")
            
            # Post-processament exacte com gradio_app.py
            print("  3. Post-processament...")
            tmp_time = time.time()
            
            # Comentat a gradio_app.py, però aquí ho fem per netejar
            mesh = self.floater_remove_worker(mesh)
            mesh = self.degenerate_face_remove_worker(mesh)
            
            # Reducció de cares
            mesh = self.face_reduce_worker(mesh)
            stats['time']['face_reduction'] = time.time() - tmp_time
            
            # Exportar malla netejada
            cleaned_mesh_path = self.export_mesh(mesh, save_folder, textured=False, file_type=file_type)
            print(f"    ✓ Malla netejada: {cleaned_mesh_path}")
            
            # Texturització si està disponible
            textured_mesh_path = None
            if self.texgen_worker is not None:
                print("  4. Generant textura...")
                tmp_time = time.time()
                textured_mesh = self.texgen_worker(mesh, processed_image)
                stats['time']['texture_generation'] = time.time() - tmp_time
                
                textured_mesh_path = self.export_mesh(textured_mesh, save_folder, textured=True, file_type=file_type)
                print(f"    ✓ Malla texturitzada: {textured_mesh_path}")
            else:
                print("    ⚠ Texturització no disponible")
            
            # Temps total
            stats['time']['total'] = time.time() - start_time_total
            
            # Guardar estadístiques
            stats_path = os.path.join(save_folder, 'stats.json')
            with open(stats_path, 'w') as f:
                json.dump(stats, f, indent=2)
            
            # Generar preview
            self._generate_preview(mesh, save_folder, image_name)
            
            # Netejar VRAM si està activat
            if self.args.low_vram_mode:
                torch.cuda.empty_cache()
            
            print(f"  ✓ Completat en {stats['time']['total']:.2f}s")
            return True, save_folder, stats
            
        except Exception as e:
            print(f"  ✗ Error processant {image_name}: {str(e)}")
            return False, None, None
    
    def _generate_preview(self, mesh, save_folder, name):
        """Genera imatges de preview del model 3D"""
        try:
            print("  5. Generant preview...")
            
            # Crear escena
            scene = mesh.scene()
            
            # Vistes predefinides
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
            
            print(f"    ✓ Previews guardats")
            
        except Exception as e:
            print(f"    ⚠ Error generant preview: {str(e)}")
    
    def process_folder(self, input_folder, file_type):
        """
        Processa totes les imatges d'una carpeta
        """
        # Formats suportats
        supported_formats = ['.jpg', '.jpeg', '.png', '.bmp', '.webp', '.tiff']
        
        # Trobar totes les imatges
        image_files = []
        for fmt in supported_formats:
            image_files.extend(Path(input_folder).glob(f'*{fmt}'))
            image_files.extend(Path(input_folder).glob(f'*{fmt.upper()}'))
        
        if not image_files:
            print(f"No s'han trobat imatges a: {input_folder}")
            return
        
        print(f"\nS'han trobat {len(image_files)} imatges per processar")
        print("=" * 80)
        
        # Estadístiques globals
        processed = 0
        errors = 0
        total_time = 0
        results = []
        
        # Configurar paràmetres de generació
        generation_params = {
            'steps': self.args.steps,
            'guidance_scale': self.args.guidance_scale,
            'seed': self.args.seed,
            'octree_resolution': self.args.octree_resolution,
            'check_box_rembg': True,
            'num_chunks': self.args.num_chunks
        }
        
        # Processar cada imatge
        for i, image_path in enumerate(tqdm(image_files, desc="Processant imatges")):
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
        
        # Resum final
        print("\n" + "=" * 80)
        print(f"\nResum del processament:")
        print(f"  - Total imatges: {len(image_files)}")
        print(f"  - Processades correctament: {processed}")
        print(f"  - Errors: {errors}")
        print(f"  - Temps total: {total_time:.2f}s")
        print(f"  - Temps mitjà per imatge: {total_time/max(processed, 1):.2f}s")
        print(f"  - Resultats guardats a: {self.output_dir}")
        
        # Guardar resum global
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
        
        print(f"  - Resum detallat: {summary_path}")

def main():
    parser = argparse.ArgumentParser(
        description='Hunyuan3D-2 Processor - Genera models 3D amb textura (una imatge o carpeta completa)',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Exemples d'ús:

  Processar una imatge individual:
    python script.py imatge.png
    python script.py imatge.jpg --file_type fbx
    python script.py imatge.png --disable_tex --steps 10

  Processar una carpeta sencera:
    python script.py /carpeta/imatges/
    python script.py /carpeta/imatges/ --file_type fbx --low_vram_mode

  Opcions avançades:
    python script.py imatge.png --octree_resolution 384 --steps 50
    python script.py /carpeta/ --enable_flashvdm --compile
        """
    )
    
    # Argument principal - pot ser imatge o carpeta
    parser.add_argument('input', 
                       help='Imatge individual (.jpg, .png, etc.) o carpeta amb imatges')
    parser.add_argument('-o', '--output', default='output_hunyuan3d', 
                       help='Carpeta de sortida (per defecte: output_hunyuan3d)')
    
    # Arguments del model (seguint gradio_app.py)
    parser.add_argument("--model_path", type=str, default='tencent/Hunyuan3D-2mini')
    parser.add_argument("--subfolder", type=str, default='hunyuan3d-dit-v2-mini-turbo')
    parser.add_argument("--texgen_model_path", type=str, default='tencent/Hunyuan3D-2')
    parser.add_argument('--device', type=str, default='cuda', 
                       help='Dispositiu (cuda, cpu, etc.)')
    parser.add_argument('--mc_algo', type=str, default='mc')
    
    # Optimitzacions
    parser.add_argument('--enable_t23d', action='store_true',
                       help='Activa text-to-3D')
    parser.add_argument('--disable_tex', action='store_true',
                       help='Desactiva generació de textures')
    parser.add_argument('--enable_flashvdm', action='store_true',
                       help='Activa FlashVDM per accelerar')
    parser.add_argument('--compile', action='store_true',
                       help='Compila el model per accelerar')
    parser.add_argument('--low_vram_mode', action='store_true',
                       help='Mode baix consum VRAM')
    
    # Paràmetres de generació
    parser.add_argument('--steps', type=int, default=30,
                       help='Passos d\'inferència')
    parser.add_argument('--guidance_scale', type=float, default=7.5,
                       help='Escala de guidance')
    parser.add_argument('--seed', type=int, default=1234,
                       help='Seed per reproducibilitat')
    parser.add_argument('--octree_resolution', type=int, default=256,
                       help='Resolució d\'octree')
    parser.add_argument('--num_chunks', type=int, default=200000,
                       help='Número de chunks')
    parser.add_argument('--file_type', type=str, default='obj', 
                       choices=['obj', 'glb', 'ply', 'stl', 'fbx'],
                       help='Tipus de fitxer de sortida (obj, glb, ply, stl, fbx)')
    
    args = parser.parse_args()
    
    # Determinar si l'entrada és imatge o carpeta
    input_path = Path(args.input)
    
    if not input_path.exists():
        print(f"Error: '{args.input}' no existeix.")
        sys.exit(1)
    
    is_single_image = input_path.is_file() and is_image_file(input_path)
    is_folder = input_path.is_dir()
    
    if not is_single_image and not is_folder:
        print(f"Error: '{args.input}' no és una imatge vàlida ni una carpeta.")
        print("Formats suportats: .jpg, .jpeg, .png, .bmp, .webp, .tiff")
        sys.exit(1)
    
    # Verificacions de sistema
    if 'cuda' in args.device and not torch.cuda.is_available():
        print("Error: CUDA no està disponible. Utilitza --device cpu")
        sys.exit(1)
    
    # Verificació FBX
    if args.file_type.lower() == 'fbx':
        print("Note: Format FBX seleccionat. Verificant dependències...")
        try:
            import bpy
            print("  ✓ Blender Python API (bpy) disponible")
        except ImportError:
            print("  ✗ bpy no disponible. Instal·la amb: pip install bpy")
            print("     O prova amb altres dependències: pip install pymeshlab open3d")
    
    # Informació inicial
    print("Hunyuan3D-2 Processor")
    print("=" * 50)
    if is_single_image:
        print(f"Mode: Imatge individual")
        print(f"Entrada: {args.input}")
    else:
        print(f"Mode: Processament en lot")
        print(f"Carpeta: {args.input}")
    
    print(f"Sortida: {args.output}")
    print(f"Format: {args.file_type.upper()}")
    print(f"Model: {args.model_path}/{args.subfolder}")
    print(f"Dispositiu: {args.device}")
    print(f"Texturització: {'Desactivada' if args.disable_tex else 'Activada'}")
    print(f"Mode baix VRAM: {'Sí' if args.low_vram_mode else 'No'}")
    print()
    
    # Crear i executar processador
    processor = HunyuanBatchProcessor(args)
    
    if is_single_image:
        # Processar imatge individual
        print("Processant imatge individual...")
        
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
            print(f"\n🎉 Imatge processada correctament!")
            print(f"📁 Resultats guardats a: {save_folder}")
            print(f"⏱️  Temps total: {stats['time']['total']:.2f}s")
            
            # Mostrar fitxers generats
            generated_files = list(Path(save_folder).glob('*'))
            print(f"\n📋 Fitxers generats:")
            for file in sorted(generated_files):
                print(f"   - {file.name}")
        else:
            print(f"\n❌ Error processant la imatge: {args.input}")
            sys.exit(1)
    else:
        # Processar carpeta
        processor.process_folder(str(input_path), args.file_type)

if __name__ == "__main__":
    main()