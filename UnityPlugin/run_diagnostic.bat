@echo off
echo =========================================
echo DIAGNOSTIC HUNYUAN3D UNITY PLUGIN
echo =========================================
echo.

echo Comprovant Python...
python --version
if %ERRORLEVEL% neq 0 (
    echo ERROR: Python no trobat. Instal la Python 3.8+ des de python.org
    pause
    exit /b 1
)
echo.

echo Executant diagnostic complet...
python diagnostic.py

echo.
echo =========================================
echo DIAGNOSTIC COMPLETAT
echo =========================================
echo.

echo Prova executar dins d Unity:
echo Tools ^> Hunyuan3D ^> Dependency Manager
echo.

pause
