# Hunyuan3D Unity Plugin

This plugin allows generating 3D models from images using the Hunyuan3D-2 AI model directly from the Unity editor.

## Features

- 🎨 **Image to 3D model conversion**: Converts images (JPG, PNG, etc.) into 3D models (.obj, .fbx, .glb, etc.)
- 🗂️ **Batch mode**: Automatically processes entire folders of images
- 🎛️ **Configurable parameters**: Full control over model generation parameters
- 🔄 **Automatic background removal**: Option to remove image backgrounds before processing
- 📦 **Automatic import**: Generated models are automatically imported as Unity assets
- 💾 **Persistent configuration**: Configuration is automatically saved
- 📊 **Real-time tracking**: Progress bar and detailed process logs
- 🔧 **Integrated dependency manager**: Automatic installation of all Python dependencies
- ⚡ **Automatic GPU/CPU detection**: Automatically configures the best mode based on hardware
- 🚀 **Automatic CUDA installation**: Detects and automatically installs CUDA Toolkit if needed

## Requirements

1. **Unity 2020.3 or higher**
2. **Python 3.8 or higher** with the following dependencies:
   - Hunyuan3D-2 (official repository)
   - torch
   - PIL (Pillow)
   - rembg (for background removal)
   - trimesh
   - tqdm

3. **Python Scripts**:
   - `batch_hunyuan3d.py` - Main script for generating 3D models
   - `remove_background.py` - Script for removing image backgrounds

4. **For GPU acceleration (optional but recommended)**:
   - NVIDIA GPU with CUDA support
   - CUDA Toolkit 11.8 or 12.x
   - cuDNN

## Installation

### Option A: Automatic Installation (Recommended)

1. **Import the plugin** into your Unity project
2. **Open the dependency manager**: `Tools > Hunyuan3D > Dependency Manager`
3. **Automatic detection**: The system will detect your current Python configuration
4. **Quick installation**: Click "Windows Quick Install" (Windows) or follow manual steps
5. **Verification**: The system will verify that all dependencies are correctly installed

### Option B: Manual Configuration

If you prefer manual configuration:

#### 1. Python Environment
```bash
# Create virtual environment
python -m venv hunyuan3d_env
hunyuan3d_env\Scripts\activate  # Windows
source hunyuan3d_env/bin/activate  # Linux/Mac
```

#### 2. Core Dependencies
```bash
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
pip install diffusers transformers accelerate
```

#### 3. Image Processing
```bash
pip install pillow opencv-python rembg[new]
```

#### 4. Mesh Processing
```bash
pip install trimesh pymeshlab pygltflib xatlas
```

#### 5. Hunyuan3D Package
```bash
# Clone and install the official repository
git clone https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git
cd Hunyuan3D-2
pip install -r requirements.txt
pip install -e .
```

## Initial Configuration

1. **Open the generator**: `Tools > Hunyuan3D > 3D Model Generator`
2. **Configure paths**:
   - **Python Executable**: Path to your Python executable
   - **Script Base Path**: Folder containing the Python scripts
3. **Configure parameters**:
   - **Steps**: Number of inference steps (1-100, recommended: 30)
   - **Guidance Scale**: Guidance scale (1-20, recommended: 7.5)
   - **Seed**: Seed for reproducibility
   - **Octree Resolution**: Octree resolution (64-512, recommended: 256)
   - **File Type**: Output format (obj, fbx, glb, ply, stl)
4. **Advanced options**:
   - **Enable Text-to-3D**: Activate text-to-3D capabilities
   - **Disable Texture**: Disable texture generation
   - **Enable FlashVDM**: Accelerate the process (requires support)
   - **Compile Model**: Compile model for better performance
   - **Low VRAM Mode**: Mode for GPUs with low memory
   - **Remove Background**: Automatically remove image backgrounds

## Usage

### Single Image
1. Select **single image mode**
2. **Select image**: Click "..." to choose an image file
3. **Configure output**: Specify the output folder within Assets/
4. **Adjust parameters** according to your needs
5. **Generate**: Click "Generate 3D Model"

### Batch Processing
1. Select **batch mode**
2. **Select folder**: Choose a folder containing images
3. **Configure output**: All models will be saved in subfolders
4. **Process**: Click "Process Folder"

## Practical Usage Example

```csharp
// Example configuration for high-quality models
Steps: 50
Guidance Scale: 10.0
Octree Resolution: 384
File Type: fbx
Enable FlashVDM: true
```

```csharp
// Example configuration for fast processing
Steps: 15
Guidance Scale: 5.0
Octree Resolution: 128
File Type: obj
Low VRAM Mode: true
```

## Output Structure

Generated models are saved with the following structure:
```
Assets/Generated3DModels/
├── image1_abc123/
│   ├── image1.obj (or .fbx, .glb, etc.)
│   ├── image1.png (texture)
│   └── other_files...
└── image2_def456/
    ├── image2.obj
    └── ...
```

## Process Monitoring

- **Progress bar**: Shows overall process progress
- **Status messages**: Informs about the current stage
- **Detailed logs**: Displays complete output from Python scripts
- **Script verification**: Automatically checks if scripts exist

## Troubleshooting

### Python not found
- Verify that Python is installed and accessible from PATH
- Use the dependency manager to automatically detect Python

### CUDA errors
- Verify that you have an NVIDIA GPU with CUDA support
- Install CUDA Toolkit 11.8 or 12.x
- Use the dependency manager to verify CUDA installation

### Models not importing
- Verify that the output folder is within `Assets/`
- Check that Unity has write permissions

### Performance issues
- Use **Low VRAM Mode** for GPUs with less than 8GB VRAM
- Reduce **Octree Resolution** for faster processing
- Enable **FlashVDM** if supported

## Automatic CUDA Management

The plugin includes a comprehensive CUDA management system:

### Automatic Detection
- **Hardware detection**: Automatically identifies NVIDIA GPUs
- **Driver verification**: Checks NVIDIA driver compatibility
- **CUDA version detection**: Identifies installed CUDA versions
- **PyTorch compatibility**: Verifies PyTorch CUDA compatibility

### Additional Features
- **Complete verification**: Functional test of the entire stack (Python + PyTorch + CUDA)
- **PATH repair**: Helps configure environment variables if needed
- **Universal compatibility**: CPU fallback if CUDA is not available
- **Multiple versions**: Detects and works with different installed CUDA versions

## Performance Optimization

### For GPU (Recommended)
- **GPU**: NVIDIA RTX 3060 or higher
- **VRAM**: 8GB or more
- **CUDA**: Version 11.8 or 12.x
- **Configuration**: Enable FlashVDM, Octree Resolution 256-384

### For CPU (Backup)
- **CPU**: 8+ cores, 3.0GHz or higher
- **RAM**: 16GB or more
- **Configuration**: Low VRAM Mode, Octree Resolution 128-256

## Recommended Output Formats

- **For Unity**: .fbx (best compatibility and features)
- **For web**: .glb (optimized size and compatibility)
- **For editing**: .obj (universal support)
- **For 3D printing**: .stl (specialized format)

## Third-Party Licenses

This plugin uses the following third-party software:

### Hunyuan3D-2
- **License**: Apache License 2.0
- **Repository**: https://github.com/Tencent-Hunyuan/Hunyuan3D-2
- **Copyright**: Tencent

### PyTorch
- **License**: BSD 3-Clause License
- **Repository**: https://github.com/pytorch/pytorch
- **Copyright**: Facebook, Inc.

### Diffusers
- **License**: Apache License 2.0
- **Repository**: https://github.com/huggingface/diffusers
- **Copyright**: HuggingFace Inc.

### rembg
- **License**: MIT License
- **Repository**: https://github.com/danielgatis/rembg
- **Copyright**: Daniel Gatis

### Trimesh
- **License**: MIT License
- **Repository**: https://github.com/mikedh/trimesh
- **Copyright**: Michael Dawson-Haggerty

### PyMeshLab
- **License**: GPL v3
- **Repository**: https://github.com/cnr-isti-vclab/PyMeshLab
- **Copyright**: Visual Computing Lab, ISTI - CNR

## Support

For support and questions:
1. Check the **troubleshooting** section
2. Use the **dependency manager** to verify your installation
3. Check the **installation logs** for specific errors
4. Open an issue in the repository if problems persist

## License

This plugin is provided under the MIT License. See the LICENSE file for details.

Note: While this plugin is MIT licensed, please respect the licenses of the third-party dependencies it uses.