using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Hunyuan3D.Editor
{
    /// <summary>
    /// Unity plugin to generate 3D models from images using Hunyuan3D-2
    /// Integrates Python scripts batch_hunyuan3d.py and remove_background.py
    /// </summary>
    public class Hunyuan3DGenerator : EditorWindow
    {
        #region UI Variables
        private string selectedImagePath = "";
        private string outputFolder = "Assets/Generated3DModels";
        private bool batchMode = false;
        
        // Persistent configuration
        private Hunyuan3DConfig config;
        
        // State control
        private bool isProcessing = false;
        private string statusMessage = "";
        private float progress = 0f;
        private List<string> logMessages = new List<string>();
        private string detectedPythonVersion = "";
        
        // Scroll for logs
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 windowScrollPosition = Vector2.zero;
        
        // File type options
        private readonly string[] fileTypeOptions = { "obj", "fbx", "glb", "ply", "stl" };
        private readonly string[] deviceOptions = { "cuda", "cpu", "mps" };
        
        // Model Path options — Hunyuan3D-2.1 ships a single Image-to-Shape model
        // (see the repo README: tencent/Hunyuan3D-2.1 / hunyuan3d-dit-v2-1).
        private readonly string[] modelPathOptions = {
            "tencent/Hunyuan3D-2.1"
        };

        // Subfolder options — the 2.1 shape DiT lives in hunyuan3d-dit-v2-1
        private readonly string[] subfolderOptions = {
            "hunyuan3d-dit-v2-1"
        };

        // Texture Model Path options — 2.1 paint model (hunyuan3d-paintpbr-v2-1) is loaded from this repo
        private readonly string[] textureModelPathOptions = {
            "tencent/Hunyuan3D-2.1"
        };
        #endregion

        #region Unity Menu
        [MenuItem("Tools/Hunyuan3D/3D Model Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<Hunyuan3DGenerator>("Hunyuan3D Generator");
            window.minSize = new Vector2(500, 500);
            window.maxSize = new Vector2(800, 1024);
            window.Initialize();
        }
        
        [MenuItem("Tools/Hunyuan3D/Dependency Manager")]
        public static void ShowDependencyManager()
        {
            Hunyuan3DDependencyManager.ShowWindow();
        }
        #endregion

        #region Initialization
        private void Initialize()
        {
            // Load persistent configuration
            config = Hunyuan3DConfig.Load();
            
            // Automatically detect the installed environment (managed UV venv Python + the plugin's
            // Scripts folder) without overwriting a valid manual configuration.
            DetectInstalledEnvironment(force: false, logResults: true);

            if (string.IsNullOrEmpty(config.scriptBasePath))
            {
                AddLogMessage("Warning: Python scripts not found automatically.");
                AddLogMessage("Please specify the Script Base Path manually with the '...' button.");
            }
            
            // Create output folder if it doesn't exist
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
                AssetDatabase.Refresh();
            }
        }
        #endregion

        #region GUI
        private void OnGUI()
        {
            // Wrap the whole window in a scroll view so nothing is cut off when the window is short.
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hunyuan3D Model Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawPathConfiguration();
            EditorGUILayout.Space(10);

            DrawInputSelection();
            EditorGUILayout.Space(10);

            DrawModelParameters();
            EditorGUILayout.Space(10);

            DrawGenerationParameters();
            EditorGUILayout.Space(10);

            DrawOptions();
            EditorGUILayout.Space(10);

            DrawProcessingControls();
            EditorGUILayout.Space(10);

            DrawProgressAndLogs();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPathConfiguration()
        {
            EditorGUILayout.LabelField("Path Configuration", EditorStyles.boldLabel);

            // Auto-detect the installed environment (managed UV venv Python + scripts) and its version.
            if (GUILayout.Button("🔍 Detect Installed Environment", GUILayout.Height(25)))
            {
                DetectInstalledEnvironment(force: true, logResults: true);
            }

            EditorGUILayout.BeginHorizontal();
            config.pythonExecutablePath = EditorGUILayout.TextField("Python Executable:", config.pythonExecutablePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Select Python", "", "exe");
                if (!string.IsNullOrEmpty(path))
                {
                    config.pythonExecutablePath = path;
                    detectedPythonVersion = GetPythonVersion(path);
                    config.Save();
                }
            }
            EditorGUILayout.EndHorizontal();

            // Show the detected Python version and whether we are using a virtual environment
            if (!string.IsNullOrEmpty(detectedPythonVersion))
            {
                EditorGUILayout.LabelField("Detected Version:", detectedPythonVersion);
            }
            if (!string.IsNullOrEmpty(config.pythonExecutablePath) && config.pythonExecutablePath.Contains(".venv"))
            {
                EditorGUILayout.HelpBox("✅ Using Python from virtual environment", MessageType.Info);
            }
            
            EditorGUILayout.BeginHorizontal();
            config.scriptBasePath = EditorGUILayout.TextField("Script Base Path:", config.scriptBasePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select scripts folder", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    config.scriptBasePath = path;
                    config.Save();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Check if scripts exist
            if (!string.IsNullOrEmpty(config.scriptBasePath))
            {
                string batchScript = Path.Combine(config.scriptBasePath, "batch_hunyuan3d.py");
                string rembgScript = Path.Combine(config.scriptBasePath, "remove_background.py");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("batch_hunyuan3d.py:", File.Exists(batchScript) ? "✓" : "✗");
                EditorGUILayout.LabelField("remove_background.py:", File.Exists(rembgScript) ? "✓" : "✗");
                EditorGUILayout.EndHorizontal();
            }
            
            // Button to save configuration
            if (GUILayout.Button("Save Configuration"))
            {
                config.Save();
                AddLogMessage("✓ Configuration saved.");
            }
        }

        private IEnumerable<string> GetPowerShellInstallRoots()
        {
            string localTemp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Temp"
            );

            return new[]
            {
                Path.Combine(localTemp, "Hunyuan3D-2.1-for-windows"),
                Path.Combine(localTemp, "Hunyuan3D-2.1"),
                Path.Combine(localTemp, "Hunyuan3D-2-for-windows"),
                Path.Combine(localTemp, "Hunyuan2-3D-for-windows")
            }.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private string FindPowerShellInstallRoot()
        {
            return GetPowerShellInstallRoots().FirstOrDefault(Directory.Exists);
        }

        private bool LooksLikeHunyuanRepository(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }

            return Directory.Exists(Path.Combine(path, "hy3dshape")) ||
                   Directory.Exists(Path.Combine(path, "hy3dpaint")) ||
                   Directory.Exists(Path.Combine(path, "hy3dgen"));
        }

        private string FindHunyuanRepositoryRoot()
        {
            var candidates = new List<string>();

            string powerShellRoot = FindPowerShellInstallRoot();
            if (!string.IsNullOrEmpty(powerShellRoot))
            {
                candidates.Add(powerShellRoot);
                candidates.Add(Path.Combine(powerShellRoot, "Hunyuan3D-2.1"));
                candidates.Add(Path.Combine(powerShellRoot, "Hunyuan3D-2"));
            }

            if (!string.IsNullOrEmpty(config?.scriptBasePath))
            {
                candidates.Add(config.scriptBasePath);
                candidates.Add(Path.Combine(config.scriptBasePath, "Hunyuan3D-2.1"));
                candidates.Add(Path.Combine(config.scriptBasePath, "Hunyuan3D-2"));

                string scriptParent = Directory.GetParent(config.scriptBasePath)?.FullName;
                if (!string.IsNullOrEmpty(scriptParent))
                {
                    candidates.Add(scriptParent);
                }
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                candidates.Add(projectRoot);
                candidates.Add(Path.Combine(projectRoot, "UnityPlugin", "Scripts"));
            }

            return candidates
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(LooksLikeHunyuanRepository);
        }

        private void DrawInputSelection()
        {
            EditorGUILayout.LabelField("Input Selection", EditorStyles.boldLabel);
            
            batchMode = EditorGUILayout.Toggle("Batch Mode (folder)", batchMode);
            
            EditorGUILayout.BeginHorizontal();
            if (batchMode)
            {
                selectedImagePath = EditorGUILayout.TextField("Image folder:", selectedImagePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select image folder", "", "");
                    if (!string.IsNullOrEmpty(path))
                        selectedImagePath = path;
                }
            }
            else
            {
                selectedImagePath = EditorGUILayout.TextField("Image:", selectedImagePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFilePanel("Select image", "", "jpg,jpeg,png,bmp,webp,tiff");
                    if (!string.IsNullOrEmpty(path))
                        selectedImagePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output folder:", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select output folder", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModelParameters()
        {
            EditorGUILayout.LabelField("Model Parameters", EditorStyles.boldLabel);
            
            // Model Path dropdown
            int modelPathIndex = System.Array.IndexOf(modelPathOptions, config.modelPath);
            if (modelPathIndex == -1) modelPathIndex = 0; // Default to Hunyuan3D-2.1
            modelPathIndex = EditorGUILayout.Popup("Model Path:", modelPathIndex, modelPathOptions);
            config.modelPath = modelPathOptions[modelPathIndex];
            
            // Subfolder dropdown
            int subfolderIndex = System.Array.IndexOf(subfolderOptions, config.subfolder);
            if (subfolderIndex == -1) subfolderIndex = 0; // Default to hunyuan3d-dit-v2-1
            subfolderIndex = EditorGUILayout.Popup("Subfolder:", subfolderIndex, subfolderOptions);
            config.subfolder = subfolderOptions[subfolderIndex];
            
            // Texture Model Path dropdown
            int textureModelPathIndex = System.Array.IndexOf(textureModelPathOptions, config.texgenModelPath);
            if (textureModelPathIndex == -1) textureModelPathIndex = 0; // Default to first option
            textureModelPathIndex = EditorGUILayout.Popup("Texture Model Path:", textureModelPathIndex, textureModelPathOptions);
            config.texgenModelPath = textureModelPathOptions[textureModelPathIndex];
            
            int deviceIndex = System.Array.IndexOf(deviceOptions, config.device);
            if (deviceIndex == -1) deviceIndex = 0;
            deviceIndex = EditorGUILayout.Popup("Device:", deviceIndex, deviceOptions);
            config.device = deviceOptions[deviceIndex];
            
            // MC Algorithm is only consumed when FlashVDM is enabled (it is passed to enable_flashvdm);
            // hide it otherwise so it does not look like an active control.
            if (config.enableFlashVDM)
            {
                config.mcAlgo = EditorGUILayout.TextField("MC Algorithm (FlashVDM):", config.mcAlgo);
            }
            
            // Show selected configuration for reference
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Selected Configuration:", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Model: {config.modelPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Subfolder: {config.subfolder}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Texture: {config.texgenModelPath}", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawGenerationParameters()
        {
            EditorGUILayout.LabelField("Generation Parameters", EditorStyles.boldLabel);
            
            config.steps = EditorGUILayout.IntSlider("Steps:", config.steps, 1, 100);
            config.guidanceScale = EditorGUILayout.Slider("Guidance Scale:", config.guidanceScale, 1f, 20f);
            config.seed = EditorGUILayout.IntField("Seed:", config.seed);
            config.octreeResolution = EditorGUILayout.IntSlider("Octree Resolution:", config.octreeResolution, 64, 512);
            config.numChunks = EditorGUILayout.IntField("Num Chunks:", config.numChunks);
            
            int fileTypeIndex = System.Array.IndexOf(fileTypeOptions, config.fileType);
            if (fileTypeIndex == -1) fileTypeIndex = 0;
            fileTypeIndex = EditorGUILayout.Popup("File Type:", fileTypeIndex, fileTypeOptions);
            config.fileType = fileTypeOptions[fileTypeIndex];
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            config.enableT23D = EditorGUILayout.Toggle("Enable Text-to-3D", config.enableT23D);
            config.disableTexture = EditorGUILayout.Toggle("Disable Texture", config.disableTexture);
            EditorGUILayout.EndHorizontal();
            
            // torch.compile depends on Triton, which is not usable on Windows, so only expose it elsewhere.
            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;
            EditorGUILayout.BeginHorizontal();
            config.enableFlashVDM = EditorGUILayout.Toggle("Enable FlashVDM", config.enableFlashVDM);
            if (!isWindows)
            {
                config.compile = EditorGUILayout.Toggle("Compile Model", config.compile);
            }
            else
            {
                config.compile = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            config.lowVramMode = EditorGUILayout.Toggle("Low VRAM Mode", config.lowVramMode);
            config.removeBackground = EditorGUILayout.Toggle("Remove Background", config.removeBackground);
            EditorGUILayout.EndHorizontal();

            // Text-to-3D prompt: HunyuanDiT generates an image from this text, then image -> 3D runs.
            if (config.enableT23D)
            {
                EditorGUILayout.Space(3);
                config.textPrompt = EditorGUILayout.TextField("Text Prompt:", config.textPrompt);
                EditorGUILayout.HelpBox(
                    "Text-to-3D ignores the image input above and downloads the HunyuanDiT model " +
                    "(~8 GB) on first use.", MessageType.Info);
            }
        }

        private void DrawProcessingControls()
        {
            EditorGUILayout.LabelField("Processing Control", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(isProcessing);
            if (GUILayout.Button(batchMode ? "Process Folder" : "Generate 3D Model", GUILayout.Height(30)))
            {
                if (ValidateInputs())
                {
                    _ = ProcessImages();
                }
            }
            EditorGUI.EndDisabledGroup();
            
            if (isProcessing)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Status:", statusMessage);
                EditorGUILayout.Space(2);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, $"{(progress * 100):F1}%");
            }
        }

        private void DrawProgressAndLogs()
        {
            EditorGUILayout.LabelField("Installation Logs", EditorStyles.boldLabel);

            // Convert log messages to a single string
            string logContent = string.Join("\n", logMessages);

            // Create a custom style for the TextArea
            GUIStyle logStyle = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap = true,
                richText = false, // Disable richText to avoid issues with selection
                fontSize = 11,
                padding = new RectOffset(5, 5, 5, 5)
            };

            // ScrollView for logs with dynamic height
            float availableHeight = position.height - 450; // Account for other UI elements
            float logHeight = Mathf.Clamp(availableHeight, 150, 300); // Min 150, max 300
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(logHeight));

            // Capture focus to allow Ctrl+C
            GUI.SetNextControlName("LogTextArea");

            // TextArea that allows selection and copy
            string newLogContent = EditorGUILayout.TextArea(logContent, logStyle, GUILayout.ExpandHeight(true));

            // If the user has focused the TextArea, process Ctrl+C
            if (GUI.GetNameOfFocusedControl() == "LogTextArea")
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.C)
                {
                    // Unity already handles Ctrl+C automatically for TextArea
                    // but we can add visual feedback if we want
                    EditorGUIUtility.systemCopyBuffer = EditorGUIUtility.systemCopyBuffer; // Force update
                }
            }

            EditorGUILayout.EndScrollView();

            // Information about the functionality
            EditorGUILayout.HelpBox("You can select text from the log and copy it with Ctrl+C", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Logs"))
            {
                logMessages.Clear();
            }
            if (GUILayout.Button("Copy All Logs"))
            {
                CopyLogsToClipboard();
            }

            // Additional button to copy the current selection
            if (GUILayout.Button("Copy Selection"))
            {
                TextEditor textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                if (textEditor != null && textEditor.hasSelection)
                {
                    EditorGUIUtility.systemCopyBuffer = textEditor.SelectedText;
                    AddLogMessage("✓ Selection copied to clipboard");
                }
                else
                {
                    AddLogMessage("⚠ No text selected");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CopyLogsToClipboard()
        {
            string allLogs = string.Join("\n", logMessages);
            GUIUtility.systemCopyBuffer = allLogs;
            AddLogMessage("Logs copied to clipboard!");
        }
        #endregion

        #region Validació
        private bool ValidateInputs()
        {
            // Text-to-3D mode: no image/folder is needed, but a prompt is required.
            if (config.enableT23D)
            {
                if (string.IsNullOrEmpty(config.textPrompt))
                {
                    AddLogMessage("Error: Enable Text-to-3D is on — please enter a Text Prompt.");
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(selectedImagePath))
                {
                    AddLogMessage("Error: Please select an image or folder.");
                    return false;
                }

                if (batchMode && !Directory.Exists(selectedImagePath))
                {
                    AddLogMessage("Error: The specified folder does not exist.");
                    return false;
                }

                if (!batchMode && !File.Exists(selectedImagePath))
                {
                    AddLogMessage("Error: The specified image does not exist.");
                    return false;
                }
            }

            // Use the configuration validation
            string errorMessage;
            if (!config.IsValid(out errorMessage))
            {
                AddLogMessage($"Configuration error: {errorMessage}");
                return false;
            }
            
            return true;
        }
        #endregion

        #region Processament Principal
        private async Task ProcessImages()
        {
            isProcessing = true;
            progress = 0f;
            statusMessage = "Starting processing...";

            try
            {
                // Verify installation first
                if (!await VerifyHunyuan3DInstallation())
                {
                    EditorUtility.DisplayDialog(
                        "Installation Error",
                        "Could not find the Hunyuan3D modules.\n\n" +
                        "Make sure that:\n" +
                        "1. You have run the Dependency Manager\n" +
                        "2. The scripts path points to the correct directory\n" +
                        "3. Hunyuan3D is installed correctly\n\n" +
                        "Try clicking 'Detect Virtual Environment' first.",
                        "OK"
                    );
                    return;
                }

                // Create absolute output folder
                string absoluteOutputPath = Path.GetFullPath(outputFolder);
                if (!Directory.Exists(absoluteOutputPath))
                {
                    Directory.CreateDirectory(absoluteOutputPath);
                }

                // Execute batch_hunyuan3d.py
                progress = 0.3f;
                statusMessage = "Generating 3D model...";

                bool success = await ExecuteHunyuan3DScript(selectedImagePath, absoluteOutputPath);

                if (success)
                {
                    progress = 0.9f;
                    statusMessage = "Importing assets to Unity...";

                    // Import new assets
                    AssetDatabase.Refresh();

                    // Find and select the new models
                    await SelectGeneratedModels(absoluteOutputPath);

                    progress = 1f;
                    statusMessage = "Processing completed!";
                    AddLogMessage("✓ Processing completed successfully!");
                }
                else
                {
                    AddLogMessage("✗ Error during processing.");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"✗ Error: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                statusMessage = "";
            }
        }        

        private async Task<string> PreprocessImage(string imagePath)
        {
            string rembgScript = Path.Combine(config.scriptBasePath, "remove_background.py");
            if (!File.Exists(rembgScript))
            {
                AddLogMessage("Warning: remove_background.py not found. Skipping preprocessing.");
                return imagePath;
            }
            
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string extension = Path.GetExtension(imagePath);
            string outputPath = Path.Combine(Path.GetTempPath(), $"{fileName}_nobg{extension}");
            
            string arguments = $"\"{rembgScript}\" \"{imagePath}\" \"{outputPath}\"";
            
            AddLogMessage($"Executing: {config.pythonExecutablePath} {arguments}");
            
            bool success = await ExecutePythonScript(arguments);
            
            if (success && File.Exists(outputPath))
            {
                AddLogMessage("✓ Background removed successfully.");
                return outputPath;
            }
            
            AddLogMessage("✗ Error removing background. Using original image.");
            return imagePath;
        }

        private async Task<bool> ExecuteHunyuan3DScript(string inputPath, string outputPath)
        {
            string batchScript = Path.Combine(config.scriptBasePath, "batch_hunyuan3d.py");
            
            bool textMode = config.enableT23D && !string.IsNullOrEmpty(config.textPrompt);

            // Build arguments following the script structure
            List<string> args = new List<string>
            {
                $"\"{batchScript}\""
            };

            // Positional input: image/folder for image-to-3D, omitted for text-to-3D
            if (!textMode)
            {
                args.Add($"\"{inputPath}\"");
            }

            args.Add($"--output \"{outputPath}\"");
            args.Add($"--model_path \"{config.modelPath}\"");
            args.Add($"--subfolder \"{config.subfolder}\"");
            args.Add($"--texgen_model_path \"{config.texgenModelPath}\"");
            args.Add($"--device {config.device}");
            args.Add($"--mc_algo {config.mcAlgo}");
            args.Add($"--steps {config.steps}");
            args.Add($"--guidance_scale {config.guidanceScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--seed {config.seed}");
            args.Add($"--octree_resolution {config.octreeResolution}");
            args.Add($"--num_chunks {config.numChunks}");
            args.Add($"--file_type {config.fileType}");

            // Add optional flags
            if (config.enableT23D) args.Add("--enable_t23d");
            if (textMode) args.Add($"--caption \"{config.textPrompt}\"");
            if (config.disableTexture) args.Add("--disable_tex");
            if (config.enableFlashVDM) args.Add("--enable_flashvdm");
            if (config.compile) args.Add("--compile");
            if (config.lowVramMode) args.Add("--low_vram_mode");
            if (config.removeBackground) args.Add("--remove_background");
            else args.Add("--skip_background_removal");
            
            string arguments = string.Join(" ", args);
            
            AddLogMessage($"Executing: {config.pythonExecutablePath} {arguments}");
            
            return await ExecutePythonScript(arguments);
        }

        private async Task<bool> ExecutePythonScript(string arguments)
        {
            try
            {
                // Set console code page to UTF-8 for Windows
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    try
                    {
                        // Change console code page to UTF-8 (65001)
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c chcp 65001 > nul",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        process.Start();
                        process.WaitForExit(1000);
                    }
                    catch
                    {
                        // Ignore errors in code page setting
                    }
                }

                // Detect if a virtual environment exists in the project
                string venvPath = DetectVirtualEnvironment();
                string pythonExe = config.pythonExecutablePath;
                string actualCommand = pythonExe;
                string actualArguments = arguments;

                // Check if UV is available
                bool useUV = await CheckUVAvailable();

                if (!string.IsNullOrEmpty(venvPath))
                {
                    // Use Python from the venv if it exists
                    string venvPython = Path.Combine(venvPath, "Scripts", "python.exe");
                    if (File.Exists(venvPython))
                    {
                        pythonExe = venvPython;
                        actualCommand = venvPython;
                        AddLogMessage($"🐍 Using Python from venv directly: {venvPython}");
                    }
                }
                else if (useUV)
                {
                    // If there is no venv but we have UV, use it anyway
                    actualCommand = "uv";
                    actualArguments = $"run python {arguments}";
                    AddLogMessage($"🚀 Using UV to run Python");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = actualCommand,
                    Arguments = actualArguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = config.scriptBasePath
                };

                // Set necessary environment variables
                SetPythonEnvironmentVariables(startInfo, venvPath);

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;

                    // Buffer per acumular output
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();

                    // Capturar output en temps real
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            MainThreadExecutor.RunOnMainThread(() =>
                            {
                                AddLogMessage($"[OUT] {e.Data}");
                                UpdateStatusFromOutput(e.Data);
                            });
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                            MainThreadExecutor.RunOnMainThread(() =>
                            {
                                AddLogMessage($"[ERR] {e.Data}");

                                // Detectar errors d'importació
                                if (e.Data.Contains("ModuleNotFoundError") || e.Data.Contains("ImportError"))
                                {
                                    AddLogMessage("❌ Error: Modules not found!");
                                    AddLogMessage("   Run the Dependency Manager first");
                                }
                            });
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Esperar amb timeout
                    bool completed = await Task.Run(() => process.WaitForExit(1200000)); // 20 minuts

                    if (!completed)
                    {
                        AddLogMessage("⚠️ Timeout: El procés ha trigat massa temps.");
                        try { process.Kill(); } catch { }
                        return false;
                    }

                    bool success = process.ExitCode == 0;
                    AddLogMessage($"Procés finalitzat amb codi: {process.ExitCode}");

                    if (!success && errorBuilder.Length > 0)
                    {
                        AddLogMessage($"Errors:\n{errorBuilder}");
                    }

                    return success;
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error executing script: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CheckUVAvailable()
        {
            foreach (string uvCommand in new[] { "uv", "uv.exe" })
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = uvCommand,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (Process process = new Process())
                    {
                        process.StartInfo = startInfo;
                        process.Start();

                        string output = await Task.Run(() => process.StandardOutput.ReadToEnd());
                        await Task.Run(() => process.WaitForExit(3000));

                        bool isAvailable = process.ExitCode == 0 && output.Contains("uv");

                        if (isAvailable)
                        {
                            AddLogMessage($"✅ UV available: {output.Trim()}");
                            return true;
                        }
                    }
                }
                catch
                {
                    // Try the next executable name.
                }
            }

            return false;
        }

        /// <summary>
        /// Auto-detects the installed environment: the managed UV virtual environment's Python and the
        /// plugin's Scripts folder, plus the Python version. With force=false it only fills values that are
        /// empty or invalid (preserving a manual configuration); with force=true it always re-detects.
        /// </summary>
        private void DetectInstalledEnvironment(bool force, bool logResults)
        {
            // Script base path: the plugin's Scripts folder under Assets/
            string assetsScripts = Path.Combine(Application.dataPath, "UnityPlugin", "Scripts");
            bool scriptsValid = !string.IsNullOrEmpty(config.scriptBasePath) &&
                                File.Exists(Path.Combine(config.scriptBasePath, "batch_hunyuan3d.py"));
            if ((force || !scriptsValid) && File.Exists(Path.Combine(assetsScripts, "batch_hunyuan3d.py")))
            {
                config.scriptBasePath = assetsScripts;
            }

            // Python executable: prefer the managed virtual environment
            bool pythonValid = !string.IsNullOrEmpty(config.pythonExecutablePath) &&
                               config.pythonExecutablePath != "python" &&
                               File.Exists(config.pythonExecutablePath);
            if (force || !pythonValid)
            {
                string venvPath = DetectVirtualEnvironment();
                if (!string.IsNullOrEmpty(venvPath))
                {
                    string venvPython = Path.Combine(venvPath, "Scripts", "python.exe");
                    if (File.Exists(venvPython))
                    {
                        config.pythonExecutablePath = venvPython;
                    }
                }
            }

            detectedPythonVersion = GetPythonVersion(config.pythonExecutablePath);
            config.Save();

            if (logResults)
            {
                AddLogMessage($"🐍 Python: {config.pythonExecutablePath}" +
                              (string.IsNullOrEmpty(detectedPythonVersion) ? "" : $" ({detectedPythonVersion})"));
                AddLogMessage($"📁 Script Base Path: {(string.IsNullOrEmpty(config.scriptBasePath) ? "(not set)" : config.scriptBasePath)}");
            }
        }

        /// <summary>
        /// Returns the version string (e.g. "Python 3.10.20") of a Python executable, or "" if it fails.
        /// </summary>
        private string GetPythonVersion(string pythonExecutable)
        {
            if (string.IsNullOrEmpty(pythonExecutable))
            {
                return "";
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutable,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(4000);

                    string combined = (stdout + " " + stderr).Trim();
                    int index = combined.IndexOf("Python", StringComparison.OrdinalIgnoreCase);
                    return index >= 0 ? combined.Substring(index).Trim() : "";
                }
            }
            catch
            {
                return "";
            }
        }

        private string DetectVirtualEnvironment()
        {
            try
            {
                // Search for .venv in different possible locations
                var possibleVenvPaths = new List<string>();

                possibleVenvPaths.AddRange(
                    GetPowerShellInstallRoots().Select(path => Path.Combine(path, ".venv"))
                );

                string repoRoot = FindHunyuanRepositoryRoot();
                if (!string.IsNullOrEmpty(repoRoot))
                {
                    possibleVenvPaths.Add(Path.Combine(repoRoot, ".venv"));
                }

                possibleVenvPaths.Add(Path.Combine(Application.dataPath, "UnityPlugin", "Scripts", ".venv"));

                // Managed UV project venv created by the Dependency Manager (<ProjectRoot>/Hunyuan3D_UV/.venv)
                string uvProjectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (!string.IsNullOrEmpty(uvProjectRoot))
                {
                    possibleVenvPaths.Add(Path.Combine(uvProjectRoot, "Hunyuan3D_UV", ".venv"));
                }

                possibleVenvPaths.Add(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".venv"));

                if (!string.IsNullOrEmpty(config.scriptBasePath))
                {
                    possibleVenvPaths.Add(Path.Combine(config.scriptBasePath, ".venv"));
                    possibleVenvPaths.Add(Path.GetFullPath(Path.Combine(config.scriptBasePath, "..", ".venv")));
                }

                foreach (string venvPath in possibleVenvPaths
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(venvPath))
                    {
                        string pythonExe = Path.Combine(venvPath, "Scripts", "python.exe");

                        if (File.Exists(pythonExe))
                        {
                            AddLogMessage($"✅ Virtual environment detected: {venvPath}");

                            // Verify that it has the dependencies installed
                            if (File.Exists(Path.Combine(venvPath, "Lib", "site-packages", "torch", "__init__.py")))
                            {
                                AddLogMessage("✅ PyTorch detected in venv");
                                return venvPath;
                            }
                            else
                            {
                                AddLogMessage("⚠️ Venv found but without PyTorch");
                            }
                        }
                    }
                }

                AddLogMessage("⚠️ No .venv virtual environment with dependencies detected");
                return null;
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error detecting venv: {ex.Message}");
                return null;
            }
        }

        private void SetPythonEnvironmentVariables(ProcessStartInfo startInfo, string venvPath = null)
        {
            // Force UTF-8 encoding for international characters
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            startInfo.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "utf-8";
            
            // Set console code page to UTF-8 for Windows
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                startInfo.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "utf-8";
                // Also set the console code page
                startInfo.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "utf-8";
            }

            // If we have a venv, configure it
            if (!string.IsNullOrEmpty(venvPath))
            {
                // Activate the virtual environment
                string venvScripts = Path.Combine(venvPath, "Scripts");
                string venvLibs = Path.Combine(venvPath, "Lib", "site-packages");

                // Update PATH to include venv Scripts
                string currentPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH");
                startInfo.EnvironmentVariables["PATH"] = $"{venvScripts};{currentPath}";

                // Set VIRTUAL_ENV
                startInfo.EnvironmentVariables["VIRTUAL_ENV"] = venvPath;

                // Remove PYTHONHOME if it exists (can interfere with venv)
                if (startInfo.EnvironmentVariables.ContainsKey("PYTHONHOME"))
                {
                    startInfo.EnvironmentVariables.Remove("PYTHONHOME");
                }
            }

            string hunyuan3dPath = FindHunyuanRepositoryRoot();
            if (!string.IsNullOrEmpty(hunyuan3dPath))
            {
                AddLogMessage($"📁 Hunyuan3D repository detected: {hunyuan3dPath}");
            }

            // Configurar PYTHONPATH
            string pythonPath = startInfo.EnvironmentVariables["PYTHONPATH"] ?? "";
            List<string> paths = new List<string>();

            if (!string.IsNullOrEmpty(hunyuan3dPath))
            {
                paths.Add(hunyuan3dPath);

                string hy3dshapePath = Path.Combine(hunyuan3dPath, "hy3dshape");
                string hy3dpaintPath = Path.Combine(hunyuan3dPath, "hy3dpaint");

                if (Directory.Exists(hy3dshapePath))
                {
                    paths.Add(hy3dshapePath);
                }

                if (Directory.Exists(hy3dpaintPath))
                {
                    paths.Add(hy3dpaintPath);
                }
            }

            // Add the scripts directory
            if (!string.IsNullOrEmpty(config.scriptBasePath))
            {
                paths.Add(config.scriptBasePath);
            }

            // If we have a venv, add site-packages
            if (!string.IsNullOrEmpty(venvPath))
            {
                string sitePackages = Path.Combine(venvPath, "Lib", "site-packages");
                if (Directory.Exists(sitePackages))
                {
                    paths.Add(sitePackages);
                }
            }

            if (!string.IsNullOrEmpty(pythonPath))
            {
                paths.Add(pythonPath);
            }

            startInfo.EnvironmentVariables["PYTHONPATH"] = string.Join(Path.PathSeparator.ToString(), paths.Distinct());
            AddLogMessage($"🔧 PYTHONPATH: {startInfo.EnvironmentVariables["PYTHONPATH"]}");

            // Forçar mode unbuffered
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            // Variables CUDA si cal
            if (config.device == "cuda")
            {
                Hunyuan3DDependencyManager.SetCudaEnvironmentVariables(startInfo);
            }
        }

        private void UpdateStatusFromOutput(string output)
        {
            string normalizedOutput = output.ToLowerInvariant();

            // Update status message based on Python script output
            if (normalizedOutput.Contains("verificant depend") ||
                normalizedOutput.Contains("verifying fbx dependencies"))
            {
                statusMessage = "Verifying FBX dependencies...";
                progress = 0.35f;
            }
            else if (normalizedOutput.Contains("inicialitzant hunyuan3d") ||
                     normalizedOutput.Contains("initializing hunyuan3d"))
            {
                statusMessage = "Initializing Hunyuan3D...";
                progress = 0.4f;
            }
            else if (normalizedOutput.Contains("carregant background remover") ||
                     normalizedOutput.Contains("loading background remover"))
            {
                statusMessage = "Loading Background Remover...";
                progress = 0.45f;
            }
            else if (normalizedOutput.Contains("carregant pipeline de generació 3d") ||
                     normalizedOutput.Contains("loading 3d generation pipeline"))
            {
                statusMessage = "Loading 3D model...";
                progress = 0.5f;
            }
            else if (normalizedOutput.Contains("models carregats correctament") ||
                     normalizedOutput.Contains("pipelines loaded"))
            {
                statusMessage = "Models loaded!";
                progress = 0.55f;
            }
            else if (normalizedOutput.Contains("carregant imatge") ||
                     normalizedOutput.Contains("loading image"))
            {
                statusMessage = "Processing image...";
                progress = 0.6f;
            }
            else if (normalizedOutput.Contains("generant forma 3d") ||
                     normalizedOutput.Contains("generating 3d shape"))
            {
                statusMessage = "Generating 3D model...";
                progress = 0.7f;
            }
            else if (normalizedOutput.Contains("post-processament") ||
                     normalizedOutput.Contains("post-processing"))
            {
                statusMessage = "Post-processing model...";
                progress = 0.8f;
            }
            else if (normalizedOutput.Contains("generant textura") ||
                     normalizedOutput.Contains("generating texture"))
            {
                statusMessage = "Generating textures...";
                progress = 0.85f;
            }
            else if (normalizedOutput.Contains("generant preview") ||
                     normalizedOutput.Contains("generating preview"))
            {
                statusMessage = "Generating previews...";
                progress = 0.9f;
            }
            else if (normalizedOutput.Contains("completat en") ||
                     normalizedOutput.Contains("completed in"))
            {
                statusMessage = "Completed!";
                progress = 0.95f;
            }
            else if (output.Contains("✓"))
            {
                // Generic success messages
                statusMessage = "Processing...";
                if (progress < 0.9f) progress += 0.05f;
            }
            else if (normalizedOutput.Contains("error") || output.Contains("✗"))
            {
                // Errors
                statusMessage = "Error detected!";
            }

            // Force UI update
            Repaint();
        }

        private async Task<bool> VerifyHunyuan3DInstallation()
        {
            try
            {
                AddLogMessage("🔍 Verifying Hunyuan3D installation...");

                // Detect venv
                string venvPath = DetectVirtualEnvironment();
                string pythonExe = config.pythonExecutablePath;
                string actualCommand = pythonExe;
                string actualArguments = "";

                // Check if UV is available
                bool useUV = await CheckUVAvailable();

                if (!string.IsNullOrEmpty(venvPath))
                {
                    pythonExe = Path.Combine(venvPath, "Scripts", "python.exe");
                }

                // Use external verification script
                string verifyScript = Path.Combine(config.scriptBasePath, "verify_hunyuan3d.py");
                
                // If the external script doesn't exist, create a temporary one
                if (!File.Exists(verifyScript))
                {
                    verifyScript = Path.Combine(Path.GetTempPath(), "verify_hunyuan3d.py");
                    string scriptContent = @"
import os
import sys

# Force UTF-8 encoding for Windows compatibility
if sys.platform.startswith('win'):
    os.environ['PYTHONIOENCODING'] = 'utf-8'
    os.environ['PYTHONUTF8'] = '1'
    os.environ['PYTHONLEGACYWINDOWSSTDIO'] = 'utf-8'

print(f'Python: {sys.executable}')
print(f'Version: {sys.version}')
print(f'Platform: {sys.platform}')

try:
    from hy3dshape.pipelines import Hunyuan3DDiTFlowMatchingPipeline
    print('[OK] Hunyuan3D 2.1 found and accessible')
    sys.exit(0)
except ImportError:
    try:
        import hy3dgen
        from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
        print('[OK] Hunyuan3D found and accessible')
        sys.exit(0)
    except ImportError as e:
        print(f'[ERROR] {e}')
        sys.exit(1)
except Exception as e:
    print(f'[ERROR] Unexpected error: {e}')
    sys.exit(1)
";
                    File.WriteAllText(verifyScript, scriptContent);
                }

                if (string.IsNullOrEmpty(venvPath) && useUV)
                {
                    actualCommand = "uv";
                    actualArguments = $"run python \"{verifyScript}\"";
                }
                else
                {
                    actualCommand = pythonExe;
                    actualArguments = $"\"{verifyScript}\"";
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = actualCommand,
                    Arguments = actualArguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = config.scriptBasePath
                };

                SetPythonEnvironmentVariables(startInfo, venvPath);

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    string output = await Task.Run(() => process.StandardOutput.ReadToEnd());
                    string error = await Task.Run(() => process.StandardError.ReadToEnd());

                    bool exited = await Task.Run(() => process.WaitForExit(30000));
                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                    }

                    // Clean up temporary script
                    if (verifyScript.StartsWith(Path.GetTempPath()))
                    {
                        try { File.Delete(verifyScript); } catch { }
                    }

                    AddLogMessage(output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        AddLogMessage($"Errors: {error}");
                    }

                    return exited &&
                           process.ExitCode == 0 &&
                           (output.Contains("[OK]") || output.Contains("Hunyuan3D found"));
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Error verifying: {ex.Message}");
                return false;
            }
        }

        private async Task TestUTF8Encoding()
        {
            try
            {
                AddLogMessage("🌐 Testing UTF-8 encoding...");

                // Detect venv
                string venvPath = DetectVirtualEnvironment();
                string pythonExe = config.pythonExecutablePath;
                string actualCommand = pythonExe;
                string actualArguments = "";

                // Check if UV is available
                bool useUV = await CheckUVAvailable();

                if (!string.IsNullOrEmpty(venvPath))
                {
                    pythonExe = Path.Combine(venvPath, "Scripts", "python.exe");
                }

                // Use external test script
                string testScript = Path.Combine(config.scriptBasePath, "test_encoding.py");
                
                // If the external script doesn't exist, create a temporary one
                if (!File.Exists(testScript))
                {
                    testScript = Path.Combine(Path.GetTempPath(), "test_encoding.py");
                    string scriptContent = @"
import os
import sys

# Force UTF-8 encoding for Windows compatibility
if sys.platform.startswith('win'):
    os.environ['PYTHONIOENCODING'] = 'utf-8'
    os.environ['PYTHONUTF8'] = '1'
    os.environ['PYTHONLEGACYWINDOWSSTDIO'] = 'utf-8'

print('=== UTF-8 Encoding Test ===')
print(f'Python executable: {sys.executable}')
print(f'Python version: {sys.version}')
print(f'Platform: {sys.platform}')
print(f'Default encoding: {sys.getdefaultencoding()}')

# Test international characters
print('\n=== International Characters Test ===')
test_strings = [
    'Hello World',
    'Hola Mundo',
    'Bonjour le Monde',
    'Hallo Welt',
    'Ciao Mondo'
]

for i, text in enumerate(test_strings, 1):
    print(f'{i:2d}. {text}')

# Test status symbols
print('\n=== Status Symbols Test ===')
print('[OK] Success message')
print('[ERROR] Error message')
print('[WARNING] Warning message')
print('[INFO] Information message')

print('\n=== Test completed successfully ===')
";
                    File.WriteAllText(testScript, scriptContent);
                }

                if (useUV)
                {
                    actualCommand = "uv.exe";
                    actualArguments = $"run python \"{testScript}\"";
                }
                else
                {
                    actualCommand = pythonExe;
                    actualArguments = $"\"{testScript}\"";
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = actualCommand,
                    Arguments = actualArguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = config.scriptBasePath
                };

                SetPythonEnvironmentVariables(startInfo, venvPath);

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    string output = await Task.Run(() => process.StandardOutput.ReadToEnd());
                    string error = await Task.Run(() => process.StandardError.ReadToEnd());

                    await Task.Run(() => process.WaitForExit(5000));

                    // Clean up temporary script
                    if (testScript.StartsWith(Path.GetTempPath()))
                    {
                        try { File.Delete(testScript); } catch { }
                    }

                    AddLogMessage("=== UTF-8 Test Results ===");
                    AddLogMessage(output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        AddLogMessage($"Errors: {error}");
                    }

                    if (output.Contains("Test completed successfully"))
                    {
                        AddLogMessage("✅ UTF-8 encoding test passed!");
                    }
                    else
                    {
                        AddLogMessage("❌ UTF-8 encoding test failed!");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Error testing UTF-8: {ex.Message}");
            }
        }

        private async Task SelectGeneratedModels(string outputPath)
        {
            await Task.Delay(500); // Wait for Unity to process the assets
            
            try
            {
                // Search for generated files
                string[] extensions = { $".{config.fileType}", ".png", ".jpg" };
                List<string> generatedFiles = new List<string>();
                
                foreach (string ext in extensions)
                {
                    generatedFiles.AddRange(Directory.GetFiles(outputPath, $"*{ext}", SearchOption.AllDirectories));
                }
                
                if (generatedFiles.Count > 0)
                {
                    // Convert absolute paths to relative Asset paths
                    List<UnityEngine.Object> objectsToSelect = new List<UnityEngine.Object>();
                    
                    foreach (string filePath in generatedFiles)
                    {
                        string relativePath = GetRelativeAssetPath(filePath);
                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
                            if (asset != null)
                            {
                                objectsToSelect.Add(asset);
                                AddLogMessage($"✓ Asset imported: {relativePath}");
                            }
                        }
                    }
                    
                    // Select the assets in the Inspector
                    if (objectsToSelect.Count > 0)
                    {
                        Selection.objects = objectsToSelect.ToArray();
                        EditorGUIUtility.PingObject(objectsToSelect[0]);
                        AddLogMessage($"✓ {objectsToSelect.Count} assets selected in the Inspector.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error selecting models: {ex.Message}");
            }
        }

        private string GetRelativeAssetPath(string absolutePath)
        {
            return Hunyuan3DUtils.GetRelativeAssetPath(absolutePath);
        }
        #endregion

        #region Utilitats
        private void AddLogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            logMessages.Add($"[{timestamp}] {message}");
            
            // Keep only the last 100 messages
            if (logMessages.Count > 100)
            {
                logMessages.RemoveAt(0);
            }
            
            // Auto-scroll the log panel to the most recent line, then repaint
            scrollPosition.y = float.MaxValue;
            Repaint();

            // Also print to the Unity console
            UnityEngine.Debug.Log($"Hunyuan3D: {message}");
        }
        #endregion
    }
}
