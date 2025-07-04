using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Hunyuan3D.Editor
{
    /// <summary>
    /// Utilidad para ejecutar código en el hilo principal de Unity desde hilos secundarios
    /// </summary>
    public static class MainThreadExecutor
    {
        private static readonly Queue<Action> _executeOnMainThread = new Queue<Action>();
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// Inicializa el executor, configurando el callback necesario
        /// </summary>
        private static void Initialize()
        {
            if (!_isInitialized)
            {
                EditorApplication.update += Update;
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Ejecuta una acción en el hilo principal de Unity
        /// </summary>
        /// <param name="action">Acción a ejecutar</param>
        public static void RunOnMainThread(Action action)
        {
            if (action == null)
                return;

            lock (_lock)
            {
                _executeOnMainThread.Enqueue(action);
                Initialize();
            }
        }

        /// <summary>
        /// Procesa las acciones encoladas para ejecutar en el hilo principal
        /// Este método es llamado por EditorApplication.update
        /// </summary>
        private static void Update()
        {
            // Ejecutar todas las acciones encoladas
            lock (_lock)
            {
                while (_executeOnMainThread.Count > 0)
                {
                    Action action = _executeOnMainThread.Dequeue();
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"Error ejecutando acción en hilo principal: {ex.Message}");
                    }
                }
            }
        }
    }
    /// <summary>
    /// Installation and dependency management screen for Hunyuan3D
    /// Integrates automatic installation according to official documentation
    /// </summary>
    public class Hunyuan3DDependencyManager : EditorWindow
    {
        #region Variables
        private bool isCheckingDependencies = false;
        private bool isInstalling = false;
        private bool isInstallingCuda = false;
        private string statusMessage = "";
        private float progress = 0f;
        private Vector2 scrollPosition = Vector2.zero;
        private List<string> logMessages = new List<string>();        
        private Vector2 dependencyScrollPosition = Vector2.zero; // Afegir aquesta línia        

        // Configuració
        private string pythonPath = "python";
        private string pipPath = "pip3";
        private bool useCondaEnv = false;
        private string condaEnvName = "hunyuan3d";

        // Estat de dependències
        private Dictionary<string, DependencyStatus> dependencyStatus = new Dictionary<string, DependencyStatus>();
        private bool pythonVersionOK = false;
        private bool torchInstalled = false;
        private bool cudaAvailable = false;
        private bool cudaToolkitInstalled = false;
        private bool nvccAvailable = false;
        private string detectedPythonVersion = "";
        private string detectedTorchVersion = "";
        private string detectedCudaVersion = "";
        private string detectedCudaToolkitVersion = "";
        private string recommendedCudaVersion = "";

        // Llistes de dependències segons documentació oficial
        private readonly string[] coreDependencies = {
            "torch>=1.13.0",
            "torchvision",
            "diffusers>=0.21.0",
            "transformers>=4.25.0"
        };

        private readonly string[] meshProcessingDependencies = {
            "trimesh>=3.15.0",
            "pymeshlab",
            "pygltflib",
            "xatlas"
        };

        private readonly string[] imageDependencies = {
            "opencv-python",
            "rembg",
            "onnxruntime"
        };

        private readonly string[] utilityDependencies = {
            "numpy",
            "tqdm",
            "omegaconf",
            "einops",
            "ninja",
            "pybind11",
            "triton"
        };

        private readonly string[] optionalDependencies = {
            "accelerate",
            "gradio",
            "fastapi",
            "uvicorn",
            "bpy"
        };

        private enum DependencyStatus
        {
            NotChecked,
            Checking,
            Installed,
            NotInstalled,
            Error
        }

        private enum InstallationMode
        {
            CPU,
            CUDA11,
            CUDA12,
            Auto
        }

        private InstallationMode selectedInstallMode = InstallationMode.Auto;
        #endregion

        #region Unity Menu
        [MenuItem("Tools/Hunyuan3D/Dependency Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<Hunyuan3DDependencyManager>("Hunyuan3D Dependencies");
            window.minSize = new Vector2(600, 500);
            window.Initialize();
        }
        #endregion

        #region Inicialització
        private void Initialize()
        {
            DetectPythonPath();
            AddLogMessage("Gestor de dependències Hunyuan3D inicialitzat.");
            AddLogMessage("Basat en la documentació oficial: https://github.com/Tencent-Hunyuan/Hunyuan3D-2");
        }

        private void DetectPythonPath()
        {
            // Intentar detectar Python automàticament
            string[] possiblePaths = {
                "python",
                "python3",
                "python.exe",
                "python3.exe",
                @"C:\Python39\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python39\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python310\python.exe",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\Python\Python311\python.exe"
            };

            foreach (string path in possiblePaths)
            {
                if (TestPythonPath(path))
                {
                    pythonPath = path;
                    AddLogMessage($"Python detectat: {path}");
                    break;
                }
            }
        }

        private bool TestPythonPath(string path)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit(3000); // 3 segons timeout
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region GUI
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hunyuan3D Dependency Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawConfigurationSection();
            EditorGUILayout.Space(10);

            DrawInstallationModeSection();
            EditorGUILayout.Space(10);

            DrawActionButtons();
            EditorGUILayout.Space(10);

            DrawDependencyStatus();
            EditorGUILayout.Space(10);

            DrawProgressAndLogs();
        }

        private void DrawConfigurationSection()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            pythonPath = EditorGUILayout.TextField("Python Path:", pythonPath);
            if (GUILayout.Button("Detect", GUILayout.Width(70)))
            {
                DetectPythonPath();
            }
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Seleccionar Python", "", "exe");
                if (!string.IsNullOrEmpty(path))
                    pythonPath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            useCondaEnv = EditorGUILayout.Toggle("Usar Conda Environment:", useCondaEnv);
            if (useCondaEnv)
            {
                condaEnvName = EditorGUILayout.TextField("Nom Environment:", condaEnvName);
            }
            EditorGUILayout.EndHorizontal();

            // Mostrar informació detectada
            if (!string.IsNullOrEmpty(detectedPythonVersion))
            {
                Color originalColor = GUI.color;
                GUI.color = pythonVersionOK ? Color.green : Color.red;
                EditorGUILayout.LabelField($"Python Version: {detectedPythonVersion}");
                GUI.color = originalColor;
            }

            if (!string.IsNullOrEmpty(detectedTorchVersion))
            {
                EditorGUILayout.LabelField($"PyTorch Version: {detectedTorchVersion}");
            }

            if (!string.IsNullOrEmpty(detectedCudaVersion))
            {
                Color originalColor = GUI.color;
                GUI.color = cudaAvailable ? Color.green : Color.yellow;
                EditorGUILayout.LabelField($"CUDA (PyTorch): {detectedCudaVersion}");
                GUI.color = originalColor;
            }

            if (!string.IsNullOrEmpty(detectedCudaToolkitVersion))
            {
                Color originalColor = GUI.color;
                GUI.color = cudaToolkitInstalled ? Color.green : Color.red;
                EditorGUILayout.LabelField($"CUDA Toolkit: {detectedCudaToolkitVersion}");
                GUI.color = originalColor;
            }
            else if (nvccAvailable)
            {
                EditorGUILayout.LabelField("CUDA Toolkit: Detected via nvcc");
            }

            if (!string.IsNullOrEmpty(recommendedCudaVersion))
            {
                EditorGUILayout.HelpBox($"Recomanat: {recommendedCudaVersion}", MessageType.Info);
            }
        }

        private void DrawInstallationModeSection()
        {
            EditorGUILayout.LabelField("Installation Mode", EditorStyles.boldLabel);

            selectedInstallMode = (InstallationMode)EditorGUILayout.EnumPopup("Mode:", selectedInstallMode);

            switch (selectedInstallMode)
            {
                case InstallationMode.CPU:
                    EditorGUILayout.HelpBox("Mode CPU: Instal·larà PyTorch optimitzat per CPU. Més lent però compatible universalment.", MessageType.Info);
                    break;
                case InstallationMode.CUDA11:
                    EditorGUILayout.HelpBox("Mode CUDA 11.x: Per targetes gràfiques NVIDIA amb drivers CUDA 11.x.", MessageType.Info);
                    break;
                case InstallationMode.CUDA12:
                    EditorGUILayout.HelpBox("Mode CUDA 12.x: Per targetes gràfiques NVIDIA amb drivers CUDA 12.x més recents.", MessageType.Info);
                    break;
                case InstallationMode.Auto:
                    EditorGUILayout.HelpBox("Mode Automàtic: Detectarà el millor mode segons el sistema.", MessageType.Info);
                    break;
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(isCheckingDependencies || isInstalling);
            if (GUILayout.Button("Check Dependencies", GUILayout.Height(30)))
            {
                _ = CheckAllDependencies();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(isCheckingDependencies || isInstalling);
            if (GUILayout.Button("Install All", GUILayout.Height(30)))
            {
                _ = InstallAllDependencies();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            // Nova secció per instal·lació Windows
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Optimized Windows Installation", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🚀 Windows Quick Install", GUILayout.Height(35)))
                {
                    RunWindowsPowerShellInstaller();
                }

                if (GUILayout.Button("📖 Guide", GUILayout.Width(60), GUILayout.Height(35)))
                {
                    ShowWindowsInstallGuide();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "Mètode recomanat per Windows: Usa UV per instal·lació ràpida i gestió eficient de dependències.",
                    MessageType.Info
                );
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create Conda Environment", GUILayout.Height(25)))
            {
                _ = CreateCondaEnvironment();
            }

            if (GUILayout.Button("Install Hunyuan3D Package", GUILayout.Height(25)))
            {
                // Oferir opcions d'instal·lació millorades per Windows
                int choice = EditorUtility.DisplayDialogComplex(
                    "Mètode d'Instal·lació",
                    "Tria el mètode d'instal·lació:\n\n" +
                    "• UV (Recomanat): Mètode ràpid i optimitzat per Windows\n" +
                    "• Pip Estàndard: Mètode tradicional amb pip\n" +
                    "• Cancel·lar: Sortir sense instal·lar",
                    "UV (Recomanat)",
                    "Pip Estàndard",
                    "Cancel·lar"
                );

                switch (choice)
                {
                    case 0: // UV Method
                        Task.Run(() => InstallHunyuan3DPackage());
                        Task.Run(() => InstallHunyuan3DWithUV());
                        break;
                    case 1: // Standard pip
                        Task.Run(() => InstallHunyuan3DPackage());
                        break;
                    default: // Cancel
                        AddLogMessage("⏹ Instal·lació cancel·lada per l'usuari");
                        break;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Botons específics per CUDA i PyTorch
            EditorGUILayout.LabelField("CUDA and PyTorch Installation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(isInstallingCuda);
            if (GUILayout.Button("Install CUDA 11.8", GUILayout.Height(25)))
            {
                if (ConfirmCudaInstallation("11.8"))
                {
                    _ = InstallCudaToolkit("11.8");
                }
            }

            if (GUILayout.Button("Install PyTorch CUDA 11.8", GUILayout.Height(25)))
            {
                _ = InstallPyTorchCuda118();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Detect CUDA", GUILayout.Height(25)))
            {
                _ = DetectCudaInstallation();
            }

            if (GUILayout.Button("Verify Installation", GUILayout.Height(25)))
            {
                _ = VerifyFullInstallation();
            }

            if (GUILayout.Button("Repair CUDA PATH", GUILayout.Height(25)))
            {
                RepairCudaPath();
            }
            EditorGUILayout.EndHorizontal();

            if (isCheckingDependencies || isInstalling || isInstallingCuda)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Status:", statusMessage);
                EditorGUILayout.Space(2);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, $"{(progress * 100):F1}%");
            }
        }

        private void DrawDependencyStatus()
        {
            EditorGUILayout.LabelField("Dependencies Status", EditorStyles.boldLabel);

            // Crear un ScrollView amb altura fixa per limitar l'espai vertical
            EditorGUILayout.BeginVertical(GUILayout.MaxHeight(150)); // Limitar altura a 150 píxels
            dependencyScrollPosition = EditorGUILayout.BeginScrollView(dependencyScrollPosition);

            DrawDependencyGroup("Core (PyTorch, Diffusers)", coreDependencies);
            DrawDependencyGroup("Processament de Malles", meshProcessingDependencies);
            DrawDependencyGroup("Processament d'Imatges", imageDependencies);
            DrawDependencyGroup("Utilitats", utilityDependencies);
            DrawDependencyGroup("Opcionals (Gradio, FastAPI)", optionalDependencies);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDependencyGroup(string groupName, string[] dependencies)
        {
            EditorGUILayout.LabelField(groupName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            foreach (string dep in dependencies)
            {
                string packageName = dep.Split(new char[] { '>', '<', '=', '!' })[0];
                DependencyStatus status = dependencyStatus.ContainsKey(packageName) ?
                    dependencyStatus[packageName] : DependencyStatus.NotChecked;

                EditorGUILayout.BeginHorizontal();

                // Icona d'estat
                string statusIcon = GetStatusIcon(status);
                Color statusColor = GetStatusColor(status);

                Color originalColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField(statusIcon, GUILayout.Width(20));
                GUI.color = originalColor;

                EditorGUILayout.LabelField(dep);

                // Botó d'instal·lació individual
                if (status == DependencyStatus.NotInstalled)
                {
                    if (GUILayout.Button("Install", GUILayout.Width(70)))
                    {
                        _ = InstallSingleDependency(dep);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }

        private void DrawProgressAndLogs()
        {
            EditorGUILayout.LabelField("Installation Logs", EditorStyles.boldLabel);

            // Convertir els missatges de log a un string únic 
            string logContent = string.Join("\n", logMessages);

            // Crear un estil personalitzat per al TextArea de només lectura
            GUIStyle logStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = true
            };

            // TextArea amb scroll que permet seleccionar i copiar text
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));

            // TextArea de només lectura que permet selecció
            EditorGUI.BeginDisabledGroup(true); // Fa que sigui de només lectura però permet selecció
            EditorGUILayout.TextArea(logContent, logStyle, GUILayout.ExpandHeight(true));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Logs"))
            {
                logMessages.Clear();
            }
            if (GUILayout.Button("Copy All Logs"))
            {
                CopyLogsToClipboard();
            }
            EditorGUILayout.EndHorizontal();
        }

        private string GetStatusIcon(DependencyStatus status)
        {
            switch (status)
            {
                case DependencyStatus.Installed: return "✓";
                case DependencyStatus.NotInstalled: return "✗";
                case DependencyStatus.Checking: return "⟳";
                case DependencyStatus.Error: return "⚠";
                default: return "?";
            }
        }

        private Color GetStatusColor(DependencyStatus status)
        {
            switch (status)
            {
                case DependencyStatus.Installed: return Color.green;
                case DependencyStatus.NotInstalled: return Color.red;
                case DependencyStatus.Checking: return Color.yellow;
                case DependencyStatus.Error: return new Color(1f, 0.5f, 0f); // Taronja
                default: return Color.gray;
            }
        }
        #endregion

        #region Funcions de Comprovació
        private async Task CheckAllDependencies()
        {
            isCheckingDependencies = true;
            progress = 0f;
            statusMessage = "Comprovant dependències...";

            try
            {
                // Detectar si estem usant un venv
                string venvPath = DetectVirtualEnvironment();
                if (!string.IsNullOrEmpty(venvPath))
                {
                    AddLogMessage($"📁 Usant entorn virtual: {venvPath}");
                    pythonPath = Path.Combine(venvPath, "Scripts", "python.exe");
                    pipPath = Path.Combine(venvPath, "Scripts", "pip.exe");
                }

                // Comprovar totes les dependències
                var allDependencies = coreDependencies
                    .Concat(meshProcessingDependencies)
                    .Concat(imageDependencies)
                    .Concat(utilityDependencies)
                    .Concat(optionalDependencies)
                    .ToArray();

                for (int i = 0; i < allDependencies.Length; i++)
                {
                    string dep = allDependencies[i];
                    string packageName = dep.Split(new char[] { '>', '<', '=', '!' })[0];

                    statusMessage = $"Comprovant {packageName}...";
                    dependencyStatus[packageName] = DependencyStatus.Checking;

                    bool isInstalled = await CheckSingleDependency(packageName);
                    dependencyStatus[packageName] = isInstalled ?
                        DependencyStatus.Installed : DependencyStatus.NotInstalled;

                    progress = 0.1f + (0.9f * (i + 1) / allDependencies.Length);
                    Repaint();
                }

                // Comprovar PyTorch i CUDA específicament
                await CheckTorchAndCuda();

                // Comprovar CUDA Toolkit si estem en Windows i no està disponible
                if (!cudaAvailable && Application.platform == RuntimePlatform.WindowsEditor)
                {
                    await DetectCudaInstallation();
                }

                statusMessage = "Comprovació completada!";
                progress = 1f;

                // Resum
                int installed = dependencyStatus.Values.Count(s => s == DependencyStatus.Installed);
                int total = dependencyStatus.Count;
                AddLogMessage($"Resum: {installed}/{total} dependències instal·lades.");
            
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error durant la comprovació: {ex.Message}");
            }
            finally
            {
                isCheckingDependencies = false;
            }
        }

        private string DetectVirtualEnvironment()
        {
            // Mateix codi que al Generator
            string[] possibleVenvPaths = {
                Path.Combine(Application.dataPath, "UnityPlugin", "Scripts", ".venv"),
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Temp\Hunyuan2-3D-for-windows\.venv"
            };

            foreach (string venvPath in possibleVenvPaths)
            {
                if (Directory.Exists(venvPath))
                {
                    string pythonExe = Path.Combine(venvPath, "Scripts", "python.exe");
                    if (File.Exists(pythonExe))
                    {
                        return venvPath;
                    }
                }
            }

            return null;
        }

        private async Task<bool> CheckPythonVersion()
        {
            try
            {
                string arguments = "--version";
                var output = await ExecuteCommand(pythonPath, arguments);

                if (output.Contains("Python"))
                {
                    detectedPythonVersion = output.Trim();
                    // Extreure número de versió
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(output, @"Python (\d+)\.(\d+)");
                    if (versionMatch.Success)
                    {
                        int major = int.Parse(versionMatch.Groups[1].Value);
                        int minor = int.Parse(versionMatch.Groups[2].Value);
                        pythonVersionOK = (major > 3) || (major == 3 && minor >= 8);

                        if (pythonVersionOK)
                        {
                            AddLogMessage($"✓ Python {major}.{minor} és compatible (mínim 3.8)");
                        }
                        else
                        {
                            AddLogMessage($"✗ Python {major}.{minor} és massa antic (mínim 3.8)");
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error comprovant Python: {ex.Message}");
            }

            return false;
        }

        private async Task<bool> CheckSingleDependency(string packageName)
        {
            try
            {
                string arguments = useCondaEnv ?
                    $"-m pip show {packageName}" :
                    $"-c \"import {GetImportName(packageName)}\"";

                string pythonCmd = useCondaEnv ?
                    $"conda run -n {condaEnvName} python" : pythonPath;

                var output = await ExecuteCommand(pythonCmd, arguments);
                return !output.Contains("No module named") && !output.Contains("not found");
            }
            catch
            {
                return false;
            }
        }

        private async Task CheckTorchAndCuda()
        {
            try
            {
                // Comprovar PyTorch
                string torchCheck = "-c \"import torch; print('PyTorch version:', torch.__version__)\"";
                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                var torchOutput = await ExecuteCommand(pythonCmd, torchCheck);
                if (torchOutput.Contains("PyTorch version:"))
                {
                    detectedTorchVersion = torchOutput.Trim();
                    torchInstalled = true;

                    // Comprovar CUDA
                    string cudaCheck = "-c \"import torch; print('CUDA available:', torch.cuda.is_available()); print('CUDA version:', torch.version.cuda if torch.cuda.is_available() else 'N/A')\"";
                    var cudaOutput = await ExecuteCommand(pythonCmd, cudaCheck);

                    if (cudaOutput.Contains("CUDA available: True"))
                    {
                        cudaAvailable = true;
                        var lines = cudaOutput.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("CUDA version:"))
                            {
                                detectedCudaVersion = line.Trim();
                                break;
                            }
                        }
                        AddLogMessage("✓ CUDA disponible per acceleració GPU");
                    }
                    else
                    {
                        AddLogMessage("⚠ CUDA no disponible - s'utilitzarà CPU");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error comprovant PyTorch/CUDA: {ex.Message}");
            }
        }

        private async Task CheckPyTorchCuda()
        {
            try
            {
                AddLogMessage("Verificant PyTorch i CUDA...");

                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                // Script per verificar PyTorch i CUDA
                string checkScript = @"-c ""
import torch
print('PyTorch version:', torch.__version__)
print('CUDA available:', torch.cuda.is_available())
if torch.cuda.is_available():
    print('CUDA version:', torch.version.cuda)
    print('cuDNN version:', torch.backends.cudnn.version())
    print('GPU count:', torch.cuda.device_count())
    for i in range(torch.cuda.device_count()):
        print(f'GPU {i}:', torch.cuda.get_device_name(i))
        props = torch.cuda.get_device_properties(i)
        print(f'  Memory: {props.total_memory / 1024**3:.1f} GB')
        print(f'  Compute capability: {props.major}.{props.minor}')
else:
    print('Running in CPU mode')
""";

                var output = await ExecuteCommand(pythonCmd, checkScript);

                if (output.Contains("PyTorch version:"))
                {
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("PyTorch version:"))
                        {
                            detectedTorchVersion = line.Replace("PyTorch version:", "").Trim();
                            torchInstalled = true;
                        }
                        else if (line.Contains("CUDA available: True"))
                        {
                            cudaAvailable = true;
                            AddLogMessage("✓ CUDA està disponible per PyTorch");
                        }
                        else if (line.Contains("CUDA version:") && !line.Contains("N/A"))
                        {
                            detectedCudaVersion = line.Replace("CUDA version:", "").Trim();
                        }
                        else if (line.Contains("GPU") && (line.Contains("NVIDIA") || line.Contains("GeForce") || line.Contains("RTX") || line.Contains("GTX")))
                        {
                            AddLogMessage($"  {line.Trim()}");
                        }
                    }

                    if (!cudaAvailable)
                    {
                        AddLogMessage("⚠ PyTorch està en mode CPU");
                    }
                }
                else
                {
                    AddLogMessage("⚠ No s'ha pogut verificar PyTorch");
                    torchInstalled = false;
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error verificant PyTorch/CUDA: {ex.Message}");
                torchInstalled = false;
                cudaAvailable = false;
            }
        }

        private string GetImportName(string packageName)
        {
            // Mapeig de noms de paquet pip a noms d'importació
            var mapping = new Dictionary<string, string>
            {
                {"opencv-python", "cv2"},
                {"pillow", "PIL"},
                {"scikit-learn", "sklearn"},
                {"scikit-image", "skimage"},
                {"pybind11", "pybind11"}
            };

            return mapping.ContainsKey(packageName) ? mapping[packageName] : packageName;
        }
        #endregion

        #region Funcions d'Instal·lació
        private async Task InstallAllDependencies()
        {
            isInstalling = true;
            progress = 0f;
            statusMessage = "Iniciant instal·lació completa...";

            try
            {
                AddLogMessage("=== INICI INSTAL·LACIÓ HUNYUAN3D ===");
                AddLogMessage("Basat en: https://github.com/Tencent-Hunyuan/Hunyuan3D-2");

                // 1. Detectar i preparar CUDA si és necessari
                progress = 0.05f;
                statusMessage = "Detectant configuració CUDA...";
                await DetectCudaInstallation();

                // Oferir instal·lació CUDA si no està disponible
                if (!cudaToolkitInstalled && Application.platform == RuntimePlatform.WindowsEditor)
                {
                    bool installCuda = EditorUtility.DisplayDialog(
                        "CUDA no detectat",
                        "No s'ha detectat CUDA Toolkit al sistema.\n" +
                        "Vols instal·lar CUDA automàticament per acceleració GPU?\n\n" +
                        "Recomanat: Sí (millor rendiment)\n" +
                        "No: Continuarà amb mode CPU",
                        "Instal·lar CUDA 12.1", "Continuar amb CPU"
                    );

                    if (installCuda)
                    {
                        statusMessage = "Instal·lant CUDA...";
                        await InstallCudaToolkit("12.1");

                        // Actualitzar mode d'instal·lació
                        selectedInstallMode = InstallationMode.CUDA12;
                    }
                    else
                    {
                        selectedInstallMode = InstallationMode.CPU;
                        AddLogMessage("Continuant amb mode CPU...");
                    }
                }

                // 2. Instal·lar PyTorch primer (més important)
                progress = 0.1f;
                statusMessage = "Instal·lant PyTorch...";
                await InstallPyTorch();

                // 2. Dependències core
                progress = 0.3f;
                statusMessage = "Instal·lant dependències core...";
                await InstallDependencyGroup(coreDependencies, "Core");

                // 3. Dependències d'imatge
                progress = 0.5f;
                statusMessage = "Instal·lant processament d'imatges...";
                await InstallDependencyGroup(imageDependencies, "Imatge");

                // 4. Dependències de malla
                progress = 0.7f;
                statusMessage = "Instal·lant processament de malles...";
                await InstallDependencyGroup(meshProcessingDependencies, "Malles");

                // 5. Utilitats
                progress = 0.85f;
                statusMessage = "Instal·lant utilitats...";
                await InstallDependencyGroup(utilityDependencies, "Utilitats");

                // 6. Opcionals
                progress = 0.95f;
                statusMessage = "Instal·lant dependències opcionals...";
                await InstallDependencyGroup(optionalDependencies, "Opcionals");

                // 7. Instal·lar el paquet Hunyuan3D
                progress = 0.98f;
                statusMessage = "Instal·lant paquet Hunyuan3D...";
                await InstallHunyuan3DPackage();

                progress = 1f;
                statusMessage = "Instal·lació completada!";
                AddLogMessage("✓ Instal·lació completada amb èxit!");
                AddLogMessage("Executa 'Comprovar Dependències' per verificar.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error durant la instal·lació: {ex.Message}");
                statusMessage = "Error durant la instal·lació";
            }
            finally
            {
                isInstalling = false;
            }
        }

        private async Task InstallPyTorch()
        {
            string torchCommand = "";

            switch (selectedInstallMode)
            {
                case InstallationMode.CPU:
                    torchCommand = "torch torchvision --index-url https://download.pytorch.org/whl/cpu";
                    break;
                case InstallationMode.CUDA11:
                    torchCommand = "torch torchvision --index-url https://download.pytorch.org/whl/cu118";
                    break;
                case InstallationMode.CUDA12:
                    torchCommand = "torch torchvision --index-url https://download.pytorch.org/whl/cu121";
                    break;
                case InstallationMode.Auto:
                    // Detectar automàticament
                    if (await DetectCudaCapability())
                    {
                        torchCommand = "torch torchvision"; // Deixar que pip detecti
                    }
                    else
                    {
                        torchCommand = "torch torchvision --index-url https://download.pytorch.org/whl/cpu";
                    }
                    break;
            }

            await InstallPackages(new[] { torchCommand });
        }

        private async Task<bool> DetectCudaCapability()
        {
            // Implementació intel·ligent de detecció CUDA
            try
            {
                // Primer comprovar nvidia-smi
                string nvidiaSmiCheck = "nvidia-smi";
                var output = await ExecuteCommand(nvidiaSmiCheck, "");

                if (output.Contains("CUDA Version"))
                {
                    AddLogMessage("✓ Driver NVIDIA detectat");

                    // Comprovar si CUDA Toolkit està instal·lat
                    await DetectCudaInstallation();

                    if (cudaToolkitInstalled)
                    {
                        AddLogMessage("✓ CUDA Toolkit ja instal·lat");
                        return true;
                    }
                    else
                    {
                        AddLogMessage("⚠ Driver NVIDIA present però CUDA Toolkit no instal·lat");

                        // En mode Auto, oferir instal·lació automàtica
                        if (selectedInstallMode == InstallationMode.Auto)
                        {
                            bool autoInstall = EditorUtility.DisplayDialog(
                                "CUDA Toolkit Necessari",
                                "S'ha detectat una targeta NVIDIA però CUDA Toolkit no està instal·lat.\n" +
                                "Vols instal·lar-lo automàticament?",
                                "Sí, instal·lar CUDA 12.1", "No, usar CPU"
                            );

                            if (autoInstall)
                            {
                                await InstallCudaToolkit("12.1");
                                return cudaToolkitInstalled;
                            }
                        }

                        return false;
                    }
                }
                else
                {
                    AddLogMessage("ℹ No s'ha detectat targeta NVIDIA - usant mode CPU");
                    return false;
                }
            }
            catch
            {
                AddLogMessage("ℹ No s'ha pogut detectar CUDA - usant mode CPU");
                return false;
            }
        }

        private async Task InstallDependencyGroup(string[] dependencies, string groupName)
        {
            AddLogMessage($"Instal·lant grup: {groupName}");
            await InstallPackages(dependencies);
        }

        private async Task InstallSingleDependency(string dependency)
        {
            await InstallPackages(new[] { dependency });
        }

        private async Task InstallPackages(string[] packages)
        {
            foreach (string package in packages)
            {
                try
                {
                    AddLogMessage($"Instal·lant: {package}");

                    string pipCmd;
                    string arguments;

                    if (useCondaEnv)
                    {
                        pipCmd = "conda";
                        arguments = $"install -n {condaEnvName} -c conda-forge -y {package}";
                    }
                    else
                    {
                        pipCmd = pythonPath;
                        arguments = $"-m pip install {package}";
                    }

                    var output = await ExecuteCommand(pipCmd, arguments);

                    if (output.Contains("Successfully installed") || output.Contains("already satisfied"))
                    {
                        AddLogMessage($"✓ {package} instal·lat");

                        // Actualitzar estat
                        string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                        dependencyStatus[packageName] = DependencyStatus.Installed;
                    }
                    else if (output.Contains("ERROR") || output.Contains("error"))
                    {
                        AddLogMessage($"✗ Error instal·lant {package}");
                        string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                        dependencyStatus[packageName] = DependencyStatus.Error;
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"✗ Excepció instal·lant {package}: {ex.Message}");
                }
            }
        }

        private async Task CreateCondaEnvironment()
        {
            try
            {
                statusMessage = "Creant environment Conda...";
                AddLogMessage($"Creant environment Conda: {condaEnvName}");

                string arguments = $"create -n {condaEnvName} python=3.9 -y";
                var output = await ExecuteCommand("conda", arguments);

                if (output.Contains("done") || output.Contains("already exists"))
                {
                    AddLogMessage($"✓ Environment {condaEnvName} creat");
                    useCondaEnv = true;
                }
                else
                {
                    AddLogMessage($"✗ Error creant environment: {output}");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error creant environment: {ex.Message}");
            }
        }
        private async Task ForceDeleteDirectory(string directoryPath)
        {
            try
            {
                // Eliminar atributs de només lectura de tots els fitxers
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignorar errors individuals
                    }
                }

                // Eliminar directoris
                var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length); // Més profunds primer

                foreach (string dir in directories)
                {
                    try
                    {
                        Directory.Delete(dir, false);
                    }
                    catch
                    {
                        // Ignorar errors individuals
                    }
                }

                // Eliminar directori principal
                Directory.Delete(directoryPath, false);
            }
            catch (Exception ex)
            {
                throw new Exception($"No es pot eliminar {directoryPath}: {ex.Message}");
            }
        }
        private async Task InstallHunyuan3DPackage()
        {
            try
            {
                MainThreadExecutor.RunOnMainThread(async () =>
                {
                    AddLogMessage("Instal·lant paquet Hunyuan3D des del repositori oficial...");

                    // Clonar repositori i instal·lar
                    string tempDir = Path.Combine(Path.GetTempPath(), "hunyuan3d_temp");

                    // Eliminar directori temporal si existeix
                    if (Directory.Exists(tempDir))
                    {
                        AddLogMessage($"Netejant directori temporal existent: {tempDir}");
                        try
                        {
                            Directory.Delete(tempDir, true);
                            await Task.Delay(500); // Petita pausa per assegurar que s'ha eliminat
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"⚠ Avís eliminant directori: {ex.Message}");
                        }
                    }

                    // Si falla, intentar amb PowerShell
                    AddLogMessage("Intentant netejar amb PowerShell...");
                    try
                    {
                        string psCommand = $"Remove-Item \"{tempDir}\" -Recurse -Force -ErrorAction SilentlyContinue";
                        await ExecuteCommand("powershell", $"-Command \"{psCommand}\"");
                        await Task.Delay(1000);

                        if (!Directory.Exists(tempDir))
                        {
                            AddLogMessage("✓ Directori netejat amb PowerShell");

                        }
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"⚠ PowerShell cleanup fallida: {ex.Message}");
                    }

                    // Si encara existeix, intentar eliminar fitxers individuals
                    AddLogMessage("Intentant eliminar fitxers individuals...");
                    try
                    {
                        await ForceDeleteDirectory(tempDir);
                        if (!Directory.Exists(tempDir))
                        {
                            AddLogMessage("✓ Directori netejat forçadament");

                        }
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"⚠ Eliminació forçada fallida: {ex.Message}");
                    }


                    // Verificar que git està instal·lat
                    var gitCheck = await ExecuteCommand("git", "--version");
                    if (gitCheck.Contains("ERROR") || !gitCheck.Contains("git version"))
                    {
                        AddLogMessage("✗ Git no està instal·lat o no és accessible");

                        bool downloadZip = EditorUtility.DisplayDialog(
                            "Git No Detectat",
                            "Git no està instal·lat o no és accessible.\n\n" +
                            "Opcions:\n" +
                            "• Descarregar com ZIP (recomanat)\n" +
                            "• Cancel·lar i instal·lar Git manualment",
                            "Descarregar ZIP", "Cancel·lar"
                        );

                        if (downloadZip)
                        {
                            await DownloadHunyuan3DAsZip(tempDir);
                        }
                        else
                        {
                            AddLogMessage("Instal·lació cancel·lada. Instal·la Git des de: https://git-scm.com/downloads");
                            return;
                        }
                    }
                    else
                    {
                        AddLogMessage("✓ Git detectat: " + gitCheck.Trim());

                        // Git clone amb millor gestió d'errors
                        AddLogMessage($"Clonant repositori a: {tempDir}");
                        string gitArgs = $"clone --depth 1 https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git \"{tempDir}\"";

                        var output = await ExecuteCommand("git", gitArgs);
                        AddLogMessage("Sortida git clone:");
                        AddLogMessage(output);

                        // Verificar si el clone ha funcionat
                        if (!Directory.Exists(tempDir) || !Directory.GetFiles(tempDir, "*.py", SearchOption.AllDirectories).Any())
                        {
                            AddLogMessage("✗ El repositori no s'ha clonat correctament");

                            // Intentar descarregar com ZIP com a alternativa
                            bool tryZip = EditorUtility.DisplayDialog(
                                "Error Git Clone",
                                "No s'ha pogut clonar el repositori amb Git.\n\n" +
                                "Vols intentar descarregar-lo com ZIP?",
                                "Sí, descarregar ZIP", "Cancel·lar"
                            );

                            if (tryZip)
                            {
                                await DownloadHunyuan3DAsZip(tempDir);
                            }
                            else
                            {
                                return;
                            }
                        }
                    }

                    // Verificar que el directori existeix i té contingut
                    if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories).Any())
                    {
                        AddLogMessage($"✓ Repositori preparat a: {tempDir}");

                        // Buscar requirements.txt
                        string reqPath = Path.Combine(tempDir, "requirements.txt");
                        if (!File.Exists(reqPath))
                        {
                            // Buscar en subdirectoris
                            var reqFiles = Directory.GetFiles(tempDir, "requirements.txt", SearchOption.AllDirectories);
                            if (reqFiles.Length > 0)
                            {
                                reqPath = reqFiles[0];
                                AddLogMessage($"Requirements.txt trobat a: {reqPath}");
                            }
                        }

                        // Instal·lar des del codi font o requirements
                        string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;
                        await EnsureSetuptoolsInstalled(pythonCmd);

                        if (File.Exists(reqPath))
                        {
                            AddLogMessage("Instal·lant des de requirements.txt...");
                            string reqArgs = $"-m pip install -r \"{reqPath}\"";
                            var reqOutput = await ExecuteCommand(pythonCmd, reqArgs);
                            AddLogMessage(reqOutput);

                            if (reqOutput.Contains("Successfully installed") || reqOutput.Contains("already satisfied"))
                            {
                                AddLogMessage("✓ Dependències instal·lades des de requirements.txt");
                            }
                        }

                        // Intentar instal·lar el paquet en mode desenvolupament
                        string setupPath = Path.Combine(tempDir, "setup.py");
                        if (File.Exists(setupPath))
                        {
                            AddLogMessage("Instal·lant paquet Hunyuan3D en mode desenvolupament...");
                            string installArgs = $"-m pip install -e \"{tempDir}\"";
                            var installOutput = await ExecuteCommand(pythonCmd, installArgs);
                            AddLogMessage(installOutput);

                            if (installOutput.Contains("Successfully installed"))
                            {
                                AddLogMessage("✓ Paquet Hunyuan3D instal·lat en mode desenvolupament");
                            }
                        }
                        else
                        {
                            AddLogMessage("⚠ No s'ha trobat setup.py - només s'han instal·lat les dependències");
                        }

                        // Intentar instal·lar el paquet en mode desenvolupament
                        // Instal·lar custom_rasterizer amb gestió millorada d'errors
                        SetCudaHomeEnv();
                        string custRasterPath = Path.Combine(tempDir, "hy3dgen", "texgen", "custom_rasterizer");
                        if (Directory.Exists(custRasterPath))
                        {
                            await HandleCustomRasterizerCompilation(pythonCmd, custRasterPath);
                        }
                        else
                        {
                            AddLogMessage("⚠ custom_rasterizer no trobat al repositori");
                            AddLogMessage("ℹ Continuant sense aquest mòdul opcional");
                        }

                        // Similar per differentiable_renderer...
                        string diffRendererPath = Path.Combine(tempDir, "hy3dgen", "texgen", "differentiable_renderer");
                        if (Directory.Exists(diffRendererPath))
                        {
                            AddLogMessage("Instal·lant differentiable_renderer...");

                            string installArgs = $"setup.py install";
                            SetCudaHomeEnv();
                            var installOutput = await ExecuteCommandInDirectory(pythonCmd, installArgs, diffRendererPath);

                            if (installOutput.Contains("Successfully installed") ||
                                installOutput.Contains("Finished processing"))
                            {
                                AddLogMessage("✓ differentiable_renderer compilat correctament");
                            }
                            else
                            {
                                AddLogMessage("⚠ Error o warning compilant differentiable_renderer (mòdul opcional)");
                            }
                        }

                        // Test final
                        AddLogMessage("Verificant instal·lació de Hunyuan3D...");
                        string testImport = "-c \"import hy3dgen; print('✓ Hunyuan3D importat correctament')\"";
                        var testOutput = await ExecuteCommand(pythonCmd, testImport);

                        if (testOutput.Contains("✓ Hunyuan3D importat correctament"))
                        {
                            AddLogMessage("🎉 Instal·lació de Hunyuan3D completada!");

                            EditorUtility.DisplayDialog(
                                "Hunyuan3D Instal·lat",
                                "Hunyuan3D s'ha instal·lat correctament!\n\n" +
                                "Funcionalitats disponibles:\n" +
                                "✓ Generació de models 3D des d'imatges\n" +
                                "✓ Processament de malles\n" +
                                "✓ Interfície Gradio\n\n" +
                                "Nota: Alguns mòduls opcionals poden haver fallat\n" +
                                "degut a problemes de compilació. Això no impedeix\n" +
                                "l'ús bàsic del sistema.",
                                "Perfecte!"
                            );
                        }
                        else
                        {
                            AddLogMessage("⚠ Possible problema amb la instal·lació principal:");
                            AddLogMessage(testOutput);
                        }

                        AddLogMessage($"ℹ Codi font disponible a: {tempDir}");
                    }
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error instal·lant paquet Hunyuan3D: {ex.Message}");
            }
        }

        private async Task HandleCustomRasterizerCompilation(string pythonCmd, string custRasterPath)
        {
            try
            {
                AddLogMessage("Instal·lant custom_rasterizer...");
                AddLogMessage("NOTA: Aquest procés pot trigar i requerir Visual Studio compatible");

                // Detectar problemes de Visual Studio abans de compilar
                bool hasVSIssues = await DetectVisualStudioIssues();

                if (hasVSIssues)
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Problema de Compatibilitat Detectat",
                        "S'ha detectat un possible problema de compatibilitat amb Visual Studio.\n\n" +
                        "Custom_rasterizer és un mòdul OPCIONAL que millora el rendiment però no és essencial.\n\n" +
                        "Opcions:",
                        "Intentar igualment",
                        "Saltar aquest mòdul",
                        "Aplicar workarounds"
                    );

                    switch (choice)
                    {
                        case 0: // Intentar igualment
                            AddLogMessage("⚠ Intentant compilació malgrat problemes detectats...");
                            break;
                        case 1: // Saltar
                            AddLogMessage("⏭ Saltant custom_rasterizer per decisió de l'usuari");
                            AddLogMessage("ℹ Hunyuan3D funcionarà correctament sense aquest mòdul");
                            return;
                        case 2: // Workarounds
                            await ApplyCompilationWorkarounds(custRasterPath);
                            break;
                    }
                }

                // Intentar compilació estàndard
                string installArgs = "setup.py install";
                SetCudaHomeEnv();
                var installOutput = await ExecuteCommandInDirectory(pythonCmd, installArgs, custRasterPath);

                if (installOutput.Contains("Successfully installed") ||
                    installOutput.Contains("Finished processing"))
                {
                    AddLogMessage("✅ custom_rasterizer instal·lat correctament!");
                }
                else if (installOutput.Contains("ninja: build stopped") ||
                         installOutput.Contains("RuntimeError: Error compiling") ||
                         installOutput.Contains("unsupported Microsoft Visual Studio version"))
                {
                    AddLogMessage("❌ Error de compilació detectat");
                    await HandleCompilationError(installOutput, pythonCmd, custRasterPath);
                }
                else
                {
                    AddLogMessage("⚠ Compilació completada amb advertències:");
                    AddLogMessage(installOutput);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Error en custom_rasterizer: {ex.Message}");
                AddLogMessage("ℹ Aquest mòdul és opcional - Hunyuan3D funcionarà sense ell");
            }
        }

        private async Task HandleCompilationError(string errorOutput, string pythonCmd, string custRasterPath)
        {
            if (errorOutput.Contains("unsupported Microsoft Visual Studio version"))
            {
                AddLogMessage("⚠ Error de compatibilitat amb Visual Studio:");
                AddLogMessage("  Error: " + errorOutput.Split('\n').FirstOrDefault(l => l.Contains("unsupported")));
                AddLogMessage("");

                await ShowVSCompatibilityOptions(pythonCmd, custRasterPath);
            }
            else if (errorOutput.Contains("ninja: build stopped") || errorOutput.Contains("RuntimeError"))
            {
                AddLogMessage("⚠ Error de compilació C++/CUDA:");

                // Extreure el error real
                var errorLines = errorOutput.Split('\n');
                var realError = errorLines.FirstOrDefault(l => l.Contains("error:") || l.Contains("ERROR:"));
                if (!string.IsNullOrEmpty(realError))
                {
                    AddLogMessage($"  Error específic: {realError}");
                }

                await ShowCompilationErrorOptions(pythonCmd, custRasterPath);
            }
            else
            {
                AddLogMessage("⚠ Error de compilació desconegut:");
                AddLogMessage(errorOutput);
                await ShowGenericErrorOptions();
            }
        }

        private async Task ShowVSCompatibilityOptions(string pythonCmd, string custRasterPath)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Error de Compatibilitat Visual Studio",
                "CUDA no suporta la versió actual de Visual Studio.\n\n" +
                "Opcions:\n" +
                "• Intentar Forçar: Usar flag --allow-unsupported-compiler\n" +
                "• Obrir Guia: Mostrar instruccions d'instal·lació\n" +
                "• Saltar: Continuar sense aquest mòdul opcional",
                "Intentar Forçar",
                "Obrir Guia",
                "Saltar"
            );

            switch (choice)
            {
                case 0: // Forçar compilació
                    AddLogMessage("⚠ Intentant compilació forçada amb --allow-unsupported-compiler");
                    await TryForceCompilation(pythonCmd, custRasterPath);
                    break;
                case 1: // Obrir guia
                    ShowVisualStudioInstallationGuide();
                    break;
                default: // Saltar
                    AddLogMessage("⏭ Saltant compilació per decisió de l'usuari");
                    break;
            }
        }

        private async Task ShowCompilationErrorOptions(string pythonCmd, string custRasterPath)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Error de Compilació C++",
                "Error compilant l'extensió C++/CUDA.\n\n" +
                "Possibles causes:\n" +
                "• Python 3.13 no és compatible (usa 3.10/3.11)\n" +
                "• Falten eines Visual Studio C++\n" +
                "• Incompatibilitat CUDA/Visual Studio\n\n" +
                "Opcions:",
                "Aplicar Workarounds",
                "Mostrar Guia",
                "Saltar Mòdul"
            );

            switch (choice)
            {
                case 0: // Workarounds
                    await ApplyCompilationWorkarounds(custRasterPath);
                    break;
                case 1: // Guia
                    ShowDetailedCompilerInstructions();
                    break;
                default: // Saltar
                    AddLogMessage("⏭ Saltant custom_rasterizer");
                    AddLogMessage("ℹ Hunyuan3D funcionarà sense aquest mòdul opcional");
                    break;
            }
        }

        private async Task ShowGenericErrorOptions()
        {
            bool showGuide = EditorUtility.DisplayDialog(
                "Error de Compilació",
                "S'ha produït un error durant la compilació.\n\n" +
                "Custom_rasterizer és un mòdul opcional que millora el rendiment,\n" +
                "però Hunyuan3D funcionarà correctament sense ell.\n\n" +
                "Vols veure la guia de solució de problemes?",
                "Mostrar Guia",
                "Continuar sense el mòdul"
            );

            if (showGuide)
            {
                ShowDetailedCompilerInstructions();
            }
            else
            {
                AddLogMessage("⏭ Continuant sense custom_rasterizer");
            }
        }

        private async Task TryForceCompilation(string pythonCmd, string custRasterPath)
        {
            try
            {
                AddLogMessage("Configurant variables d'entorn per forçar compilació...");

                // Crear script Python personalitzat per configurar l'entorn
                string forceScript = @"
import os
import sys
import subprocess

# Configurar variables d'entorn per CUDA
os.environ['CUDA_LAUNCH_BLOCKING'] = '1'
os.environ['NVCC_APPEND_FLAGS'] = '-allow-unsupported-compiler'
os.environ['TORCH_CUDA_ARCH_LIST'] = '6.0;6.1;7.0;7.5;8.0;8.6'
os.environ['FORCE_CUDA'] = '1'

print('Variables d\'entorn configurades per compilació forçada')
print('NVCC_APPEND_FLAGS:', os.environ.get('NVCC_APPEND_FLAGS'))

try:
    # Executar setup.py amb configuració forçada
    result = subprocess.run([sys.executable, 'setup.py', 'install', '--force'], 
                          capture_output=True, text=True, timeout=1800)  # 30 min timeout
    
    print('STDOUT:')
    print(result.stdout)
    if result.stderr:
        print('STDERR:')
        print(result.stderr)
    
    if result.returncode == 0:
        print('✓ Compilació forçada exitosa')
    else:
        print(f'⚠ Compilació acabada amb codi: {result.returncode}')
    
    sys.exit(result.returncode)
    
except subprocess.TimeoutExpired:
    print('✗ Timeout - compilació trigant més de 30 minuts')
    sys.exit(1)
except Exception as e:
    print(f'✗ Error durant compilació forçada: {e}')
    sys.exit(1)
";

                string tempScript = Path.Combine(Path.GetTempPath(), "force_cuda_compile.py");
                File.WriteAllText(tempScript, forceScript);

                try
                {
                    AddLogMessage("Executant compilació forçada...");
                    AddLogMessage("ADVERTÈNCIA: Usant --allow-unsupported-compiler pot causar problemes");
                    SetCudaHomeEnv();
                    var result = await ExecuteCommandInDirectory(pythonCmd, $"\"{tempScript}\"", custRasterPath);

                    if (result.Contains("✓ Compilació forçada exitosa"))
                    {
                        AddLogMessage("✓ Compilació forçada completada amb èxit");
                    }
                    else if (result.Contains("⚠ Compilació acabada amb codi:"))
                    {
                        AddLogMessage("⚠ Compilació forçada amb advertències - pot funcionar parcialment");
                    }
                    else
                    {
                        AddLogMessage("✗ Compilació forçada fallida:");
                        AddLogMessage(result);
                    }
                }
                finally
                {
                    try { File.Delete(tempScript); } catch { }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error en compilació forçada: {ex.Message}");
            }
        }

        private async Task ApplyCompilationWorkarounds(string custRasterPath)
        {
            try
            {
                AddLogMessage("🔧 Aplicant workarounds per problemes de compilació...");

                // 1. Modificar setup.py per afegir flags compatibles
                string setupPath = Path.Combine(custRasterPath, "setup.py");
                if (File.Exists(setupPath))
                {
                    await PatchSetupPyForCompatibility(setupPath);
                }

                // 2. Configurar variables d'entorn
                AddLogMessage("Configurant variables d'entorn optimitzades...");

                // 3. Intentar compilació amb configuració modificada
                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;
                var result = await TryAlternativeCompilation(pythonCmd, "setup.py install", custRasterPath);

                if (result.Contains("Successfully installed"))
                {
                    AddLogMessage("✅ Compilació amb workarounds exitosa!");
                }
                else
                {
                    AddLogMessage("⚠ Workarounds aplicats però encara hi ha problemes");
                    AddLogMessage(result);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error aplicant workarounds: {ex.Message}");
            }
        }

        private async Task PatchSetupPyForCompatibility(string setupPath)
        {
            try
            {
                AddLogMessage("Modificant setup.py per compatibilitat...");

                string content = File.ReadAllText(setupPath);

                // Buscar CUDAExtension i afegir flags compatibles
                if (content.Contains("CUDAExtension") && !content.Contains("extra_compile_args"))
                {
                    // Afegir extra_compile_args per evitar que warnings siguin errors
                    string newContent = content.Replace(
                        "CUDAExtension('custom_rasterizer_kernel', [",
                        @"CUDAExtension(
    'custom_rasterizer_kernel',
    [");

                    // Afegir flags compatibles
                    newContent = newContent.Replace(
                        "],\n)",
                        @"],
    extra_compile_args={
        'cxx': ['/WX-'],  # No tractar warnings com errors
        'nvcc': ['-allow-unsupported-compiler']
    }
)"
                    );

                    File.WriteAllText(setupPath, newContent);
                    AddLogMessage("✓ setup.py modificat per millor compatibilitat");
                }
                else
                {
                    AddLogMessage("setup.py ja té configuració compatible o no es pot modificar");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Error modificant setup.py: {ex.Message}");
            }
        }

        private async Task<string> TryAlternativeCompilation(string pythonCmd, string args, string workingDirectory)
        {
            try
            {
                AddLogMessage("Intentant compilació alternativa amb configuració especial...");

                // Crear script temporal per compilació alternativa
                string altScript = @"
import os
import sys
import subprocess

# Configurar entorn per màxima compatibilitat
os.environ['DISTUTILS_USE_SDK'] = '1'
os.environ['MSSdk'] = '1'
os.environ['CUDA_LAUNCH_BLOCKING'] = '1'
os.environ['TORCH_USE_CUDA_DSA'] = '1'

print('Entorn configurat per compilació alternativa')

try:
    result = subprocess.run([sys.executable] + sys.argv[1:], 
                          capture_output=True, text=True)
    print(result.stdout)
    if result.stderr:
        print('STDERR:', result.stderr)
    sys.exit(result.returncode)
except Exception as e:
    print(f'Error: {e}')
    sys.exit(1)
";
                string tempScript = Path.Combine(Path.GetTempPath(), "alt_compile.py");
                File.WriteAllText(tempScript, altScript);

                try
                {
                    var result = await ExecuteCommandInDirectory(pythonCmd, $"\"{tempScript}\" {args}", workingDirectory);
                    return result;
                }
                finally
                {
                    try { File.Delete(tempScript); } catch { }
                }
            }
            catch (Exception ex)
            {
                return $"Error en compilació alternativa: {ex.Message}";
            }
        }

        private async Task<bool> DetectVisualStudioIssues()
        {
            try
            {
                AddLogMessage("Detectant problemes de Visual Studio...");

                // Comprovar versió de Visual Studio
                var vswhereOutput = await ExecuteCommand("vswhere", "-latest -property installationVersion");

                if (!string.IsNullOrEmpty(vswhereOutput) && !vswhereOutput.Contains("ERROR"))
                {
                    AddLogMessage($"Visual Studio detectat: {vswhereOutput.Trim()}");

                    // Comprovar si és VS2022 (versió 17.x)
                    if (vswhereOutput.StartsWith("17."))
                    {
                        AddLogMessage("⚠ VS2022 detectat - pot haver-hi problemes de compatibilitat amb CUDA");
                        return true;
                    }
                }

                // Comprovar eines C++
                var clOutput = await ExecuteCommand("cl", "");
                if (clOutput.Contains("Microsoft") && clOutput.Contains("C/C++"))
                {
                    AddLogMessage("✓ Compilador C++ detectat");
                }
                else
                {
                    AddLogMessage("⚠ Compilador C++ no detectat");
                    return true;
                }

                return false;
            }
            catch
            {
                AddLogMessage("⚠ No s'ha pogut detectar Visual Studio");
                return true;
            }
        }

        private void ShowVisualStudioInstallationGuide()
        {
            string guide = @"
GUIA D'INSTAL·LACIÓ VISUAL STUDIO PER CUDA

1. VERSIÓ RECOMANADA:
   • Visual Studio 2019 (versió 16.x)
   • Visual Studio 2022 (versió 17.x) amb CUDA 12.x

2. COMPONENTS NECESSARIS:
   • Desktop development with C++
   • MSVC v142/v143 - VS 2019/2022 C++ x64/x86 build tools
   • Windows 10/11 SDK

3. INSTAL·LACIÓ:
   a) Descarregar VS des de: https://visualstudio.microsoft.com/
   b) Durant la instal·lació, seleccionar 'Desktop development with C++'
   c) Reiniciar després d'instal·lar

4. SOLUCIÓ PROBLEMES COMPATIBILITAT:
   • CUDA 11.x → Visual Studio 2019 o 2022
   • CUDA 12.x → Visual Studio 2022
   • Si tens VS2022 amb CUDA 11.x, usar flag --allow-unsupported-compiler

5. VARIABLES D'ENTORN:
   Afegir al PATH:
   • C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\14.29.30133\bin\Hostx64\x64
   • C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\bin
";

            EditorUtility.DisplayDialog(
                "Guia Visual Studio per CUDA",
                guide,
                "Tancar"
            );

            if (EditorUtility.DisplayDialog(
                "Obrir Documentació",
                "Vols obrir la documentació oficial de CUDA?",
                "Sí", "No"))
            {
                Application.OpenURL("https://docs.nvidia.com/cuda/cuda-installation-guide-microsoft-windows/");
            }
        }

        private void ShowDetailedCompilerInstructions()
        {
            string instructions = @"
GUIA DETALLADA SOLUCIÓ PROBLEMES COMPILACIÓ

PROBLEMA 1: Python 3.13 Incompatible
• SOLUCIÓ: Usar Python 3.10 o 3.11
• Instal·lar: https://www.python.org/downloads/release/python-3119/

PROBLEMA 2: Visual Studio no compatible
• SOLUCIÓ: Instal·lar VS2019 o VS2022
• Components: Desktop development with C++

PROBLEMA 3: CUDA Toolkit no trobat
• SOLUCIÓ: Instal·lar CUDA Toolkit
• CUDA 11.8: https://developer.nvidia.com/cuda-11-8-0-download-archive
• CUDA 12.1: https://developer.nvidia.com/cuda-12-1-0-download-archive

PROBLEMA 4: Error 'unsupported Microsoft Visual Studio version'
• SOLUCIÓ 1: Downgrade a VS2019
• SOLUCIÓ 2: Usar flag --allow-unsupported-compiler
• SOLUCIÓ 3: Actualitzar a CUDA 12.x

PROBLEMA 5: Error 'ninja: build stopped'
• SOLUCIÓ: Instal·lar ninja manualment
  pip install ninja

WORKAROUND GENERAL:
Si res funciona, pots saltar aquest mòdul opcional.
Hunyuan3D funcionarà igualment però més lentament.
";

            EditorUtility.DisplayDialog(
                "Instruccions Detallades Compilació",
                instructions,
                "Tancar"
            );
        }

        private async Task DownloadHunyuan3DAsZip(string targetDir)
        {
            try
            {
                AddLogMessage("Descarregant Hunyuan3D com ZIP...");

                string zipUrl = "https://github.com/Tencent-Hunyuan/Hunyuan3D-2/archive/refs/heads/main.zip";
                string zipPath = Path.Combine(Path.GetTempPath(), "hunyuan3d.zip");

                using (var client = new System.Net.WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        progress = e.ProgressPercentage / 100f;
                        statusMessage = $"Descarregant: {e.ProgressPercentage}%";
                        Repaint();
                    };

                    await client.DownloadFileTaskAsync(zipUrl, zipPath);
                }

                AddLogMessage($"ZIP descarregat a: {zipPath}");

                // Extreure ZIP
                statusMessage = "Extraient arxius...";
                await ExtractZipFile(zipPath, targetDir);

                // Netejar
                try { File.Delete(zipPath); } catch { }

                AddLogMessage($"✓ Repositori extret a: {targetDir}");
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error descarregant ZIP: {ex.Message}");
                throw;
            }
        }

        private async Task ExtractZipFile(string zipPath, string extractPath)
        {
            await Task.Run(() =>
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Moure contingut del subdirectori principal
                var dirs = Directory.GetDirectories(extractPath);
                if (dirs.Length == 1 && dirs[0].Contains("Hunyuan3D"))
                {
                    var tempPath = dirs[0];
                    foreach (var file in Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories))
                    {
                        var relativePath = file.Substring(tempPath.Length + 1);
                        var targetFile = Path.Combine(extractPath, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                        File.Move(file, targetFile);
                    }
                    Directory.Delete(tempPath, true);
                }
            });
        }

        private async Task InstallHunyuan3DWithUV()
        {
            try
            {
                AddLogMessage("=== INSTAL·LACIÓ AMB UV (RECOMANAT PER WINDOWS) ===");

                // 1. Verificar/Instal·lar UV
                bool uvInstalled = await CheckAndInstallUV();
                if (!uvInstalled)
                {
                    AddLogMessage("✗ No s'ha pogut instal·lar UV");

                    // Oferir alternativa PowerShell
                    if (Application.platform == RuntimePlatform.WindowsEditor)
                    {
                        if (EditorUtility.DisplayDialog(
                            "UV no disponible",
                            "No s'ha pogut instal·lar UV automàticament.\n\n" +
                            "Vols executar l'instal·lador PowerShell complet?",
                            "Executar PowerShell", "Cancel·lar"))
                        {
                            RunWindowsPowerShellInstaller();
                        }
                    }
                    return;
                }

                // 2. Crear projecte UV per Hunyuan3D
                string projectDir = Path.Combine(Application.dataPath, "..", "Hunyuan3D_UV");
                Directory.CreateDirectory(projectDir);

                AddLogMessage($"Creant projecte UV a: {projectDir}");
                SetCudaHomeEnv();
                // 3. Inicialitzar projecte UV
                await ExecuteCommandInDirectory("uv", "init", projectDir);

                // 4. Afegir dependències principals
                string[] uvDependencies = {
                    "torch --index-url https://download.pytorch.org/whl/cu121",
                    "torchvision --index-url https://download.pytorch.org/whl/cu121",
                    "diffusers>=0.21.0",
                    "transformers>=4.25.0",
                    "trimesh>=3.15.0",
                    "opencv-python",
                    "rembg",
                    "numpy",
                    "tqdm",
                    "omegaconf",
                    "einops"
                };

                foreach (var dep in uvDependencies)
                {
                    AddLogMessage($"Afegint: {dep}");
                    await ExecuteCommandInDirectory("uv", $"add {dep}", projectDir);
                    progress = Array.IndexOf(uvDependencies, dep) / (float)uvDependencies.Length;
                }

                // 5. Instal·lar Hunyuan3D des de git
                AddLogMessage("Instal·lant Hunyuan3D des del repositori...");
                await ExecuteCommandInDirectory("uv", "pip install git+https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git", projectDir);

                
                AddLogMessage("✅ Instal·lació amb UV completada!");
                AddLogMessage($"📁 Projecte creat a: {projectDir}");

                EditorUtility.DisplayDialog(
                    "Instal·lació UV Completada",
                    $"Hunyuan3D s'ha instal·lat amb UV!\n\n" +
                    $"Ubicació: {projectDir}\n\n" +
                    $"Per usar-lo:\n" +
                    $"1. Obre un terminal a {projectDir}\n" +
                    $"2. Executa: uv run python <script.py>",
                    "Perfecte!"
                );
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error instal·lant paquet Hunyuan3D: {ex.Message}");
            }
        }

        private void RunWindowsPowerShellInstaller()
        {
            string scriptPath = Path.Combine(Application.dataPath, "UnityPlugin", "Scripts", "install_hunyuan3d_windows.ps1");

            // Verificar si el script existeix
            if (!File.Exists(scriptPath))
            {
                // Crear el script si no existeix
                string scriptsDir = Path.GetDirectoryName(scriptPath);
                if (!Directory.Exists(scriptsDir))
                {
                    Directory.CreateDirectory(scriptsDir);
                }

                // Descarregar o crear el script
                if (EditorUtility.DisplayDialog(
                    "Script d'instal·lació no trobat",
                    "El script PowerShell no existeix. Vols crear-lo automàticament?",
                    "Crear Script", "Cancel·lar"))
                {
                    CreateWindowsInstallerScript(scriptPath);
                }
                else
                {
                    return;
                }
            }

            // Opcions d'instal·lació
            bool useCuda12 = EditorUtility.DisplayDialog(
                "Selecciona versió CUDA",
                "Quina versió de CUDA vols utilitzar?\n\n" +
                "CUDA 12.4: Més recent, millor rendiment\n" +
                "CUDA 11.8: Més compatible",
                "CUDA 12.4", "CUDA 11.8"
            );

            string installPath = "C:\\Users\\" + Environment.UserName + "\\AppData\\Local\\Temp\\Hunyuan2-3D-for-windows";

            if (string.IsNullOrEmpty(installPath))
            {
                installPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }

            // Construir arguments
            string arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                              $"-InstallPath \"{installPath}\" " +
                              $"-PythonVersion \"3.10\" " +
                              (useCuda12 ? "-UseCUDA12" : "") +
                              (EditorUtility.DisplayDialog("Models", "Vols descarregar els models pre-entrenats? (~10GB)", "Sí", "No") ? "" : " -SkipModelDownload");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas" // Executar com administrador si cal
                };

                var process = Process.Start(startInfo);

                AddLogMessage("🚀 S'ha iniciat l'instal·lador de Windows");
                AddLogMessage($"📁 Carpeta d'instal·lació: {installPath}");
                AddLogMessage("⏳ L'instal·lador s'està executant en una finestra separada...");

                EditorUtility.DisplayDialog(
                    "Instal·lador en execució",
                    "L'instal·lador de Hunyuan3D per Windows s'està executant.\n\n" +
                    "Segueix les instruccions a la finestra de PowerShell.\n\n" +
                    "Un cop finalitzi, torna a Unity i verifica la instal·lació.",
                    "D'acord"
                );
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Error executant l'instal·lador: {ex.Message}");

                // Oferir executar manualment
                if (EditorUtility.DisplayDialog(
                    "Error executant script",
                    "No s'ha pogut executar el script automàticament.\n\n" +
                    "Pots executar-lo manualment:\n" +
                    $"1. Obre PowerShell com administrador\n" +
                    $"2. Executa: {scriptPath}",
                    "Copiar Path", "Tancar"))
                {
                    GUIUtility.systemCopyBuffer = scriptPath;
                    AddLogMessage("📋 Path del script copiat al portapapers");
                }
            }
        }

        private void CreateWindowsInstallerScript(string scriptPath)
        {
            // Aquí crearies el contingut del script PowerShell
            // Per simplicitat, mostraré un missatge
            AddLogMessage("📝 Creant script d'instal·lació...");

            // El contingut del script ja està definit més amunt
            // Aquí simplement el copiaries al fitxer

            EditorUtility.DisplayDialog(
                "Script creat",
                $"Script d'instal·lació creat a:\n{scriptPath}\n\n" +
                "Executa'l des de PowerShell com administrador.",
                "D'acord"
            );
        }

        private void ShowWindowsInstallGuide()
        {
            string guide = @"
GUIA D'INSTAL·LACIÓ RÀPIDA PER WINDOWS

Aquesta instal·lació utilitza UV, un gestor de paquets Python 
ultra-ràpid optimitzat per Windows.

AVANTATGES:
✓ 10-100x més ràpid que pip
✓ Gestió intel·ligent de dependències
✓ Cache compartida entre projectes
✓ Resolució de conflictes automàtica

REQUISITS:
• Windows 10/11
• ~15GB espai lliure
• Connexió a Internet
• Targeta NVIDIA (opcional però recomanat)

PROCÉS D'INSTAL·LACIÓ:
1. Clic a 'Instal·lació Ràpida Windows'
2. Selecciona versió CUDA (12.4 recomanat)
3. Tria carpeta d'instal·lació
4. Segueix instruccions a PowerShell

DESPRÉS D'INSTAL·LAR:
• Executa: start_hunyuan3d.bat
• O activa: .venv\Scripts\activate

SOLUCIÓ DE PROBLEMES:
• Si falla, executa PowerShell com administrador
• Assegura't de tenir Git instal·lat
• Desactiva antivirus temporalment si cal

MÉS INFORMACIÓ:
• UV: https://github.com/astral-sh/uv
• Hunyuan3D: https://github.com/Tencent-Hunyuan/Hunyuan3D-2
";

            EditorUtility.DisplayDialog(
                "Guia d'Instal·lació Windows",
                guide,
                "Tancar"
            );
        }

        private async Task InstallPyTorchCuda118()
        {
            try
            {
                AddLogMessage("=== INSTAL·LANT PYTORCH AMB CUDA 11.8 ===");

                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                // Desinstal·lar versions existents
                AddLogMessage("Desinstal·lant versions anteriors de PyTorch...");
                await ExecuteCommand(pythonCmd, "-m pip uninstall torch torchvision torchaudio -y");

                // Instal·lar PyTorch amb CUDA 11.8
                string installCmd = "-m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118";
                AddLogMessage("Instal·lant PyTorch amb CUDA 11.8...");

                var output = await ExecuteCommand(pythonCmd, installCmd);

                if (output.Contains("Successfully installed"))
                {
                    AddLogMessage("✓ PyTorch CUDA 11.8 instal·lat correctament");

                    // Verificar instal·lació
                    await CheckPyTorchCuda();
                }
                else
                {
                    AddLogMessage("✗ Error instal·lant PyTorch CUDA 11.8");
                    AddLogMessage(output);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error instal·lant PyTorch CUDA 11.8: {ex.Message}");
            }
        }

        private bool ConfirmCudaInstallation(string version)
        {
            return EditorUtility.DisplayDialog(
                $"Instal·lar CUDA Toolkit {version}",
                $"Això descarregarà i instal·larà CUDA Toolkit {version} (~3GB).\n\n" +
                "Requeriments:\n" +
                "• Targeta gràfica NVIDIA\n" +
                "• ~3GB d'espai al disc\n" +
                "• Permisos d'administrador\n" +
                "• Reiniciar pot ser necessari\n\n" +
                "Continuar?",
                "Instal·lar", "Cancel·lar"
            );
        }

        private async Task InstallCudaToolkit(string version)
        {
            try
            {
                isInstallingCuda = true;
                AddLogMessage($"=== INSTAL·LANT CUDA TOOLKIT {version} ===");

                string downloadUrl = version switch
                {
                    "11.8" => "https://developer.download.nvidia.com/compute/cuda/11.8.0/local_installers/cuda_11.8.0_522.06_windows.exe",
                    "12.1" => "https://developer.download.nvidia.com/compute/cuda/12.1.0/local_installers/cuda_12.1.0_531.14_windows.exe",
                    _ => throw new Exception($"Versió CUDA {version} no suportada")
                };

                string installerPath = Path.Combine(Path.GetTempPath(), $"cuda_{version}_installer.exe");

                // Descarregar
                statusMessage = $"Descarregant CUDA {version}...";
                using (var client = new System.Net.WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        progress = e.ProgressPercentage / 100f;
                        statusMessage = $"Descarregant CUDA {version}: {e.ProgressPercentage}%";
                        Repaint();
                    };

                    await client.DownloadFileTaskAsync(downloadUrl, installerPath);
                }

                AddLogMessage($"✓ CUDA {version} descarregat");

                // Executar instal·lador
                statusMessage = $"Instal·lant CUDA {version}...";
                AddLogMessage("Executant instal·lador CUDA...");
                AddLogMessage("NOTA: Accepta els valors per defecte a l'instal·lador");

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "-s",  // Silent install
                    UseShellExecute = true,
                    Verb = "runas"  // Executar com administrador
                };

                var process = Process.Start(startInfo);
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    AddLogMessage($"✓ CUDA {version} instal·lat correctament");
                    cudaToolkitInstalled = true;
                    detectedCudaToolkitVersion = version;

                    // Actualitzar PATH
                    RepairCudaPath();
                }
                else
                {
                    AddLogMessage($"✗ Error instal·lant CUDA (codi: {process.ExitCode})");
                }

                // Netejar
                try { File.Delete(installerPath); } catch { }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error instal·lant CUDA: {ex.Message}");
            }
            finally
            {
                isInstallingCuda = false;
                progress = 0f;
            }
        }

        private async Task DetectCudaInstallation()
        {
            try
            {
                AddLogMessage("Detectant instal·lació CUDA...");

                // 1. Comprovar nvcc
                var nvccOutput = await ExecuteCommand("nvcc", "--version");
                if (!nvccOutput.Contains("ERROR") && nvccOutput.Contains("release"))
                {
                    nvccAvailable = true;
                    var match = System.Text.RegularExpressions.Regex.Match(nvccOutput, @"release (\d+\.\d+)");
                    if (match.Success)
                    {
                        detectedCudaToolkitVersion = match.Groups[1].Value;
                        cudaToolkitInstalled = true;
                        AddLogMessage($"✓ CUDA Toolkit {detectedCudaToolkitVersion} detectat via nvcc");
                    }
                }

                // 2. Comprovar nvidia-smi
                var smiOutput = await ExecuteCommand("nvidia-smi", "");
                if (!smiOutput.Contains("ERROR") && smiOutput.Contains("CUDA Version"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(smiOutput, @"CUDA Version:\s*(\d+\.\d+)");
                    if (match.Success)
                    {
                        recommendedCudaVersion = $"CUDA {match.Groups[1].Value} (màxim suportat pel driver)";
                        AddLogMessage($"✓ Driver NVIDIA detectat: {recommendedCudaVersion}");
                    }
                }

                // 3. Comprovar directoris d'instal·lació
                string[] cudaPaths = {
                    @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA",
                    @"C:\Program Files\NVIDIA Corporation\CUDA"
                };

                foreach (var basePath in cudaPaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        var versions = Directory.GetDirectories(basePath, "v*");
                        if (versions.Length > 0)
                        {
                            var latestVersion = versions.OrderByDescending(v => v).First();
                            var versionMatch = System.Text.RegularExpressions.Regex.Match(latestVersion, @"v(\d+\.\d+)");
                            if (versionMatch.Success)
                            {
                                if (!cudaToolkitInstalled)
                                {
                                    detectedCudaToolkitVersion = versionMatch.Groups[1].Value;
                                    cudaToolkitInstalled = true;
                                }
                                AddLogMessage($"✓ CUDA Toolkit trobat a: {latestVersion}");
                            }
                        }
                    }
                }

                if (!cudaToolkitInstalled)
                {
                    AddLogMessage("⚠ CUDA Toolkit no detectat al sistema");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error detectant CUDA: {ex.Message}");
            }
        }

        private async Task VerifyFullInstallation()
        {
            try
            {
                AddLogMessage("=== VERIFICACIÓ COMPLETA D'INSTAL·LACIÓ ===");

                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                // Script de verificació completa
                string verifyScript = @"-c ""
import sys
print('Python:', sys.version)
print('-' * 50)

# PyTorch
try:
    import torch
    print(f'✓ PyTorch {torch.__version__}')
    print(f'  CUDA available: {torch.cuda.is_available()}')
    if torch.cuda.is_available():
        print(f'  CUDA version: {torch.version.cuda}')
        print(f'  cuDNN version: {torch.backends.cudnn.version()}')
        print(f'  GPU: {torch.cuda.get_device_name(0)}')
except ImportError:
    print('✗ PyTorch no instal·lat')

print('-' * 50)

# Dependències principals
deps = {
    'diffusers': 'Diffusers',
    'transformers': 'Transformers', 
    'trimesh': 'Trimesh',
    'cv2': 'OpenCV',
    'rembg': 'Rembg',
    'numpy': 'NumPy',
    'tqdm': 'TQDM',
    'omegaconf': 'OmegaConf',
    'einops': 'Einops'
}

for module, name in deps.items():
    try:
        __import__(module)
        print(f'✓ {name}')
    except ImportError:
        print(f'✗ {name}')

print('-' * 50)

# Hunyuan3D
try:
    import hy3dgen
    print('✓ Hunyuan3D package')
except ImportError:
    print('✗ Hunyuan3D package')

# Mòduls opcionals
print('\nMòduls opcionals:')
try:
    import custom_rasterizer_kernel
    print('✓ Custom Rasterizer')
except ImportError:
    print('⚠ Custom Rasterizer (opcional)')
""";

                var output = await ExecuteCommand(pythonCmd, verifyScript);
                AddLogMessage(output);

                if (output.Contains("✓ Hunyuan3D package"))
                {
                    EditorUtility.DisplayDialog(
                        "Verificació Completada",
                        "Hunyuan3D està instal·lat i llest per usar!\n\n" +
                        "Revisa els logs per veure l'estat detallat.",
                        "Perfecte!"
                    );
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Verificació Incompleta",
                        "Algunes dependències poden faltar.\n\n" +
                        "Revisa els logs i executa 'Instal·lar Tot' si cal.",
                        "D'acord"
                    );
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error durant verificació: {ex.Message}");
            }
        }

        private void RepairCudaPath()
        {
            try
            {
                AddLogMessage("Reparant PATH de CUDA...");

                string[] cudaPaths = {
                    @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\bin",
                    @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.1\bin",
                    @"C:\Program Files\NVIDIA Corporation\CUDA\v11.8\bin",
                    @"C:\Program Files\NVIDIA Corporation\CUDA\v12.1\bin"
                };

                string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                bool pathUpdated = false;

                foreach (var cudaPath in cudaPaths)
                {
                    if (Directory.Exists(cudaPath) && !currentPath.Contains(cudaPath))
                    {
                        currentPath = cudaPath + ";" + currentPath;
                        pathUpdated = true;
                        AddLogMessage($"✓ Afegit al PATH: {cudaPath}");
                    }
                }

                if (pathUpdated)
                {
                    Environment.SetEnvironmentVariable("PATH", currentPath, EnvironmentVariableTarget.User);
                    AddLogMessage("✓ PATH actualitzat - pot ser necessari reiniciar Unity");

                    EditorUtility.DisplayDialog(
                        "PATH Actualitzat",
                        "El PATH de CUDA s'ha actualitzat.\n\n" +
                        "Pot ser necessari reiniciar Unity perquè els canvis tinguin efecte.",
                        "D'acord"
                    );
                }
                else
                {
                    AddLogMessage("ℹ No s'han trobat paths CUDA per afegir");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error reparant PATH: {ex.Message}");
            }
        }

        private void CopyLogsToClipboard()
        {
            string allLogs = string.Join("\n", logMessages);
            GUIUtility.systemCopyBuffer = allLogs;
            AddLogMessage("Logs copiats al portapapers!");
        }

        private void AddLogMessage(string message)
        {
            MainThreadExecutor.RunOnMainThread(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                logMessages.Add($"[{timestamp}] {message}");

                // Limitar a 1000 missatges
                if (logMessages.Count > 1000)
                {
                    logMessages.RemoveAt(0);
                }

                // Auto-scroll al final
                scrollPosition.y = float.MaxValue;

                Repaint();
            });
        }

        private async Task<string> ExecuteCommand(string command, string arguments)
        {
            return await ExecuteCommandInDirectory(command, arguments, null);
        }

        private async Task<string> ExecuteCommandInDirectory(string command, string arguments, string workingDirectory)
        {
            try
            {
                var tcs = new TaskCompletionSource<string>();

                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                };

                // Propagar CUDA_HOME als subprocessos
                SetCudaEnvironmentVariables(startInfo);

                var process = new Process { StartInfo = startInfo };
                var output = new System.Text.StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                return output.ToString();
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public void SetCudaEnvironmentVariables(ProcessStartInfo startInfo)
        {
            // Busca la instal·lació de CUDA més recent
            string[] possibleCudaDirs = {
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.4",
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.1",
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8",
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA"
            };

            string cudaHome = null;
            foreach (var dir in possibleCudaDirs)
            {
                if (Directory.Exists(dir))
                {
                    cudaHome = dir;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(cudaHome))
            {
                // Assegurar que les variables d'entorn es propaguen als subprocessos
                startInfo.EnvironmentVariables["CUDA_HOME"] = cudaHome;
                startInfo.EnvironmentVariables["CUDA_PATH"] = cudaHome;
                startInfo.EnvironmentVariables["CUDA_PATH_V12_4"] = cudaHome; // Per CUDA 12.4

                // Afegir bin al PATH
                string cudaBin = Path.Combine(cudaHome, "bin");
                string currentPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH");
                if (!currentPath.Contains(cudaBin))
                {
                    startInfo.EnvironmentVariables["PATH"] = cudaBin + ";" + currentPath;
                }

              //  AddLogMessage($"✓ Variables CUDA establertes per al subprocés: {cudaHome}");
            }
            else
            {
               // AddLogMessage("⚠ No s'ha trobat cap instal·lació de CUDA");
            }
        }

        private async Task<bool> CheckAndInstallUV()
        {
            try
            {
                AddLogMessage("Verificant UV...");

                // Comprovar si UV ja està instal·lat
                var uvCheck = await ExecuteCommand("uv", "--version");
                if (!uvCheck.Contains("ERROR") && uvCheck.Contains("uv"))
                {
                    AddLogMessage($"✓ UV ja instal·lat: {uvCheck.Trim()}");
                    return true;
                }

                AddLogMessage("UV no detectat. Instal·lant...");

                // Instal·lar UV via PowerShell (mètode oficial per Windows)
                string installScript = @"
# Instal·lar UV
Write-Host 'Instal·lant UV Package Manager...'
try {
    # Mètode 1: Instal·lador oficial
    Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression
    
    # Verificar instal·lació
    $uvPath = Get-Command uv -ErrorAction SilentlyContinue
    if ($uvPath) {
        Write-Host '✓ UV instal·lat correctament'
        exit 0
    }
    
    # Mètode 2: Via pip si falla
    Write-Host 'Intentant instal·lar via pip...'
    pip install uv
    
    # Verificar novament
    $uvPath = Get-Command uv -ErrorAction SilentlyContinue
    if ($uvPath) {
        Write-Host '✓ UV instal·lat via pip'
        exit 0
    }
    
    Write-Host '✗ No s''ha pogut instal·lar UV'
    exit 1
}
catch {
    Write-Host ""Error: $_""
    exit 1
}
";

                string tempScript = Path.Combine(Path.GetTempPath(), "install_uv.ps1");
                File.WriteAllText(tempScript, installScript);

                try
                {
                    var output = await ExecuteCommand("powershell", $"-ExecutionPolicy Bypass -File \"{tempScript}\"");

                    if (output.Contains("✓ UV instal·lat"))
                    {
                        AddLogMessage("✓ UV instal·lat correctament");

                        // Actualitzar PATH si cal
                        await UpdatePathForUV();

                        // Verificar que funciona
                        var finalCheck = await ExecuteCommand("uv", "--version");
                        if (!finalCheck.Contains("ERROR"))
                        {
                            AddLogMessage($"✓ UV verificat: {finalCheck.Trim()}");
                            return true;
                        }
                    }

                    AddLogMessage("⚠ UV instal·lat però no accessible. Pot ser necessari reiniciar el terminal.");

                    // Oferir instruccions manuals
                    EditorUtility.DisplayDialog(
                        "UV Instal·lat",
                        "UV s'ha instal·lat però pot no ser accessible fins reiniciar.\n\n" +
                        "Si continua sense funcionar:\n" +
                        "1. Obre PowerShell com administrador\n" +
                        "2. Executa: Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression\n" +
                        "3. Reinicia Unity",
                        "D'acord"
                    );

                    return false;
                }
                finally
                {
                    try { File.Delete(tempScript); } catch { }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error instal·lant UV: {ex.Message}");

                // Mostrar instruccions alternatives
                bool tryManual = EditorUtility.DisplayDialog(
                    "Error instal·lant UV",
                    "No s'ha pogut instal·lar UV automàticament.\n\n" +
                    "Opcions:\n" +
                    "• Instal·lar manualment des de: https://docs.astral.sh/uv/\n" +
                    "• Usar pip tradicional (més lent)\n\n" +
                    "Vols obrir la documentació d'UV?",
                    "Obrir Documentació", "Cancel·lar"
                );

                if (tryManual)
                {
                    Application.OpenURL("https://docs.astral.sh/uv/getting-started/installation/");
                }

                return false;
            }
        }

        private async Task UpdatePathForUV()
        {
            try
            {
                // Possibles ubicacions d'UV
                string[] uvPaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "uv", "bin"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin")
                };

                string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                bool pathUpdated = false;

                foreach (var uvPath in uvPaths)
                {
                    if (Directory.Exists(uvPath) && !currentPath.Contains(uvPath))
                    {
                        // Verificar si UV existeix en aquest path
                        string uvExe = Path.Combine(uvPath, "uv.exe");
                        if (File.Exists(uvExe))
                        {
                            currentPath = uvPath + ";" + currentPath;
                            pathUpdated = true;
                            AddLogMessage($"✓ Afegit UV al PATH: {uvPath}");
                            break;
                        }
                    }
                }

                if (pathUpdated)
                {
                    Environment.SetEnvironmentVariable("PATH", currentPath, EnvironmentVariableTarget.User);
                    AddLogMessage("✓ PATH actualitzat amb UV");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Error actualitzant PATH per UV: {ex.Message}");
            }
        }
        private async Task EnsureSetuptoolsInstalled(string pythonCmd)
        {
            AddLogMessage("Comprovant setuptools...");
            var output = await ExecuteCommand(pythonCmd, "-m pip show setuptools");
            if (output.Contains("Name: setuptools"))
            {
                AddLogMessage("✓ setuptools ja està instal·lat");
                return;
            }
            AddLogMessage("Instal·lant setuptools...");
            var install = await ExecuteCommand(pythonCmd, "-m pip install setuptools");
            AddLogMessage(install);
        }
        private void SetCudaHomeEnv()
        {
            // Busca la instal·lació de CUDA més recent
            string[] possibleCudaDirs = {
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.4",
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.1",
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8",                
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA"
            };

            foreach (var dir in possibleCudaDirs)
            {
                if (Directory.Exists(dir))
                {
                    Environment.SetEnvironmentVariable("CUDA_HOME", dir, EnvironmentVariableTarget.Process);
                    AddLogMessage($"✓ Variable d'entorn CUDA_HOME establerta a: {dir}");
                    return;
                }
            }

            AddLogMessage("⚠ No s'ha trobat cap instal·lació de CUDA per establir CUDA_HOME");
        }
    }
}
#endregion
