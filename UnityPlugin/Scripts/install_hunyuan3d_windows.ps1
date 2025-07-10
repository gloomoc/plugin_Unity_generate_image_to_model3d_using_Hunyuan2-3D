# Hunyuan3D-2 Installation Script for Windows
# Based on https://github.com/sdbds/Hunyuan3D-2-for-windows
# UTF-8 encoding configuration included

param(
    [string]$InstallPath = $PSScriptRoot,
    [string]$PythonVersion = "3.10",
    [switch]$UseCUDA12 = $true,
    [switch]$LowDiskMode = $false,
    [switch]$SkipModelDownload = $false
)

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

function Configure-UTF8Encoding {
    <#
    .SYNOPSIS
    Configures PowerShell to use UTF-8 encoding
    .DESCRIPTION
    This function configures UTF-8 encoding to avoid issues with special characters
    #>
    try {
        # Configure encoding for current session
        $OutputEncoding = [System.Text.Encoding]::UTF8
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
        [Console]::InputEncoding = [System.Text.Encoding]::UTF8
        
        # Configure PowerShell encoding
        $PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'
        $PSDefaultParameterValues['Export-Csv:Encoding'] = 'utf8'
        
        Write-Host "UTF-8 encoding configured for this session" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "Could not configure UTF-8 encoding: $_" -ForegroundColor Yellow
        return $false
    }
}

function InstallFail {
    Write-Error "Installation failed."
    Write-Output "Press any key to exit..."
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

# Configure UTF-8 encoding
Configure-UTF8Encoding

# Configure script execution policy if needed
$ExecutionPolicy = Get-ExecutionPolicy -Scope CurrentUser
if ($ExecutionPolicy -eq "Restricted") {
    Write-Host "Configuring script execution policy for current user..." -ForegroundColor Yellow
    Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
    Write-Host "Execution policy configured" -ForegroundColor Green
}

# Clone Hunyuan3D-2 repository if it doesn't exist
$repoPath = $InstallPath + "\Hunyuan3D-2-for-windows"
if (-not (Test-Path $repoPath)) {
    Write-Info "Cloning Hunyuan3D-2 for windows repository..."
    git clone --depth 1 https://github.com/sdbds/Hunyuan3D-2-for-windows.git $repoPath
    Check "Error cloning repository"
    Write-Success "Repository cloned"
} else {
    Write-Info "Repository already exists at $repoPath"
}

Set-Location $InstallPath

# Environment configuration
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

# Banner
Write-Info @"
===================================================
           Hunyuan3D-2 for Windows - Installer
                  Unity Integration
===================================================
"@

Write-Info "Installation directory: $InstallPath"
Write-Info "Python version: $PythonVersion"
Write-Info "CUDA: $(if ($UseCUDA12) { 'CUDA 12.4' } else { 'CUDA 11.8' })"
Write-Info ""

# Check disk space
Write-Info "Checking disk space..."
try {
    # Check UV cache first
    if (Test-Path -Path "${env:LOCALAPPDATA}/uv/cache") {
        Write-Info "UV cache directory already exists"
    }
    else {
        $CDrive = Get-WmiObject Win32_LogicalDisk -Filter "DeviceID='C:'" -ErrorAction Stop
        if ($CDrive) {
            $FreeSpaceGB = [math]::Round($CDrive.FreeSpace / 1GB, 2)
            Write-Info "Free space on C: ${FreeSpaceGB}GB"
            
            if ($FreeSpaceGB -lt 15) {
                Write-Error "Insufficient space detected. At least 15GB recommended."
                if ($FreeSpaceGB -lt 10) {
                    Write-Info "Using local cache directory due to low space"
                    $Env:UV_CACHE_DIR = ".cache"
                    $LowDiskMode = $true
                }
            }
        }
    }
}
catch {
    Write-Error "Could not check disk space: $_"
}

# Install UV if not installed
Write-Info "Checking UV (fast package manager)..."
try {
    $uvPath = "$HOME\.local\bin\uv"
    & $uvPath --version | Out-Null
    Write-Success "UV is already installed"
}
catch {
    Write-Info "Installing UV..."
    try {
        # Download and run installation script
        Invoke-Expression ((Invoke-WebRequest -UseBasicParsing https://astral.sh/uv/install.ps1).Content)
        Check "Error installing UV"
        Write-Success "UV installed successfully"
    }
    catch {
        Write-Error "Could not install UV automatically"
        Write-Info "Install manually from: https://github.com/astral/uv"
        InstallFail
    }
}

# Check Git
Write-Info "Checking Git..."
try {
    git --version | Out-Null
    Write-Success "Git detected"
}
catch {
    Write-Error "Git is not installed"
    Write-Info "Download Git from: https://git-scm.com/download/win"
    
    $installGit = Read-Host "Do you want to open the download page? (Y/N)"
    if ($installGit -eq 'Y' -or $installGit -eq 'y') {
        Start-Process "https://git-scm.com/download/win"
    }
    InstallFail
}

# Create or activate virtual environment
Write-Info "Setting up Python environment..."
$venvPath = ".venv"
$activateScript = "$venvPath\Scripts\Activate.ps1"

if (Test-Path $activateScript) {
    Write-Info "Existing virtual environment found"
    & $activateScript
}
else {
    Write-Info "Creating new virtual environment with Python $PythonVersion..."
    & $uvPath venv -p $PythonVersion
    Check "Error creating virtual environment"
    & $activateScript
    Write-Success "Virtual environment created"
}

# Update pip and basic tools
Write-Info "Updating basic tools..."
& $uvPath pip install --upgrade pip setuptools wheel
Check "Error updating basic tools"

# Install PyTorch with CUDA
Write-Info "Installing PyTorch with $(if ($UseCUDA12) { 'CUDA 12.4' } else { 'CUDA 11.8' })..."
if ($UseCUDA12) {
    & $uvPath pip install torch==2.5.1+cu124 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
} else {
    & $uvPath pip install torch==2.5.1+cu118 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
}
Check "Error installing PyTorch"
Write-Success "PyTorch installed"

# Create optimized requirements-uv.txt
Write-Info "Creating optimized requirements file..."
$requirementsContent = @"
# Core dependencies
diffusers>=0.21.0
transformers>=4.25.0
accelerate
omegaconf
einops
tqdm
mmgp
optimum
optimum.quanto

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
triton-windows==3.2.0.post11
"@

$requirementsContent | Out-File -FilePath "requirements-uv.txt" -Encoding UTF8
Write-Success "Requirements created"

# Install main dependencies
Write-Info "Installing main dependencies..."
& $uvPath pip sync requirements-uv.txt --index-strategy unsafe-best-match
Check "Error installing dependencies"
Write-Success "Dependencies installed"

# Install Triton only if CUDA is available
Write-Info "Checking compilation support..."
try {
    # Check if CUDA is available
    $cudaAvailable = $false
    if ($UseCUDA12 -or !$UseCUDA12) {
        # Try to detect CUDA
        if ($env:CUDA_PATH -or $env:CUDA_HOME) {
            $cudaAvailable = $true
            Write-Info "CUDA detected, installing Triton..."
            
            try {
                if ($UseCUDA12) {
                    & $uvPath pip install triton-windows --index-url https://download.pytorch.org/whl/cu124
                } else {
                    & $uvPath pip install triton-windows --index-url https://download.pytorch.org/whl/cu118
                }
                Write-Success "Triton installed (--compile support)"
            }
            catch {
                Write-Error "Could not install Triton"
                Write-Info "    --compile parameter will not be available"
            }
        }
    }
    
    if (-not $cudaAvailable) {
        Write-Info "CUDA not detected, skipping Triton installation"
        Write-Info "    --compile parameter will not be available"
    }
}
catch {
    Write-Error "Error checking CUDA: $_"
}

# Install optional FBX dependencies
Write-Info "Installing optional FBX dependencies..."
try {
    # PyMeshLab is the most stable for FBX
    & $uvPath pip install pymeshlab --quiet
    Write-Success "  PyMeshLab installed"
}
catch {
    Write-Error "  PyMeshLab not available"
}

try {
    # Open3D as alternative
    & $uvPath pip install open3d --quiet
    Write-Success "  Open3D installed"
}
catch {
    Write-Error "  Open3D not available"
}

# bpy (Blender) - only if user wants it explicitly
Write-Info "Installing Blender Python API (this may take 5-10 minutes)..."
try {
    & $uvPath pip install bpy --quiet
    Write-Success "  Blender Python API installed"
}
catch {
    Write-Error "  Could not install bpy"
    Write-Info "    This will not affect main functionality"
}

# Try to compile optional C++ modules
Write-Info "Attempting to compile optional C++ modules..."

# First install compilation dependencies
Write-Info "  Installing compilation dependencies..."
& $uvPath pip install pybind11 ninja setuptools wheel --quiet
Check "Error installing compilation dependencies"

$custRasterPath = "$repoPath\hy3dgen\texgen\custom_rasterizer"
$diffRendererPath = "$repoPath\hy3dgen\texgen\differentiable_renderer"

if (Test-Path $custRasterPath) {
    Write-Info "  Compiling custom_rasterizer..."
    Push-Location $custRasterPath
    try {
        # Use uv pip to run setup.py within virtual environment
        & $uvPath pip install torch torchvision torchaudio
        & $uvPath pip install . --force-reinstall --quiet
        Write-Success "  custom_rasterizer compiled"
    }
    catch {
        Write-Error "  Could not compile custom_rasterizer (optional)"
        Write-Info "    This will not affect main functionality"
    }
    Pop-Location
}

if (Test-Path $diffRendererPath) {
    Write-Info "  Compiling differentiable_renderer..."
    Push-Location $diffRendererPath
    try {
        # Use uv pip to run setup.py within virtual environment
        & $uvPath pip install . --force-reinstall --quiet
        Write-Success "  differentiable_renderer compiled"
    }
    catch {
        Write-Error "  Could not compile differentiable_renderer (optional)"
        Write-Info "    This will not affect main functionality"
    }
    Pop-Location
}

# Install Hunyuan3D-2 in development mode
Write-Info "Installing Hunyuan3D-2..."
Push-Location $repoPath
& $uvPath pip install -e . --quiet
$installSuccess = $?
Pop-Location

if ($installSuccess) {
    Write-Success "Hunyuan3D-2 installed successfully"
} else {
    Write-Error "Possible error installing Hunyuan3D-2"
}

# Final verification
Write-Info "`nVerifying installation..."
$verifyScript = @"
import sys
print('Python:', sys.version)

try:
    import torch
    print(f'PyTorch {torch.__version__}')
    print(f'  CUDA available: {torch.cuda.is_available()}')
    if torch.cuda.is_available():
        print(f'  CUDA version: {torch.version.cuda}')
        print(f'  GPU: {torch.cuda.get_device_name(0)}')
except ImportError:
    print('PyTorch not installed')

try:
    import hy3dgen
    print('Hunyuan3D imported successfully')
except ImportError:
    print('Hunyuan3D cannot be imported')

# Check Triton for compilation
try:
    import triton
    print('Triton available (--compile support)')
except ImportError:
    print('Triton not available (--compile will not work)')

# Check FBX dependencies
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
    print(f'FBX support: {", ".join(fbx_support)}')
else:
    print('No FBX support detected')

required_packages = [
    'diffusers', 'transformers', 'trimesh', 
    'cv2', 'rembg', 'gradio'
]

for package in required_packages:
    try:
        __import__(package)
        print(f'{package}')
    except ImportError:
        print(f'{package}')
"@

$verifyScript | Out-File -FilePath "verify_install.py" -Encoding UTF8
python verify_install.py
Remove-Item "verify_install.py"

# Create improved startup script
Write-Info "`nCreating startup scripts..."
$startScript = @"
@echo off
cd /d "%~dp0"
call .venv\Scripts\activate
echo.
echo Hunyuan3D-2 Environment Activated
echo.
echo Available options:
echo   - python batch_hunyuan3d.py [image] --file_type obj
echo   - python batch_hunyuan3d.py [image] --file_type fbx
echo   - python batch_hunyuan3d.py [image] --disable_tex --steps 10
echo   - python -m gradio app.py
echo.
echo Tips:
echo   - Use --file_type obj for faster processing
echo   - Use --file_type fbx only if you have PyMeshLab/Open3D
echo   - Avoid --compile if Triton is not available
echo.
cmd /k
"@

$startScript | Out-File -FilePath "start_hunyuan3d.bat" -Encoding ASCII

# Create quick test script
$testScript = @"
@echo off
echo Testing installation...
call .venv\Scripts\activate
python -c "
try:
    import hy3dgen
    print('Hunyuan3D working')
except Exception as e:
    print(f'Error: {e}')

try:
    import triton
    print('Triton available (--compile OK)')
except:
    print('Triton not available')

try:
    import pymeshlab
    print('PyMeshLab (FBX supported)')
except:
    print('PyMeshLab not available')
"
pause
"@

$testScript | Out-File -FilePath "test_install.bat" -Encoding ASCII

Write-Output "Press any key to exit..."
Read-Host | Out-Null 