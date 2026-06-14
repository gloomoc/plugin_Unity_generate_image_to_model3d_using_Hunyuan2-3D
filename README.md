# Unity Plugin — Image / Text to 3D using Hunyuan3D 2.1

A **Unity Editor plugin** that generates **textured 3D meshes** from an **image** (or a **text prompt**)
using Tencent's **Hunyuan3D‑2.1** model — directly inside the Unity Editor.

The plugin itself contains *no* ML code: the C# editor layer builds a command line and runs the real
work as a **Python subprocess**, streams progress back into the UI, and imports the finished asset into
your project automatically.

> ✅ Verified end‑to‑end on **Windows 11 + NVIDIA RTX 4090 + CUDA 13.2**: shape + full **PBR texture**
> (albedo / metallic / roughness) + **FBX** export, for both image‑to‑3D and text‑to‑3D.

---

## What it does

1. You give it an **image** (single file or a whole folder) or a **text prompt**.
2. It removes the background, generates the 3D shape, cleans the mesh (floaters / degenerate faces /
   face reduction), bakes a **PBR texture**, and exports the model.
3. The result is **imported into Unity automatically** as a ready‑to‑use asset.

Two cooperating processes talk over argv + streamed stdout/stderr:

```
Unity Editor (C#)                       Python subprocess (Hunyuan3D-2.1)
  3D Model Generator  ──spawns──►  batch_hunyuan3d.py <image|--caption> --flags...
       ▲  parses progress                  │  image/text → shape → clean → PBR texture → export
       └──────── stdout/stderr ◄───────────┘  writes mesh + textures + previews + stats.json
```

---

## Features

- 🎨 **Image → 3D**: JPG / PNG / BMP / WEBP / TIFF → textured 3D mesh.
- ✍️ **Text → 3D**: type a prompt; HunyuanDiT generates the image, then it runs image → 3D.
- 🧵 **Full PBR texturing**: albedo + metallic + roughness maps (configured on the OBJ/MTL).
- 📦 **Multiple output formats**: OBJ, FBX, GLB, PLY, STL.
- 🗂️ **Batch mode**: process an entire folder of images automatically.
- 🔄 **Automatic background removal** (toggleable).
- ⚡ **FlashVDM** acceleration and **Low‑VRAM** mode.
- 🧩 **Integrated Dependency Manager**: installs Python, UV, PyTorch (CUDA‑matched), and the Hunyuan3D
  repo, and builds the native CUDA extensions for texturing.
- 🖥️ **Automatic GPU/CPU + CUDA detection**, with CPU fallback.
- 📥 **Auto‑import** of generated models into `Assets/`, with live progress and detailed logs.
- 💾 **Persistent configuration** (stored in `%APPDATA%/Unity/Hunyuan3D/config.json`).

---

## What it supports

| Area | Supported |
|---|---|
| **Model** | `tencent/Hunyuan3D-2.1` — shape `hunyuan3d-dit-v2-1`, paint `hunyuan3d-paintpbr-v2-1` (+ `facebook/dinov2-giant`, RealESRGAN x4) |
| **Text‑to‑3D model** | `Tencent-Hunyuan/HunyuanDiT-v1.1-Diffusers-Distilled` (via diffusers) |
| **Input images** | `.jpg`, `.jpeg`, `.png`, `.bmp`, `.webp`, `.tiff` |
| **Output meshes** | `.obj`, `.fbx`, `.glb`, `.ply`, `.stl` (FBX via bpy / pymeshlab / open3d) |
| **Texture** | PBR: base color (albedo), metallic, roughness |
| **Compute** | NVIDIA CUDA (recommended) or CPU fallback |
| **CUDA Toolkit** | 11.x → cu118, 12.x → cu124, 13.x → cu130 (auto‑matched) |
| **Python** | 3.8 – 3.12 (3.11 recommended) |
| **OS** | Windows 10/11 (automatic install flow). The editor code is Editor‑only and cross‑platform; the one‑click installer targets Windows. |
| **Unity** | 2020.3 LTS or newer |

---

## Dependencies

Installed automatically by the **Dependency Manager**. Listed here for reference.

**Core (PyTorch / diffusion)**
- `torch`, `torchvision` (CUDA wheel matched to your toolkit: cu118 / cu124 / cu130)
- `diffusers`, `transformers`

**Mesh processing**
- `trimesh`, `pymeshlab`, `pygltflib`, `xatlas`

**Image processing**
- `opencv-python`, `rembg`, `onnxruntime`, `Pillow`

**Utilities / build**
- `numpy`, `tqdm`, `omegaconf`, `einops`, `ninja`, `pybind11`, `triton-windows`, `sentencepiece`

**Optional**
- `accelerate`, `gradio`, `fastapi`, `uvicorn`
- `bpy` (Blender Python) — for FBX export

**Native texturing extensions** (built from the Hunyuan3D‑2.1 repo during install)
- `custom_rasterizer` (CUDA C++) and `mesh_inpaint_processor` (pybind11) — required for PBR texture baking.
  Building these needs **Visual Studio Build Tools** + a CUDA toolkit matching the installed PyTorch.

**External tooling**
- [UV](https://github.com/astral-sh/uv) (astral) for the managed virtual environment
- Git (to clone the official [Hunyuan3D‑2.1](https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1) repo)

---

## Quick start

1. Copy `UnityPlugin/` into your Unity project's `Assets/` folder.
2. Open **`Tools > Hunyuan3D > Dependency Manager`** and run the essential flow:
   *Detect → Check Dependencies → Install All → Install Hunyuan3D Package → Verify Installation*.
3. Open **`Tools > Hunyuan3D > 3D Model Generator`**.
4. Pick an image (or enable **Text‑to‑3D** and type a prompt), choose the output format, and click **Generate**.

In‑depth plugin docs are in [`UnityPlugin/README.md`](UnityPlugin/README.md).

---

## Generation parameters

| Parameter | Range / values | Notes |
|---|---|---|
| Steps | 1–100 (def. 30) | Inference steps |
| Guidance Scale | 1–20 (def. 7.5) | Prompt/image adherence |
| Seed | int (def. 1234) | Reproducibility |
| Octree Resolution | 64–512 (def. 256) | Higher = more detail / slower |
| Num Chunks | int (def. 200000) | Memory vs. speed |
| File Type | obj / fbx / glb / ply / stl | Output format |
| Device | cuda / cpu | |
| MC Algorithm | mc / dmc | Only used when **FlashVDM** is enabled |
| Enable Text‑to‑3D | toggle | Uses the prompt; ignores image input (downloads HunyuanDiT ~8 GB on first use) |
| Disable Texture | toggle | Skip the texture pass (faster, white mesh) |
| Enable FlashVDM | toggle | Acceleration |
| Low VRAM Mode | toggle | For GPUs with limited memory |
| Remove Background | toggle (def. on) | |
| Compile Model | toggle | Hidden on Windows (torch.compile needs Triton) |

---

## Output structure

```
Assets/Generated3DModels/
└── <name>_<uuid8>/
    ├── input.png / rembg.png         # source + background-removed image
    ├── textured_mesh.obj / .fbx / .glb
    ├── textured_mesh.mtl             # material referencing the maps (OBJ)
    ├── textured_mesh.jpg             # base color (albedo)
    ├── textured_mesh_metallic.jpg
    ├── textured_mesh_roughness.jpg
    └── stats.json
```

---

## Project layout

- `UnityPlugin/Editor/` — C# editor windows (`Hunyuan3DGenerator`, `Hunyuan3DDependencyManager`,
  `Hunyuan3DSystemProbe`, `Hunyuan3DConfig`, `Hunyuan3DWelcome`), assembly `Hunyuan3D.Editor` (Editor‑only).
- `UnityPlugin/Scripts/` — the Python pipeline (`batch_hunyuan3d.py`, `remove_background.py`,
  `verify_hunyuan3d.py`, `test_encoding.py`, `install_hunyuan3d_windows.ps1`).

---

## Third‑party licenses

This plugin orchestrates several open‑source components — respect each project's license:

- **Hunyuan3D‑2.1** — Tencent Hunyuan Community License — https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1
- **PyTorch** — BSD‑3‑Clause · **diffusers / transformers** — Apache‑2.0 · **rembg** — MIT
- **trimesh** — MIT · **PyMeshLab** — GPL‑3.0 · **Real‑ESRGAN** — BSD‑3‑Clause · **Blender (bpy)** — GPL

See [`UnityPlugin/README.md`](UnityPlugin/README.md) for the detailed licensing notes.
