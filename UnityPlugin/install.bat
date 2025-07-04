@echo off
:: Script d'instal·lació per al plugin Hunyuan3D Unity
:: Aquest script automatitza la configuració inicial

echo ===============================================
echo INSTAL·LACIO HUNYUAN3D UNITY PLUGIN
echo ===============================================
echo.

:: Comprovar si Python està instal·lat
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python no esta instal·lat o accessible
    echo Si us plau, instal·la Python 3.8 o superior des de https://python.org
    pause
    exit /b 1
)

echo [INFO] Python detectat:
python --version

:: Executar el verificador de dependències
echo.
echo [INFO] Executant verificador de dependències...
python check_dependencies.py

if errorlevel 1 (
    echo.
    echo [ERROR] Hi ha hagut problemes amb les dependències
    echo Revisa els missatges d'error anteriors
    pause
    exit /b 1
)

:: Comprovar si els scripts existeixen
echo.
echo [INFO] Verificant scripts necessaris...

if not exist "batch_hunyuan3d.py" (
    echo [ERROR] batch_hunyuan3d.py no trobat
    echo Assegura't que aquest fitxer estigui al mateix directori
    pause
    exit /b 1
)

if not exist "remove_background.py" (
    echo [ERROR] remove_background.py no trobat
    echo Assegura't que aquest fitxer estigui al mateix directori
    pause
    exit /b 1
)

echo [OK] Scripts trobats correctament

:: Crear carpeta d'exemple si no existeix
if not exist "example_images" (
    echo [INFO] Creant carpeta d'exemple...
    mkdir example_images
    echo Pots posar imatges d'exemple a la carpeta 'example_images'
)

:: Crear carpeta de sortida si no existeix
if not exist "output_hunyuan3d" (
    echo [INFO] Creant carpeta de sortida...
    mkdir output_hunyuan3d
)

echo.
echo ===============================================
echo INSTAL·LACIO COMPLETADA
echo ===============================================
echo.
echo Passos següents:
echo 1. Copia la carpeta UnityPlugin al teu projecte Unity
echo 2. Obre Unity i navega a Tools ^> Hunyuan3D ^> 3D Model Generator
echo 3. Configura els paths de Python i scripts
echo 4. Comença a generar models 3D!
echo.
echo Per provar el sistema:
echo   python batch_hunyuan3d.py example_images\imatge.jpg
echo.
echo Documentació completa disponible a README.md
echo.
pause
