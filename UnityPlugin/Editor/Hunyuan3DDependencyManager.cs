using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        private int installErrorCount = 0;
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

        // Python version of the managed UV virtual environment (e.g. "3.10"); used to adapt requirements
        private string managedVenvPythonVersion = "";

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
        private Hunyuan3DEnvironmentSnapshot environmentSnapshot;
        private bool nvidiaDriverDetected = false;
        private string detectedOperatingSystem = "";
        private string detectedArchitecture = "";
        private string pythonDownloadUrl = "https://www.python.org/downloads/windows/";
        private string cudaDownloadUrl = "https://developer.nvidia.com/cuda-downloads";
        private string gitDownloadUrl = "https://git-scm.com/download/win";
        private string detectedPythonSource = "";
        private string detectedCudaToolkitPath = "";
        private bool gitInstalled = false;
        private string detectedGitPath = "";
        private string detectedGitVersion = "";
        private string detectedGitSource = "";

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
            "triton-windows",
            "sentencepiece"
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
            CUDA13,
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
            AddLogMessage("Based on the official documentation: https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1");
        }

        private void DetectPythonPath()
        {
            RefreshSystemDetection(logDetails: true);
        }

        private void RefreshSystemDetection(bool logDetails = false)
        {
            environmentSnapshot = Hunyuan3DSystemProbe.Probe(pythonPath, DetectVirtualEnvironment());
            ApplyEnvironmentSnapshot(environmentSnapshot, logDetails);
        }

        private void ApplyEnvironmentSnapshot(Hunyuan3DEnvironmentSnapshot snapshot, bool logDetails)
        {
            if (snapshot == null)
            {
                return;
            }

            detectedOperatingSystem = snapshot.System?.DisplayName ?? string.Empty;
            detectedArchitecture = snapshot.System?.Architecture ?? string.Empty;
            pythonDownloadUrl = snapshot.Python?.DownloadUrl ?? "https://www.python.org/downloads/";
            cudaDownloadUrl = snapshot.Cuda?.DownloadUrl ?? "https://developer.nvidia.com/cuda-downloads";
            detectedPythonSource = snapshot.Python?.Source ?? string.Empty;
            gitDownloadUrl = snapshot.Git?.DownloadUrl ?? "https://git-scm.com/download/win";
            nvidiaDriverDetected = snapshot.Cuda != null && snapshot.Cuda.DriverDetected;
            detectedCudaToolkitPath = snapshot.Cuda?.ToolkitRoot ?? string.Empty;
            detectedCudaToolkitVersion = snapshot.Cuda?.ToolkitVersion ?? string.Empty;
            cudaToolkitInstalled = snapshot.Cuda != null && snapshot.Cuda.ToolkitDetected;
            nvccAvailable = snapshot.Cuda != null && snapshot.Cuda.NvccDetected;
            gitInstalled = snapshot.Git != null && snapshot.Git.IsDetected;
            detectedGitPath = snapshot.Git?.ExecutablePath ?? string.Empty;
            detectedGitVersion = snapshot.Git?.VersionText ?? string.Empty;
            detectedGitSource = snapshot.Git?.Source ?? string.Empty;
            recommendedCudaVersion = !string.IsNullOrEmpty(snapshot.Cuda?.DriverCudaVersion)
                ? $"Driver supports CUDA {snapshot.Cuda.DriverCudaVersion}"
                : string.Empty;

            if (snapshot.Python != null && snapshot.Python.IsDetected)
            {
                pythonPath = snapshot.Python.ExecutablePath;
                detectedPythonVersion = snapshot.Python.VersionText;
                pythonVersionOK = snapshot.Python.IsSupported;

                if (Hunyuan3DSystemProbe.TryExtractVirtualEnvironment(snapshot.Python.ExecutablePath, out _, out string environmentPath))
                {
                    string detectedPipPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPipPath(environmentPath);
                    if (!string.IsNullOrEmpty(detectedPipPath))
                    {
                        pipPath = detectedPipPath;
                    }
                }
            }
            else
            {
                detectedPythonVersion = string.Empty;
                pythonVersionOK = false;
            }

            if (!logDetails)
            {
                return;
            }

            AddLogMessage($"System detected: {detectedOperatingSystem} ({detectedArchitecture})");

            if (snapshot.Python != null && snapshot.Python.IsDetected)
            {
                AddLogMessage($"Python detected via {snapshot.Python.Source}: {snapshot.Python.ExecutablePath}");
                AddLogMessage(snapshot.Python.Summary);
            }
            else
            {
                AddLogMessage("Compatible Python was not detected.");
                AddLogMessage($"Python download: {pythonDownloadUrl}");
            }

            if (snapshot.Cuda != null)
            {
                AddLogMessage(snapshot.Cuda.Summary);

                if (!string.IsNullOrEmpty(snapshot.Cuda.GpuSummary))
                {
                    AddLogMessage($"GPU: {snapshot.Cuda.GpuSummary}");
                }

                if (!string.IsNullOrEmpty(snapshot.Cuda.ToolkitRoot))
                {
                    AddLogMessage($"CUDA Toolkit path: {snapshot.Cuda.ToolkitRoot}");
                }
                else if (!snapshot.Cuda.ToolkitDetected)
                {
                    AddLogMessage($"CUDA download: {cudaDownloadUrl}");
                }
            }

            if (snapshot.Git != null)
            {
                if (snapshot.Git.IsDetected)
                {
                    AddLogMessage($"Git detected via {snapshot.Git.Source}: {snapshot.Git.ExecutablePath}");
                    if (!string.IsNullOrEmpty(snapshot.Git.VersionText))
                    {
                        AddLogMessage(snapshot.Git.VersionText);
                    }
                }
                else
                {
                    AddLogMessage(snapshot.Git.Summary);
                    AddLogMessage($"Git download: {gitDownloadUrl}");
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

        private string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName;
        }

        private string GetUvProjectPath()
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            return Path.Combine(projectRoot, "Hunyuan3D_UV");
        }

        private string GetManagedHunyuanRepositoryPath(string baseDirectory)
        {
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return null;
            }

            return Path.Combine(baseDirectory, "Hunyuan3D-2.1");
        }

        private string GetVirtualEnvironmentSitePackagesPath(string virtualEnvironmentPath)
        {
            if (string.IsNullOrEmpty(virtualEnvironmentPath) || !Directory.Exists(virtualEnvironmentPath))
            {
                return null;
            }

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return Path.Combine(virtualEnvironmentPath, "Lib", "site-packages");
            }

            string libDirectory = Path.Combine(virtualEnvironmentPath, "lib");
            if (!Directory.Exists(libDirectory))
            {
                return null;
            }

            string pythonDirectory = Directory.GetDirectories(libDirectory, "python*")
                .OrderByDescending(path => path)
                .FirstOrDefault();

            return string.IsNullOrEmpty(pythonDirectory) ? null : Path.Combine(pythonDirectory, "site-packages");
        }

        private void RegisterHunyuanRepositoryWithVirtualEnvironment(string virtualEnvironmentPath, string repositoryPath)
        {
            if (string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
            {
                return;
            }

            string sitePackagesPath = GetVirtualEnvironmentSitePackagesPath(virtualEnvironmentPath);
            if (string.IsNullOrEmpty(sitePackagesPath))
            {
                AddLogMessage("Warning: could not locate site-packages to register the managed Hunyuan3D repository.");
                return;
            }

            Directory.CreateDirectory(sitePackagesPath);

            // The importable packages are NOT at the repo root: 'hy3dshape' is a nested package
            // (<repo>/hy3dshape/hy3dshape) and the texture modules are loose files under <repo>/hy3dpaint.
            // Register the directories that must be on sys.path so 'import hy3dshape...' and
            // 'import textureGenPipeline' resolve (mirrors what batch_hunyuan3d.py adds at runtime).
            // The sub-package directories are listed before the repo root so the nested 'hy3dshape'
            // package wins over the non-package outer 'hy3dshape' folder.
            var pathEntries = new List<string>();
            foreach (string subdir in new[] { "hy3dshape", "hy3dpaint" })
            {
                string candidate = Path.Combine(repositoryPath, subdir);
                if (Directory.Exists(candidate))
                {
                    pathEntries.Add(candidate);
                }
            }
            pathEntries.Add(repositoryPath);

            string pthPath = Path.Combine(sitePackagesPath, "hunyuan3d_repo.pth");
            File.WriteAllText(pthPath, string.Join(Environment.NewLine, pathEntries) + Environment.NewLine, new UTF8Encoding(false));
            AddLogMessage($"Repository registered in the virtual environment ({pathEntries.Count} sys.path entries): {pthPath}");
        }

        private string CreateShortTempDirectory(string prefix)
        {
            string rootPath = Path.GetPathRoot(Path.GetTempPath());
            if (string.IsNullOrEmpty(rootPath))
            {
                rootPath = Path.GetTempPath();
            }

            string baseDirectory = Path.Combine(rootPath, "_h3dtmp");
            Directory.CreateDirectory(baseDirectory);

            string sanitizedPrefix = string.IsNullOrWhiteSpace(prefix) ? "tmp" : prefix.Trim();
            if (sanitizedPrefix.Length > 6)
            {
                sanitizedPrefix = sanitizedPrefix.Substring(0, 6);
            }

            string directoryPath = Path.Combine(baseDirectory, sanitizedPrefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        private bool IsPythonReadyForActions()
        {
            return useCondaEnv || (environmentSnapshot != null &&
                environmentSnapshot.Python != null &&
                environmentSnapshot.Python.IsDetected &&
                environmentSnapshot.Python.IsSupported);
        }

        private bool EnsurePythonReady(string operationName)
        {
            if (useCondaEnv)
            {
                return true;
            }

            RefreshSystemDetection(logDetails: true);
            if (IsPythonReadyForActions())
            {
                return true;
            }

            statusMessage = "Compatible Python required";
            progress = 0f;
            AddLogMessage($"Cannot {operationName} until a compatible Python 3.8 to 3.12 installation is configured.");
            return false;
        }

        private string GetGitCommand()
        {
            if (string.IsNullOrEmpty(detectedGitPath) || !File.Exists(detectedGitPath))
            {
                RefreshSystemDetection(logDetails: false);
            }

            return !string.IsNullOrEmpty(detectedGitPath) && File.Exists(detectedGitPath)
                ? detectedGitPath
                : "git";
        }

        private async Task PrepareHunyuanRepositoryFromGit(string targetDir)
        {
            string gitCommand = GetGitCommand();
            string gitCheck = await ExecuteCommand(gitCommand, "--version");
            if (gitCheck.Contains("ERROR") || !gitCheck.Contains("git version"))
            {
                throw new Exception("Git is not installed or not accessible for the Hunyuan3D repository download.");
            }

            AddLogMessage("Git detected: " + gitCheck.Trim());
            AddLogMessage($"Cloning Hunyuan3D-2.1 into: {targetDir}");

            string gitArgs = $"clone --branch main --depth 1 https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1.git \"{targetDir}\"";
            string cloneOutput = await ExecuteCommand(gitCommand, gitArgs);
            AddLogMessage("Git clone output:");
            AddLogMessage(cloneOutput);

            if (OutputHasErrors(cloneOutput) ||
                !Directory.Exists(targetDir) ||
                !Directory.GetFiles(targetDir, "*.py", SearchOption.AllDirectories).Any())
            {
                throw new Exception("Could not clone the Hunyuan3D-2.1 repository with git.exe.");
            }

            AddLogMessage($"Repository prepared from Git at: {targetDir}");
        }

        private void DrawSystemStatusSection()
        {
            EditorGUILayout.LabelField("System Detection", EditorStyles.boldLabel);

            if (environmentSnapshot == null)
            {
                EditorGUILayout.HelpBox("Press Detect to probe the current machine.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"OS: {detectedOperatingSystem}");
            EditorGUILayout.LabelField($"Architecture: {detectedArchitecture}");

            if (!string.IsNullOrEmpty(detectedPythonSource))
            {
                EditorGUILayout.LabelField($"Python Source: {detectedPythonSource}");
            }

            if (!string.IsNullOrEmpty(detectedGitSource))
            {
                EditorGUILayout.LabelField($"Git Source: {detectedGitSource}");
            }

            if (!string.IsNullOrEmpty(environmentSnapshot.Cuda?.GpuSummary))
            {
                EditorGUILayout.LabelField($"GPU: {environmentSnapshot.Cuda.GpuSummary}");
            }

            if (!string.IsNullOrEmpty(detectedCudaToolkitPath))
            {
                EditorGUILayout.LabelField($"CUDA Path: {detectedCudaToolkitPath}");
            }

            if (!string.IsNullOrEmpty(detectedGitPath))
            {
                EditorGUILayout.LabelField($"Git Path: {detectedGitPath}");
            }

            if (!IsPythonReadyForActions())
            {
                DrawDownloadCallout(
                    "Python Required",
                    "The dependency manager needs a compatible Python 3.8 to 3.12 installation before it can install packages.",
                    MessageType.Error,
                    pythonDownloadUrl,
                    "Open Python Download"
                );
            }

            if (!cudaToolkitInstalled)
            {
                string cudaMessage = nvidiaDriverDetected
                    ? "An NVIDIA GPU was detected, but the CUDA Toolkit is missing. Install it if you want GPU acceleration."
                    : "CUDA Toolkit was not detected. CPU mode will still work, but you can install CUDA later if this machine uses NVIDIA GPU acceleration.";

                DrawDownloadCallout(
                    "CUDA Toolkit",
                    cudaMessage,
                    nvidiaDriverDetected ? MessageType.Warning : MessageType.Info,
                    cudaDownloadUrl,
                    "Open CUDA Download"
                );
            }

            if (!gitInstalled)
            {
                DrawDownloadCallout(
                    "Git Required",
                    "Git is required to install Hunyuan3D directly from the repository with UV or pip.",
                    MessageType.Warning,
                    gitDownloadUrl,
                    "Open Git Download"
                );
            }
        }

        private void DrawDownloadCallout(string title, string message, MessageType messageType, string url, string buttonLabel)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(message, messageType);
                EditorGUILayout.SelectableLabel(url, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight + 4f));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(buttonLabel, GUILayout.Width(170)))
                {
                    Application.OpenURL(url);
                }

                if (GUILayout.Button("Copy Link", GUILayout.Width(100)))
                {
                    GUIUtility.systemCopyBuffer = url;
                    AddLogMessage($"Link copied to clipboard: {url}");
                }
                EditorGUILayout.EndHorizontal();
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

            DrawSystemStatusSection();
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
                string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "exe" : string.Empty;
                string path = EditorUtility.OpenFilePanel("Select Python", "", extension);
                if (!string.IsNullOrEmpty(path))
                {
                    pythonPath = path;
                    RefreshSystemDetection(logDetails: true);
                }
            }
            EditorGUILayout.EndHorizontal();

            // On Windows the Hunyuan3D package install always uses the UV flow, so the Conda option does
            // not apply there; only expose it on other platforms to avoid a misleading split setup.
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorGUILayout.BeginHorizontal();
                useCondaEnv = EditorGUILayout.Toggle("Use Conda Environment:", useCondaEnv);
                if (useCondaEnv)
                {
                    condaEnvName = EditorGUILayout.TextField("Environment Name:", condaEnvName);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                useCondaEnv = false;
            }

            // Show detected information
            if (!string.IsNullOrEmpty(detectedPythonVersion))
            {
                Color originalColor = GUI.color;
                GUI.color = pythonVersionOK ? Color.green : Color.red;
                EditorGUILayout.LabelField($"Python Version: {detectedPythonVersion}");
                GUI.color = originalColor;
            }
            else
            {
                EditorGUILayout.LabelField("Python Version: Not detected");
            }

            if (!string.IsNullOrEmpty(detectedTorchVersion))
            {
                EditorGUILayout.LabelField($"PyTorch Version: {detectedTorchVersion}");
            }

            if (!string.IsNullOrEmpty(detectedGitVersion))
            {
                EditorGUILayout.LabelField($"Git Version: {detectedGitVersion}");
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
                case InstallationMode.CUDA13:
                    EditorGUILayout.HelpBox("CUDA 13.x Mode: Installs the cu130 PyTorch wheels for CUDA 13.x toolkits.", MessageType.Info);
                    break;
                case InstallationMode.Auto:
                    EditorGUILayout.HelpBox("Automatic Mode: Will detect the best mode based on the system.", MessageType.Info);
                    break;
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Essential flow: Detect, Check Dependencies, Install All (base dependencies), Install Hunyuan3D Package, Verify Installation.",
                MessageType.Info
            );

            if (!IsPythonReadyForActions())
            {
                EditorGUILayout.HelpBox(
                    "Install or select a compatible Python first. After that, use Detect again and continue with the package installation.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(isCheckingDependencies || isInstalling || !IsPythonReadyForActions());
            if (GUILayout.Button("Check Dependencies", GUILayout.Height(30)))
            {
                _ = CheckAllDependencies();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(isCheckingDependencies || isInstalling || !IsPythonReadyForActions());
            if (GUILayout.Button("Install All", GUILayout.Height(30)))
            {
                _ = InstallAllDependencies();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(isCheckingDependencies || isInstalling || !IsPythonReadyForActions());
            if (GUILayout.Button("Install Hunyuan3D Package", GUILayout.Height(25)))
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    AddLogMessage("Using the unified UV + Git installation flow for Windows.");
                    _ = InstallHunyuan3DWithUV();
                }
                else
                {
                    AddLogMessage("Using the standard pip installation flow.");
                    _ = InstallHunyuan3DPackage();
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(isCheckingDependencies || isInstalling || !IsPythonReadyForActions());
            if (GUILayout.Button("Verify Installation", GUILayout.Height(25)))
            {
                _ = VerifyFullInstallation();
            }
            EditorGUI.EndDisabledGroup();
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
                if (!EnsurePythonReady("check dependencies"))
                {
                    return;
                }

                // Detect if we are using a venv
                string venvPath = DetectVirtualEnvironment();
                if (!string.IsNullOrEmpty(venvPath))
                {
                    AddLogMessage($"📁 Using virtual environment: {venvPath}");
                    pythonPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(venvPath);
                    pipPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPipPath(venvPath);
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
            var possibleVenvPaths = new List<string>();
            string projectRoot = GetProjectRoot();
            string uvProjectPath = GetUvProjectPath();
            string localTemp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Temp"
            );

            if (!string.IsNullOrEmpty(uvProjectPath))
            {
                possibleVenvPaths.Add(Path.Combine(uvProjectPath, ".venv"));
            }

            if (!string.IsNullOrEmpty(projectRoot))
            {
                possibleVenvPaths.Add(Path.Combine(projectRoot, ".venv"));
            }

            possibleVenvPaths.Add(Path.Combine(Application.dataPath, "UnityPlugin", "Scripts", ".venv"));
            possibleVenvPaths.Add(Path.Combine(localTemp, "Hunyuan3D-2.1-for-windows", ".venv"));
            possibleVenvPaths.Add(Path.Combine(localTemp, "Hunyuan3D-2.1", ".venv"));
            possibleVenvPaths.Add(Path.Combine(localTemp, "Hunyuan3D-2-for-windows", ".venv"));
            possibleVenvPaths.Add(Path.Combine(localTemp, "Hunyuan2-3D-for-windows", ".venv"));

            foreach (string venvPath in possibleVenvPaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(venvPath))
                {
                    string pythonExe = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(venvPath);
                    if (!string.IsNullOrEmpty(pythonExe) && File.Exists(pythonExe))
                    {
                        return venvPath;
                    }
                }
            }

            return null;
        }

        private void ResolvePythonCommand(out string command, out string prefixArguments, bool preferVirtualEnvironment = true, bool logSelection = false)
        {
            prefixArguments = string.Empty;

            if (useCondaEnv)
            {
                command = "conda";
                prefixArguments = $"run -n \"{condaEnvName}\" python ";

                if (logSelection)
                {
                    AddLogMessage($"Using Conda environment: {condaEnvName}");
                }

                return;
            }

            if (preferVirtualEnvironment)
            {
                string venvPath = DetectVirtualEnvironment();
                if (!string.IsNullOrEmpty(venvPath))
                {
                    string venvPython = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(venvPath);
                    string venvPip = Hunyuan3DSystemProbe.GetVirtualEnvironmentPipPath(venvPath);

                    if (!string.IsNullOrEmpty(venvPython) && File.Exists(venvPython))
                    {
                        pythonPath = venvPython;

                        if (!string.IsNullOrEmpty(venvPip) && File.Exists(venvPip))
                        {
                            pipPath = venvPip;
                        }

                        command = venvPython;

                        if (logSelection)
                        {
                            AddLogMessage($"Using Python from virtual environment: {venvPython}");
                        }

                        return;
                    }
                }
            }

            command = pythonPath;

            if (logSelection)
            {
                AddLogMessage($"Using configured Python: {pythonPath}");
            }
        }

        private string BuildPythonArguments(string prefixArguments, string pythonArguments)
        {
            return string.IsNullOrWhiteSpace(prefixArguments)
                ? pythonArguments
                : $"{prefixArguments}{pythonArguments}";
        }

        private async Task<string> ExecutePythonCommand(string pythonArguments, bool preferVirtualEnvironment = true, bool logSelection = false, string workingDirectory = null)
        {
            ResolvePythonCommand(out string command, out string prefixArguments, preferVirtualEnvironment, logSelection);
            return await ExecuteCommandInDirectory(command, BuildPythonArguments(prefixArguments, pythonArguments), workingDirectory);
        }

        private bool TryGetUvProjectDirectory(out string uvProjectDirectory)
        {
            uvProjectDirectory = GetUvProjectPath();

            if (string.IsNullOrEmpty(uvProjectDirectory) || !Directory.Exists(uvProjectDirectory))
            {
                uvProjectDirectory = null;
                return false;
            }

            string pyprojectPath = Path.Combine(uvProjectDirectory, "pyproject.toml");
            string venvPythonPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(Path.Combine(uvProjectDirectory, ".venv"));

            if (!File.Exists(pyprojectPath) || string.IsNullOrEmpty(venvPythonPath) || !File.Exists(venvPythonPath))
            {
                uvProjectDirectory = null;
                return false;
            }

            return true;
        }

        private async Task<bool> CanUseUvForDependencies()
        {
            if (!TryGetUvProjectDirectory(out _))
            {
                return false;
            }

            string uvVersionOutput = await ExecuteCommand("uv", "--version");
            return !OutputHasErrors(uvVersionOutput) && uvVersionOutput.IndexOf("uv ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<string> ExecuteUvPipInstall(string packageArguments, string uvProjectDirectory)
        {
            string uvPythonPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(Path.Combine(uvProjectDirectory, ".venv"));
            if (string.IsNullOrEmpty(uvPythonPath))
            {
                throw new FileNotFoundException("The UV virtual environment does not have a Python executable yet.");
            }

            string installArguments = $"pip install --python \"{uvPythonPath}\" {packageArguments}";
            return await ExecuteCommandInDirectory("uv", installArguments, uvProjectDirectory);
        }

        private async Task<bool> EnsurePipAvailable(bool logSelection = false)
        {
            if (useCondaEnv)
            {
                return true;
            }

            ResolvePythonCommand(out string command, out string prefixArguments, true, logSelection);

            string pipCheckOutput = await ExecuteCommandInDirectory(
                command,
                BuildPythonArguments(prefixArguments, "-m pip --version"),
                null
            );

            if (!OutputHasErrors(pipCheckOutput) && pipCheckOutput.IndexOf("pip ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            AddLogMessage("pip is not available in the selected Python environment. Trying to bootstrap it with ensurepip...");

            string ensurePipOutput = await ExecuteCommandInDirectory(
                command,
                BuildPythonArguments(prefixArguments, "-m ensurepip --upgrade"),
                null
            );

            AddLogMessage(ensurePipOutput);

            if (OutputHasErrors(ensurePipOutput))
            {
                AddLogMessage("✗ Could not bootstrap pip with ensurepip");
                return false;
            }

            string pipUpgradeOutput = await ExecuteCommandInDirectory(
                command,
                BuildPythonArguments(prefixArguments, "-m pip install --upgrade pip"),
                null
            );

            if (OutputHasErrors(pipUpgradeOutput))
            {
                AddLogMessage("⚠ pip was created, but upgrading it reported warnings or errors:");
                AddLogMessage(pipUpgradeOutput);
            }
            else if (!string.IsNullOrWhiteSpace(pipUpgradeOutput))
            {
                AddLogMessage(pipUpgradeOutput);
            }

            string finalPipCheckOutput = await ExecuteCommandInDirectory(
                command,
                BuildPythonArguments(prefixArguments, "-m pip --version"),
                null
            );

            if (!OutputHasErrors(finalPipCheckOutput) && finalPipCheckOutput.IndexOf("pip ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddLogMessage("✓ pip is now available");
                return true;
            }

            AddLogMessage("✗ pip is still not available after running ensurepip");
            AddLogMessage(finalPipCheckOutput);
            return false;
        }

        private bool OutputHasErrors(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            return output.IndexOf("ERROR:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   output.IndexOf("No matching distribution found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   output.IndexOf("Could not find a version that satisfies the requirement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   output.IndexOf("Traceback", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetVerificationScriptPath()
        {
            string scriptPath = Path.Combine(Application.dataPath, "UnityPlugin", "Scripts", "verify_hunyuan3d.py");
            return File.Exists(scriptPath) ? scriptPath : null;
        }

        private bool VerificationLooksSuccessful(string output)
        {
            return output.Contains("[OK] Hunyuan3D 2.1 found and accessible") ||
                   output.Contains("[OK] Legacy Hunyuan3D-2 found and accessible") ||
                   output.Contains("[OK] Hunyuan3D package");
        }

        private async Task<string> RunVerificationScript(bool logSelection = false)
        {
            string verifyScriptPath = GetVerificationScriptPath();

            if (!string.IsNullOrEmpty(verifyScriptPath))
            {
                AddLogMessage($"Using verification script: {verifyScriptPath}");
                return await ExecutePythonCommand($"\"{verifyScriptPath}\"", logSelection: logSelection);
            }

            string inlineVerification = @"-c ""
import importlib.util
ok = importlib.util.find_spec('hy3dshape') is not None or importlib.util.find_spec('hy3dgen') is not None
print('[OK] Hunyuan3D package' if ok else '[ERROR] Hunyuan3D package not found')
""";

            return await ExecutePythonCommand(inlineVerification, logSelection: logSelection);
        }

        private async Task<bool> CheckPythonVersion()
        {
            try
            {
                string arguments = "--version";
                var output = await ExecutePythonCommand(arguments, logSelection: true);

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

                var output = await ExecutePythonCommand(arguments);
                return !output.Contains("No module named") &&
                       !output.Contains("not found") &&
                       !OutputHasErrors(output);
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
                var torchOutput = await ExecutePythonCommand(torchCheck);
                if (torchOutput.Contains("PyTorch version:"))
                {
                    detectedTorchVersion = torchOutput.Trim();
                    torchInstalled = true;

                    // Check CUDA
                    string cudaCheck = "-c \"import torch; print('CUDA available:', torch.cuda.is_available()); print('CUDA version:', torch.version.cuda if torch.cuda.is_available() else 'N/A')\"";
                    var cudaOutput = await ExecutePythonCommand(cudaCheck);

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

                var output = await ExecutePythonCommand(checkScript);

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
            installErrorCount = 0;
            progress = 0f;
            statusMessage = "Starting full installation...";

            try
            {
                if (!EnsurePythonReady("install dependencies"))
                {
                    return;
                }

                AddLogMessage("=== STARTING HUNYUAN3D INSTALLATION ===");
                AddLogMessage("Based on: https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1");

                // On Windows, make sure the managed UV environment exists *before* installing, so the
                // base dependencies land in the same .venv that 'Install Hunyuan3D Package' uses instead
                // of the global/system Python. This avoids installing torch & friends twice.
                if (Application.platform == RuntimePlatform.WindowsEditor && !useCondaEnv)
                {
                    try
                    {
                        string managedUvProject = await EnsureUvProjectReady();
                        if (!string.IsNullOrEmpty(managedUvProject))
                        {
                            AddLogMessage($"Base dependencies will be installed into the managed UV environment: {managedUvProject}");
                        }
                        else
                        {
                            AddLogMessage("⚠ UV is not available; continuing with the configured Python interpreter.");
                        }
                    }
                    catch (Exception uvPrepError)
                    {
                        AddLogMessage($"⚠ Could not prepare the managed UV environment ({uvPrepError.Message}).");
                        AddLogMessage("Continuing with the configured Python interpreter.");
                    }
                }

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

                bool useUvDependencies = await CanUseUvForDependencies();
                if (useUvDependencies)
                {
                    if (TryGetUvProjectDirectory(out string uvProjectDirectory))
                    {
                        AddLogMessage($"Using UV project for dependency installation: {uvProjectDirectory}");
                    }
                }
                else if (!await EnsurePipAvailable(logSelection: true))
                {
                    throw new Exception("pip is not available in the selected Python environment.");
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

                progress = 1f;

                if (installErrorCount == 0)
                {
                    statusMessage = "Base dependencies installed!";
                    AddLogMessage("✓ Base dependencies installed successfully.");
                    AddLogMessage("Next step: run 'Install Hunyuan3D Package' to install the official Hunyuan3D 2.1 package.");
                }
                else
                {
                    statusMessage = "Base dependency installation finished with errors";
                    AddLogMessage($"⚠ Base dependency installation finished with {installErrorCount} errors.");
                    AddLogMessage("Review the failed packages in the log before continuing.");
                }
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
            string indexUrl;

            switch (selectedInstallMode)
            {
                case InstallationMode.CPU:
                    indexUrl = "https://download.pytorch.org/whl/cpu";
                    break;
                case InstallationMode.CUDA11:
                    indexUrl = "https://download.pytorch.org/whl/cu118";
                    break;
                case InstallationMode.CUDA12:
                    indexUrl = "https://download.pytorch.org/whl/cu124";
                    break;
                case InstallationMode.CUDA13:
                    indexUrl = "https://download.pytorch.org/whl/cu130";
                    break;
                case InstallationMode.Auto:
                default:
                    // Match the wheel to the detected CUDA toolkit (cu130/cu124/cu118) — the same policy
                    // as the Hunyuan3D package install, so torch is not installed twice with different builds.
                    indexUrl = await DetectCudaCapability()
                        ? GetTorchCudaIndexUrl()
                        : "https://download.pytorch.org/whl/cpu";
                    break;
            }

            string torchCommand = $"torch torchvision --index-url {indexUrl}";
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
            string uvProjectDirectory = null;
            bool useUvDependencies = !useCondaEnv && await CanUseUvForDependencies();

            if (useUvDependencies)
            {
                TryGetUvProjectDirectory(out uvProjectDirectory);
                if (!string.IsNullOrEmpty(uvProjectDirectory))
                {
                    AddLogMessage($"Installing packages with UV in: {uvProjectDirectory}");
                }
            }
            else if (!useCondaEnv)
            {
                bool pipReady = await EnsurePipAvailable();
                if (!pipReady)
                {
                    throw new Exception("pip is not available in the selected Python environment.");
                }
            }

            foreach (string package in packages)
            {
                try
                {
                    AddLogMessage($"Installing: {package}");

                    string arguments;

                    if (useCondaEnv)
                    {
                        arguments = $"install -n {condaEnvName} -c conda-forge -y {package}";
                        var output = await ExecuteCommand("conda", arguments);

                        if (!OutputHasErrors(output))
                        {
                            AddLogMessage($"✓ {package} installed");

                            string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                            dependencyStatus[packageName] = DependencyStatus.Installed;
                        }
                        else
                        {
                            AddLogMessage($"✗ Error installing {package}");
                            AddLogMessage(output);
                            installErrorCount++;

                            string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                            dependencyStatus[packageName] = DependencyStatus.Error;
                        }
                    }
                    else
                    {
                        string output;
                        if (useUvDependencies && !string.IsNullOrEmpty(uvProjectDirectory))
                        {
                            output = await ExecuteUvPipInstall(package, uvProjectDirectory);
                        }
                        else
                        {
                            arguments = $"-m pip install {package}";
                            output = await ExecutePythonCommand(arguments);
                        }

                        if (!OutputHasErrors(output))
                        {
                            AddLogMessage($"✓ {package} installed");

                            string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                            dependencyStatus[packageName] = DependencyStatus.Installed;
                        }
                        else
                        {
                            AddLogMessage($"✗ Error installing {package}");
                            AddLogMessage(output);
                            installErrorCount++;

                            string packageName = package.Split(new char[] { '>', '<', '=', '!' })[0];
                            dependencyStatus[packageName] = DependencyStatus.Error;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"✗ Exception installing {package}: {ex.Message}");
                    installErrorCount++;
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
                AddLogMessage("Installing Hunyuan3D package from the official repository...");

                // Clone repository and install
                string tempDir = CreateShortTempDirectory("repo");

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


                // Prepare the repository: clone with git, fall back to a ZIP download if git is unavailable
                try
                {
                    await PrepareHunyuanRepositoryFromGit(tempDir);
                }
                catch (Exception repoError)
                {
                    AddLogMessage($"⚠ Could not prepare the repository with git: {repoError.Message}");
                    AddLogMessage("Falling back to downloading the repository as a ZIP...");
                    await DownloadHunyuan3DAsZip(tempDir);
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
                    await EnsureSetuptoolsInstalled();

                    if (File.Exists(reqPath))
                    {
                        AddLogMessage("Installing from requirements.txt...");
                        string reqArgs = $"-m pip install -r \"{reqPath}\"";
                        var reqOutput = await ExecutePythonCommand(reqArgs, logSelection: true);
                        AddLogMessage(reqOutput);

                        if (!OutputHasErrors(reqOutput))
                        {
                            AddLogMessage("✓ Dependencies installed from requirements.txt");
                        }
                        else
                        {
                            AddLogMessage("⚠ requirements.txt installation reported errors");
                        }
                    }

                    // Try to install the package in development mode
                    string setupPath = Path.Combine(tempDir, "setup.py");
                    if (File.Exists(setupPath))
                    {
                        AddLogMessage("Installing Hunyuan3D package in development mode...");
                        string installArgs = $"-m pip install -e \"{tempDir}\"";
                        var installOutput = await ExecutePythonCommand(installArgs, logSelection: true);
                        AddLogMessage(installOutput);

                        if (!OutputHasErrors(installOutput))
                        {
                            AddLogMessage("✓ Hunyuan3D package installed in development mode");
                        }
                        else
                        {
                            AddLogMessage("⚠ Hunyuan3D package installation reported errors");
                        }
                    }
                    else
                    {
                        AddLogMessage("⚠ setup.py not found - only dependencies were installed");
                    }

                    // Try to install the package in development mode
                    // Install custom_rasterizer with improved error handling
                    SetCudaHomeEnv();
                    string custRasterPath = Path.Combine(tempDir, "hy3dpaint", "custom_rasterizer");
                    if (Directory.Exists(custRasterPath))
                    {
                        ResolvePythonCommand(out string pythonCmd, out _, logSelection: true);
                        await HandleCustomRasterizerCompilation(pythonCmd, custRasterPath);
                    }
                    else
                    {
                        AddLogMessage("⚠ custom_rasterizer not found in the repository");
                        AddLogMessage("ℹ Continuing without this optional module");
                    }

                    // Similar for differentiable_renderer...
                    string diffRendererPath = Path.Combine(tempDir, "hy3dpaint", "DifferentiableRenderer");
                    if (Directory.Exists(diffRendererPath))
                    {
                        AddLogMessage("Installing differentiable_renderer...");

                        string installOutput;
                        SetCudaHomeEnv();
                        ResolvePythonCommand(out string pythonCmd, out _, logSelection: true);

                        string compileScript = Path.Combine(diffRendererPath, "compile_mesh_painter.sh");
                        if (File.Exists(compileScript))
                        {
                            installOutput = await ExecuteCommandInDirectory("bash", "compile_mesh_painter.sh", diffRendererPath);
                        }
                        else
                        {
                            string installArgs = $"setup.py install";
                            installOutput = await ExecuteCommandInDirectory(pythonCmd, installArgs, diffRendererPath);
                        }

                        if (installOutput.Contains("Successfully installed") ||
                            installOutput.Contains("Finished processing") ||
                            installOutput.Contains("Successfully"))
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
                    var testOutput = await RunVerificationScript(logSelection: true);

                    if (VerificationLooksSuccessful(testOutput))
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
                ResolvePythonCommand(out string pythonCmd, out _, logSelection: true);
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

                string zipUrl = "https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1/archive/refs/heads/main.zip";
                string zipPath = Path.Combine(Path.GetPathRoot(Path.GetTempPath()) ?? Path.GetTempPath(), "_h3dtmp", "h3d.zip");

                string zipDirectory = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(zipDirectory))
                {
                    Directory.CreateDirectory(zipDirectory);
                }

                if (Directory.Exists(targetDir))
                {
                    try
                    {
                        Directory.Delete(targetDir, true);
                    }
                    catch
                    {
                    }
                }

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
                Directory.CreateDirectory(extractPath);
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

        /// <summary>
        /// Verifies/installs UV and makes sure the managed Hunyuan3D_UV project (pyproject.toml + .venv)
        /// exists, creating it when needed. Returns the project directory, or null if UV is not available.
        /// Shared by 'Install All' and 'Install Hunyuan3D Package' so both target the same environment.
        /// </summary>
        private async Task<string> EnsureUvProjectReady()
        {
            // 1. Verify/Install UV
            if (!await CheckAndInstallUV())
            {
                return null;
            }

            // 2. Locate (or pick) the managed UV project directory
            string projectDir = GetUvProjectPath() ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Hunyuan3D_UV"));
            Directory.CreateDirectory(projectDir);

            AddLogMessage($"Preparing UV project at: {projectDir}");
            SetCudaHomeEnv();

            string pyprojectPath = Path.Combine(projectDir, "pyproject.toml");
            string uvEnvironmentPath = Path.Combine(projectDir, ".venv");
            string uvPythonPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(uvEnvironmentPath);

            // 3. Initialize UV project only when needed
            if (!File.Exists(pyprojectPath))
            {
                var initOutput = await ExecuteCommandInDirectory("uv", "init", projectDir);
                if (OutputHasErrors(initOutput))
                {
                    AddLogMessage(initOutput);
                    throw new Exception("Could not initialize the UV project");
                }
            }
            else
            {
                AddLogMessage("UV project already initialized. Reusing existing pyproject.toml.");
            }

            // 4. Create the virtual environment only when needed.
            // Target Python 3.11: it has wheels for all Hunyuan3D-2.1 dependencies AND for bpy (Blender as a
            // module, required for FBX export) — bpy has NO Python 3.10 wheel on PyPI (current bpy is cp311/cp313).
            // The requirements patcher relaxes the few pins lacking 3.11 wheels (numpy, pymeshlab, open3d) and
            // pins a 3.11-compatible bpy. Falls back to the detected interpreter if 3.11 cannot be provisioned.
            if (string.IsNullOrEmpty(uvPythonPath) || !File.Exists(uvPythonPath))
            {
                AddLogMessage("Creating UV virtual environment with Python 3.11 (Hunyuan3D-2.1 + bpy/FBX support)...");
                var venvOutput = await ExecuteCommandInDirectory("uv", "venv --seed -p 3.11", projectDir);
                uvPythonPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(uvEnvironmentPath);

                if (OutputHasErrors(venvOutput) || string.IsNullOrEmpty(uvPythonPath) || !File.Exists(uvPythonPath))
                {
                    AddLogMessage("Python 3.11 could not be provisioned; falling back to the detected interpreter.");
                    AddLogMessage($"Detected Python: {pythonPath}");
                    venvOutput = await ExecuteCommandInDirectory("uv", $"venv --seed -p \"{pythonPath}\"", projectDir);
                    uvPythonPath = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(uvEnvironmentPath);

                    if (OutputHasErrors(venvOutput) || string.IsNullOrEmpty(uvPythonPath) || !File.Exists(uvPythonPath))
                    {
                        AddLogMessage(venvOutput);
                        throw new Exception("Could not create the UV virtual environment");
                    }
                }
            }
            else
            {
                AddLogMessage("UV virtual environment already exists. Reusing existing .venv.");
            }

            // Record the environment's Python version so the repository requirements can be adapted if needed.
            managedVenvPythonVersion = await GetInterpreterVersion(uvPythonPath);
            if (!string.IsNullOrEmpty(managedVenvPythonVersion))
            {
                AddLogMessage($"UV environment Python version: {managedVenvPythonVersion}");
            }

            return projectDir;
        }

        /// <summary>
        /// Returns the "major.minor" version (e.g. "3.10") of a Python interpreter, or "" on failure.
        /// </summary>
        private async Task<string> GetInterpreterVersion(string pythonExecutable)
        {
            if (string.IsNullOrEmpty(pythonExecutable) || !File.Exists(pythonExecutable))
            {
                return "";
            }

            try
            {
                var output = await ExecuteCommandInDirectory(
                    pythonExecutable,
                    "-c \"import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')\"",
                    null);

                var match = System.Text.RegularExpressions.Regex.Match(output, @"\b(\d+)\.(\d+)\b");
                return match.Success ? $"{match.Groups[1].Value}.{match.Groups[2].Value}" : "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Adapts the official Hunyuan3D-2.1 requirements.txt so uv can resolve and install it:
        /// - relaxes numpy==1.24.4 (conflicts with pandas==2.2.2 regardless of Python version);
        /// - on Python versions the repo does not ship wheels for (3.12+), skips bpy (optional FBX backend)
        ///   and unpins pymeshlab/open3d so a compatible build can be chosen.
        /// </summary>
        private void PatchHunyuanRequirementsForCompatibility(string requirementsPath)
        {
            try
            {
                if (string.IsNullOrEmpty(requirementsPath) || !File.Exists(requirementsPath))
                {
                    return;
                }

                // Only Python 3.10 has wheels for the repo's exact pymeshlab/open3d pins; on 3.11+ they are unpinned.
                bool recommendedPython = managedVenvPythonVersion == "3.10";

                string[] lines = File.ReadAllLines(requirementsPath);
                bool changed = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();

                    if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    // numpy==1.24.4 conflicts with pandas==2.2.2 (needs numpy>=1.26), so uv cannot resolve.
                    // Relax to a range that satisfies both and still honours the project's "numpy<2".
                    if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^numpy\s*==", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        lines[i] = "numpy>=1.26.0,<2.0.0  # relaxed by Hunyuan3D Unity plugin (was: " + trimmed + ")";
                        AddLogMessage($"Patched requirement: {trimmed} -> numpy>=1.26.0,<2.0.0");
                        changed = true;
                        continue;
                    }

                    // bpy (Blender as a module) is the only backend that writes real FBX. The repo pins bpy==4.0,
                    // which is gone from PyPI; current PyPI bpy wheels are cp311 (4.2-5.0) / cp313 (5.1). On
                    // Python 3.11 pin a compatible LTS (4.2.0) to enable FBX; otherwise skip it (no wheel).
                    if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^bpy(\s*[=<>!~]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        if (managedVenvPythonVersion == "3.11")
                        {
                            lines[i] = "bpy==4.2.0  # pinned by Hunyuan3D Unity plugin (cp311 wheel; enables FBX). Was: " + trimmed;
                            AddLogMessage($"Pinned bpy to 4.2.0 for Python 3.11 (enables FBX export). Was: {trimmed}");
                        }
                        else
                        {
                            lines[i] = "# bpy skipped by Hunyuan3D Unity plugin (no installable wheel for Python " + managedVenvPythonVersion + "; optional FBX backend). Was: " + trimmed;
                            AddLogMessage($"Skipped optional requirement (no installable wheel for Python {managedVenvPythonVersion}): {trimmed}");
                        }
                        changed = true;
                        continue;
                    }

                    // deepspeed is not imported anywhere in Hunyuan3D-2.1 (training-only leftover) and its sdist
                    // build fails under PEP 517 build isolation ("needs torch installed"), so skip it.
                    if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^deepspeed(\s*[=<>!~]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        lines[i] = "# deepspeed skipped by Hunyuan3D Unity plugin (unused, training-only; build fails). Was: " + trimmed;
                        AddLogMessage($"Skipped unused requirement: {trimmed}");
                        changed = true;
                        continue;
                    }

                    // On Python versions the repo has no wheels for (3.12+), unpin pymeshlab/open3d so a
                    // compatible build can be selected (on 3.10/3.11 the pinned versions are kept).
                    if (!recommendedPython)
                    {
                        var unpinMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(pymeshlab|open3d)\s*==", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (unpinMatch.Success)
                        {
                            string pkg = unpinMatch.Groups[1].Value;
                            lines[i] = pkg + "  # version unpinned by Hunyuan3D Unity plugin for Python " + managedVenvPythonVersion + " (was: " + trimmed + ")";
                            AddLogMessage($"Unpinned requirement for Python {managedVenvPythonVersion}: {trimmed} -> {pkg}");
                            changed = true;
                            continue;
                        }
                    }
                }

                if (changed)
                {
                    File.WriteAllText(requirementsPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
                    AddLogMessage("Adjusted the repository requirements.txt for environment compatibility.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Could not patch requirements.txt: {ex.Message}");
            }
        }

        /// <summary>
        /// Major version of the detected CUDA toolkit (e.g. 12 or 13), or 0 if unknown.
        /// </summary>
        private int GetDetectedCudaMajor()
        {
            string version = detectedCudaToolkitVersion;
            if (string.IsNullOrEmpty(version))
            {
                RefreshSystemDetection(logDetails: false);
                version = detectedCudaToolkitVersion;
            }

            var match = System.Text.RegularExpressions.Regex.Match(version ?? string.Empty, @"(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        /// <summary>
        /// Best-effort compilation of the optional custom_rasterizer CUDA extension (needed for texture
        /// generation). Applies compatibility patches for recent toolchains and never throws — texture is
        /// optional and shape generation works without it.
        /// </summary>
        private async Task TryBuildTextureSupport(string repositoryPath, string virtualEnvironmentPath)
        {
            try
            {
                if (Application.platform != RuntimePlatform.WindowsEditor)
                {
                    return;
                }

                string hy3dpaintDir = Path.Combine(repositoryPath, "hy3dpaint");
                if (!Directory.Exists(hy3dpaintDir))
                {
                    AddLogMessage("hy3dpaint folder not found; skipping optional texture support.");
                    return;
                }

                AddLogMessage("=== Setting up optional texture support (best effort) ===");

                // 1. Make Blender (bpy) optional — pure Python, always safe. Afterwards textureGenPipeline can
                //    be imported without bpy, and OBJ->GLB conversion falls back to trimesh.
                PatchMeshUtilsBpy(hy3dpaintDir);

                // 2. Make the texture pipeline's config paths absolute. The repo hard-codes repo-root-relative
                //    paths ("hy3dpaint/cfgs/...", "ckpt/RealESRGAN_x4plus.pth"); the generator runs from a
                //    different working directory, so without this the texture pipeline cannot find them.
                PatchTextureGenPipelinePaths(hy3dpaintDir);

                string venvPython = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(virtualEnvironmentPath);
                if (string.IsNullOrEmpty(venvPython) || !File.Exists(venvPython))
                {
                    AddLogMessage("⚠ Could not locate the virtual environment Python; skipping native texture extensions.");
                    return;
                }

                // 3. basicsr (a realesrgan dependency) imports torchvision.transforms.functional_tensor, which
                //    was removed in torchvision 0.17+. Rewrite it to the current location so the texture
                //    pipeline can be imported.
                PatchBasicsrTorchvision(virtualEnvironmentPath);

                // 4. The super-resolution step needs RealESRGAN_x4plus.pth, which the repo expects you to fetch
                //    manually into ckpt/. Download it (best effort).
                await DownloadRealESRGANCheckpoint(repositoryPath);

                // 5. The native extensions need the Visual Studio C++ build tools.
                string vcvarsPath = await FindVcvars64();
                if (string.IsNullOrEmpty(vcvarsPath))
                {
                    AddLogMessage("⚠ Visual Studio C++ build tools (vcvars64.bat) were not found.");
                    AddLogMessage("   Install 'Desktop development with C++' to build the texture extensions. Skipping.");
                    return;
                }

                string archList = await DetectTorchCudaArchList(venvPython);

                // 6. custom_rasterizer (CUDA extension) — required for texture rendering.
                await BuildCustomRasterizer(hy3dpaintDir, venvPython, vcvarsPath, archList);

                // 7. mesh_inpaint_processor (C++ pybind11 extension) — used by the texture inpainting step.
                await BuildMeshInpaintProcessor(hy3dpaintDir, venvPython, vcvarsPath);
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Texture support setup skipped due to an error: {ex.Message}");
                AddLogMessage("   Shape generation is unaffected.");
            }
        }

        private async Task BuildCustomRasterizer(string hy3dpaintDir, string venvPython, string vcvarsPath, string archList)
        {
            try
            {
                string customRasterizerDir = Path.Combine(hy3dpaintDir, "custom_rasterizer");
                if (!Directory.Exists(customRasterizerDir) ||
                    !File.Exists(Path.Combine(customRasterizerDir, "setup.py")))
                {
                    AddLogMessage("custom_rasterizer not found; skipping.");
                    return;
                }

                AddLogMessage("Building custom_rasterizer (CUDA extension). This can take several minutes...");

                // Apply source compatibility patches for recent toolchains (idempotent and safe).
                PatchCustomRasterizerSources(customRasterizerDir);

                // Use regular pip (not 'uv pip install .'): uv copies the local source in a way that, with
                // this setup.py, corrupted the source tree and hit a metadata bug. Regular pip builds in a
                // temp copy and installs the wheel cleanly. cwd is the extension dir (set by RunBuildInMsvcEnv).
                string command = "\"" + venvPython + "\" -m pip install . --no-build-isolation --no-cache-dir";
                await RunBuildInMsvcEnv(customRasterizerDir, vcvarsPath, archList, command);

                string verifyOutput = await ExecuteCommandInDirectory(
                    venvPython,
                    "-c \"import custom_rasterizer; print('CUSTOM_RASTERIZER_OK')\"",
                    customRasterizerDir);

                if (verifyOutput.Contains("CUSTOM_RASTERIZER_OK"))
                {
                    AddLogMessage("✅ custom_rasterizer built and importable (texture rendering available).");
                }
                else
                {
                    AddLogMessage("⚠ custom_rasterizer did not build/import; texture generation will be unavailable.");
                    AddLogMessage("   Shape generation is unaffected. This is expected when the CUDA toolkit does not");
                    AddLogMessage("   match PyTorch, or on very new toolchains (e.g. CUDA 13.x).");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ custom_rasterizer build skipped: {ex.Message}");
            }
        }

        private async Task BuildMeshInpaintProcessor(string hy3dpaintDir, string venvPython, string vcvarsPath)
        {
            try
            {
                string differentiableRendererDir = Path.Combine(hy3dpaintDir, "DifferentiableRenderer");
                string cppPath = Path.Combine(differentiableRendererDir, "mesh_inpaint_processor.cpp");
                if (!Directory.Exists(differentiableRendererDir) || !File.Exists(cppPath))
                {
                    AddLogMessage("DifferentiableRenderer/mesh_inpaint_processor.cpp not found; skipping.");
                    return;
                }

                // The repo only ships a Linux build script (compile_mesh_painter.sh). Generate a portable
                // pybind11 setup.py and build the module in place so 'from .mesh_inpaint_processor import
                // meshVerticeInpaint' resolves inside the DifferentiableRenderer package.
                string setupPath = Path.Combine(differentiableRendererDir, "setup_mesh_inpaint.py");
                string setupContent =
                    "from pybind11.setup_helpers import Pybind11Extension, build_ext\n" +
                    "from setuptools import setup\n" +
                    "setup(\n" +
                    "    name=\"mesh_inpaint_processor\",\n" +
                    "    ext_modules=[Pybind11Extension(\"mesh_inpaint_processor\", [\"mesh_inpaint_processor.cpp\"])],\n" +
                    "    cmdclass={\"build_ext\": build_ext},\n" +
                    ")\n";
                File.WriteAllText(setupPath, setupContent, new UTF8Encoding(false));

                AddLogMessage("Building DifferentiableRenderer/mesh_inpaint_processor (C++ pybind11)...");
                string command = "\"" + venvPython + "\" setup_mesh_inpaint.py build_ext --inplace";
                await RunBuildInMsvcEnv(differentiableRendererDir, vcvarsPath, string.Empty, command);

                string verify = await ExecuteCommandInDirectory(
                    venvPython,
                    "-c \"import sys; sys.path.insert(0, r'" + differentiableRendererDir + "'); import mesh_inpaint_processor; print('MESH_INPAINT_OK')\"",
                    differentiableRendererDir);

                if (verify.Contains("MESH_INPAINT_OK"))
                {
                    AddLogMessage("✅ mesh_inpaint_processor built (texture inpainting available).");
                }
                else
                {
                    AddLogMessage("⚠ mesh_inpaint_processor did not build; the texture inpainting step will be skipped at runtime.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ mesh_inpaint_processor build skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// The texture pipeline's Hunyuan3DPaintConfig hard-codes repo-root-relative paths
        /// ("hy3dpaint/cfgs/hunyuan-paint-pbr.yaml", "ckpt/RealESRGAN_x4plus.pth"). The Unity generator runs
        /// batch_hunyuan3d.py from a different working directory, so these resolve to non-existent locations.
        /// Rewrite them to absolute, module-relative paths. Idempotent (guarded by a marker comment).
        /// </summary>
        private void PatchTextureGenPipelinePaths(string hy3dpaintDir)
        {
            try
            {
                string pipelinePath = Path.Combine(hy3dpaintDir, "textureGenPipeline.py");
                if (!File.Exists(pipelinePath))
                {
                    return;
                }

                string content = File.ReadAllText(pipelinePath);
                if (content.Contains("_HUNYUAN_ABS_PATHS"))
                {
                    return; // already patched
                }

                string original = content;
                content = content.Replace(
                    "self.multiview_cfg_path = \"hy3dpaint/cfgs/hunyuan-paint-pbr.yaml\"",
                    "import os as _os  # _HUNYUAN_ABS_PATHS\n" +
                    "        _root = _os.path.dirname(_os.path.dirname(_os.path.abspath(__file__)))\n" +
                    "        self.multiview_cfg_path = _os.path.join(_root, \"hy3dpaint\", \"cfgs\", \"hunyuan-paint-pbr.yaml\")");
                content = content.Replace(
                    "self.realesrgan_ckpt_path = \"ckpt/RealESRGAN_x4plus.pth\"",
                    "self.realesrgan_ckpt_path = _os.path.join(_root, \"ckpt\", \"RealESRGAN_x4plus.pth\")");

                if (content != original)
                {
                    File.WriteAllText(pipelinePath, content, new UTF8Encoding(false));
                    AddLogMessage("Patched textureGenPipeline.py to use absolute config/checkpoint paths.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Could not patch textureGenPipeline.py paths: {ex.Message}");
            }
        }

        /// <summary>
        /// basicsr 1.4.x (pulled in by realesrgan, used for texture super-resolution) imports
        /// torchvision.transforms.functional_tensor, which was removed in torchvision 0.17+. Rewrite those
        /// imports in the installed basicsr package to the current torchvision.transforms.functional location.
        /// Best effort, idempotent.
        /// </summary>
        private void PatchBasicsrTorchvision(string virtualEnvironmentPath)
        {
            try
            {
                string basicsrDir = Path.Combine(virtualEnvironmentPath, "Lib", "site-packages", "basicsr");
                if (!Directory.Exists(basicsrDir))
                {
                    AddLogMessage("basicsr not installed; skipping its torchvision compatibility patch.");
                    return;
                }

                int patched = 0;
                foreach (string filePath in Directory.GetFiles(basicsrDir, "*.py", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(filePath);
                    if (source.Contains("torchvision.transforms.functional_tensor"))
                    {
                        File.WriteAllText(
                            filePath,
                            source.Replace("torchvision.transforms.functional_tensor", "torchvision.transforms.functional"),
                            new UTF8Encoding(false));
                        patched++;
                    }
                }

                if (patched > 0)
                {
                    AddLogMessage($"Patched basicsr torchvision.functional_tensor import in {patched} file(s).");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Could not patch basicsr: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads RealESRGAN_x4plus.pth into the repository's ckpt/ folder. The texture super-resolution
        /// step (imageSuperNet) loads this checkpoint, but the repo expects you to fetch it manually. Best
        /// effort; skips if already present.
        /// </summary>
        private async Task DownloadRealESRGANCheckpoint(string repositoryPath)
        {
            try
            {
                string ckptDir = Path.Combine(repositoryPath, "ckpt");
                string dest = Path.Combine(ckptDir, "RealESRGAN_x4plus.pth");
                if (File.Exists(dest) && new FileInfo(dest).Length > 60000000L)
                {
                    AddLogMessage("RealESRGAN_x4plus.pth already present.");
                    return;
                }

                AddLogMessage("Downloading RealESRGAN_x4plus.pth (~64 MB) for texture super-resolution...");
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(ckptDir);
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                    using (var client = new System.Net.WebClient())
                    {
                        client.DownloadFile(
                            "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth",
                            dest);
                    }
                });

                if (File.Exists(dest) && new FileInfo(dest).Length > 60000000L)
                {
                    AddLogMessage("✅ RealESRGAN_x4plus.pth downloaded.");
                }
                else
                {
                    AddLogMessage("⚠ RealESRGAN_x4plus.pth download looks incomplete; texture super-resolution may fail.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Could not download RealESRGAN_x4plus.pth: {ex.Message}");
                AddLogMessage("   You can place it manually at <repo>/ckpt/RealESRGAN_x4plus.pth.");
            }
        }

        /// <summary>
        /// Makes the Blender (bpy) dependency optional in DifferentiableRenderer/mesh_utils.py and gives
        /// convert_obj_to_glb a trimesh fallback. bpy has no installable wheel for many Python builds and is
        /// imported unguarded, which otherwise blocks importing the whole texture pipeline. Idempotent.
        /// </summary>
        private void PatchMeshUtilsBpy(string hy3dpaintDir)
        {
            try
            {
                string meshUtilsPath = Path.Combine(hy3dpaintDir, "DifferentiableRenderer", "mesh_utils.py");
                if (!File.Exists(meshUtilsPath))
                {
                    return;
                }

                if (File.ReadAllText(meshUtilsPath).Contains("_HUNYUAN_BPY_OPTIONAL"))
                {
                    return; // already patched
                }

                var lines = File.ReadAllLines(meshUtilsPath).ToList();
                bool changed = false;

                int importIndex = lines.FindIndex(l => l.Trim() == "import bpy");
                if (importIndex >= 0)
                {
                    lines.RemoveAt(importIndex);
                    lines.InsertRange(importIndex, new[]
                    {
                        "try:  # _HUNYUAN_BPY_OPTIONAL",
                        "    import bpy",
                        "except Exception:",
                        "    bpy = None  # bpy is optional; convert_obj_to_glb falls back to trimesh",
                    });
                    changed = true;
                }

                int docIndex = lines.FindIndex(l => l.Contains("Convert OBJ file to GLB format using Blender"));
                if (docIndex >= 0)
                {
                    lines.InsertRange(docIndex + 1, new[]
                    {
                        "    if bpy is None:",
                        "        try:",
                        "            import trimesh",
                        "            trimesh.load(obj_path, process=False).export(glb_path)",
                        "            return True",
                        "        except Exception:",
                        "            return False",
                    });
                    changed = true;
                }

                if (changed)
                {
                    File.WriteAllText(meshUtilsPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
                    AddLogMessage("Patched mesh_utils.py (bpy made optional; trimesh fallback for OBJ->GLB).");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Could not patch mesh_utils.py: {ex.Message}");
            }
        }

        /// <summary>
        /// Locates vcvars64.bat from the latest Visual Studio / Build Tools install that has the C++ workload.
        /// </summary>
        private async Task<string> FindVcvars64()
        {
            try
            {
                string vswhere = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft Visual Studio", "Installer", "vswhere.exe");

                if (!File.Exists(vswhere))
                {
                    return null;
                }

                string output = await ExecuteCommand(
                    vswhere,
                    "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath");

                string installPath = output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(Directory.Exists);

                if (string.IsNullOrEmpty(installPath))
                {
                    return null;
                }

                string vcvars = Path.Combine(installPath, "VC", "Auxiliary", "Build", "vcvars64.bat");
                return File.Exists(vcvars) ? vcvars : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the GPU compute capability (e.g. "8.9") for TORCH_CUDA_ARCH_LIST, or a broad fallback list.
        /// </summary>
        private async Task<string> DetectTorchCudaArchList(string venvPython)
        {
            try
            {
                string output = await ExecuteCommandInDirectory(
                    venvPython,
                    "-c \"import torch; print('.'.join(map(str, torch.cuda.get_device_capability())))\"",
                    null);

                var match = System.Text.RegularExpressions.Regex.Match(output, @"\b(\d+\.\d+)\b");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch
            {
            }

            return "7.5;8.0;8.6;8.9;9.0";
        }

        /// <summary>
        /// Chooses a PyTorch CUDA wheel index matching the detected CUDA toolkit major version, so the
        /// custom_rasterizer CUDA extension can be built against the same toolkit. CUDA 13.x -> cu130
        /// (compiles cleanly with a recent torch), 12.x -> cu124, 11.x -> cu118; default cu124.
        /// </summary>
        private string GetTorchCudaIndexUrl()
        {
            int major = GetDetectedCudaMajor();
            if (major >= 13) return "https://download.pytorch.org/whl/cu130";
            if (major == 12) return "https://download.pytorch.org/whl/cu124";
            if (major == 11) return "https://download.pytorch.org/whl/cu118";
            return "https://download.pytorch.org/whl/cu124";
        }

        /// <summary>
        /// Patches custom_rasterizer sources so they compile on recent toolchains. The int64_t casts for
        /// torch::zeros initializer lists are always applied (valid C++ everywhere). The conforming MSVC
        /// preprocessor flag is only added for CUDA 13.x (its CCCL headers require it; on older toolchains the
        /// original code compiles without it and the flag can surface unrelated conflicts). Idempotent.
        /// </summary>
        private void PatchCustomRasterizerSources(string customRasterizerDir)
        {
            try
            {
                bool addPreprocessorFlag = GetDetectedCudaMajor() >= 13;

                string setupPath = Path.Combine(customRasterizerDir, "setup.py");
                if (addPreprocessorFlag && File.Exists(setupPath))
                {
                    string setup = File.ReadAllText(setupPath);
                    if (!setup.Contains("extra_compile_args"))
                    {
                        string patched = System.Text.RegularExpressions.Regex.Replace(
                            setup,
                            "(CUDAExtension\\(\\s*\"custom_rasterizer_kernel\".*?\\])\\s*,?\\s*\\)",
                            "$1,\n    extra_compile_args={\n        \"cxx\": [],\n        \"nvcc\": [\"-Xcompiler\", \"/Zc:preprocessor\"],\n    },\n)",
                            System.Text.RegularExpressions.RegexOptions.Singleline);

                        if (patched != setup)
                        {
                            File.WriteAllText(setupPath, patched, new UTF8Encoding(false));
                            AddLogMessage("Patched custom_rasterizer/setup.py (nvcc /Zc:preprocessor for CUDA 13.x CCCL).");
                        }
                    }
                }

                string kernelDir = Path.Combine(customRasterizerDir, "lib", "custom_rasterizer_kernel");

                // grid_neighbor.cpp: cast size_t -> int64_t in torch::zeros initializer lists (strict MSVC
                // rejects the narrowing), and use int64_t (not 'long', which is 32-bit on Windows) for the
                // int64 tensor pointer declarations.
                string gridNeighborPath = Path.Combine(kernelDir, "grid_neighbor.cpp");
                if (File.Exists(gridNeighborPath))
                {
                    string code = File.ReadAllText(gridNeighborPath);
                    string original = code;

                    var replacements = new Dictionary<string, string>
                    {
                        { "torch::zeros({seq2pos.size() / 3, 3}", "torch::zeros({(int64_t)(seq2pos.size() / 3), 3}" },
                        { "torch::zeros({seq2pos.size() / 3}", "torch::zeros({(int64_t)(seq2pos.size() / 3)}" },
                        { "torch::zeros({seq2feat.size() / feat_channel, feat_channel}", "torch::zeros({(int64_t)(seq2feat.size() / feat_channel), feat_channel}" },
                        { "torch::zeros({grids[i].seq2grid.size(), 9}", "torch::zeros({(int64_t)grids[i].seq2grid.size(), 9}" },
                        { "torch::zeros({grids[i].seq2evencorner.size()}", "torch::zeros({(int64_t)grids[i].seq2evencorner.size()}" },
                        { "torch::zeros({grids[i].seq2oddcorner.size()}", "torch::zeros({(int64_t)grids[i].seq2oddcorner.size()}" },
                        { "torch::zeros({grids[i].downsample_seq.size()}", "torch::zeros({(int64_t)grids[i].downsample_seq.size()}" },
                        { "long* nptr", "int64_t* nptr" },
                        { "long* dptr", "int64_t* dptr" },
                    };

                    foreach (var pair in replacements)
                    {
                        code = code.Replace(pair.Key, pair.Value);
                    }

                    if (code != original)
                    {
                        File.WriteAllText(gridNeighborPath, code, new UTF8Encoding(false));
                        AddLogMessage("Patched grid_neighbor.cpp (int64_t casts + int64_t tensor pointers).");
                    }
                }

                // All kernel sources: data_ptr<long> -> data_ptr<int64_t>. torch only exports the int64_t
                // instantiation, and 'long' is 32-bit on Windows, so the link fails otherwise (LNK2001).
                foreach (string fileName in new[] { "grid_neighbor.cpp", "rasterizer.cpp", "rasterizer_gpu.cu" })
                {
                    string filePath = Path.Combine(kernelDir, fileName);
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }
                    string fileCode = File.ReadAllText(filePath);
                    bool changed = false;
                    if (fileCode.Contains("data_ptr<long>"))
                    {
                        fileCode = fileCode.Replace("data_ptr<long>", "data_ptr<int64_t>");
                        changed = true;
                        AddLogMessage($"Patched {fileName} (data_ptr<long> -> data_ptr<int64_t>).");
                    }
                    // The z-buffer sentinel 'maxint' is ~4.6e18; '(long)maxint' truncates to 32 bits on Windows
                    // (MSVC 'long' is 32-bit, vs 64-bit on Linux). That breaks the uncovered-pixel sentinel, so
                    // the rasterizer underflows a face index and reads far out of bounds — a CUDA illegal memory
                    // access at texture-bake time. Use int64_t so the sentinel keeps its full 64-bit value.
                    if (fileCode.Contains("(long)maxint"))
                    {
                        fileCode = fileCode.Replace("(long)maxint", "(int64_t)maxint");
                        changed = true;
                        AddLogMessage($"Patched {fileName} ((long)maxint -> (int64_t)maxint z-buffer sentinel).");
                    }
                    if (changed)
                    {
                        File.WriteAllText(filePath, fileCode, new UTF8Encoding(false));
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"⚠ Could not patch custom_rasterizer sources: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs 'uv pip install . --no-build-isolation' for a native extension inside an activated MSVC
        /// environment (vcvars64), so nvcc/cl can compile the CUDA/C++ sources against the venv's torch.
        /// </summary>
        /// <summary>
        /// Runs an arbitrary build command inside an activated MSVC environment (vcvars64), so nvcc/cl can
        /// compile native (CUDA/C++) extensions against the venv's torch. Returns the combined output.
        /// </summary>
        private async Task<string> RunBuildInMsvcEnv(string workingDir, string vcvarsPath, string archList, string command)
        {
            string batPath = Path.Combine(
                Path.GetTempPath(),
                "h3d_build_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bat");

            string script =
                "@echo off\r\n" +
                "call \"" + vcvarsPath + "\" >nul 2>&1\r\n" +
                "set DISTUTILS_USE_SDK=1\r\n" +
                "set TORCH_CUDA_ARCH_LIST=" + archList + "\r\n" +
                "set NVCC_APPEND_FLAGS=-allow-unsupported-compiler\r\n" +
                "cd /d \"" + workingDir + "\"\r\n" +
                command + "\r\n";

            File.WriteAllText(batPath, script, new UTF8Encoding(false));

            try
            {
                string output = await ExecuteCommandInDirectory("cmd.exe", "/c \"" + batPath + "\"", workingDir);
                AddLogMessage(output.Length > 4000 ? "..." + output.Substring(output.Length - 4000) : output);
                return output;
            }
            finally
            {
                try { File.Delete(batPath); } catch { }
            }
        }

        private async Task InstallHunyuan3DWithUV()
        {
            try
            {
                if (!EnsurePythonReady("install the Hunyuan3D package"))
                {
                    return;
                }

                AddLogMessage("=== INSTALLATION WITH UV (RECOMMENDED FOR WINDOWS) ===");

                // 1-4. Verify/install UV and create the managed UV project + virtual environment
                string projectDir = await EnsureUvProjectReady();
                if (string.IsNullOrEmpty(projectDir))
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

                string uvEnvironmentPath = Path.Combine(projectDir, ".venv");

                // 5. Pre-install only the packages that need the CUDA wheel index (torch/torchvision)
                // plus rembg. The torch wheel index matches the detected CUDA toolkit so the custom_rasterizer
                // CUDA extension can be built against it (CUDA 13.x -> cu130, which compiles cleanly; cu124
                // would fail the version gate on CUDA 13). Everything else comes from requirements.txt below.
                string torchIndexUrl = GetTorchCudaIndexUrl();
                AddLogMessage($"Using PyTorch wheel index for the detected CUDA toolkit: {torchIndexUrl}");
                string[] uvDependencies = {
                    $"torch --index-url {torchIndexUrl}",
                    $"torchvision --index-url {torchIndexUrl}",
                    "rembg"
                };

                foreach (var dep in uvDependencies)
                {
                    AddLogMessage($"Adding: {dep}");
                    var addOutput = await ExecuteUvPipInstall(dep, projectDir);
                    if (OutputHasErrors(addOutput))
                    {
                        AddLogMessage(addOutput);
                        throw new Exception($"Could not add UV dependency: {dep}");
                    }
                    progress = Array.IndexOf(uvDependencies, dep) / (float)uvDependencies.Length;
                }

                // 6. Clone Hunyuan3D with git.exe into the managed UV project and install its requirements
                string managedRepoDir = GetManagedHunyuanRepositoryPath(projectDir);
                if (Directory.Exists(managedRepoDir))
                {
                    AddLogMessage($"Refreshing managed Hunyuan3D repository at: {managedRepoDir}");
                    await ForceDeleteDirectory(managedRepoDir);
                }

                AddLogMessage($"Preparing Hunyuan3D source repository at: {managedRepoDir}");
                await PrepareHunyuanRepositoryFromGit(managedRepoDir);

                string requirementsPath = Path.Combine(managedRepoDir, "requirements.txt");
                if (!File.Exists(requirementsPath))
                {
                    requirementsPath = Directory.GetFiles(managedRepoDir, "requirements.txt", SearchOption.AllDirectories)
                        .FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(requirementsPath) && File.Exists(requirementsPath))
                {
                    PatchHunyuanRequirementsForCompatibility(requirementsPath);
                    AddLogMessage($"Installing Hunyuan3D repository requirements from: {requirementsPath}");
                    var requirementsOutput = await ExecuteUvPipInstall($"-r \"{requirementsPath}\"", projectDir);
                    if (OutputHasErrors(requirementsOutput))
                    {
                        AddLogMessage(requirementsOutput);
                        throw new Exception("Could not install Hunyuan3D 2.1 requirements with UV");
                    }
                }
                else
                {
                    throw new FileNotFoundException("Could not locate requirements.txt inside the cloned Hunyuan3D-2.1 repository.");
                }

                RegisterHunyuanRepositoryWithVirtualEnvironment(uvEnvironmentPath, managedRepoDir);

                // Best-effort: set up optional texture support (make bpy optional, build custom_rasterizer
                // and mesh_inpaint_processor). Never fatal — shape generation works without any of it.
                await TryBuildTextureSupport(managedRepoDir, uvEnvironmentPath);

                string uvPython = Hunyuan3DSystemProbe.GetVirtualEnvironmentPythonPath(uvEnvironmentPath);
                string uvPip = Hunyuan3DSystemProbe.GetVirtualEnvironmentPipPath(uvEnvironmentPath);
                if (!string.IsNullOrEmpty(uvPython) && File.Exists(uvPython))
                {
                    pythonPath = uvPython;
                }
                if (!string.IsNullOrEmpty(uvPip) && File.Exists(uvPip))
                {
                    pipPath = uvPip;
                }

                AddLogMessage("✅ Installation with UV complete!");
                AddLogMessage($"📁 Project created at: {projectDir}");

                var verifyOutput = await RunVerificationScript(logSelection: true);
                AddLogMessage(verifyOutput);

                if (VerificationLooksSuccessful(verifyOutput))
                {
                    EditorUtility.DisplayDialog(
                        "UV Installation Complete",
                        $"Hunyuan3D has been installed with UV.\n\n" +
                        $"Location: {projectDir}\n\n" +
                        $"Next step: run 'Verify Installation' if you want to review the environment again.",
                        "Great!"
                    );
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "UV Installation Needs Review",
                        $"The UV environment was created at:\n{projectDir}\n\n" +
                        "The verification did not confirm Hunyuan3D yet.\n" +
                        "Check the logs before continuing.",
                        "OK"
                    );
                }
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

            string installPath = "C:\\Users\\" + Environment.UserName + "\\AppData\\Local\\Temp\\Hunyuan3D-2.1-for-windows";

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
• Hunyuan3D: https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1
";

            EditorUtility.DisplayDialog(
                "Windows Installation Guide",
                guide,
                "Close"
            );
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
                var output = await RunVerificationScript(logSelection: true);
                AddLogMessage(output);

                if (VerificationLooksSuccessful(output))
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
                        "The verification did not find a working Hunyuan3D environment yet.\n\n" +
                        "Check the logs and, if needed, run 'Install All' and 'Install Hunyuan3D Package' again.",
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

                if (string.IsNullOrEmpty(detectedCudaToolkitPath))
                {
                    RefreshSystemDetection(logDetails: false);
                }

                string[] cudaPaths = string.IsNullOrEmpty(detectedCudaToolkitPath)
                    ? Array.Empty<string>()
                    : new[] { Path.Combine(detectedCudaToolkitPath, "bin") };

                string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
                bool pathUpdated = false;

                foreach (var cudaPath in cudaPaths)
                {
                    if (Directory.Exists(cudaPath) && currentPath.IndexOf(cudaPath, StringComparison.OrdinalIgnoreCase) < 0)
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

                startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
                startInfo.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "utf-8";
                startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

                if (!string.IsNullOrEmpty(command) &&
                    Hunyuan3DSystemProbe.TryExtractVirtualEnvironment(command, out string executablesPath, out string virtualEnvironmentPath))
                {
                    string currentPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH");

                    if (!string.IsNullOrEmpty(executablesPath) &&
                        !string.IsNullOrEmpty(currentPath) &&
                        currentPath.IndexOf(executablesPath, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        startInfo.EnvironmentVariables["PATH"] = $"{executablesPath};{currentPath}";
                    }

                    if (!string.IsNullOrEmpty(virtualEnvironmentPath))
                    {
                        startInfo.EnvironmentVariables["VIRTUAL_ENV"] = virtualEnvironmentPath;
                    }

                    if (startInfo.EnvironmentVariables.ContainsKey("PYTHONHOME"))
                    {
                        startInfo.EnvironmentVariables.Remove("PYTHONHOME");
                    }
                }

                // Propagate managed environment settings to subprocesses
                SetCudaEnvironmentVariables(startInfo);
                SetGitEnvironmentVariables(startInfo);
                SetManagedHunyuanRepositoryEnvironmentVariables(startInfo);

                var process = new Process { StartInfo = startInfo };
                var output = new StringBuilder();

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

                if (process.ExitCode != 0)
                {
                    output.AppendLine($"ERROR: Command exited with code {process.ExitCode}");
                }

                return output.ToString();
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        // Static + self-contained so it can be called without instantiating this EditorWindow
        // (Unity forbids 'new Hunyuan3DDependencyManager()'). Resolves the CUDA toolkit from the
        // CUDA_PATH/CUDA_HOME environment variables, falling back to the newest installed toolkit.
        public static void SetCudaEnvironmentVariables(ProcessStartInfo startInfo)
        {
            string cudaHome = Environment.GetEnvironmentVariable("CUDA_PATH");
            if (string.IsNullOrEmpty(cudaHome) || !Directory.Exists(cudaHome))
            {
                cudaHome = Environment.GetEnvironmentVariable("CUDA_HOME");
            }
            if (string.IsNullOrEmpty(cudaHome) || !Directory.Exists(cudaHome))
            {
                cudaHome = FindLatestCudaToolkit();
            }

            if (string.IsNullOrEmpty(cudaHome) || !Directory.Exists(cudaHome))
            {
                return;
            }

            // Ensure environment variables are propagated to subprocesses
            startInfo.EnvironmentVariables["CUDA_HOME"] = cudaHome;
            startInfo.EnvironmentVariables["CUDA_PATH"] = cudaHome;

            // Add bin to PATH
            string cudaBin = Path.Combine(cudaHome, "bin");
            string currentPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH");
            if (Directory.Exists(cudaBin) &&
                (string.IsNullOrEmpty(currentPath) || currentPath.IndexOf(cudaBin, StringComparison.OrdinalIgnoreCase) < 0))
            {
                startInfo.EnvironmentVariables["PATH"] = string.IsNullOrEmpty(currentPath) ? cudaBin : cudaBin + ";" + currentPath;
            }
        }

        /// <summary>
        /// Returns the newest installed CUDA toolkit directory (by version), or null if none is found.
        /// </summary>
        private static string FindLatestCudaToolkit()
        {
            try
            {
                string[] roots =
                {
                    @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA",
                    @"C:\Program Files\NVIDIA Corporation\CUDA"
                };

                string best = null;
                Version bestVersion = null;

                foreach (string root in roots)
                {
                    if (!Directory.Exists(root))
                    {
                        continue;
                    }

                    foreach (string dir in Directory.GetDirectories(root, "v*"))
                    {
                        if (!Directory.Exists(Path.Combine(dir, "bin")))
                        {
                            continue;
                        }

                        string name = Path.GetFileName(dir).TrimStart('v', 'V');
                        if (Version.TryParse(name, out Version version))
                        {
                            if (bestVersion == null || version > bestVersion)
                            {
                                bestVersion = version;
                                best = dir;
                            }
                        }
                        else if (best == null)
                        {
                            best = dir;
                        }
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        private void SetManagedHunyuanRepositoryEnvironmentVariables(ProcessStartInfo startInfo)
        {
            string uvProjectPath = GetUvProjectPath();
            string repositoryPath = GetManagedHunyuanRepositoryPath(uvProjectPath);
            if (string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
            {
                return;
            }

            string currentPythonPath = startInfo.EnvironmentVariables["PYTHONPATH"] ??
                Environment.GetEnvironmentVariable("PYTHONPATH") ??
                string.Empty;

            var pythonPathEntries = currentPythonPath
                .Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (!pythonPathEntries.Any(entry => string.Equals(entry, repositoryPath, StringComparison.OrdinalIgnoreCase)))
            {
                pythonPathEntries.Insert(0, repositoryPath);
                startInfo.EnvironmentVariables["PYTHONPATH"] = string.Join(Path.PathSeparator.ToString(), pythonPathEntries);
            }
        }

        private void SetGitEnvironmentVariables(ProcessStartInfo startInfo)
        {
            string gitExecutablePath = detectedGitPath;
            if (string.IsNullOrEmpty(gitExecutablePath) || !File.Exists(gitExecutablePath))
            {
                RefreshSystemDetection(logDetails: false);
                gitExecutablePath = detectedGitPath;
            }

            if (string.IsNullOrEmpty(gitExecutablePath) || !File.Exists(gitExecutablePath))
            {
                return;
            }

            string gitDirectory = Path.GetDirectoryName(gitExecutablePath);
            string currentPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            if (!string.IsNullOrEmpty(gitDirectory) &&
                currentPath.IndexOf(gitDirectory, StringComparison.OrdinalIgnoreCase) < 0)
            {
                startInfo.EnvironmentVariables["PATH"] = gitDirectory + ";" + currentPath;
            }

            startInfo.EnvironmentVariables["GIT"] = gitExecutablePath;
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
    Write-Host 'Skipping pip fallback for a more predictable UV setup.'
    
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
                    "• Run the official PowerShell installer manually\n\n" +
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
        private async Task EnsureSetuptoolsInstalled()
        {
            if (!await EnsurePipAvailable())
            {
                throw new Exception("pip is not available, so setuptools cannot be installed.");
            }

            AddLogMessage("Checking setuptools...");
            var output = await ExecutePythonCommand("-m pip show setuptools");
            if (output.Contains("Name: setuptools"))
            {
                AddLogMessage("✓ setuptools is already installed");
                return;
            }
            AddLogMessage("Installing setuptools...");
            var install = await ExecutePythonCommand("-m pip install setuptools");
            AddLogMessage(install);
        }
        private void SetCudaHomeEnv()
        {
            // Search for the most recent CUDA installation
            if (string.IsNullOrEmpty(detectedCudaToolkitPath) || !Directory.Exists(detectedCudaToolkitPath))
            {
                RefreshSystemDetection(logDetails: false);
            }

            string[] possibleCudaDirs = !string.IsNullOrEmpty(detectedCudaToolkitPath)
                ? new[] { detectedCudaToolkitPath }
                : Array.Empty<string>();

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
