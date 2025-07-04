#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Script d'instal·lació i verificació de dependències per al plugin Hunyuan3D Unity
Aquest script ajuda a configurar l'entorn Python necessari per executar el plugin
"""

import os
import sys
import subprocess
import importlib.util
from pathlib import Path

def check_python_version():
    """Comprova que la versió de Python sigui compatible"""
    print("Comprovant versió de Python...")
    
    if sys.version_info < (3, 8):
        print("❌ Error: Python 3.8 o superior és necessari")
        print(f"   Versió actual: {sys.version}")
        return False
    
    print(f"✅ Python {sys.version_info.major}.{sys.version_info.minor} és compatible")
    return True

def check_package(package_name, import_name=None):
    """Comprova si un paquet està instal·lat"""
    if import_name is None:
        import_name = package_name
    
    try:
        importlib.util.find_spec(import_name)
        print(f"✅ {package_name} està instal·lat")
        return True
    except ImportError:
        print(f"❌ {package_name} no està instal·lat")
        return False

def install_package(package_name):
    """Instal·la un paquet amb pip"""
    print(f"Instal·lant {package_name}...")
    try:
        subprocess.check_call([sys.executable, "-m", "pip", "install", package_name])
        print(f"✅ {package_name} instal·lat correctament")
        return True
    except subprocess.CalledProcessError:
        print(f"❌ Error instal·lant {package_name}")
        return False

def check_gpu_support():
    """Comprova si hi ha suport per GPU"""
    print("\nComprovant suport GPU...")
    
    try:
        import torch
        if torch.cuda.is_available():
            print(f"✅ CUDA disponible - {torch.cuda.get_device_name(0)}")
            print(f"   Versió CUDA: {torch.version.cuda}")
            return True
        else:
            print("⚠️  CUDA no disponible - s'utilitzarà CPU")
            return False
    except ImportError:
        print("❌ PyTorch no està instal·lat")
        return False

def check_scripts():
    """Comprova si els scripts necessaris existeixen"""
    print("\nComprovant scripts necessaris...")
    
    current_dir = Path(__file__).parent
    scripts = [
        "batch_hunyuan3d.py",
        "remove_background.py"
    ]
    
    all_found = True
    for script in scripts:
        script_path = current_dir / script
        if script_path.exists():
            print(f"✅ {script} trobat")
        else:
            print(f"❌ {script} no trobat a {current_dir}")
            all_found = False
    
    return all_found

def main():
    """Funció principal de verificació i instal·lació"""
    print("=" * 60)
    print("VERIFICADOR DE DEPENDÈNCIES HUNYUAN3D UNITY PLUGIN")
    print("=" * 60)
    
    # Comprovar Python
    if not check_python_version():
        return False
    
    # Dependències bàsiques
    print("\nComprovant dependències bàsiques...")
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
    
    # Instal·lar paquets que falten
    if missing_packages:
        print(f"\nFalten {len(missing_packages)} paquets:")
        for package in missing_packages:
            print(f"  - {package}")
        
        response = input("\nVols instal·lar els paquets que falten? (s/n): ")
        if response.lower() in ['s', 'sí', 'si', 'y', 'yes']:
            for package in missing_packages:
                install_package(package)
        else:
            print("⚠️  Alguns paquets no estan instal·lats. El plugin pot no funcionar correctament.")
    
    # Comprovar suport GPU
    check_gpu_support()
    
    # Comprovar scripts
    if not check_scripts():
        print("\n⚠️  Alguns scripts no s'han trobat. Assegura't que estiguin al mateix directori.")
    
    # Resum final
    print("\n" + "=" * 60)
    print("RESUM DE LA CONFIGURACIÓ")
    print("=" * 60)
    
    # Recomprovar tot
    print("Estat de les dependències:")
    all_ok = True
    
    for package_name, import_name in basic_packages:
        status = "✅" if check_package(package_name, import_name) else "❌"
        print(f"  {status} {package_name}")
        if status == "❌":
            all_ok = False
    
    if all_ok:
        print("\n🎉 Totes les dependències estan instal·lades!")
        print("El plugin Hunyuan3D Unity està llest per utilitzar.")
        
        # Proporcionar exemples d'ús
        print("\nExemples d'ús:")
        print("1. Processar una imatge:")
        print("   python batch_hunyuan3d.py imatge.jpg")
        print("\n2. Processar una carpeta:")
        print("   python batch_hunyuan3d.py carpeta_imatges/")
        print("\n3. Eliminar fons d'una imatge:")
        print("   python remove_background.py imatge.jpg sortida.png")
        
    else:
        print("\n⚠️  Algunes dependències falten. El plugin pot no funcionar correctament.")
        print("Executa aquest script de nou per instal·lar les dependències que falten.")
    
    return all_ok

if __name__ == "__main__":
    try:
        success = main()
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print("\n\nInstal·lació cancel·lada per l'usuari.")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ Error inesperat: {e}")
        sys.exit(1)
