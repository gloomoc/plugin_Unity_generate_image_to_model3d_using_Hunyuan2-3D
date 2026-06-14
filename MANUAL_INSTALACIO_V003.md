# Manual d'instalacio de `v003` per Unity + Hunyuan3D 2.1

Aquest manual esta pensat per a la versio `v003` del projecte:

`D:\Biblioteca Programacio\Proyectos IA\Game_Development\Pluggin Generar Imagen a Modelo 3D usando Hunyuan2-3D en Unity\_versiones\v003`

La integracio actual de `v003` esta preparada per treballar amb:

- `tencent/Hunyuan3D-2.1`
- `hunyuan3d-dit-v2-1`
- `tencent/Hunyuan3D-2.1` per a textura

Repositori oficial de referencia:

- https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1

## 1. Requisits previs

Abans de comencar, comprova aixo:

- Windows 10 o Windows 11
- Unity 2020.3 o superior
- Git instal.lat i disponible a terminal
- Python 3.10
- PowerShell
- GPU NVIDIA recomanada

Notes importants:

- Hunyuan3D 2.1 funciona molt millor amb GPU CUDA.
- Si no tens prou VRAM, pots provar `Low VRAM Mode` i `Disable Texture`.
- Per exportar `fbx`, es recomana tenir `PyMeshLab`, `Open3D` o `bpy`.

## 2. Rutes que faras servir

El flux recomanat es:

1. fer servir aquesta `v003` com a carpeta origen
2. copiar `UnityPlugin` dins de `Assets` del projecte Unity on ho vols fer anar
3. instal.lar les dependencies des d'aquell projecte Unity

Per no haver d'escriure la ruta llarga cada vegada, pots definir aquestes variables a PowerShell:

```powershell
$SourceRoot = "D:\Biblioteca Programacio\Proyectos IA\Game_Development\Pluggin Generar Imagen a Modelo 3D usando Hunyuan2-3D en Unity\_versiones\v003"
$SourceUnityPlugin = "$SourceRoot\UnityPlugin"
$TargetProject = "D:\RUTA\AL_TEU_PROJECTE_UNITY"
$TargetPlugin = "$TargetProject\Assets\UnityPlugin"
$TargetPluginScripts = "$TargetPlugin\Scripts"
$InstallRoot = "$env:LOCALAPPDATA\Temp\Hunyuan3D-2.1-for-windows"
```

## 3. Copiar `UnityPlugin` al projecte Unity on el vols fer servir

### Pas 1. Copia la carpeta del plugin

Has de copiar aquesta carpeta:

```text
<v003>\UnityPlugin
```

dins d'aquesta ruta del projecte Unity desti:

```text
<el_teu_projecte_unity>\Assets\UnityPlugin
```

Comanda PowerShell d'exemple:

```powershell
$SourceRoot = "D:\Biblioteca Programacio\Proyectos IA\Game_Development\Pluggin Generar Imagen a Modelo 3D usando Hunyuan2-3D en Unity\_versiones\v003"
$SourceUnityPlugin = "$SourceRoot\UnityPlugin"
$TargetProject = "D:\RUTA\AL_TEU_PROJECTE_UNITY"
$TargetAssets = "$TargetProject\Assets"

Copy-Item -Recurse -Force "$SourceUnityPlugin" "$TargetAssets"
```

Si al projecte desti ja existeix `Assets\UnityPlugin`, substitueix-la per la de `v003`.

### Pas 2. Obre el projecte Unity desti

Ara ja no treballes des de `v003` com a projecte Unity principal.
Ara obres el projecte Unity on acabes de copiar `Assets\UnityPlugin`.

## 4. Opcio recomanada: instalacio des de Unity

### Pas 1. Obre el projecte

Obre Unity i carrega el projecte desti on has copiat `Assets\UnityPlugin`.

### Pas 2. Obre el gestor de dependencies

Al menu superior:

`Tools > Hunyuan3D > Dependency Manager`

### Pas 3. Fes servir nomes els botons essencials

Per instal.lar dependencies i Hunyuan3D 2.1, centra't nomes en aquests botons:

- `Detect`
- `Check Dependencies`
- `Install All`
- `Install Hunyuan3D Package`
- `Verify Installation`

`Install All` instal.la la base de dependencies.
`Install Hunyuan3D Package` fa la instal.lacio del paquet oficial Hunyuan3D 2.1.
`Install All` no instal.la els moduls opcionals que no fan falta per al flux normal d'Unity.

A Windows, el boto `Install Hunyuan3D Package` ja fa servir directament el flux recomanat amb `uv`.

### Pas 4. Botons que no cal tocar en una instal.lacio normal

En una instal.lacio normal no cal fer servir aquests botons:

- `Install CUDA 11.8`
- `Install PyTorch CUDA 11.8`
- `Repair CUDA PATH`
- `Windows Quick Install`
- `Guide`
- `Detect CUDA`
- `Create Conda Environment`

### Pas 5. Que fa la instal.lacio essencial

Amb el flux anterior, el plugin et deixa preparat:

- `uv`
- `.venv`
- `torch`
- dependencies Python
- repositori oficial `Hunyuan3D-2.1`

### Pas 6. Configura el generador

Obre:

`Tools > Hunyuan3D > 3D Model Generator`

Comprova aquests camps:

- `Python Executable`
- `Script Base Path`

Valors recomanats:

```text
Python Executable:
C:\Users\<EL_TEU_USUARI>\AppData\Local\Temp\Hunyuan3D-2.1-for-windows\.venv\Scripts\python.exe

Script Base Path:
<ruta del projecte Unity desti>\Assets\UnityPlugin\Scripts
```

Si el camp de Python esta buit o mal detectat:

- prem `Detect PowerShell Installation`

Despres revisa els valors del model:

- `Model Path`: `tencent/Hunyuan3D-2.1`
- `Subfolder`: `hunyuan3d-dit-v2-1`
- `Texture Model Path`: `tencent/Hunyuan3D-2.1`
- `Device`: `cuda` si tens GPU, `cpu` si no

### Pas 7. Prova rapida

Per a la primera prova:

- `File Type`: `obj`
- `Steps`: `20` o `30`
- `Remove Background`: activat
- `Disable Texture`: desactivat si tens bona GPU
- `Low VRAM Mode`: activat si tens una GPU justa

## 5. Instalacio per terminal amb l'script del projecte

Si prefereixes fer-ho manualment, obre PowerShell i executa:

```powershell
$SourceRoot = "D:\Biblioteca Programacio\Proyectos IA\Game_Development\Pluggin Generar Imagen a Modelo 3D usando Hunyuan2-3D en Unity\_versiones\v003"
$TargetProject = "D:\RUTA\AL_TEU_PROJECTE_UNITY"
$TargetPluginScripts = "$TargetProject\Assets\UnityPlugin\Scripts"
$InstallRoot = "$env:LOCALAPPDATA\Temp\Hunyuan3D-2.1-for-windows"

Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
cd "$TargetPluginScripts"

powershell -ExecutionPolicy Bypass -File ".\install_hunyuan3d_windows.ps1" `
  -InstallPath "$InstallRoot" `
  -PythonVersion "3.10" `
  -UseCUDA12
```

Si vols provar amb CUDA 11.8, canvia l'ultima linia per:

```powershell
powershell -ExecutionPolicy Bypass -File ".\install_hunyuan3d_windows.ps1" `
  -InstallPath "$InstallRoot" `
  -PythonVersion "3.10"
```

Quan acabi, la instalacio hauria de quedar aproximadament aqui:

```text
C:\Users\<EL_TEU_USUARI>\AppData\Local\Temp\Hunyuan3D-2.1-for-windows
```

## 6. Instalacio manual avancada amb `uv`

Fes servir aquesta opcio nomes si l'script automatic falla.

### Pas 1. Instal.la `uv`

```powershell
Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression
```

Tanca i torna a obrir PowerShell si la comanda `uv --version` no respon.

### Pas 2. Clona el repo oficial

```powershell
$TargetProject = "D:\RUTA\AL_TEU_PROJECTE_UNITY"
$TargetPluginScripts = "$TargetProject\Assets\UnityPlugin\Scripts"
$InstallRoot = "$env:LOCALAPPDATA\Temp\Hunyuan3D-2.1-for-windows"
New-Item -ItemType Directory -Force -Path "$InstallRoot" | Out-Null
cd "$InstallRoot"

git clone --depth 1 https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1.git
```

### Pas 3. Crea i activa la `.venv`

```powershell
cd "$InstallRoot"
uv venv -p 3.10
.\.venv\Scripts\Activate.ps1
```

### Pas 4. Instal.la PyTorch

Per CUDA 12.4:

```powershell
uv pip install torch==2.5.1+cu124 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
```

Per CUDA 11.8:

```powershell
uv pip install torch==2.5.1+cu118 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
```

### Pas 5. Instal.la dependencies del plugin

```powershell
cd "$TargetPluginScripts"
uv pip sync .\requirements-uv.txt --index-strategy unsafe-best-match
```

### Pas 6. Instal.la el repo de Hunyuan3D 2.1

```powershell
cd "$InstallRoot\Hunyuan3D-2.1"
uv pip install -e .
```

### Pas 7. Moduls opcionals

```powershell
cd "$InstallRoot\Hunyuan3D-2.1\hy3dpaint\custom_rasterizer"
uv pip install . --force-reinstall
```

Si existeix el directori del renderer:

```powershell
cd "$InstallRoot\Hunyuan3D-2.1\hy3dpaint\DifferentiableRenderer"
uv pip install . --force-reinstall
```

## 7. Verificacio per terminal

Quan la instalacio estigui feta, comprova-la amb:

```powershell
$TargetProject = "D:\RUTA\AL_TEU_PROJECTE_UNITY"
$TargetPluginScripts = "$TargetProject\Assets\UnityPlugin\Scripts"
$InstallRoot = "$env:LOCALAPPDATA\Temp\Hunyuan3D-2.1-for-windows"

cd "$TargetPluginScripts"
& "$InstallRoot\.venv\Scripts\python.exe" ".\verify_hunyuan3d.py"
```

Si tot va be, hauries de veure un missatge de tipus:

```text
[OK] Hunyuan3D 2.1 found and accessible
```

## 8. Prova directa del generador per terminal

Pots provar el pipeline fora de Unity amb una imatge:

```powershell
$TargetProject = "D:\RUTA\AL_TEU_PROJECTE_UNITY"
$TargetPluginScripts = "$TargetProject\Assets\UnityPlugin\Scripts"
$InstallRoot = "$env:LOCALAPPDATA\Temp\Hunyuan3D-2.1-for-windows"
$InputImage = "C:\RUTA\A\la_teva_imatge.png"
$OutputDir = "C:\RUTA\sortida_hunyuan"

New-Item -ItemType Directory -Force -Path "$OutputDir" | Out-Null
cd "$TargetPluginScripts"

& "$InstallRoot\.venv\Scripts\python.exe" ".\batch_hunyuan3d.py" `
  "$InputImage" `
  --output "$OutputDir" `
  --model_path "tencent/Hunyuan3D-2.1" `
  --subfolder "hunyuan3d-dit-v2-1" `
  --texgen_model_path "tencent/Hunyuan3D-2.1" `
  --device cuda `
  --mc_algo mc `
  --steps 30 `
  --guidance_scale 7.5 `
  --seed 1234 `
  --octree_resolution 256 `
  --num_chunks 200000 `
  --file_type obj `
  --remove_background
```

Per a una prova lleugera:

- usa `--file_type obj`
- activa `--remove_background`
- si vas curt de memoria, desactiva textura des de Unity o afegeix `--disable_tex`

## 9. Configuracio recomanada dins de Unity

Quan ja estigui instal.lat, deixa aquests valors:

```text
Python Executable:
C:\Users\<EL_TEU_USUARI>\AppData\Local\Temp\Hunyuan3D-2.1-for-windows\.venv\Scripts\python.exe

Script Base Path:
<ruta del projecte Unity desti>\Assets\UnityPlugin\Scripts

Model Path:
tencent/Hunyuan3D-2.1

Subfolder:
hunyuan3d-dit-v2-1

Texture Model Path:
tencent/Hunyuan3D-2.1
```

Si tens poca VRAM:

- `Device`: `cuda`
- `Low VRAM Mode`: activat
- `Disable Texture`: activat per a la primera prova
- `File Type`: `obj`

## 10. Errors habituals i solucions

### `uv` no es troba

Executa:

```powershell
Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression
```

Despres reinicia PowerShell i Unity.

### `Git` no es troba

Instal.la Git:

- https://git-scm.com/download/win

Despres comprova:

```powershell
git --version
```

### Unity no detecta el Python correcte

Posa manualment:

```text
C:\Users\<EL_TEU_USUARI>\AppData\Local\Temp\Hunyuan3D-2.1-for-windows\.venv\Scripts\python.exe
```

### `Script Base Path` no es correcte

Posa manualment la carpeta de scripts del plugin:

```text
<ruta del projecte Unity desti>\Assets\UnityPlugin\Scripts
```

### Error d'importacio de `hy3dshape` o `hy3dpaint`

Reexecuta:

```powershell
cd "$InstallRoot\Hunyuan3D-2.1"
uv pip install -e .
```

### Falta memoria GPU

Prova aquesta combinacio:

- `Low VRAM Mode`: activat
- `Disable Texture`: activat
- `File Type`: `obj`
- `Steps`: `20`

## 11. Flux curt recomanat

Si vols anar pel cami mes curt:

1. Copia `UnityPlugin` de `v003` a `Assets` del teu projecte Unity.
2. Obre el projecte Unity desti.
3. Ves a `Tools > Hunyuan3D > Dependency Manager`.
4. Prem `Detect`.
5. Prem `Check Dependencies`.
6. Prem `Install All`.
7. Prem `Install Hunyuan3D Package`.
8. Prem `Verify Installation`.
9. Ves a `Tools > Hunyuan3D > 3D Model Generator`.
10. Comprova `Python Executable` i `Script Base Path`.
11. Selecciona una imatge.
12. Genera primer en `obj`.

## 12. Fitxers clau de `v003`

Si algun dia has de revisar o tocar la instalacio, aquests son els fitxers principals:

- `UnityPlugin\Editor\Hunyuan3DGenerator.cs`
- `UnityPlugin\Editor\Hunyuan3DDependencyManager.cs`
- `UnityPlugin\Editor\Hunyuan3DConfig.cs`
- `UnityPlugin\Scripts\batch_hunyuan3d.py`
- `UnityPlugin\Scripts\verify_hunyuan3d.py`
- `UnityPlugin\Scripts\install_hunyuan3d_windows.ps1`
