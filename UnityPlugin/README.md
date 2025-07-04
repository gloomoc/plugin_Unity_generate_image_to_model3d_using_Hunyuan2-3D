# Hunyuan3D Unity Plugin

Aquest plugin permet generar models 3D a partir d'imatges utilitzant el model d'IA Hunyuan3D-2 directament des de l'editor de Unity.

## Funcionalitats

- 🎨 **Conversió d'imatge a model 3D**: Converteix imatges (JPG, PNG, etc.) en models 3D (.obj, .fbx, .glb, etc.)
- 🗂️ **Mode batch**: Processa carpetes senceres d'imatges automàticament
- 🎛️ **Paràmetres configurables**: Control total sobre els paràmetres de generació del model
- 🔄 **Eliminació automàtica de fons**: Opció per eliminar el fons de les imatges abans de processar
- 📦 **Importació automàtica**: Els models generats s'importen automàticament com assets de Unity
- 💾 **Configuració persistent**: La configuració es guarda automàticament
- 📊 **Seguiment en temps real**: Barra de progrés i logs detallats del procés
- 🔧 **Gestor de dependències integrat**: Instal·lació automàtica de totes les dependències Python
- ⚡ **Detecció automàtica GPU/CPU**: Configura automàticament el millor mode segons el hardware
- 🚀 **Instal·lació automàtica CUDA**: Detecta i instal·la CUDA Toolkit automàticament si és necessari

## Requisits

1. **Unity 2020.3 o superior**
2. **Python 3.8 o superior** amb les següents dependències:
   - Hunyuan3D-2 (repositori oficial)
   - torch
   - PIL (Pillow)
   - rembg (per eliminació de fons)
   - trimesh
   - tqdm

3. **Scripts de Python**:
   - `batch_hunyuan3d.py` - Script principal per generar models 3D
   - `remove_background.py` - Script per eliminar fons d'imatges

## Instal·lació

1. **Copia el plugin a Unity**:
   ```
   ProjecteUnity/
   ├── Assets/
   │   └── Plugins/
   │       └── Hunyuan3D/
   │           └── Editor/
   │               ├── Hunyuan3DGenerator.cs
   │               ├── Hunyuan3DConfig.cs
   │               ├── Hunyuan3DDependencyManager.cs
   │               └── Hunyuan3D.Editor.asmdef
   ```

2. **Col·loca els scripts de Python** en el directori arrel del projecte o en una subcarpeta accessible:
   ```
   ProjecteUnity/
   ├── batch_hunyuan3d.py
   ├── remove_background.py
   └── Assets/
   ```

3. **Instal·la les dependències Python**:
   - Obre Unity i navega a `Tools > Hunyuan3D > Dependency Manager`
   - Configura el path de Python
   - Fes clic a "Instal·lar Tot" per una instal·lació automàtica
   - O segueix la configuració manual més endavant

4. **Obre el generador principal**: `Tools > Hunyuan3D > 3D Model Generator`

## Configuració Inicial

### Opció A: Configuració Automàtica (Recomanada)

1. **Obre el Dependency Manager**: `Tools > Hunyuan3D > Dependency Manager`
2. **Detecta Python**: Fes clic a "Detectar" per trobar automàticament Python
3. **Selecciona Mode d'Instal·lació**:
   - **Auto**: Detecta automàticament el millor mode
   - **CUDA 12**: Per targetes NVIDIA modernes
   - **CUDA 11**: Per targetes NVIDIA més antigues  
   - **CPU**: Mode universal però més lent
4. **Instal·la Tot**: Fes clic per instal·lar automàticament totes les dependències
5. **Instal·lació Automàtica de CUDA** (si és necessari):
   - El sistema detecta si tens drivers NVIDIA però no CUDA Toolkit
   - Ofereix descarregar i instal·lar automàticament CUDA 11.8, 12.1 o 12.4
   - Després reinstal·la PyTorch amb suport CUDA
   - Funciona a Windows amb permisos d'administrador
6. **Verifica**: Usa "Comprovar Dependències" per confirmar la instal·lació

### Opció B: Configuració Manual

Si prefereixes la configuració manual:

### Opció B: Configuració Manual

Si prefereixes la configuració manual:

#### 1. Instal·lació Base de Python
```bash
# Instal·lar PyTorch (escull una opció segons el teu sistema)
# Per CPU:
pip install torch torchvision --index-url https://download.pytorch.org/whl/cpu

# Per CUDA 11.8:
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118

# Per CUDA 12.1:
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
```

#### 2. Dependències Core
```bash
pip install diffusers>=0.21.0 transformers>=4.25.0 numpy tqdm omegaconf einops
```

#### 3. Processament d'Imatges
```bash
pip install opencv-python rembg onnxruntime
```

#### 4. Processament de Malles
```bash
pip install trimesh pymeshlab pygltflib xatlas
```

#### 5. Hunyuan3D Package
```bash
# Clonar i instal·lar el repositori oficial
git clone https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git
cd Hunyuan3D-2
pip install -r requirements.txt
pip install -e .
```

### Configuració del Plugin Unity

### 1. Configuració de Paths
- **Python Executable**: Path a l'executable de Python (per exemple: `C:\\Python39\\python.exe`)
- **Script Base Path**: Directori que conté els scripts de Python

### 2. Paràmetres del Model
- **Model Path**: `tencent/Hunyuan3D-2mini` (per defecte)
- **Subfolder**: `hunyuan3d-dit-v2-mini-turbo`
- **Texture Model Path**: `tencent/Hunyuan3D-2`
- **Device**: `cuda` (per GPU) o `cpu`

### 3. Paràmetres de Generació
- **Steps**: Número de passos d'inferència (1-100, recomanat: 30)
- **Guidance Scale**: Escala de guidance (1-20, recomanat: 7.5)
- **Seed**: Llavor per reproducibilitat
- **Octree Resolution**: Resolució d'octree (64-512, recomanat: 256)
- **File Type**: Format de sortida (obj, fbx, glb, ply, stl)

### 4. Opcions Avançades
- **Enable Text-to-3D**: Activa capacitats text-to-3D
- **Disable Texture**: Desactiva generació de textures
- **Enable FlashVDM**: Accelera el procés (requereix suport)
- **Compile Model**: Compila el model per millor rendiment
- **Low VRAM Mode**: Mode per GPUs amb poca memòria
- **Remove Background**: Elimina automàticament el fons de les imatges

## Ús

### Mode Imatge Individual

1. **Selecciona una imatge**: Fes clic a "..." al costat de "Imatge" i selecciona una imatge
2. **Configura la sortida**: Especifica la carpeta de sortida dins de Assets
3. **Ajusta els paràmetres** segons les teves necessitats
4. **Fes clic a "Generar Model 3D"**

### Mode Batch (Carpeta)

1. **Activa "Mode Batch (carpeta)"**
2. **Selecciona una carpeta** que contingui imatges
3. **Configura la sortida**: Especifica la carpeta de sortida
4. **Fes clic a "Processar Carpeta"**

## Exemple d'Ús Pràctic

```csharp
// Exemple de configuració per models d'alta qualitat
Steps: 50
Guidance Scale: 10.0
Octree Resolution: 384
File Type: fbx
Enable FlashVDM: true
```

```csharp
// Exemple de configuració per processat ràpid
Steps: 15
Guidance Scale: 5.0
Octree Resolution: 128
File Type: obj
Low VRAM Mode: true
```

## Estructura de Sortida

Els models generats es guarden amb l'estructura següent:
```
Assets/Generated3DModels/
├── imatge1_abc123/
│   ├── imatge1.obj (o .fbx, .glb, etc.)
│   ├── imatge1.png (textura)
│   └── altres_fitxers...
└── imatge2_def456/
    ├── imatge2.obj
    └── ...
```

## Monitorització del Procés

- **Barra de progrés**: Mostra el progrés general del procés
- **Missatges d'estat**: Informa sobre l'etapa actual
- **Logs detallats**: Visualitza la sortida completa dels scripts de Python
- **Verificació de scripts**: Comprova automàticament si els scripts existeixen

## Resolució de Problemes

### Scripts de Diagnòstic
Per facilitar la resolució de problemes, el plugin inclou scripts de diagnòstic:

```bash
# Executar diagnòstic complet (Windows)
run_diagnostic.bat

# O directament amb Python
python diagnostic.py
```

Aquests scripts comproven:
- Versió de Python i rutes
- Drivers NVIDIA i disponibilitat de CUDA
- Instal·lació de PyTorch i compatibilitat CUDA
- Totes les dependències core i opcionals
- Test funcional del stack complet

### Problemes de Dependències
- **Obre el Dependency Manager**: `Tools > Hunyuan3D > Dependency Manager`
- **Comprova l'estat**: Usa "Comprovar Dependències" per veure què falta
- **Instal·lació automàtica**: Prova "Instal·lar Tot" per resoldre problemes automàticament
- **Mode Conda**: Si tens problemes amb pip, activa "Usar Conda Environment"

### Script no trobat
- Verifica que els scripts `batch_hunyuan3d.py` i `remove_background.py` existeixin al path especificat
- Comprova que el path dels scripts sigui correcte al generador principal

### Error de Python
- Usa el Dependency Manager per verificar que Python ≥ 3.8 estigui instal·lat
- Comprova que totes les dependències estiguin correctament instal·lades
- Revisa els logs del Dependency Manager per errors específics

### Error de CUDA
- **Instal·lació automàtica**: El gestor pot instal·lar CUDA 11.8, 12.1 o 12.4 automàticament (Windows)
- **Detecció intel·ligent**: Detecta drivers NVIDIA i recomana la millor versió CUDA
- **Configuració automàtica**: Després d'instal·lar CUDA, reinstal·la PyTorch amb suport GPU
- **Verificació completa**: Usa "Verificar Instal·lació" per comprovar tot el stack
- **Reparació PATH**: Botó "Reparar PATH CUDA" per problemes de configuració
- Si tots els passos automàtics fallen, canvia el mode d'instal·lació a "CPU"
- Verifica que els drivers NVIDIA estiguin actualitzats (mínim versió 470+)

## Gestió Automàtica de CUDA

El plugin inclou un sistema avançat de gestió de CUDA que:

### Detecció Intel·ligent
- **Detecta drivers NVIDIA**: Comprova si tens una targeta gràfica compatible
- **Identifica CUDA Toolkit**: Busca instal·lacions existents en rutes estàndard
- **Recomana la millor versió**: Segons els teus drivers i hardware

### Instal·lació Automàtica (Windows)
- **CUDA 11.8**: Compatible amb drivers més antics (RTX 20xx, GTX 16xx)
- **CUDA 12.1**: Recomanat per la majoria de sistemes moderns (RTX 30xx+)
- **CUDA 12.4**: Última versió per hardware més recent (RTX 40xx)
- **Descàrrega automàtica**: Baixa l'instal·lador oficial de NVIDIA (~3GB)
- **Instal·lació silenciosa**: Executa amb permisos d'administrador
- **Configuració POST**: Reinstal·la PyTorch amb suport CUDA automàticament

### Funcionalitats Addicionals
- **Verificació completa**: Test funcional de tot el stack (Python + PyTorch + CUDA)
- **Reparació PATH**: Ajuda a configurar variables d'entorn si és necessari
- **Compatibilitat universal**: Fallback a CPU si CUDA no està disponible
- **Múltiples versions**: Detecta i treballa amb diferents versions de CUDA instal·lades

### Models no s'importen
- Verifica que la carpeta de sortida estigui dins de `Assets/`
- Comprova que Unity tingui permisos d'escriptura

## Optimització de Rendiment

### Per GPU potents:
- Device: `cuda`
- Enable FlashVDM: `true`
- Compile Model: `true`
- Steps: 30-50

### Per sistemes limitats:
- Device: `cpu` o `cuda` amb Low VRAM Mode
- Steps: 15-25
- Octree Resolution: 128-256
- Disable Texture: `true` (si no necessites textures)

## Formats de Sortida Recomanats

- **OBJ**: Universalment compatible, bon per Unity
- **FBX**: Millor per animacions i materials complexos
- **GLB**: Compact, bon per web i AR/VR
- **PLY**: Bon per meshes amb colors per vèrtex

## Suport

Aquest plugin és compatible amb:
- Windows, macOS, Linux
- Unity 2020.3 LTS i versions superiors
- Python 3.8+
- CUDA 11.0+ (opcional per acceleració GPU)

Per problemes o millores, consulta la documentació del repositori Hunyuan3D-2 oficial.
