#!/usr/bin/env python3
"""
Script de diagnòstic per Hunyuan3D Unity Plugin
Comprova l'estat complet del sistema: Python, CUDA, PyTorch i dependències
"""

import sys
import os
import subprocess
import importlib
import json
from datetime import datetime

def run_command(cmd):
    """Executa una comanda i retorna el resultat"""
    try:
        result = subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=30)
        return result.stdout.strip(), result.stderr.strip(), result.returncode
    except subprocess.TimeoutExpired:
        return "", "Timeout", -1
    except Exception as e:
        return "", str(e), -1

def check_import(module_name, package_name=None):
    """Comprova si es pot importar un mòdul"""
    try:
        module = importlib.import_module(module_name)
        version = getattr(module, '__version__', 'Unknown')
        return True, version
    except ImportError as e:
        return False, str(e)

def check_system_info():
    """Checks system information"""
    print("=== SYSTEM INFORMATION ===")
    print(f"Python: {sys.version}")
    print(f"Platform: {sys.platform}")
    print(f"Executable: {sys.executable}")
    print()

def check_nvidia_drivers():
    """Checks NVIDIA drivers"""
    print("=== NVIDIA DRIVERS ===")
    
    stdout, stderr, code = run_command("nvidia-smi")
    if code == 0 and "CUDA Version" in stdout:
        lines = stdout.split('\n')
        for line in lines:
            if "CUDA Version" in line:
                print(f"✓ {line.strip()}")
                break
    else:
        print("✗ nvidia-smi not available or drivers not installed")
    print()

def check_cuda_toolkit():
    """Checks CUDA Toolkit"""
    print("=== CUDA TOOLKIT ===")
    
    stdout, stderr, code = run_command("nvcc --version")
    if code == 0 and "release" in stdout:
        lines = stdout.split('\n')
        for line in lines:
            if "release" in line:
                print(f"✓ nvcc {line.strip()}")
                break
    else:
        print("✗ nvcc not available - CUDA Toolkit not installed or not in PATH")
    
    # Check typical paths on Windows
    if sys.platform == "win32":
        cuda_paths = [
            r"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA",
        ]
        
        for base_path in cuda_paths:
            if os.path.exists(base_path):
                versions = [d for d in os.listdir(base_path) if d.startswith('v')]
                if versions:
                    print(f"ℹ CUDA installations found: {', '.join(sorted(versions))}")
    print()

def check_pytorch():
    """Checks PyTorch and CUDA"""
    print("=== PYTORCH ===")
    
    torch_available, torch_info = check_import('torch')
    if torch_available:
        print(f"✓ PyTorch: {torch_info}")
        
        try:
            import torch
            print(f"✓ CUDA available: {torch.cuda.is_available()}")
            
            if torch.cuda.is_available():
                print(f"✓ CUDA version (PyTorch): {torch.version.cuda}")
                print(f"✓ GPU count: {torch.cuda.device_count()}")
                
                for i in range(torch.cuda.device_count()):
                    name = torch.cuda.get_device_name(i)
                    memory = torch.cuda.get_device_properties(i).total_memory / (1024**3)
                    print(f"  GPU {i}: {name} ({memory:.1f} GB)")
                
                # Test bàsic
                try:
                    x = torch.randn(2, 3).cuda()
                    y = x * 2
                    print("✓ Test GPU funcional OK")
                except Exception as e:
                    print(f"✗ Error en test GPU: {e}")
            else:
                print("ℹ CUDA not available - CPU mode")
                # Test CPU
                try:
                    x = torch.randn(2, 3)
                    y = x * 2
                    print("✓ CPU test functional OK")
                except Exception as e:
                    print(f"✗ Error in CPU test: {e}")
                    
        except Exception as e:
            print(f"✗ Error importing PyTorch: {e}")
    else:
        print(f"✗ PyTorch not installed: {torch_info}")
    print()

def check_core_dependencies():
    """Checks core dependencies"""
    print("=== CORE DEPENDENCIES ===")
    
    core_deps = [
        ('diffusers', 'diffusers'),
        ('transformers', 'transformers'),
        ('numpy', 'numpy'),
        ('PIL', 'Pillow'),
        ('cv2', 'opencv-python'),
        ('rembg', 'rembg'),
        ('trimesh', 'trimesh'),
        ('tqdm', 'tqdm'),
        ('omegaconf', 'omegaconf'),
        ('einops', 'einops'),
    ]
    
    for import_name, package_name in core_deps:
        available, info = check_import(import_name)
        status = "✓" if available else "✗"
        print(f"{status} {package_name}: {info}")
    print()

def check_optional_dependencies():
    """Checks optional dependencies"""
    print("=== OPTIONAL DEPENDENCIES ===")
    
    optional_deps = [
        ('pymeshlab', 'pymeshlab'),
        ('pygltflib', 'pygltflib'),
        ('xatlas', 'xatlas'),
        ('accelerate', 'accelerate'),
        ('onnxruntime', 'onnxruntime'),
    ]
    
    for import_name, package_name in optional_deps:
        available, info = check_import(import_name)
        status = "✓" if available else "⚠"
        print(f"{status} {package_name}: {info}")
    print()

def check_hunyuan3d():
    """Checks if Hunyuan3D is available"""
    print("=== HUNYUAN3D ===")
    
    # Check if we can import Hunyuan3D components
    hunyuan_modules = [
        'hunyuan3d',
        'Hunyuan3D',
    ]
    
    found = False
    for module in hunyuan_modules:
        available, info = check_import(module)
        if available:
            print(f"✓ {module}: {info}")
            found = True
        
    if not found:
        print("ℹ Hunyuan3D package not detected (may be installed differently)")
    print()

def generate_report():
    """Generates a complete report"""
    print("="*60)
    print("HUNYUAN3D DIAGNOSTIC REPORT")
    print(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("="*60)
    print()
    
    check_system_info()
    check_nvidia_drivers()
    check_cuda_toolkit()
    check_pytorch()
    check_core_dependencies()
    check_optional_dependencies()
    check_hunyuan3d()
    
    print("="*60)
    print("END OF REPORT")
    print("="*60)
    
    # Recommendations
    print("\n=== RECOMMENDATIONS ===")
    
    torch_available, _ = check_import('torch')
    if not torch_available:
        print("- Install PyTorch: pip install torch torchvision")
    
    stdout, _, code = run_command("nvidia-smi")
    nvcc_out, _, nvcc_code = run_command("nvcc --version")
    
    if code == 0 and nvcc_code != 0:
        print("- NVIDIA driver detected but CUDA Toolkit not installed")
        print("  Use the Dependency Manager to install CUDA automatically")
    
    if torch_available:
        import torch
        if not torch.cuda.is_available() and code == 0:
            print("- CUDA available on system but not in PyTorch")
            print("  Reinstall PyTorch with CUDA support")

if __name__ == "__main__":
    generate_report()
