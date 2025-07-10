#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Installation and dependency verification script for the Hunyuan3D Unity plugin
This script helps configure the Python environment necessary to run the plugin
"""

import os
import sys
import subprocess
import importlib.util
from pathlib import Path

def check_python_version():
    """Checks that the Python version is compatible"""
    print("Checking Python version...")
    
    if sys.version_info < (3, 8):
        print("❌ Error: Python 3.8 or higher is required")
        print(f"   Current version: {sys.version}")
        return False
    
    print(f"✅ Python {sys.version_info.major}.{sys.version_info.minor} is compatible")
    return True

def check_package(package_name, import_name=None):
    """Checks if a package is installed"""
    if import_name is None:
        import_name = package_name
    
    try:
        importlib.util.find_spec(import_name)
        print(f"✅ {package_name} is installed")
        return True
    except ImportError:
        print(f"❌ {package_name} is not installed")
        return False

def install_package(package_name):
    """Installs a package with pip"""
    print(f"Installing {package_name}...")
    try:
        subprocess.check_call([sys.executable, "-m", "pip", "install", package_name])
        print(f"✅ {package_name} installed successfully")
        return True
    except subprocess.CalledProcessError:
        print(f"❌ Error installing {package_name}")
        return False

def check_gpu_support():
    """Checks if there is GPU support"""
    print("\nChecking GPU support...")
    
    try:
        import torch
        if torch.cuda.is_available():
            print(f"✅ CUDA available - {torch.cuda.get_device_name(0)}")
            print(f"   CUDA version: {torch.version.cuda}")
            return True
        else:
            print("⚠️  CUDA not available - CPU will be used")
            return False
    except ImportError:
        print("❌ PyTorch is not installed")
        return False

def check_scripts():
    """Checks if the necessary scripts exist"""
    print("\nChecking necessary scripts...")
    
    current_dir = Path(__file__).parent
    scripts = [
        "batch_hunyuan3d.py",
        "remove_background.py"
    ]
    
    all_found = True
    for script in scripts:
        script_path = current_dir / script
        if script_path.exists():
            print(f"✅ {script} found")
        else:
            print(f"❌ {script} not found in {current_dir}")
            all_found = False
    
    return all_found

def main():
    """Main verification and installation function"""
    print("=" * 60)
    print("HUNYUAN3D UNITY PLUGIN DEPENDENCY VERIFIER")
    print("=" * 60)
    
    # Check Python
    if not check_python_version():
        return False
    
    # Basic dependencies
    print("\nChecking basic dependencies...")
    basic_packages = [
        ("torch", "torch"),
        ("pillow", "PIL"),
        ("numpy", "numpy"),
        ("trimesh", "trimesh"),
        ("tqdm", "tqdm"),
        ("rembg", "rembg")
    ]
    
    missing_packages = []
    for package_name, import_name in basic_packages:
        if not check_package(package_name, import_name):
            missing_packages.append(package_name)
    
    # Install missing packages
    if missing_packages:
        print(f"\n{len(missing_packages)} packages missing:")
        for package in missing_packages:
            print(f"  - {package}")
        
        response = input("\nDo you want to install the missing packages? (y/n): ")
        if response.lower() in ['s', 'sí', 'si', 'y', 'yes']:
            for package in missing_packages:
                install_package(package)
        else:
            print("⚠️  Some packages are not installed. The plugin may not work correctly.")
    
    # Check GPU support
    check_gpu_support()
    
    # Check scripts
    if not check_scripts():
        print("\n⚠️  Some scripts were not found. Make sure they are in the same directory.")
    
    # Final summary
    print("\n" + "=" * 60)
    print("CONFIGURATION SUMMARY")
    print("=" * 60)
    
    # Recheck everything
    print("Dependency status:")
    all_ok = True
    
    for package_name, import_name in basic_packages:
        status = "✅" if check_package(package_name, import_name) else "❌"
        print(f"  {status} {package_name}")
        if status == "❌":
            all_ok = False
    
    if all_ok:
        print("\n🎉 All dependencies are installed!")
        print("The Hunyuan3D Unity plugin is ready to use.")
        
        # Provide usage examples
        print("\nUsage examples:")
        print("1. Process an image:")
        print("   python batch_hunyuan3d.py image.jpg")
        print("\n2. Process a folder:")
        print("   python batch_hunyuan3d.py images_folder/")
        print("\n3. Remove background from an image:")
        print("   python remove_background.py image.jpg output.png")
        
    else:
        print("\n⚠️  Some dependencies are missing. The plugin may not work correctly.")
        print("Run this script again to install the missing dependencies.")
    
    return all_ok

if __name__ == "__main__":
    try:
        success = main()
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print("\n\nInstallation cancelled by user.")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")
        sys.exit(1)
