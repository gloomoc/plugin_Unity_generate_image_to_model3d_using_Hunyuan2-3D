# Script d'instal·lació de Hunyuan3D-2 per Windows
# Basat en https://github.com/sdbds/Hunyuan3D-2-for-windows

param(
    [string]$InstallPath = $PSScriptRoot,
    [string]$PythonVersion = "3.10",
    [switch]$UseCUDA12 = $true,
    [switch]$LowDiskMode = $false,
    [switch]$SkipModelDownload = $false
)

Set-Location $InstallPath

# Configuració d'entorn
$Env:HF_HOME = "huggingface"
$Env:PIP_DISABLE_PIP_VERSION_CHECK = 1
$Env:PIP_NO_CACHE_DIR = 1
$Env:UV_EXTRA_INDEX_URL = if ($UseCUDA12) { "https://download.pytorch.org/whl/cu124" } else { "https://download.pytorch.org/whl/cu118" }
$Env:UV_CACHE_DIR = "${env:LOCALAPPDATA}/uv/cache"
$Env:UV_NO_BUILD_ISOLATION = 1
$Env:UV_NO_CACHE = 0
$Env:UV_LINK_MODE = "symlink"
$Env:GIT_LFS_SKIP_SMUDGE = 1
$Env:CUDA_HOME = "${env:CUDA_PATH}"

# Colors per output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Info {
    Write-ColorOutput Cyan @args
}

function Write-Success {
    Write-ColorOutput Green @args
}

function Write-Error {
    Write-ColorOutput Red @args
}

function InstallFail {
    Write-Error "❌ Instal·lació fallida."
    Write-Output "Prem qualsevol tecla per sortir..."
    Read-Host | Out-Null
    Exit 1
}

function Check {
    param (
        $ErrorInfo
    )
    if (!($?)) {
        Write-Error $ErrorInfo
        InstallFail
    }
}

# Banner
Write-Info @"
╔═══════════════════════════════════════════════════════════════╗
║           Hunyuan3D-2 per Windows - Instal·lador              ║
║                  Integració amb Unity                          ║
╚═══════════════════════════════════════════════════════════════╝
"@

Write-Info "📁 Directori d'instal·lació: $InstallPath"
Write-Info "🐍 Python versió: $PythonVersion"
Write-Info "🎮 CUDA: $(if ($UseCUDA12) { 'CUDA 12.4' } else { 'CUDA 11.8' })"
Write-Info ""

# Verificar espai al disc
Write-Info "🔍 Verificant espai al disc..."
try {
    # Comprovar cache UV primer
    if (Test-Path -Path "${env:LOCALAPPDATA}/uv/cache") {
        Write-Info "✓ Directori cache UV ja existeix"
    }
    else {
        $CDrive = Get-WmiObject Win32_LogicalDisk -Filter "DeviceID='C:'" -ErrorAction Stop
        if ($CDrive) {
            $FreeSpaceGB = [math]::Round($CDrive.FreeSpace / 1GB, 2)
            Write-Info "💾 Espai lliure a C: ${FreeSpaceGB}GB"
            
            if ($FreeSpaceGB -lt 15) {
                Write-Error "⚠️  Espai insuficient detectat. Es recomanen almenys 15GB."
                if ($FreeSpaceGB -lt 10) {
                    Write-Info "📁 Usant directori cache local degut a poc espai"
                    $Env:UV_CACHE_DIR = ".cache"
                    $LowDiskMode = $true
                }
            }
        }
    }
}
catch {
    Write-Error "⚠️  No s'ha pogut verificar l'espai: $_"
}

# Instal·lar UV si no està instal·lat
Write-Info "🔧 Verificant UV (gestor de paquets ràpid)..."
try {
    $uvPath = "$HOME\.local\bin\uv"
    & $uvPath --version | Out-Null
    Write-Success "✓ UV ja està instal·lat"
}
catch {
    Write-Info "📦 Instal·lant UV..."
    try {
        # Descarregar i executar script d'instal·lació
        Invoke-Expression ((Invoke-WebRequest -UseBasicParsing https://astral.sh/uv/install.ps1).Content)
        Check "❌ Error instal·lant UV"
        Write-Success "✓ UV instal·lat correctament"
    }
    catch {
        Write-Error "❌ No s'ha pogut instal·lar UV automàticament"
        Write-Info "Instal·la manualment des de: https://github.com/astral/uv"
        InstallFail
    }
}

# Verificar Git
Write-Info "🔍 Verificant Git..."
try {
    git --version | Out-Null
    Write-Success "✓ Git detectat"
}
catch {
    Write-Error "❌ Git no està instal·lat"
    Write-Info "Descarrega Git des de: https://git-scm.com/download/win"
    
    $installGit = Read-Host "Vols que obri la pàgina de descàrrega? (S/N)"
    if ($installGit -eq 'S' -or $installGit -eq 's') {
        Start-Process "https://git-scm.com/download/win"
    }
    InstallFail
}

# Crear o activar entorn virtual
Write-Info "🐍 Configurant entorn Python..."
$venvPath = ".venv"
$activateScript = "$venvPath\Scripts\Activate.ps1"

if (Test-Path $activateScript) {
    Write-Info "✓ Entorn virtual existent trobat"
    & $activateScript
}
else {
    Write-Info "📦 Creant nou entorn virtual amb Python $PythonVersion..."
    & $uvPath venv -p $PythonVersion
    Check "❌ Error creant entorn virtual"
    & $activateScript
    Write-Success "✓ Entorn virtual creat"
}

# Actualitzar pip i eines bàsiques
Write-Info "📦 Actualitzant eines bàsiques..."
& $uvPath pip install --upgrade pip setuptools wheel
Check "❌ Error actualitzant eines bàsiques"

# Instal·lar PyTorch amb CUDA
Write-Info "🔥 Instal·lant PyTorch amb $(if ($UseCUDA12) { 'CUDA 12.4' } else { 'CUDA 11.8' })..."
if ($UseCUDA12) {
    & $uvPath pip install torch==2.5.1+cu124 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
} else {
    & $uvPath pip install torch==2.5.1+cu118 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
}
Check "❌ Error instal·lant PyTorch"
Write-Success "✓ PyTorch instal·lat"

# Clonar repositori Hunyuan3D-2 si no existeix
$repoPath = "Hunyuan3D-2"
if (-not (Test-Path $repoPath)) {
    Write-Info "📥 Clonant repositori Hunyuan3D-2..."
    git clone --depth 1 https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git $repoPath
    Check "❌ Error clonant repositori"
    Write-Success "✓ Repositori clonat"
} else {
    Write-Info "✓ Repositori ja existeix a $repoPath"
}

# Crear requirements-uv.txt optimitzat
Write-Info "📝 Creant fitxer de requirements optimitzat..."
$requirementsContent = @"
# Core dependencies
diffusers>=0.21.0
transformers>=4.25.0
accelerate
omegaconf
einops
tqdm

# Image processing
opencv-python
pillow>=9.5.0
rembg
onnxruntime

# 3D processing
trimesh>=3.15.0
pymeshlab
pygltflib
xatlas
ninja


# Optional but recommended
gradio>=4.0.0
fastapi
uvicorn
bpy

# Development tools
ipywidgets
ipython
setuptools
"@

$requirementsContent | Out-File -FilePath "requirements-uv.txt" -Encoding UTF8
Write-Success "✓ Requirements creat"

# Instal·lar dependències principals
Write-Info "📦 Instal·lant dependències principals..."
& $uvPath pip sync requirements-uv.txt --index-strategy unsafe-best-match
Check "❌ Error instal·lant dependències"
Write-Success "✓ Dependències instal·lades"

# Instal·lar Triton només si CUDA està disponible
Write-Info "🔧 Verificant suport per compilació..."
try {
    # Verificar si CUDA està disponible
    $cudaAvailable = $false
    if ($UseCUDA12 -or !$UseCUDA12) {
        # Intentar detectar CUDA
        if ($env:CUDA_PATH -or $env:CUDA_HOME) {
            $cudaAvailable = $true
            Write-Info "✓ CUDA detectat, instal·lant Triton..."
            
            try {
                if ($UseCUDA12) {
                    & $uvPath pip install triton --index-url https://download.pytorch.org/whl/cu124
                } else {
                    & $uvPath pip install triton --index-url https://download.pytorch.org/whl/cu118
                }
                Write-Success "✓ Triton instal·lat (suport per --compile)"
            }
            catch {
                Write-Error "⚠️ No s'ha pogut instal·lar Triton"
                Write-Info "    El paràmetre --compile no estarà disponible"
            }
        }
    }
    
    if (-not $cudaAvailable) {
        Write-Info "⚠️ CUDA no detectat, saltant instal·lació de Triton"
        Write-Info "    El paràmetre --compile no estarà disponible"
    }
}
catch {
    Write-Error "⚠️ Error verificant CUDA: $_"
}

# Instal·lar dependències FBX opcionals
Write-Info "📦 Instal·lant dependències opcionals per FBX..."
try {
    # PyMeshLab és el més estable per FBX
    & $uvPath pip install pymeshlab --quiet
    Write-Success "  ✓ PyMeshLab instal·lat"
}
catch {
    Write-Error "  ⚠️ PyMeshLab no disponible"
}

try {
    # Open3D com alternativa
    & $uvPath pip install open3d --quiet
    Write-Success "  ✓ Open3D instal·lat"
}
catch {
    Write-Error "  ⚠️ Open3D no disponible"
}

# bpy (Blender) - només si l'usuari ho vol explícitament
$installBpy = Read-Host "Vols instal·lar Blender Python API (bpy)? Pot trigar molt temps (S/N)"
if ($installBpy -eq 'S' -or $installBpy -eq 's') {
    Write-Info "📦 Instal·lant Blender Python API (això pot trigar 5-10 minuts)..."
    try {
        & $uvPath pip install bpy --quiet
        Write-Success "  ✓ Blender Python API instal·lat"
    }
    catch {
        Write-Error "  ⚠️ No s'ha pogut instal·lar bpy"
        Write-Info "    Això no afectarà la funcionalitat principal"
    }
}

# Intentar compilar mòduls C++ opcionals
Write-Info "🔨 Intentant compilar mòduls C++ opcionals..."

# Primer instal·lar dependències necessàries per compilació
Write-Info "  📦 Instal·lant dependències de compilació..."
& $uvPath pip install pybind11 ninja setuptools wheel --quiet
Check "❌ Error instal·lant dependències de compilació"

$custRasterPath = "$repoPath\hy3dgen\texgen\custom_rasterizer"
$diffRendererPath = "$repoPath\hy3dgen\texgen\differentiable_renderer"

if (Test-Path $custRasterPath) {
    Write-Info "  Compilant custom_rasterizer..."
    Push-Location $custRasterPath
    try {
        # Usar uv pip per executar setup.py dins l'entorn virtual
        & $uvPath pip install torch torchvision torchaudio
        & $uvPath pip install . --no-deps --force-reinstall --quiet
        Write-Success "  ✓ custom_rasterizer compilat"
    }
    catch {
        Write-Error "  ⚠️ No s'ha pogut compilar custom_rasterizer (opcional)"
        Write-Info "    Això no afectarà la funcionalitat principal"
    }
    Pop-Location
}

if (Test-Path $diffRendererPath) {
    Write-Info "  Compilant differentiable_renderer..."
    Push-Location $diffRendererPath
    try {
        # Usar uv pip per executar setup.py dins l'entorn virtual
        & $uvPath pip install . --no-deps --force-reinstall --quiet
        Write-Success "  ✓ differentiable_renderer compilat"
    }
    catch {
        Write-Error "  ⚠️ No s'ha pogut compilar diferenciable_renderer (opcional)"
        Write-Info "    Això no afectarà la funcionalitat principal"
    }
    Pop-Location
}

# Instal·lar Hunyuan3D-2 en mode desenvolupament
Write-Info "🚀 Instal·lant Hunyuan3D-2..."
Push-Location $repoPath
& $uvPath pip install -e . --quiet
$installSuccess = $?
Pop-Location

if ($installSuccess) {
    Write-Success "✓ Hunyuan3D-2 instal·lat correctament"
} else {
    Write-Error "⚠️ Possible error instal·lant Hunyuan3D-2"
}

# Descarregar models si no s'ha saltat
if (-not $SkipModelDownload) {
    Write-Info "📥 Vols descarregar els models pre-entrenats? (~10GB)"
    $downloadModels = Read-Host "(S/N)"
    
    if ($downloadModels -eq 'S' -or $downloadModels -eq 's') {
        Write-Info "Descarregant models..."
        
        # Crear directori per models
        $modelsDir = "models"
        New-Item -ItemType Directory -Force -Path $modelsDir | Out-Null
        
        # Script Python per descarregar models
        $downloadScript = @"
from huggingface_hub import snapshot_download
import os

models = [
    'tencent/Hunyuan3D-2',
    'tencent/Hunyuan3D-2mini'
]

for model in models:
    print(f'Descarregant {model}...')
    try:
        snapshot_download(
            repo_id=model,
            local_dir=f'models/{model.split("/")[-1]}',
            local_dir_use_symlinks=False
        )
        print(f'✓ {model} descarregat')
    except Exception as e:
        print(f'✗ Error descarregant {model}: {e}')
"@
        
        $downloadScript | Out-File -FilePath "download_models.py" -Encoding UTF8
        python download_models.py
        Remove-Item "download_models.py"
    }
}

# Verificació final
# Verificació final ampliada
Write-Info "`n🔍 Verificant instal·lació..."
$verifyScript = @"
import sys
print('Python:', sys.version)

try:
    import torch
    print(f'✓ PyTorch {torch.__version__}')
    print(f'  CUDA disponible: {torch.cuda.is_available()}')
    if torch.cuda.is_available():
        print(f'  CUDA version: {torch.version.cuda}')
        print(f'  GPU: {torch.cuda.get_device_name(0)}')
except ImportError:
    print('✗ PyTorch no instal·lat')

try:
    import hy3dgen
    print('✓ Hunyuan3D importat correctament')
except ImportError:
    print('✗ Hunyuan3D no es pot importar')

# Verificar Triton per compilació
try:
    import triton
    print('✓ Triton disponible (suport --compile)')
except ImportError:
    print('⚠️ Triton no disponible (--compile no funcionarà)')

# Verificar dependències FBX
fbx_support = []
try:
    import pymeshlab
    fbx_support.append('PyMeshLab')
except ImportError:
    pass

try:
    import open3d
    fbx_support.append('Open3D')
except ImportError:
    pass

try:
    import bpy
    fbx_support.append('Blender')
except ImportError:
    pass

if fbx_support:
    print(f'✓ Suport FBX: {", ".join(fbx_support)}')
else:
    print('⚠️ Cap suport FBX detectat')

required_packages = [
    'diffusers', 'transformers', 'trimesh', 
    'cv2', 'rembg', 'gradio'
]

for package in required_packages:
    try:
        __import__(package)
        print(f'✓ {package}')
    except ImportError:
        print(f'✗ {package}')
"@

$verifyScript | Out-File -FilePath "verify_install.py" -Encoding UTF8
python verify_install.py
Remove-Item "verify_install.py"


# Crear script d'inici millorat
Write-Info "`n📝 Creant scripts d'inici..."
$startScript = @"
@echo off
cd /d "%~dp0"
call .venv\Scripts\activate
echo.
echo ✨ Hunyuan3D-2 Environment Activated ✨
echo.
echo 📋 Opcions disponibles:
echo   - python batch_hunyuan3d.py [imatge] --file_type obj
echo   - python batch_hunyuan3d.py [imatge] --file_type fbx
echo   - python batch_hunyuan3d.py [imatge] --disable_tex --steps 10
echo   - python -m gradio app.py
echo.
echo 💡 Consells:
echo   - Usa --file_type obj per més velocitat
echo   - Usa --file_type fbx només si tens PyMeshLab/Open3D
echo   - Evita --compile si Triton no està disponible
echo.
cmd /k
"@

$startScript | Out-File -FilePath "start_hunyuan3d.bat" -Encoding ASCII

# Crear script de test ràpid
$testScript = @"
@echo off
echo Testejant instal·lació...
call .venv\Scripts\activate
python -c "
try:
    import hy3dgen
    print('✓ Hunyuan3D funcionant')
except Exception as e:
    print(f'✗ Error: {e}')

try:
    import triton
    print('✓ Triton disponible (--compile OK)')
except:
    print('⚠️ Triton no disponible (evita --compile)')

try:
    import pymeshlab
    print('✓ PyMeshLab (FBX suportat)')
except:
    print('⚠️ PyMeshLab no disponible')
"
pause
"@

$testScript | Out-File -FilePath "test_install.bat" -Encoding ASCII

Write-Success @"

✨ Instal·lació completada! ✨

📁 Estructura creada:
   $InstallPath\
   ├── .venv\                 (Entorn Python)
   ├── Hunyuan3D-2\           (Codi font)
   ├── models\                (Models pre-entrenats)
   ├── start_hunyuan3d.bat    (Script d'inici)
   └── test_install.bat       (Test ràpid)

🚀 Per començar:
   1. Executa: start_hunyuan3d.bat
   2. Test ràpid: test_install.bat
   3. O activa manualment: .venv\Scripts\activate

⚡ Recomanacions per Unity:
   - Usa --file_type obj per màxima compatibilitat
   - Evita --compile si veus errors de Triton
   - Usa --low_vram_mode si tens poca VRAM

📖 Documentació:
   https://github.com/Tencent-Hunyuan/Hunyuan3D-2

"@

Write-Output "Prem qualsevol tecla per sortir..."
Read-Host | Out-Null