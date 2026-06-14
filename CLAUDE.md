# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A **Unity Editor plugin** that generates textured 3D meshes from images using Tencent's
**Hunyuan3D-2.1** model. The plugin itself contains *no* ML code: the C# Editor layer builds a
command line and spawns the real work as a **Python subprocess**. This repo is the plugin *source* —
to use it you copy `UnityPlugin/` into a Unity project's `Assets/` folder (there are no `.meta`
files here because it is not currently inside a Unity project).

The whole system is two cooperating processes that talk over argv + streamed stdout/stderr:

```
Unity Editor (C#, UnityPlugin/Editor/)          Python (UnityPlugin/Scripts/ + cloned repo)
  Hunyuan3DGenerator  ──spawns──►  python batch_hunyuan3d.py <image> --flags...
       ▲  parses [OUT]/[ERR] lines for progress         │ imports hy3dshape/hy3dpaint (or legacy hy3dgen)
       └─────────── stdout/stderr ◄──────────────────────┘ writes meshes + previews + stats.json
```

## Python scripts location

The canonical, current, Hunyuan3D-2.1-aware Python scripts live in **`UnityPlugin/Scripts/`** — this is
what the plugin ships and runs. `batch_hunyuan3d.py` there has the `_find_hunyuan_root()` / `hy3dshape` /
dual `API_VERSION` logic. (The old stale root-level duplicate scripts have been removed.)

## Architecture

### C# Editor layer (`UnityPlugin/Editor/`, assembly `Hunyuan3D.Editor`, Editor-only)

- **`Hunyuan3DGenerator.cs`** — main window (`Tools/Hunyuan3D/3D Model Generator`). Builds the
  `batch_hunyuan3d.py` argument list from `Hunyuan3DConfig`, runs it via `ExecutePythonScript`, and
  streams output back to the UI. Owns the runtime concerns: venv detection, `PYTHONPATH`
  construction (repo root + `hy3dshape` + `hy3dpaint`), UTF-8 env vars, CUDA env vars, 20-minute
  process timeout, and importing the generated assets back into Unity (`AssetDatabase.Refresh` +
  `Selection`).
- **`Hunyuan3DDependencyManager.cs`** — by far the largest file (~3700 lines); window
  `Tools/Hunyuan3D/Dependency Manager`. Detects/installs Python, UV, PyTorch, and the Hunyuan3D repo.
  Also defines **`MainThreadExecutor`** (top of the file) — the static queue used everywhere to
  marshal background-thread callbacks onto Unity's main thread via `EditorApplication.update`.
- **`Hunyuan3DSystemProbe.cs`** — pure (non-window) detection. `Probe()` returns a
  `Hunyuan3DEnvironmentSnapshot` (System / Python / Cuda / Git sub-objects). Enforces the supported
  Python range **3.8–3.12**. Use the snapshot rather than re-detecting ad hoc.
- **`Hunyuan3DConfig.cs`** — serializable config + `Hunyuan3DUtils` helpers. **Persisted as JSON to
  `%APPDATA%/Unity/Hunyuan3D/config.json`** (a machine-global file, NOT EditorPrefs and NOT inside
  the Unity project). `IsValid()` gates generation.
- **`Hunyuan3DWelcome.cs`** — auto-opens on editor load via `[InitializeOnLoadMethod]` (gated by the
  `Hunyuan3D_ShowWelcomeOnStartup` EditorPref).

### Python pipeline (`UnityPlugin/Scripts/batch_hunyuan3d.py`)

- **`setup_imports()`** tries the **2.1 API** (`hy3dshape`/`hy3dpaint`, sets `API_VERSION='2.1'`)
  first, then falls back to the **legacy 2.0 API** (`hy3dgen`, `API_VERSION='2.0'`). The returned
  modules dict carries `API_VERSION`, and code branches on it throughout — preserve both paths when
  editing.
- **`_find_hunyuan_root()`** locates the cloned official repo via `$HUNYUAN3D_ROOT`, script-relative
  paths, and the known temp install dirs, then injects it onto `sys.path`.
- **`HunyuanBatchProcessor`** mirrors the official `gradio_app.py`. Per image:
  load → resize to 512×512 → background removal (`BackgroundRemover`, or a `rembg` fallback) →
  shape generation → export white mesh → floater/degenerate-face removal + face reduction →
  optional texture pass → preview PNGs → `stats.json`. Output per image goes to
  `<output>/<imageName>_<uuid8>/`.
- **FBX is not native**: meshes export to an intermediate OBJ, then convert via whichever of
  **bpy / pymeshlab / open3d** is installed; if none, it silently falls back to OBJ.

### Installation flow (the genuinely complex part)

Windows install ("Install Hunyuan3D Package") uses **UV** (astral) + a git clone, NOT plain pip:

- Creates a UV project at `<UnityProjectRoot>/Hunyuan3D_UV/` containing `.venv/` and a clone of
  `Hunyuan3D-2.1/`. The cloned repo is registered into the venv via a `hunyuan3d_repo.pth` file
  written into `site-packages`.
- `install_hunyuan3d_windows.ps1` is the standalone/manual alternative; it installs into
  `%LOCALAPPDATA%/Temp/Hunyuan3D-2.1-for-windows` (pinned `torch==2.5.1+cu124` or `+cu118`).
- venv auto-detection (`DetectVirtualEnvironment`, in both the generator and the manager) probes many
  locations: the UV project, the temp install roots, `scriptBasePath/.venv`, project-root `.venv`,
  etc. A venv only "counts" if it also contains PyTorch.

### UTF-8 on Windows is a recurring hazard

Console code-page mismatches corrupt the streamed logs, so UTF-8 is forced in several layers:
`PYTHONIOENCODING` / `PYTHONUTF8` / `PYTHONLEGACYWINDOWSSTDIO` env vars (C# and Python), `chcp 65001`,
and `sys.stdout.reconfigure`. There is a "Test UTF-8 Encoding" button + `test_encoding.py`. Keep this
in mind before changing any process-spawning or logging code.

## Commands

There is **no CLI build or lint** — C# is compiled by the Unity Editor when `UnityPlugin/` sits inside
a Unity project, and there is no automated test suite. Verification and the pipeline are run manually
through Python. Substitute the real venv python (see the manual) for `python` below.

```powershell
# Verify the install can import Hunyuan3D (mirrors the manager's "Verify Installation" button)
python UnityPlugin/Scripts/verify_hunyuan3d.py

# Run the full pipeline standalone on one image (fast, light settings)
python UnityPlugin/Scripts/batch_hunyuan3d.py <image.png> `
  --output <outdir> --model_path tencent/Hunyuan3D-2.1 --subfolder hunyuan3d-dit-v2-1 `
  --texgen_model_path tencent/Hunyuan3D-2.1 --device cuda --steps 20 `
  --file_type obj --remove_background

# Useful flags while iterating: --disable_tex (skip texture), --low_vram_mode, --skip_background_removal
```

`MANUAL_INSTALACIO_V003.md` is the authoritative end-to-end install/usage guide (in Catalan) and
includes the full PowerShell installer invocation and the complete `batch_hunyuan3d.py` argument set.

## Conventions

- **Mixed natural languages**: code comments, `#region` headers, and some log strings are a mix of
  English, Catalan, and Spanish (e.g. region `Processament Principal`, logs like
  `El procés ha trigat massa temps`). Match the surrounding language when editing a given block.
- **All C# is Editor-only** (`includePlatforms: ["Editor"]`); never assume runtime/player availability.
  Any work triggered from a background thread must return to the UI through `MainThreadExecutor`.
- When adding a generation parameter, thread it through **all four** places or it silently does
  nothing: a field in `Hunyuan3DConfig`, a control in `Hunyuan3DGenerator` (`DrawModelParameters` /
  `DrawGenerationParameters` / `DrawOptions`), the argv builder in `ExecuteHunyuan3DScript`, and a
  matching `argparse` entry in `batch_hunyuan3d.py`.
