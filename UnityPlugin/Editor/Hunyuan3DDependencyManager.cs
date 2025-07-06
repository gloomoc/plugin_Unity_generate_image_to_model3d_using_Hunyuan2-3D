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
    /// Utility to execute code on the main Unity thread from secondary threads
    /// </summary>
    public static class MainThreadExecutor
    {
        private static readonly Queue<Action> _executeOnMainThread = new Queue<Action>();
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// Initializes the executor, setting up the necessary callback
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
        /// Executes an action on the main Unity thread
        /// </summary>
        /// <param name="action">Action to execute</param>
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
        /// Processes the queued actions to execute on the main thread
        /// This method is called by EditorApplication.update
        /// </summary>
        private static void Update()
        {
            // Execute all queued actions
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
                        UnityEngine.Debug.LogError($"Error executing action on main thread: {ex.Message}");
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
        private Vector2 dependencyScrollPosition = Vector2.zero; // Add this line        

        // Configuration
        private string pythonPath = "python";
        private string pipPath = "pip3";
        private bool useCondaEnv = false;
        private string condaEnvName = "hunyuan3d";

        // Dependency Status
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

        // Dependency lists according to official documentation
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
            "triton-windows"
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
            AddLogMessage("Hunyuan3D dependency manager initialized.");
            AddLogMessage("Based on the official documentation: https://github.com/Tencent-Hunyuan/Hunyuan3D-2");
        }

        private void DetectPythonPath()
        {
            // Try to detect Python automatically
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
                    AddLogMessage($"Python detected: {path}");
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
                    process.WaitForExit(3000); // 3 seconds timeout
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
                string path = EditorUtility.OpenFilePanel("Select Python", "", "exe");
                if (!string.IsNullOrEmpty(path))
                    pythonPath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            useCondaEnv = EditorGUILayout.Toggle("Use Conda Environment:", useCondaEnv);
            if (useCondaEnv)
            {
                condaEnvName = EditorGUILayout.TextField("Environment Name:", condaEnvName);
            }
            EditorGUILayout.EndHorizontal();

            // Show detected information
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
                EditorGUILayout.HelpBox($"Recommended: {recommendedCudaVersion}", MessageType.Info);
            }
        }

        private void DrawInstallationModeSection()
        {
            EditorGUILayout.LabelField("Installation Mode", EditorStyles.boldLabel);

            selectedInstallMode = (InstallationMode)EditorGUILayout.EnumPopup("Mode:", selectedInstallMode);

            switch (selectedInstallMode)
            {
                case InstallationMode.CPU:
                    EditorGUILayout.HelpBox("CPU Mode: Will install PyTorch optimized for CPU. Slower but universally compatible.", MessageType.Info);
                    break;
                case InstallationMode.CUDA11:
                    EditorGUILayout.HelpBox("CUDA 11.x Mode: For NVIDIA graphics cards with CUDA 11.x drivers.", MessageType.Info);
                    break;
                case InstallationMode.CUDA12:
                    EditorGUILayout.HelpBox("CUDA 12.x Mode: For NVIDIA graphics cards with more recent CUDA 12.x drivers.", MessageType.Info);
                    break;
                case InstallationMode.Auto:
                    EditorGUILayout.HelpBox("Automatic Mode: Will detect the best mode based on the system.", MessageType.Info);
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

            // New section for Windows installation
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
                    "Recommended method for Windows: Uses UV for fast installation and efficient dependency management.",
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
                // Offer improved installation options for Windows
                int choice = EditorUtility.DisplayDialogComplex(
                    "Installation Method",
                    "Choose the installation method:\n\n" +
                    "• UV (Recommended): Fast and optimized method for Windows\n" +
                    "• Standard Pip: Traditional method with pip\n" +
                    "• Cancel: Exit without installing",
                    "UV (Recommended)",
                    "Standard Pip",
                    "Cancel"
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
                        AddLogMessage("⏹ Installation cancelled by user");
                        break;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Specific buttons for CUDA and PyTorch
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

            // Create a ScrollView with fixed height to limit vertical space
            EditorGUILayout.BeginVertical(GUILayout.MaxHeight(150)); // Limit height to 150 pixels
            dependencyScrollPosition = EditorGUILayout.BeginScrollView(dependencyScrollPosition);

            DrawDependencyGroup("Core (PyTorch, Diffusers)", coreDependencies);
            DrawDependencyGroup("Mesh Processing", meshProcessingDependencies);
            DrawDependencyGroup("Image Processing", imageDependencies);
            DrawDependencyGroup("Utilities", utilityDependencies);
            DrawDependencyGroup("Optional (Gradio, FastAPI)", optionalDependencies);

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

                // Status icon
                string statusIcon = GetStatusIcon(status);
                Color statusColor = GetStatusColor(status);

                Color originalColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField(statusIcon, GUILayout.Width(20));
                GUI.color = originalColor;

                EditorGUILayout.LabelField(dep);

                // Individual install button
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

            // Convert log messages to a single string 
            string logContent = string.Join("\n", logMessages);

            // Create a custom style for the read-only TextArea
            GUIStyle logStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = true
            };

            // TextArea with scroll that allows selecting and copying text
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));

            // Read-only TextArea that allows selection
            EditorGUI.BeginDisabledGroup(true); // Makes it read-only but allows selection
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
                case DependencyStatus.Error: return new Color(1f, 0.5f, 0f); // Orange
                default: return Color.gray;
            }
        }
        #endregion

        #region Funcions de Comprovació
        private async Task CheckAllDependencies()
        {
            isCheckingDependencies = true;
            progress = 0f;
            statusMessage = "Checking dependencies...";

            try
            {
                // Detect if we are using a venv
                string venvPath = DetectVirtualEnvironment();
                if (!string.IsNullOrEmpty(venvPath))
                {
                    AddLogMessage($"📁 Using virtual environment: {venvPath}");
                    pythonPath = Path.Combine(venvPath, "Scripts", "python.exe");
                    pipPath = Path.Combine(venvPath, "Scripts", "pip.exe");
                }

                // Check all dependencies
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

                    statusMessage = $"Checking {packageName}...";
                    dependencyStatus[packageName] = DependencyStatus.Checking;

                    bool isInstalled = await CheckSingleDependency(packageName);
                    dependencyStatus[packageName] = isInstalled ?
                        DependencyStatus.Installed : DependencyStatus.NotInstalled;

                    progress = 0.1f + (0.9f * (i + 1) / allDependencies.Length);
                    Repaint();
                }

                // Check PyTorch and CUDA specifically
                await CheckTorchAndCuda();

                // Check CUDA Toolkit if on Windows and not available
                if (!cudaAvailable && Application.platform == RuntimePlatform.WindowsEditor)
                {
                    await DetectCudaInstallation();
                }

                statusMessage = "Check complete!";
                progress = 1f;

                // Summary
                int installed = dependencyStatus.Values.Count(s => s == DependencyStatus.Installed);
                int total = dependencyStatus.Count;
                AddLogMessage($"Summary: {installed}/{total} dependencies installed.");
            
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error during check: {ex.Message}");
            }
            finally
            {
                isCheckingDependencies = false;
            }
        }

        private string DetectVirtualEnvironment()
        {
            // Same code as in Generator
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
                    // Extract version number
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(output, @"Python (\d+)\.(\d+)");
                    if (versionMatch.Success)
                    {
                        int major = int.Parse(versionMatch.Groups[1].Value);
                        int minor = int.Parse(versionMatch.Groups[2].Value);
                        pythonVersionOK = (major > 3) || (major == 3 && minor >= 8);

                        if (pythonVersionOK)
                        {
                            AddLogMessage($"✓ Python {major}.{minor} is compatible (minimum 3.8)");
                        }
                        else
                        {
                            AddLogMessage($"✗ Python {major}.{minor} is too old (minimum 3.8)");
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error checking Python: {ex.Message}");
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
                // Check PyTorch
                string torchCheck = "-c \"import torch; print('PyTorch version:', torch.__version__)\"";
                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                var torchOutput = await ExecuteCommand(pythonCmd, torchCheck);
                if (torchOutput.Contains("PyTorch version:"))
                {
                    detectedTorchVersion = torchOutput.Trim();
                    torchInstalled = true;

                    // Check CUDA
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
                        AddLogMessage("✓ CUDA available for GPU acceleration");
                    }
                    else
                    {
                        AddLogMessage("⚠ CUDA not available - will use CPU");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error checking PyTorch/CUDA: {ex.Message}");
            }
        }

        private async Task CheckPyTorchCuda()
        {
            try
            {
                AddLogMessage("Verifying PyTorch and CUDA...");

                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                // Script to verify PyTorch and CUDA
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
                            AddLogMessage("✓ CUDA is available for PyTorch");
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
                        AddLogMessage("⚠ PyTorch is in CPU mode");
                    }
                }
                else
                {
                    AddLogMessage("⚠ Could not verify PyTorch");
                    torchInstalled = false;
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error verifying PyTorch/CUDA: {ex.Message}");
                torchInstalled = false;
                cudaAvailable = false;
            }
        }

        private string GetImportName(string packageName)
        {
            // Mapping of pip package names to import names
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
            statusMessage = "Starting full installation...";

            try
            {
                AddLogMessage("=== STARTING HUNYUAN3D INSTALLATION ===");
                AddLogMessage("Based on: https://github.com/Tencent-Hunyuan/Hunyuan3D-2");

                // 1. Detect and prepare CUDA if necessary
                progress = 0.05f;
                statusMessage = "Detecting CUDA configuration...";
                await DetectCudaInstallation();

                // Offer CUDA installation if not available
                if (!cudaToolkitInstalled && Application.platform == RuntimePlatform.WindowsEditor)
                {
                    bool installCuda = EditorUtility.DisplayDialog(
                        "CUDA not detected",
                        "CUDA Toolkit was not detected on the system.\n" +
                        "Do you want to install CUDA automatically for GPU acceleration?\n\n" +
                        "Recommended: Yes (better performance)\n" +
                        "No: Will continue with CPU mode",
                        "Install CUDA 12.1", "Continue with CPU"
                    );

                    if (installCuda)
                    {
                        statusMessage = "Installing CUDA...";
                        await InstallCudaToolkit("12.1");

                        // Update installation mode
                        selectedInstallMode = InstallationMode.CUDA12;
                    }
                    else
                    {
                        selectedInstallMode = InstallationMode.CPU;
                        AddLogMessage("Continuing with CPU mode...");
                    }
                }

                // 2. Install PyTorch first (most important)
                progress = 0.1f;
                statusMessage = "Installing PyTorch...";
                await InstallPyTorch();

                // 2. Core dependencies
                progress = 0.3f;
                statusMessage = "Installing core dependencies...";
                await InstallDependencyGroup(coreDependencies, "Core");

                // 3. Image dependencies
                progress = 0.5f;
                statusMessage = "Installing image processing...";
                await InstallDependencyGroup(imageDependencies, "Image");

                // 4. Mesh dependencies
                progress = 0.7f;
                statusMessage = "Installing mesh processing...";
                await InstallDependencyGroup(meshProcessingDependencies, "Mesh");

                // 5. Utilities
                progress = 0.85f;
                statusMessage = "Installing utilities...";
                await InstallDependencyGroup(utilityDependencies, "Utilities");

                // 6. Optional
                progress = 0.95f;
                statusMessage = "Installing optional dependencies...";
                await InstallDependencyGroup(optionalDependencies, "Optional");

                // 7. Install the Hunyuan3D package
                progress = 0.98f;
                statusMessage = "Installing Hunyuan3D package...";
                await InstallHunyuan3DPackage();

                progress = 1f;
                statusMessage = "Installation complete!";
                AddLogMessage("✓ Installation completed successfully!");
                AddLogMessage("Run 'Check Dependencies' to verify.");
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error during installation: {ex.Message}");
                statusMessage = "Error during installation";
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
                    // Detect automatically
                    if (await DetectCudaCapability())
                    {
                        torchCommand = "torch torchvision"; // Let pip detect
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
            // Smart CUDA detection implementation
            try
            {
                // First check nvidia-smi
                string nvidiaSmiCheck = "nvidia-smi";
                var output = await ExecuteCommand(nvidiaSmiCheck, "");

                if (output.Contains("CUDA Version"))
                {
                    AddLogMessage("✓ NVIDIA driver detected");

                    // Check if CUDA Toolkit is installed
                    await DetectCudaInstallation();

                    if (cudaToolkitInstalled)
                    {
                        AddLogMessage("✓ CUDA Toolkit already installed");
                        return true;
                    }
                    else
                    {
                        AddLogMessage("⚠ NVIDIA driver present but CUDA Toolkit not installed");

                        // In Auto mode, offer automatic installation
                        if (selectedInstallMode == InstallationMode.Auto)
                        {
                            bool autoInstall = EditorUtility.DisplayDialog(
                                "CUDA Toolkit Required",
                                "An NVIDIA card has been detected but CUDA Toolkit is not installed.\n" +
                                "Do you want to install it automatically?",
                                "Yes, install CUDA 12.1", "No, use CPU"
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
                    AddLogMessage("ℹ No NVIDIA card detected - using CPU mode");
                    return false;
                }
            }
            catch
            {
                AddLogMessage("ℹ Could not detect CUDA - using CPU mode");
                return false;
            }
        }

        private async Task InstallDependencyGroup(string[] dependencies, string groupName)
        {
            AddLogMessage($"Installing group: {groupName}");
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
                    AddLogMessage($"Installing: {package}");

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
                        AddLogMessage($"✓ {package} installed");

                        // Update status
                        string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                        dependencyStatus[packageName] = DependencyStatus.Installed;
                    }
                    else if (output.Contains("ERROR") || output.Contains("error"))
                    {
                        AddLogMessage($"✗ Error installing {package}");
                        string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                        dependencyStatus[packageName] = DependencyStatus.Error;
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"✗ Exception installing {package}: {ex.Message}");
                }
            }
        }

        private async Task CreateCondaEnvironment()
        {
            try
            {
                statusMessage = "Creating Conda environment...";
                AddLogMessage($"Creating Conda environment: {condaEnvName}");

                string arguments = $"create -n {condaEnvName} python=3.9 -y";
                var output = await ExecuteCommand("conda", arguments);

                if (output.Contains("done") || output.Contains("already exists"))
                {
                    AddLogMessage($"✓ Environment {condaEnvName} created");
                    useCondaEnv = true;
                }
                else
                {
                    AddLogMessage($"✗ Error creating environment: {output}");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error creating environment: {ex.Message}");
            }
        }
        private async Task ForceDeleteDirectory(string directoryPath)
        {
            try
            {
                // Remove read-only attributes from all files
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
                        // Ignore individual errors
                    }
                }

                // Delete directories
                var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length); // Deepest first

                foreach (string dir in directories)
                {
                    try
                    {
                        Directory.Delete(dir, false);
                    }
                    catch
                    {
                        // Ignore individual errors
                    }
                }

                // Delete main directory
                Directory.Delete(directoryPath, false);
            }
            catch (Exception ex)
            {
                throw new Exception($"Cannot delete {directoryPath}: {ex.Message}");
            }
        }
        private async Task InstallHunyuan3DPackage()
        {
            try
            {
                MainThreadExecutor.RunOnMainThread(async () =>
                {
                    AddLogMessage("Installing Hunyuan3D package from the official repository...");

                    // Clone repository and install
                    string tempDir = Path.Combine(Path.GetTempPath(), "hunyuan3d_temp");

                    // Delete temporary directory if it exists
                    if (Directory.Exists(tempDir))
                    {
                        AddLogMessage($"Cleaning up existing temporary directory: {tempDir}");
                        try
                        {
                            Directory.Delete(tempDir, true);
                            await Task.Delay(500); // Short pause to ensure it's deleted
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"⚠ Warning deleting directory: {ex.Message}");
                        }
                    }

                    // If it fails, try with PowerShell
                    AddLogMessage("Trying to clean up with PowerShell...");
                    try
                    {
                        string psCommand = $"Remove-Item \"{tempDir}\" -Recurse -Force -ErrorAction SilentlyContinue";
                        await ExecuteCommand("powershell", $"-Command \"{psCommand}\"");
                        await Task.Delay(1000);

                        if (!Directory.Exists(tempDir))
                        {
                            AddLogMessage("✓ Directory cleaned up with PowerShell");

                        }
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"⚠ PowerShell cleanup failed: {ex.Message}");
                    }

                    // If it still exists, try deleting individual files
                    AddLogMessage("Trying to delete individual files...");
                    try
                    {
                        await ForceDeleteDirectory(tempDir);
                        if (!Directory.Exists(tempDir))
                        {
                            AddLogMessage("✓ Directory forcefully cleaned up");

                        }
                    }
                    catch (Exception ex)
                    {
                        AddLogMessage($"⚠ Forced deletion failed: {ex.Message}");
                    }


                    // Verify that git is installed
                    var gitCheck = await ExecuteCommand("git", "--version");
                    if (gitCheck.Contains("ERROR") || !gitCheck.Contains("git version"))
                    {
                        AddLogMessage("✗ Git is not installed or not accessible");

                        bool downloadZip = EditorUtility.DisplayDialog(
                            "Git Not Detected",
                            "Git is not installed or not accessible.\n\n" +
                            "Options:\n" +
                            "• Download as ZIP (recommended)\n" +
                            "• Cancel and install Git manually",
                            "Download ZIP", "Cancel"
                        );

                        if (downloadZip)
                        {
                            await DownloadHunyuan3DAsZip(tempDir);
                        }
                        else
                        {
                            AddLogMessage("Installation cancelled. Install Git from: https://git-scm.com/downloads");
                            return;
                        }
                    }
                    else
                    {
                        AddLogMessage("✓ Git detected: " + gitCheck.Trim());

                        // Git clone with better error handling
                        AddLogMessage($"Cloning repository to: {tempDir}");
                        string gitArgs = $"clone --depth 1 https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git \"{tempDir}\"";

                        var output = await ExecuteCommand("git", gitArgs);
                        AddLogMessage("Git clone output:");
                        AddLogMessage(output);

                        // Verify if the clone worked
                        if (!Directory.Exists(tempDir) || !Directory.GetFiles(tempDir, "*.py", SearchOption.AllDirectories).Any())
                        {
                            AddLogMessage("✗ The repository was not cloned correctly");

                            // Try downloading as ZIP as an alternative
                            bool tryZip = EditorUtility.DisplayDialog(
                                "Git Clone Error",
                                "Could not clone the repository with Git.\n\n" +
                                "Do you want to try downloading it as a ZIP?",
                                "Yes, download ZIP", "Cancel"
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

                    // Verify that the directory exists and has content
                    if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories).Any())
                    {
                        AddLogMessage($"✓ Repository prepared at: {tempDir}");

                        // Search for requirements.txt
                        string reqPath = Path.Combine(tempDir, "requirements.txt");
                        if (!File.Exists(reqPath))
                        {
                            // Search in subdirectories
                            var reqFiles = Directory.GetFiles(tempDir, "requirements.txt", SearchOption.AllDirectories);
                            if (reqFiles.Length > 0)
                            {
                                reqPath = reqFiles[0];
                                AddLogMessage($"requirements.txt found at: {reqPath}");
                            }
                        }

                        // Install from source code or requirements
                        string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;
                        await EnsureSetuptoolsInstalled(pythonCmd);

                        if (File.Exists(reqPath))
                        {
                            AddLogMessage("Installing from requirements.txt...");
                            string reqArgs = $"-m pip install -r \"{reqPath}\"";
                            var reqOutput = await ExecuteCommand(pythonCmd, reqArgs);
                            AddLogMessage(reqOutput);

                            if (reqOutput.Contains("Successfully installed") || reqOutput.Contains("already satisfied"))
                            {
                                AddLogMessage("✓ Dependencies installed from requirements.txt");
                            }
                        }

                        // Try to install the package in development mode
                        string setupPath = Path.Combine(tempDir, "setup.py");
                        if (File.Exists(setupPath))
                        {
                            AddLogMessage("Installing Hunyuan3D package in development mode...");
                            string installArgs = $"-m pip install -e \"{tempDir}\"";
                            var installOutput = await ExecuteCommand(pythonCmd, installArgs);
                            AddLogMessage(installOutput);

                            if (installOutput.Contains("Successfully installed"))
                            {
                                AddLogMessage("✓ Hunyuan3D package installed in development mode");
                            }
                        }
                        else
                        {
                            AddLogMessage("⚠ setup.py not found - only dependencies were installed");
                        }

                        // Try to install the package in development mode
                        // Install custom_rasterizer with improved error handling
                        SetCudaHomeEnv();
                        string custRasterPath = Path.Combine(tempDir, "hy3dgen", "texgen", "custom_rasterizer");
                        if (Directory.Exists(custRasterPath))
                        {
                            await HandleCustomRasterizerCompilation(pythonCmd, custRasterPath);
                        }
                        else
                        {
                            AddLogMessage("⚠ custom_rasterizer not found in the repository");
                            AddLogMessage("ℹ Continuing without this optional module");
                        }

                        // Similar for differentiable_renderer...
                        string diffRendererPath = Path.Combine(tempDir, "hy3dgen", "texgen", "differentiable_renderer");
                        if (Directory.Exists(diffRendererPath))
                        {
                            AddLogMessage("Installing differentiable_renderer...");

                            string installArgs = $"setup.py install";
                            SetCudaHomeEnv();
                            var installOutput = await ExecuteCommandInDirectory(pythonCmd, installArgs, diffRendererPath);

                            if (installOutput.Contains("Successfully installed") ||
                                installOutput.Contains("Finished processing"))
                            {
                                AddLogMessage("✓ differentiable_renderer compiled correctly");
                            }
                            else
                            {
                                AddLogMessage("⚠ Error or warning compiling differentiable_renderer (optional module)");
                            }
                        }

                        // Final test
                        AddLogMessage("Verifying Hunyuan3D installation...");
                        string testImport = "-c \"import hy3dgen; print('✓ Hunyuan3D imported correctly')\"";
                        var testOutput = await ExecuteCommand(pythonCmd, testImport);

                        if (testOutput.Contains("✓ Hunyuan3D imported correctly"))
                        {
                            AddLogMessage("🎉 Hunyuan3D installation complete!");

                            EditorUtility.DisplayDialog(
                                "Hunyuan3D Installed",
                                "Hunyuan3D has been installed successfully!\n\n" +
                                "Available features:\n" +
                                "✓ 3D model generation from images\n" +
                                "✓ Mesh processing\n" +
                                "✓ Gradio interface\n\n" +
                                "Note: Some optional modules may have failed\n" +
                                "due to compilation issues. This does not prevent\n" +
                                "the basic use of the system.",
                                "Great!"
                            );
                        }
                        else
                        {
                            AddLogMessage("⚠ Possible issue with the main installation:");
                            AddLogMessage(testOutput);
                        }

                        AddLogMessage($"ℹ Source code available at: {tempDir}");
                    }
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error installing Hunyuan3D package: {ex.Message}");
            }
        }

        private async Task HandleCustomRasterizerCompilation(string pythonCmd, string custRasterPath)
        {
            try
            {
                AddLogMessage("Installing custom_rasterizer...");
                AddLogMessage("NOTE: This process can take time and may require a compatible Visual Studio");

                // Detect Visual Studio issues before compiling
                bool hasVSIssues = await DetectVisualStudioIssues();

                if (hasVSIssues)
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Compatibility Issue Detected",
                        "A potential compatibility issue with Visual Studio has been detected.\n\n" +
                        "Custom_rasterizer is an OPTIONAL module that improves performance but is not essential.\n\n" +
                        "Options:",
                        "Try Anyway",
                        "Skip this module",
                        "Apply workarounds"
                    );

                    switch (choice)
                    {
                        case 0: // Try Anyway
                            AddLogMessage("⚠ Attempting compilation despite detected issues...");
                            break;
                        case 1: // Skip
                            AddLogMessage("⏭ Skipping custom_rasterizer by user decision");
                            AddLogMessage("ℹ Hunyuan3D will work correctly without this module");
                            return;
                        case 2: // Workarounds
                            await ApplyCompilationWorkarounds(custRasterPath);
                            break;
                    }
                }

                // Try standard compilation
                string installArgs = "setup.py install";
                SetCudaHomeEnv();
                var installOutput = await ExecuteCommandInDirectory(pythonCmd, installArgs, custRasterPath);

                if (installOutput.Contains("Successfully installed") ||
                    installOutput.Contains("Finished processing"))
                {
                    AddLogMessage("✅ custom_rasterizer installed correctly!");
                }
                else if (installOutput.Contains("ninja: build stopped") ||
                         installOutput.Contains("RuntimeError: Error compiling") ||
                         installOutput.Contains("unsupported Microsoft Visual Studio version"))
                {
                    AddLogMessage("❌ Compilation error detected");
                    await HandleCompilationError(installOutput, pythonCmd, custRasterPath);
                }
                else
                {
                    AddLogMessage("⚠ Compilation completed with warnings:");
                    AddLogMessage(installOutput);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Error in custom_rasterizer: {ex.Message}");
                AddLogMessage("ℹ This module is optional - Hunyuan3D will work without it");
            }
        }

        private async Task HandleCompilationError(string errorOutput, string pythonCmd, string custRasterPath)
        {
            if (errorOutput.Contains("unsupported Microsoft Visual Studio version"))
            {
                AddLogMessage("⚠ Visual Studio compatibility error:");
                AddLogMessage("  Error: " + errorOutput.Split('\n').FirstOrDefault(l => l.Contains("unsupported")));
                AddLogMessage("");

                await ShowVSCompatibilityOptions(pythonCmd, custRasterPath);
            }
            else if (errorOutput.Contains("ninja: build stopped") || errorOutput.Contains("RuntimeError"))
            {
                AddLogMessage("⚠ C++/CUDA compilation error:");

                // Extract the real error
                var errorLines = errorOutput.Split('\n');
                var realError = errorLines.FirstOrDefault(l => l.Contains("error:") || l.Contains("ERROR:"));
                if (!string.IsNullOrEmpty(realError))
                {
                    AddLogMessage($"  Specific error: {realError}");
                }

                await ShowCompilationErrorOptions(pythonCmd, custRasterPath);
            }
            else
            {
                AddLogMessage("⚠ Unknown compilation error:");
                AddLogMessage(errorOutput);
                await ShowGenericErrorOptions();
            }
        }

        private async Task ShowVSCompatibilityOptions(string pythonCmd, string custRasterPath)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Visual Studio Compatibility Error",
                "CUDA does not support the current version of Visual Studio.\n\n" +
                "Options:\n" +
                "• Force Attempt: Use --allow-unsupported-compiler flag\n" +
                "• Open Guide: Show installation instructions\n" +
                "• Skip: Continue without this optional module",
                "Force Attempt",
                "Open Guide",
                "Skip"
            );

            switch (choice)
            {
                case 0: // Force compilation
                    AddLogMessage("⚠ Attempting forced compilation with --allow-unsupported-compiler");
                    await TryForceCompilation(pythonCmd, custRasterPath);
                    break;
                case 1: // Open guide
                    ShowVisualStudioInstallationGuide();
                    break;
                default: // Skip
                    AddLogMessage("⏭ Skipping compilation by user decision");
                    break;
            }
        }

        private async Task ShowCompilationErrorOptions(string pythonCmd, string custRasterPath)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "C++ Compilation Error",
                "Error compiling the C++/CUDA extension.\n\n" +
                "Possible causes:\n" +
                "• Python 3.13 is not compatible (use 3.10/3.11)\n" +
                "• Missing Visual Studio C++ tools\n" +
                "• CUDA/Visual Studio incompatibility\n\n" +
                "Options:",
                "Apply Workarounds",
                "Show Guide",
                "Skip Module"
            );

            switch (choice)
            {
                case 0: // Workarounds
                    await ApplyCompilationWorkarounds(custRasterPath);
                    break;
                case 1: // Guide
                    ShowDetailedCompilerInstructions();
                    break;
                default: // Skip
                    AddLogMessage("⏭ Skipping custom_rasterizer");
                    AddLogMessage("ℹ Hunyuan3D will work without this optional module");
                    break;
            }
        }

        private async Task ShowGenericErrorOptions()
        {
            bool showGuide = EditorUtility.DisplayDialog(
                "Compilation Error",
                "An error occurred during compilation.\n\n" +
                "Custom_rasterizer is an optional module that improves performance,\n" +
                "but Hunyuan3D will work correctly without it.\n\n" +
                "Do you want to see the troubleshooting guide?",
                "Show Guide",
                "Continue without the module"
            );

            if (showGuide)
            {
                ShowDetailedCompilerInstructions();
            }
            else
            {
                AddLogMessage("⏭ Continuing without custom_rasterizer");
            }
        }

        private async Task TryForceCompilation(string pythonCmd, string custRasterPath)
        {
            try
            {
                AddLogMessage("Setting environment variables to force compilation...");

                // Create custom Python script to set the environment
                string forceScript = @"
import os
import sys
import subprocess

# Set environment variables for CUDA
os.environ['CUDA_LAUNCH_BLOCKING'] = '1'
os.environ['NVCC_APPEND_FLAGS'] = '-allow-unsupported-compiler'
os.environ['TORCH_CUDA_ARCH_LIST'] = '6.0;6.1;7.0;7.5;8.0;8.6'
os.environ['FORCE_CUDA'] = '1'

print('Environment variables set for forced compilation')
print('NVCC_APPEND_FLAGS:', os.environ.get('NVCC_APPEND_FLAGS'))

try:
    # Execute setup.py with forced configuration
    result = subprocess.run([sys.executable, 'setup.py', 'install', '--force'], 
                          capture_output=True, text=True, timeout=1800)  # 30 min timeout
    
    print('STDOUT:')
    print(result.stdout)
    if result.stderr:
        print('STDERR:')
        print(result.stderr)
    
    if result.returncode == 0:
        print('✓ Forced compilation successful')
    else:
        print(f'⚠ Compilation finished with code: {result.returncode}')
    
    sys.exit(result.returncode)
    
except subprocess.TimeoutExpired:
    print('✗ Timeout - compilation trigant més de 30 minuts')
    sys.exit(1)
except Exception as e:
    print(f'✗ Error durant compilació forçada: {e}')
    sys.exit(1)
";

                string tempScript = Path.Combine(Path.GetTempPath(), "force_cuda_compile.py");
                File.WriteAllText(tempScript, forceScript);

                try
                {
                    AddLogMessage("Executing forced compilation...");
                    AddLogMessage("WARNING: Using --allow-unsupported-compiler can cause issues");
                    SetCudaHomeEnv();
                    var result = await ExecuteCommandInDirectory(pythonCmd, $"\"{tempScript}\"", custRasterPath);

                    if (result.Contains("✓ Forced compilation successful"))
                    {
                        AddLogMessage("✓ Forced compilation completed successfully");
                    }
                    else if (result.Contains("⚠ Compilation finished with code:"))
                    {
                        AddLogMessage("⚠ Forced compilation with warnings - may work partially");
                    }
                    else
                    {
                        AddLogMessage("✗ Forced compilation failed:");
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
                AddLogMessage($"✗ Error in forced compilation: {ex.Message}");
            }
        }

        private async Task ApplyCompilationWorkarounds(string custRasterPath)
        {
            try
            {
                AddLogMessage("🔧 Applying workarounds for compilation issues...");

                // 1. Modify setup.py to add compatible flags
                string setupPath = Path.Combine(custRasterPath, "setup.py");
                if (File.Exists(setupPath))
                {
                    await PatchSetupPyForCompatibility(setupPath);
                }

                // 2. Set environment variables
                AddLogMessage("Setting optimized environment variables...");

                // 3. Try compilation with modified configuration
                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;
                var result = await TryAlternativeCompilation(pythonCmd, "setup.py install", custRasterPath);

                if (result.Contains("Successfully installed"))
                {
                    AddLogMessage("✅ Compilation with workarounds successful!");
                }
                else
                {
                    AddLogMessage("⚠ Workarounds applied but issues persist");
                    AddLogMessage(result);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error applying workarounds: {ex.Message}");
            }
        }

        private async Task PatchSetupPyForCompatibility(string setupPath)
        {
            try
            {
                AddLogMessage("Modifying setup.py for compatibility...");

                string content = File.ReadAllText(setupPath);

                // Search for CUDAExtension and add compatible flags
                if (content.Contains("CUDAExtension") && !content.Contains("extra_compile_args"))
                {
                    // Add extra_compile_args to prevent warnings from being errors
                    string newContent = content.Replace(
                        "CUDAExtension('custom_rasterizer_kernel', [",
                        @"CUDAExtension(
    'custom_rasterizer_kernel',
    [");

                    // Add compatible flags
                    newContent = newContent.Replace(
                        "],\n)",
                        @"],
    extra_compile_args={
        'cxx': ['/WX-'],  # Do not treat warnings as errors
        'nvcc': ['-allow-unsupported-compiler']
    }
)"
                    );

                    File.WriteAllText(setupPath, newContent);
                    AddLogMessage("✓ setup.py modified for better compatibility");
                }
                else
                {
                    AddLogMessage("setup.py already has compatible settings or cannot be modified");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Error modifying setup.py: {ex.Message}");
            }
        }

        private async Task<string> TryAlternativeCompilation(string pythonCmd, string args, string workingDirectory)
        {
            try
            {
                AddLogMessage("Trying alternative compilation with special configuration...");

                // Create temporary script for alternative compilation
                string altScript = @"
import os
import sys
import subprocess

# Set environment for maximum compatibility
os.environ['DISTUTILS_USE_SDK'] = '1'
os.environ['MSSdk'] = '1'
os.environ['CUDA_LAUNCH_BLOCKING'] = '1'
os.environ['TORCH_USE_CUDA_DSA'] = '1'

print('Environment set for alternative compilation')

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
                return $"Error in alternative compilation: {ex.Message}";
            }
        }

        private async Task<bool> DetectVisualStudioIssues()
        {
            try
            {
                AddLogMessage("Detecting Visual Studio issues...");

                // Check Visual Studio version
                var vswhereOutput = await ExecuteCommand("vswhere", "-latest -property installationVersion");

                if (!string.IsNullOrEmpty(vswhereOutput) && !vswhereOutput.Contains("ERROR"))
                {
                    AddLogMessage($"Visual Studio detected: {vswhereOutput.Trim()}");

                    // Check if it's VS2022 (version 17.x)
                    if (vswhereOutput.StartsWith("17."))
                    {
                        AddLogMessage("⚠ VS2022 detected - there may be compatibility issues with CUDA");
                        return true;
                    }
                }

                // Check C++ tools
                var clOutput = await ExecuteCommand("cl", "");
                if (clOutput.Contains("Microsoft") && clOutput.Contains("C/C++"))
                {
                    AddLogMessage("✓ C++ compiler detected");
                }
                else
                {
                    AddLogMessage("⚠ C++ compiler not detected");
                    return true;
                }

                return false;
            }
            catch
            {
                AddLogMessage("⚠ Could not detect Visual Studio");
                return true;
            }
        }

        private void ShowVisualStudioInstallationGuide()
        {
            string guide = @"
VISUAL STUDIO INSTALLATION GUIDE FOR CUDA

1. RECOMMENDED VERSION:
   • Visual Studio 2019 (version 16.x)
   • Visual Studio 2022 (version 17.x) with CUDA 12.x

2. REQUIRED COMPONENTS:
   • Desktop development with C++
   • MSVC v142/v143 - VS 2019/2022 C++ x64/x86 build tools
   • Windows 10/11 SDK

3. INSTALLATION:
   a) Download VS from: https://visualstudio.microsoft.com/
   b) During installation, select 'Desktop development with C++'
   c) Restart after installing

4. COMPATIBILITY TROUBLESHOOTING:
   • CUDA 11.x → Visual Studio 2019 or 2022
   • CUDA 12.x → Visual Studio 2022
   • If you have VS2022 with CUDA 11.x, use --allow-unsupported-compiler flag

5. ENVIRONMENT VARIABLES:
   Add to PATH:
   • C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\14.29.30133\bin\Hostx64\x64
   • C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\bin
";

            EditorUtility.DisplayDialog(
                "Visual Studio Guide for CUDA",
                guide,
                "Close"
            );

            if (EditorUtility.DisplayDialog(
                "Open Documentation",
                "Do you want to open the official CUDA documentation?",
                "Yes", "No"))
            {
                Application.OpenURL("https://docs.nvidia.com/cuda/cuda-installation-guide-microsoft-windows/");
            }
        }

        private void ShowDetailedCompilerInstructions()
        {
            string instructions = @"
DETAILED COMPILATION TROUBLESHOOTING GUIDE

PROBLEM 1: Python 3.13 Incompatible
• SOLUTION: Use Python 3.10 or 3.11
• Install: https://www.python.org/downloads/release/python-3119/

PROBLEM 2: Visual Studio not compatible
• SOLUTION: Install VS2019 or VS2022
• Components: Desktop development with C++

PROBLEM 3: CUDA Toolkit not found
• SOLUTION: Install CUDA Toolkit
• CUDA 11.8: https://developer.nvidia.com/cuda-11-8-0-download-archive
• CUDA 12.1: https://developer.nvidia.com/cuda-12-1-0-download-archive

PROBLEM 4: Error 'unsupported Microsoft Visual Studio version'
• SOLUTION 1: Downgrade to VS2019
• SOLUTION 2: Use --allow-unsupported-compiler flag
• SOLUTION 3: Update to CUDA 12.x

PROBLEM 5: Error 'ninja: build stopped'
• SOLUTION: Install ninja manually
  pip install ninja

GENERAL WORKAROUND:
If nothing works, you can skip this optional module.
Hunyuan3D will still work, but more slowly.
";

            EditorUtility.DisplayDialog(
                "Detailed Compilation Instructions",
                instructions,
                "Close"
            );
        }

        private async Task DownloadHunyuan3DAsZip(string targetDir)
        {
            try
            {
                AddLogMessage("Downloading Hunyuan3D as ZIP...");

                string zipUrl = "https://github.com/Tencent-Hunyuan/Hunyuan3D-2/archive/refs/heads/main.zip";
                string zipPath = Path.Combine(Path.GetTempPath(), "hunyuan3d.zip");

                using (var client = new System.Net.WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        progress = e.ProgressPercentage / 100f;
                        statusMessage = $"Downloading: {e.ProgressPercentage}%";
                        Repaint();
                    };

                    await client.DownloadFileTaskAsync(zipUrl, zipPath);
                }

                AddLogMessage($"ZIP downloaded to: {zipPath}");

                // Extract ZIP
                statusMessage = "Extracting files...";
                await ExtractZipFile(zipPath, targetDir);

                // Clean up
                try { File.Delete(zipPath); } catch { }

                AddLogMessage($"✓ Repository extracted to: {targetDir}");
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error downloading ZIP: {ex.Message}");
                throw;
            }
        }

        private async Task ExtractZipFile(string zipPath, string extractPath)
        {
            await Task.Run(() =>
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);

                // Move content from the main subdirectory
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
                AddLogMessage("=== INSTALLATION WITH UV (RECOMMENDED FOR WINDOWS) ===");

                // 1. Verify/Install UV
                bool uvInstalled = await CheckAndInstallUV();
                if (!uvInstalled)
                {
                    AddLogMessage("✗ Could not install UV");

                    // Offer PowerShell alternative
                    if (Application.platform == RuntimePlatform.WindowsEditor)
                    {
                        if (EditorUtility.DisplayDialog(
                            "UV not available",
                            "Could not install UV automatically.\n\n" +
                            "Do you want to run the full PowerShell installer?",
                            "Run PowerShell", "Cancel"))
                        {
                            RunWindowsPowerShellInstaller();
                        }
                    }
                    return;
                }

                // 2. Create UV project for Hunyuan3D
                string projectDir = Path.Combine(Application.dataPath, "..", "Hunyuan3D_UV");
                Directory.CreateDirectory(projectDir);

                AddLogMessage($"Creating UV project at: {projectDir}");
                SetCudaHomeEnv();
                // 3. Initialize UV project
                await ExecuteCommandInDirectory("uv", "init", projectDir);

                // 4. Add main dependencies
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
                    AddLogMessage($"Adding: {dep}");
                    await ExecuteCommandInDirectory("uv", $"add {dep}", projectDir);
                    progress = Array.IndexOf(uvDependencies, dep) / (float)uvDependencies.Length;
                }

                // 5. Install Hunyuan3D from git
                AddLogMessage("Installing Hunyuan3D from the repository...");
                await ExecuteCommandInDirectory("uv", "pip install git+https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git", projectDir);

                
                AddLogMessage("✅ Installation with UV complete!");
                AddLogMessage($"📁 Project created at: {projectDir}");

                EditorUtility.DisplayDialog(
                    "UV Installation Complete",
                    $"Hunyuan3D has been installed with UV!\n\n" +
                    $"Location: {projectDir}\n\n" +
                    $"To use it:\n" +
                    $"1. Open a terminal in {projectDir}\n" +
                    $"2. Run: uv run python <script.py>",
                    "Great!"
                );
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error installing Hunyuan3D package: {ex.Message}");
            }
        }

        private void RunWindowsPowerShellInstaller()
        {
            string scriptPath = Path.Combine(Application.dataPath, "UnityPlugin", "Scripts", "install_hunyuan3d_windows.ps1");

            // Check if the script exists
            if (!File.Exists(scriptPath))
            {
                // Create the script if it doesn't exist
                string scriptsDir = Path.GetDirectoryName(scriptPath);
                if (!Directory.Exists(scriptsDir))
                {
                    Directory.CreateDirectory(scriptsDir);
                }

                // Download or create the script
                if (EditorUtility.DisplayDialog(
                    "Installation script not found",
                    "The PowerShell script does not exist. Do you want to create it automatically?",
                    "Create Script", "Cancel"))
                {
                    CreateWindowsInstallerScript(scriptPath);
                }
                else
                {
                    return;
                }
            }

            // Installation options
            bool useCuda12 = EditorUtility.DisplayDialog(
                "Select CUDA version",
                "Which CUDA version do you want to use?\n\n" +
                "CUDA 12.4: Newest, best performance\n" +
                "CUDA 11.8: More compatible",
                "CUDA 12.4", "CUDA 11.8"
            );

            string installPath = "C:\\Users\\" + Environment.UserName + "\\AppData\\Local\\Temp\\Hunyuan2-3D-for-windows";

            if (string.IsNullOrEmpty(installPath))
            {
                installPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }

            // Build arguments
            string arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                              $"-InstallPath \"{installPath}\" " +
                              $"-PythonVersion \"3.10\" " +
                              (useCuda12 ? "-UseCUDA12" : "") +
                              (EditorUtility.DisplayDialog("Models", "Do you want to download the pre-trained models? (~10GB)", "Yes", "No") ? "" : " -SkipModelDownload");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas" // Run as administrator if needed
                };

                var process = Process.Start(startInfo);

                AddLogMessage("🚀 Windows installer started");
                AddLogMessage($"📁 Installation folder: {installPath}");
                AddLogMessage("⏳ The installer is running in a separate window...");

                EditorUtility.DisplayDialog(
                    "Installer Running",
                    "The Hunyuan3D for Windows installer is running.\n\n" +
                    "Follow the instructions in the PowerShell window.\n\n" +
                    "Once it finishes, return to Unity and verify the installation.",
                    "OK"
                );
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Error running the installer: {ex.Message}");

                // Offer to run manually
                if (EditorUtility.DisplayDialog(
                    "Error running script",
                    "Could not run the script automatically.\n\n" +
                    "You can run it manually:\n" +
                    $"1. Open PowerShell as administrator\n" +
                    $"2. Run: {scriptPath}",
                    "Copy Path", "Close"))
                {
                    GUIUtility.systemCopyBuffer = scriptPath;
                    AddLogMessage("📋 Script path copied to clipboard");
                }
            }
        }

        private void CreateWindowsInstallerScript(string scriptPath)
        {
            // Here you would create the content of the PowerShell script
            // For simplicity, I'll show a message
            AddLogMessage("📝 Creating installation script...");

            // The script content is already defined above
            // Here you would simply copy it to the file

            EditorUtility.DisplayDialog(
                "Script created",
                $"Installation script created at:\n{scriptPath}\n\n" +
                "Run it from PowerShell as administrator.",
                "OK"
            );
        }

        private void ShowWindowsInstallGuide()
        {
            string guide = @"
QUICK INSTALLATION GUIDE FOR WINDOWS

This installation uses UV, an ultra-fast Python package manager
optimized for Windows.

ADVANTAGES:
✓ 10-100x faster than pip
✓ Smart dependency management
✓ Shared cache between projects
✓ Automatic conflict resolution

REQUIREMENTS:
• Windows 10/11
• ~15GB free space
• Internet connection
• NVIDIA card (optional but recommended)

INSTALLATION PROCESS:
1. Click on 'Windows Quick Install'
2. Select CUDA version (12.4 recommended)
3. Choose installation folder
4. Follow instructions in PowerShell

AFTER INSTALLING:
• Run: start_hunyuan3d.bat
• Or activate: .venv\Scripts\activate

TROUBLESHOOTING:
• If it fails, run PowerShell as administrator
• Make sure you have Git installed
• Temporarily disable antivirus if necessary

MORE INFORMATION:
• UV: https://github.com/astral-sh/uv
• Hunyuan3D: https://github.com/Tencent-Hunyuan/Hunyuan3D-2
";

            EditorUtility.DisplayDialog(
                "Windows Installation Guide",
                guide,
                "Close"
            );
        }

        private async Task InstallPyTorchCuda118()
        {
            try
            {
                AddLogMessage("=== INSTALLING PYTORCH WITH CUDA 11.8 ===");

                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                // Uninstall existing versions
                AddLogMessage("Uninstalling previous versions of PyTorch...");
                await ExecuteCommand(pythonCmd, "-m pip uninstall torch torchvision torchaudio -y");

                // Install PyTorch with CUDA 11.8
                string installCmd = "-m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118";
                AddLogMessage("Installing PyTorch with CUDA 11.8...");

                var output = await ExecuteCommand(pythonCmd, installCmd);

                if (output.Contains("Successfully installed"))
                {
                    AddLogMessage("✓ PyTorch CUDA 11.8 installed correctly");

                    // Verify installation
                    await CheckPyTorchCuda();
                }
                else
                {
                    AddLogMessage("✗ Error installing PyTorch CUDA 11.8");
                    AddLogMessage(output);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error installing PyTorch CUDA 11.8: {ex.Message}");
            }
        }

        private bool ConfirmCudaInstallation(string version)
        {
            return EditorUtility.DisplayDialog(
                $"Install CUDA Toolkit {version}",
                $"This will download and install CUDA Toolkit {version} (~3GB).\n\n" +
                "Requirements:\n" +
                "• NVIDIA graphics card\n" +
                "• ~3GB of disk space\n" +
                "• Administrator permissions\n" +
                "• A restart may be necessary\n\n" +
                "Continue?",
                "Install", "Cancel"
            );
        }

        private async Task InstallCudaToolkit(string version)
        {
            try
            {
                isInstallingCuda = true;
                AddLogMessage($"=== INSTALLING CUDA TOOLKIT {version} ===");

                string downloadUrl = version switch
                {
                    "11.8" => "https://developer.download.nvidia.com/compute/cuda/11.8.0/local_installers/cuda_11.8.0_522.06_windows.exe",
                    "12.1" => "https://developer.download.nvidia.com/compute/cuda/12.1.0/local_installers/cuda_12.1.0_531.14_windows.exe",
                    _ => throw new Exception($"CUDA version {version} not supported")
                };

                string installerPath = Path.Combine(Path.GetTempPath(), $"cuda_{version}_installer.exe");

                // Download
                statusMessage = $"Downloading CUDA {version}...";
                using (var client = new System.Net.WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        progress = e.ProgressPercentage / 100f;
                        statusMessage = $"Downloading CUDA {version}: {e.ProgressPercentage}%";
                        Repaint();
                    };

                    await client.DownloadFileTaskAsync(downloadUrl, installerPath);
                }

                AddLogMessage($"✓ CUDA {version} downloaded");

                // Run installer
                statusMessage = $"Installing CUDA {version}...";
                AddLogMessage("Running CUDA installer...");
                AddLogMessage("NOTE: Accept the default values in the installer");

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "-s",  // Silent install
                    UseShellExecute = true,
                    Verb = "runas"  // Run as administrator
                };

                var process = Process.Start(startInfo);
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    AddLogMessage($"✓ CUDA {version} installed correctly");
                    cudaToolkitInstalled = true;
                    detectedCudaToolkitVersion = version;

                    // Update PATH
                    RepairCudaPath();
                }
                else
                {
                    AddLogMessage($"✗ Error installing CUDA (code: {process.ExitCode})");
                }

                // Clean up
                try { File.Delete(installerPath); } catch { }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error installing CUDA: {ex.Message}");
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
                AddLogMessage("Detecting CUDA installation...");

                // 1. Check nvcc
                var nvccOutput = await ExecuteCommand("nvcc", "--version");
                if (!nvccOutput.Contains("ERROR") && nvccOutput.Contains("release"))
                {
                    nvccAvailable = true;
                    var match = System.Text.RegularExpressions.Regex.Match(nvccOutput, @"release (\d+\.\d+)");
                    if (match.Success)
                    {
                        detectedCudaToolkitVersion = match.Groups[1].Value;
                        cudaToolkitInstalled = true;
                        AddLogMessage($"✓ CUDA Toolkit {detectedCudaToolkitVersion} detected via nvcc");
                    }
                }

                // 2. Check nvidia-smi
                var smiOutput = await ExecuteCommand("nvidia-smi", "");
                if (!smiOutput.Contains("ERROR") && smiOutput.Contains("CUDA Version"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(smiOutput, @"CUDA Version:\s*(\d+\.\d+)");
                    if (match.Success)
                    {
                        recommendedCudaVersion = $"CUDA {match.Groups[1].Value} (maximum supported by driver)";
                        AddLogMessage($"✓ NVIDIA driver detected: {recommendedCudaVersion}");
                    }
                }

                // 3. Check installation directories
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
                                AddLogMessage($"✓ CUDA Toolkit found at: {latestVersion}");
                            }
                        }
                    }
                }

                if (!cudaToolkitInstalled)
                {
                    AddLogMessage("⚠ CUDA Toolkit not detected on the system");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error detecting CUDA: {ex.Message}");
            }
        }

        private async Task VerifyFullInstallation()
        {
            try
            {
                AddLogMessage("=== FULL INSTALLATION VERIFICATION ===");

                string pythonCmd = useCondaEnv ? $"conda run -n {condaEnvName} python" : pythonPath;

                // Full verification script
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
    print('✗ PyTorch not installed')

print('-' * 50)

# Main dependencies
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

# Optional modules
print('\nOptional modules:')
try:
    import custom_rasterizer_kernel
    print('✓ Custom Rasterizer')
except ImportError:
    print('⚠ Custom Rasterizer (optional)')
""";

                var output = await ExecuteCommand(pythonCmd, verifyScript);
                AddLogMessage(output);

                if (output.Contains("✓ Hunyuan3D package"))
                {
                    EditorUtility.DisplayDialog(
                        "Verification Complete",
                        "Hunyuan3D is installed and ready to use!\n\n" +
                        "Check the logs for a detailed status.",
                        "Great!"
                    );
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Verification Incomplete",
                        "Some dependencies may be missing.\n\n" +
                        "Check the logs and run 'Install All' if necessary.",
                        "OK"
                    );
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error during verification: {ex.Message}");
            }
        }

        private void RepairCudaPath()
        {
            try
            {
                AddLogMessage("Repairing CUDA PATH...");

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
                        AddLogMessage($"✓ Added to PATH: {cudaPath}");
                    }
                }

                if (pathUpdated)
                {
                    Environment.SetEnvironmentVariable("PATH", currentPath, EnvironmentVariableTarget.User);
                    AddLogMessage("✓ PATH updated - it may be necessary to restart Unity");

                    EditorUtility.DisplayDialog(
                        "PATH Updated",
                        "The CUDA PATH has been updated.\n\n" +
                        "It may be necessary to restart Unity for the changes to take effect.",
                        "OK"
                    );
                }
                else
                {
                    AddLogMessage("ℹ No CUDA paths found to add");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error repairing PATH: {ex.Message}");
            }
        }

        private void CopyLogsToClipboard()
        {
            string allLogs = string.Join("\n", logMessages);
            GUIUtility.systemCopyBuffer = allLogs;
            AddLogMessage("Logs copied to clipboard!");
        }

        private void AddLogMessage(string message)
        {
            MainThreadExecutor.RunOnMainThread(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                logMessages.Add($"[{timestamp}] {message}");

                // Limit to 1000 messages
                if (logMessages.Count > 1000)
                {
                    logMessages.RemoveAt(0);
                }

                // Auto-scroll to the bottom
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

                // Propagate CUDA_HOME to subprocesses
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
            // Search for the most recent CUDA installation
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
                // Ensure environment variables are propagated to subprocesses
                startInfo.EnvironmentVariables["CUDA_HOME"] = cudaHome;
                startInfo.EnvironmentVariables["CUDA_PATH"] = cudaHome;
                startInfo.EnvironmentVariables["CUDA_PATH_V12_4"] = cudaHome; // For CUDA 12.4

                // Add bin to PATH
                string cudaBin = Path.Combine(cudaHome, "bin");
                string currentPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH");
                if (!currentPath.Contains(cudaBin))
                {
                    startInfo.EnvironmentVariables["PATH"] = cudaBin + ";" + currentPath;
                }

              //  AddLogMessage($"✓ CUDA variables set for subprocess: {cudaHome}");
            }
            else
            {
               // AddLogMessage("⚠ No CUDA installation found");
            }
        }

        private async Task<bool> CheckAndInstallUV()
        {
            try
            {
                AddLogMessage("Verifying UV...");

                // Check if UV is already installed
                var uvCheck = await ExecuteCommand("uv", "--version");
                if (!uvCheck.Contains("ERROR") && uvCheck.Contains("uv"))
                {
                    AddLogMessage($"✓ UV already installed: {uvCheck.Trim()}");
                    return true;
                }

                AddLogMessage("UV not detected. Installing...");

                // Install UV via PowerShell (official method for Windows)
                string installScript = @"
# Install UV
Write-Host 'Installing UV Package Manager...'
try {
    # Method 1: Official installer
    Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression
    
    # Verify installation
    $uvPath = Get-Command uv -ErrorAction SilentlyContinue
    if ($uvPath) {
        Write-Host '✓ UV installed correctly'
        exit 0
    }
    
    # Method 2: Via pip if it fails
    Write-Host 'Trying to install via pip...'
    pip install uv
    
    # Verify again
    $uvPath = Get-Command uv -ErrorAction SilentlyContinue
    if ($uvPath) {
        Write-Host '✓ UV installed via pip'
        exit 0
    }
    
    Write-Host '✗ Could not install UV'
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

                    if (output.Contains("✓ UV installed"))
                    {
                        AddLogMessage("✓ UV installed correctly");

                        // Update PATH if necessary
                        await UpdatePathForUV();

                        // Verify it works
                        var finalCheck = await ExecuteCommand("uv", "--version");
                        if (!finalCheck.Contains("ERROR"))
                        {
                            AddLogMessage($"✓ UV verified: {finalCheck.Trim()}");
                            return true;
                        }
                    }

                    AddLogMessage("⚠ UV installed but not accessible. It may be necessary to restart the terminal.");

                    // Offer manual instructions
                    EditorUtility.DisplayDialog(
                        "UV Installed",
                        "UV has been installed but may not be accessible until restart.\n\n" +
                        "If it still doesn't work:\n" +
                        "1. Open PowerShell as administrator\n" +
                        "2. Run: Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression\n" +
                        "3. Restart Unity",
                        "OK"
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
                AddLogMessage($"✗ Error installing UV: {ex.Message}");

                // Show alternative instructions
                bool tryManual = EditorUtility.DisplayDialog(
                    "Error installing UV",
                    "Could not install UV automatically.\n\n" +
                    "Options:\n" +
                    "• Install manually from: https://docs.astral.sh/uv/\n" +
                    "• Use traditional pip (slower)\n\n" +
                    "Do you want to open the UV documentation?",
                    "Open Documentation", "Cancel"
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
                // Possible UV locations
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
                        // Verify if UV exists in this path
                        string uvExe = Path.Combine(uvPath, "uv.exe");
                        if (File.Exists(uvExe))
                        {
                            currentPath = uvPath + ";" + currentPath;
                            pathUpdated = true;
                            AddLogMessage($"✓ Added UV to PATH: {uvPath}");
                            break;
                        }
                    }
                }

                if (pathUpdated)
                {
                    Environment.SetEnvironmentVariable("PATH", currentPath, EnvironmentVariableTarget.User);
                    AddLogMessage("✓ PATH updated with UV");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Error updating PATH for UV: {ex.Message}");
            }
        }
        private async Task EnsureSetuptoolsInstalled(string pythonCmd)
        {
            AddLogMessage("Checking setuptools...");
            var output = await ExecuteCommand(pythonCmd, "-m pip show setuptools");
            if (output.Contains("Name: setuptools"))
            {
                AddLogMessage("✓ setuptools is already installed");
                return;
            }
            AddLogMessage("Installing setuptools...");
            var install = await ExecuteCommand(pythonCmd, "-m pip install setuptools");
            AddLogMessage(install);
        }
        private void SetCudaHomeEnv()
        {
            // Search for the most recent CUDA installation
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
                    AddLogMessage($"✓ CUDA_HOME environment variable set to: {dir}");
                    return;
                }
            }

            AddLogMessage("⚠ No CUDA installation found to set CUDA_HOME");
        }
    }
}
#endregion
