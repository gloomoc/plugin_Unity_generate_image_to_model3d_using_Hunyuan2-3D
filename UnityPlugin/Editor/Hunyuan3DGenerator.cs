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
    /// Plugin d'Unity per generar models 3D a partir d'imatges utilitzant Hunyuan3D-2
    /// Integra els scripts de Python batch_hunyuan3d.py i remove_background.py
    /// </summary>
    public class Hunyuan3DGenerator : EditorWindow
    {
        #region Variables de la UI
        private string selectedImagePath = "";
        private string outputFolder = "Assets/Generated3DModels";
        private bool batchMode = false;
        
        // Configuració persistent
        private Hunyuan3DConfig config;
        
        // Control d'estat
        private bool isProcessing = false;
        private string statusMessage = "";
        private float progress = 0f;
        private List<string> logMessages = new List<string>();
        
        // Scroll per logs
        private Vector2 scrollPosition = Vector2.zero;
        
        // Opcions de tipus de fitxer
        private readonly string[] fileTypeOptions = { "obj", "fbx", "glb", "ply", "stl" };
        private readonly string[] deviceOptions = { "cuda", "cpu", "mps" };
        #endregion

        #region Menú Unity
        [MenuItem("Tools/Hunyuan3D/3D Model Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<Hunyuan3DGenerator>("Hunyuan3D Generator");
            window.minSize = new Vector2(500, 500);
            window.maxSize = new Vector2(800, 800);
            window.Initialize();
        }
        
        [MenuItem("Tools/Hunyuan3D/Dependency Manager")]
        public static void ShowDependencyManager()
        {
            Hunyuan3DDependencyManager.ShowWindow();
        }
        #endregion

        #region Inicialització
        private void Initialize()
        {
            // Carregar configuració persistent
            config = Hunyuan3DConfig.Load();
            
            // Intentar detectar automàticament el path dels scripts si no està configurat
            if (string.IsNullOrEmpty(config.scriptBasePath))
            {
                string currentPath = Application.dataPath;
                string projectRoot = Directory.GetParent(currentPath).FullName;
                
                // Buscar els scripts en el directori pare o en directoris relacionats
                string[] possiblePaths = {
                    Path.Combine(projectRoot, "batch_hunyuan3d.py"),
                    Path.Combine(projectRoot, "Scripts", "batch_hunyuan3d.py"),
                    Path.Combine(projectRoot, "Python", "batch_hunyuan3d.py"),
                    Path.Combine(Directory.GetParent(projectRoot).FullName, "batch_hunyuan3d.py")
                };
                
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        config.scriptBasePath = Path.GetDirectoryName(path);
                        AddLogMessage($"Scripts trobats a: {config.scriptBasePath}");
                        config.Save(); // Guardar la configuració actualitzada
                        break;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(config.scriptBasePath))
            {
                AddLogMessage("Advertència: No s'han trobat els scripts de Python automàticament.");
                AddLogMessage("Si us plau, especifica el path manualment a la configuració.");
            }
            
            // Crear carpeta de sortida si no existeix
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
        }

        private void DrawPathConfiguration()
        {
            EditorGUILayout.LabelField("Configuració de Paths", EditorStyles.boldLabel);

            // Botó per detectar instal·lació del PowerShell
            if (GUILayout.Button("🔍 Detectar Instal·lació PowerShell", GUILayout.Height(25)))
            {
                string powerShellPath = @"C:\Users\" + Environment.UserName + @"\AppData\Local\Temp\Hunyuan2-3D-for-windows";
                if (Directory.Exists(powerShellPath))
                {
                    string venvPath = Path.Combine(powerShellPath, ".venv");
                    if (Directory.Exists(venvPath))
                    {
                        string venvPython = Path.Combine(venvPath, "Scripts", "python.exe");
                        if (File.Exists(venvPython))
                        {
                            config.pythonExecutablePath = venvPython;
                            config.scriptBasePath = powerShellPath;
                            config.Save();

                            AddLogMessage($"✅ Instal·lació PowerShell detectada!");
                            AddLogMessage($"📁 Path: {powerShellPath}");
                            AddLogMessage($"🐍 Python: {venvPython}");

                            EditorUtility.DisplayDialog(
                                "Instal·lació Detectada",
                                $"S'ha detectat la instal·lació del PowerShell!\n\n" +
                                $"Path: {powerShellPath}\n" +
                                $"Python: {venvPython}\n\n" +
                                "La configuració s'ha actualitzat automàticament.",
                                "Perfecte!"
                            );
                        }
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Instal·lació No Trobada",
                        "No s'ha trobat la instal·lació del PowerShell.\n\n" +
                        "Assegura't d'haver executat el script d'instal·lació PowerShell\n" +
                        "des del Dependency Manager.",
                        "D'acord"
                    );
                }
            }

            EditorGUILayout.BeginHorizontal();
            config.pythonExecutablePath = EditorGUILayout.TextField("Python Executable:", config.pythonExecutablePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Seleccionar Python", "", "exe");
                if (!string.IsNullOrEmpty(path))
                {
                    config.pythonExecutablePath = path;
                    config.Save();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Mostrar si estem usant un venv
            if (config.pythonExecutablePath.Contains(".venv"))
            {
                EditorGUILayout.HelpBox("✅ Usant Python d'un entorn virtual", MessageType.Info);
            }
            
            EditorGUILayout.BeginHorizontal();
            config.scriptBasePath = EditorGUILayout.TextField("Script Base Path:", config.scriptBasePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Seleccionar carpeta dels scripts", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    config.scriptBasePath = path;
                    config.Save();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Verificar si els scripts existeixen
            if (!string.IsNullOrEmpty(config.scriptBasePath))
            {
                string batchScript = Path.Combine(config.scriptBasePath, "batch_hunyuan3d.py");
                string rembgScript = Path.Combine(config.scriptBasePath, "remove_background.py");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("batch_hunyuan3d.py:", File.Exists(batchScript) ? "✓" : "✗");
                EditorGUILayout.LabelField("remove_background.py:", File.Exists(rembgScript) ? "✓" : "✗");
                EditorGUILayout.EndHorizontal();
            }
            
            // Botó per guardar configuració
            if (GUILayout.Button("Guardar Configuració"))
            {
                config.Save();
                AddLogMessage("✓ Configuració guardada.");
            }
        }

        private void DrawInputSelection()
        {
            EditorGUILayout.LabelField("Selecció d'Entrada", EditorStyles.boldLabel);
            
            batchMode = EditorGUILayout.Toggle("Mode Batch (carpeta)", batchMode);
            
            EditorGUILayout.BeginHorizontal();
            if (batchMode)
            {
                selectedImagePath = EditorGUILayout.TextField("Carpeta d'imatges:", selectedImagePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFolderPanel("Seleccionar carpeta d'imatges", "", "");
                    if (!string.IsNullOrEmpty(path))
                        selectedImagePath = path;
                }
            }
            else
            {
                selectedImagePath = EditorGUILayout.TextField("Imatge:", selectedImagePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFilePanel("Seleccionar imatge", "", "jpg,jpeg,png,bmp,webp,tiff");
                    if (!string.IsNullOrEmpty(path))
                        selectedImagePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Carpeta de sortida:", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Seleccionar carpeta de sortida", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModelParameters()
        {
            EditorGUILayout.LabelField("Paràmetres del Model", EditorStyles.boldLabel);
            
            config.modelPath = EditorGUILayout.TextField("Model Path:", config.modelPath);
            config.subfolder = EditorGUILayout.TextField("Subfolder:", config.subfolder);
            config.texgenModelPath = EditorGUILayout.TextField("Texture Model Path:", config.texgenModelPath);
            
            int deviceIndex = System.Array.IndexOf(deviceOptions, config.device);
            if (deviceIndex == -1) deviceIndex = 0;
            deviceIndex = EditorGUILayout.Popup("Device:", deviceIndex, deviceOptions);
            config.device = deviceOptions[deviceIndex];
            
            config.mcAlgo = EditorGUILayout.TextField("MC Algorithm:", config.mcAlgo);
        }

        private void DrawGenerationParameters()
        {
            EditorGUILayout.LabelField("Paràmetres de Generació", EditorStyles.boldLabel);
            
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
            EditorGUILayout.LabelField("Opcions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            config.enableT23D = EditorGUILayout.Toggle("Enable Text-to-3D", config.enableT23D);
            config.disableTexture = EditorGUILayout.Toggle("Disable Texture", config.disableTexture);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            config.enableFlashVDM = EditorGUILayout.Toggle("Enable FlashVDM", config.enableFlashVDM);
            config.compile = EditorGUILayout.Toggle("Compile Model", config.compile);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            config.lowVramMode = EditorGUILayout.Toggle("Low VRAM Mode", config.lowVramMode);
            config.removeBackground = EditorGUILayout.Toggle("Remove Background", config.removeBackground);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProcessingControls()
        {
            EditorGUILayout.LabelField("Control de Processament", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(isProcessing);
            if (GUILayout.Button(batchMode ? "Processar Carpeta" : "Generar Model 3D", GUILayout.Height(30)))
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
                EditorGUILayout.LabelField("Estat:", statusMessage);
                EditorGUILayout.Space(2);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, $"{(progress * 100):F1}%");
            }
        }

        private void DrawProgressAndLogs()
        {
            EditorGUILayout.LabelField("Logs d'Instal·lació", EditorStyles.boldLabel);

            // Convertir els missatges de log a un string únic
            string logContent = string.Join("\n", logMessages);

            // Crear un estil personalitzat per al TextArea
            GUIStyle logStyle = new GUIStyle(GUI.skin.textArea)
            {
                wordWrap = true,
                richText = false, // Desactivar richText per evitar problemes amb la selecció
                fontSize = 11,
                padding = new RectOffset(5, 5, 5, 5)
            };

            // ScrollView per als logs with dynamic height
            float availableHeight = position.height - 450; // Account for other UI elements
            float logHeight = Mathf.Clamp(availableHeight, 150, 300); // Min 150, max 300
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(logHeight));

            // Capturar focus per permetre Ctrl+C
            GUI.SetNextControlName("LogTextArea");

            // TextArea que permet selecció i còpia
            string newLogContent = EditorGUILayout.TextArea(logContent, logStyle, GUILayout.ExpandHeight(true));

            // Si l'usuari ha fet focus al TextArea, processar Ctrl+C
            if (GUI.GetNameOfFocusedControl() == "LogTextArea")
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.C)
                {
                    // Unity ja gestiona Ctrl+C automàticament per TextArea
                    // però podem afegir feedback visual si volem
                    EditorGUIUtility.systemCopyBuffer = EditorGUIUtility.systemCopyBuffer; // Forçar actualització
                }
            }

            EditorGUILayout.EndScrollView();

            // Informació sobre la funcionalitat
            EditorGUILayout.HelpBox("Pots seleccionar text del log i copiar-lo amb Ctrl+C", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Netejar Logs"))
            {
                logMessages.Clear();
            }
            if (GUILayout.Button("Copiar Tots els Logs"))
            {
                CopyLogsToClipboard();
            }

            // Botó addicional per copiar la selecció actual
            if (GUILayout.Button("Copiar Selecció"))
            {
                TextEditor textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                if (textEditor != null && textEditor.hasSelection)
                {
                    EditorGUIUtility.systemCopyBuffer = textEditor.SelectedText;
                    AddLogMessage("✓ Selecció copiada al portapapers");
                }
                else
                {
                    AddLogMessage("⚠ No hi ha text seleccionat");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CopyLogsToClipboard()
        {
            string allLogs = string.Join("\n", logMessages);
            GUIUtility.systemCopyBuffer = allLogs;
            AddLogMessage("Logs copiats al portapapers!");
        }
        #endregion

        #region Validació
        private bool ValidateInputs()
        {
            if (string.IsNullOrEmpty(selectedImagePath))
            {
                AddLogMessage("Error: Si us plau, selecciona una imatge o carpeta.");
                return false;
            }
            
            if (batchMode && !Directory.Exists(selectedImagePath))
            {
                AddLogMessage("Error: La carpeta especificada no existeix.");
                return false;
            }
            
            if (!batchMode && !File.Exists(selectedImagePath))
            {
                AddLogMessage("Error: La imatge especificada no existeix.");
                return false;
            }
            
            // Utilitzar la validació de la configuració
            string errorMessage;
            if (!config.IsValid(out errorMessage))
            {
                AddLogMessage($"Error de configuració: {errorMessage}");
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
            statusMessage = "Iniciant processament...";

            try
            {
                // Verificar instal·lació primer
                if (!await VerifyHunyuan3DInstallation())
                {
                    EditorUtility.DisplayDialog(
                        "Error d'instal·lació",
                        "No s'ha pogut trobar els mòduls de Hunyuan3D.\n\n" +
                        "Assegura't que:\n" +
                        "1. Has executat el Dependency Manager\n" +
                        "2. El path dels scripts apunta al directori correcte\n" +
                        "3. Hunyuan3D està instal·lat correctament\n\n" +
                        "Prova a fer clic a 'Detectar Entorn Virtual' primer.",
                        "D'acord"
                    );
                    return;
                }

                // Crear carpeta de sortida absoluta
                string absoluteOutputPath = Path.GetFullPath(outputFolder);
                if (!Directory.Exists(absoluteOutputPath))
                {
                    Directory.CreateDirectory(absoluteOutputPath);
                }

                // Preprocessar amb remove_background si és necessari
                string processedImagePath = selectedImagePath;
                if (config.removeBackground && !batchMode)
                {
                    progress = 0.1f;
                    statusMessage = "Eliminant fons de la imatge...";
                    processedImagePath = await PreprocessImage(selectedImagePath);
                    if (string.IsNullOrEmpty(processedImagePath))
                    {
                        AddLogMessage("Error: No s'ha pogut eliminar el fons de la imatge.");
                        return;
                    }
                }

                // Executar batch_hunyuan3d.py
                progress = 0.3f;
                statusMessage = "Generant model 3D...";

                bool success = await ExecuteHunyuan3DScript(processedImagePath, absoluteOutputPath);

                if (success)
                {
                    progress = 0.9f;
                    statusMessage = "Important assets a Unity...";

                    // Importar els nous assets
                    AssetDatabase.Refresh();

                    // Buscar i seleccionar els nous models
                    await SelectGeneratedModels(absoluteOutputPath);

                    progress = 1f;
                    statusMessage = "Processament completat!";
                    AddLogMessage("✓ Processament completat amb èxit!");
                }
                else
                {
                    AddLogMessage("✗ Error durant el processament.");
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
                AddLogMessage("Advertència: remove_background.py no trobat. Saltant preprocessament.");
                return imagePath;
            }
            
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            string extension = Path.GetExtension(imagePath);
            string outputPath = Path.Combine(Path.GetTempPath(), $"{fileName}_nobg{extension}");
            
            string arguments = $"\"{rembgScript}\" \"{imagePath}\" \"{outputPath}\"";
            
            AddLogMessage($"Executant: {config.pythonExecutablePath} {arguments}");
            
            bool success = await ExecutePythonScript(arguments);
            
            if (success && File.Exists(outputPath))
            {
                AddLogMessage("✓ Fons eliminat correctament.");
                return outputPath;
            }
            
            AddLogMessage("✗ Error eliminant el fons. Utilitzant imatge original.");
            return imagePath;
        }

        private async Task<bool> ExecuteHunyuan3DScript(string inputPath, string outputPath)
        {
            string batchScript = Path.Combine(config.scriptBasePath, "batch_hunyuan3d.py");
            
            // Construir arguments seguint l'estructura del script
            List<string> args = new List<string>
            {
                $"\"{batchScript}\"",
                $"\"{inputPath}\"",
                $"--output \"{outputPath}\"",
                $"--model_path \"{config.modelPath}\"",
                $"--subfolder \"{config.subfolder}\"",
                $"--texgen_model_path \"{config.texgenModelPath}\"",
                $"--device {config.device}",
                $"--mc_algo {config.mcAlgo}",
                $"--steps {config.steps}",
                $"--guidance_scale {config.guidanceScale}",
                $"--seed {config.seed}",
                $"--octree_resolution {config.octreeResolution}",
                $"--num_chunks {config.numChunks}",
                $"--file_type {config.fileType}"
            };
            
            // Afegir flags opcionals
            if (config.enableT23D) args.Add("--enable_t23d");
            if (config.disableTexture) args.Add("--disable_tex");
            if (config.enableFlashVDM) args.Add("--enable_flashvdm");
            if (config.compile) args.Add("--compile");
            if (config.lowVramMode) args.Add("--low_vram_mode");
            
            string arguments = string.Join(" ", args);
            
            AddLogMessage($"Executant: {config.pythonExecutablePath} {arguments}");
            
            return await ExecutePythonScript(arguments);
        }

        private async Task<bool> ExecutePythonScript(string arguments)
        {
            try
            {
                // Detectar si existeix un entorn virtual al projecte
                string venvPath = DetectVirtualEnvironment();
                string pythonExe = config.pythonExecutablePath;
                string actualCommand = pythonExe;
                string actualArguments = arguments;

                // Comprovar si UV està disponible
                bool useUV = await CheckUVAvailable();

                if (!string.IsNullOrEmpty(venvPath))
                {
                    // Usar Python del venv si existeix
                    string venvPython = Path.Combine(venvPath, "Scripts", "python.exe");
                    if (File.Exists(venvPython))
                    {
                        pythonExe = venvPython;

                        if (useUV)
                        {
                            // Si tenim UV, usar-lo per executar Python
                            actualCommand = "uv.exe";
                            actualArguments = $"run python {arguments}";
                            AddLogMessage($"🚀 Usant UV per executar Python del venv");
                        }
                        else
                        {
                            actualCommand = venvPython;
                            AddLogMessage($"🐍 Usant Python del venv directament: {venvPython}");
                        }
                    }
                }
                else if (useUV)
                {
                    // Si no hi ha venv però tenim UV, usar-lo igualment
                    actualCommand = "uv.exe";
                    actualArguments = $"run python {arguments}";
                    AddLogMessage($"🚀 Usant UV per executar Python");
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

                // Configurar variables d'entorn necessàries
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
                                    AddLogMessage("❌ Error: Mòduls no trobats!");
                                    AddLogMessage("   Executa primer el Dependency Manager");
                                }
                            });
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Esperar amb timeout
                    bool completed = await Task.Run(() => process.WaitForExit(600000)); // 10 minuts

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
                AddLogMessage($"Error executant script: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CheckUVAvailable()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "uv.exe",
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
                    await Task.Run(() => process.WaitForExit(3000)); // 3 segons timeout

                    bool isAvailable = process.ExitCode == 0 && output.Contains("uv");

                    if (isAvailable)
                    {
                        AddLogMessage($"✅ UV disponible: {output.Trim()}");
                    }

                    return isAvailable;
                }
            }
            catch
            {
                return false;
            }
        }

        private string DetectVirtualEnvironment()
        {
            try
            {
                // Buscar .venv en diferents ubicacions possibles
                string[] possibleVenvPaths = {
                    // Path del PowerShell installer
                    @"C:\Users\" + Environment.UserName + @"\AppData\Local\Temp\Hunyuan2-3D-for-windows\.venv",
                    // Path dins del projecte Unity
                    Path.Combine(Application.dataPath, "UnityPlugin", "Scripts", ".venv"),
                    Path.Combine(config.scriptBasePath, ".venv"),
                    Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".venv"),
                    // Path alternatiu si s'ha mogut
                    Path.Combine(config.scriptBasePath, "..", ".venv")
                };

                foreach (string venvPath in possibleVenvPaths)
                {
                    if (Directory.Exists(venvPath))
                    {
                        string pythonExe = Path.Combine(venvPath, "Scripts", "python.exe");

                        if (File.Exists(pythonExe))
                        {
                            AddLogMessage($"✅ Entorn virtual detectat: {venvPath}");

                            // Verificar que té les dependències instal·lades
                            if (File.Exists(Path.Combine(venvPath, "Lib", "site-packages", "torch", "__init__.py")))
                            {
                                AddLogMessage("✅ PyTorch detectat al venv");
                                return venvPath;
                            }
                            else
                            {
                                AddLogMessage("⚠️ Venv trobat però sense PyTorch");
                            }
                        }
                    }
                }

                AddLogMessage("⚠️ No s'ha detectat cap entorn virtual .venv amb dependències");
                return null;
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error detectant venv: {ex.Message}");
                return null;
            }
        }

        private void SetPythonEnvironmentVariables(ProcessStartInfo startInfo, string venvPath = null)
        {
            // Si tenim un venv, configurar-lo
            if (!string.IsNullOrEmpty(venvPath))
            {
                // Activar l'entorn virtual
                string venvScripts = Path.Combine(venvPath, "Scripts");
                string venvLibs = Path.Combine(venvPath, "Lib", "site-packages");

                // Actualitzar PATH per incloure Scripts del venv
                string currentPath = startInfo.EnvironmentVariables["PATH"] ?? Environment.GetEnvironmentVariable("PATH");
                startInfo.EnvironmentVariables["PATH"] = $"{venvScripts};{currentPath}";

                // Configurar VIRTUAL_ENV
                startInfo.EnvironmentVariables["VIRTUAL_ENV"] = venvPath;

                // Eliminar PYTHONHOME si existeix (pot interferir amb venv)
                if (startInfo.EnvironmentVariables.ContainsKey("PYTHONHOME"))
                {
                    startInfo.EnvironmentVariables.Remove("PYTHONHOME");
                }
            }

            // Determinar el directori de Hunyuan3D
            string hunyuan3dPath = "";

            // Path específic del PowerShell installer
            string installerPath = @"C:\Users\" + Environment.UserName + @"\AppData\Local\Temp\Hunyuan2-3D-for-windows";
            string hunyuan3dInstallerPath = Path.Combine(installerPath, "Hunyuan3D-2");

            if (Directory.Exists(hunyuan3dInstallerPath))
            {
                hunyuan3dPath = hunyuan3dInstallerPath;
                AddLogMessage($"📁 Hunyuan3D detectat (PowerShell): {hunyuan3dPath}");

                // Actualitzar també el scriptBasePath si està buit
                if (string.IsNullOrEmpty(config.scriptBasePath) || !Directory.Exists(config.scriptBasePath))
                {
                    config.scriptBasePath = installerPath;
                    config.Save();
                    AddLogMessage($"📁 Script base path actualitzat: {config.scriptBasePath}");
                }
            }
            else
            {
                // Buscar en altres ubicacions
                string parentDir = Directory.GetParent(config.scriptBasePath)?.FullName;
                if (parentDir != null && Directory.Exists(Path.Combine(parentDir, "hy3dgen")))
                {
                    hunyuan3dPath = parentDir;
                }
                else if (Directory.Exists(Path.Combine(config.scriptBasePath, "hy3dgen")))
                {
                    hunyuan3dPath = config.scriptBasePath;
                }
                else if (Directory.Exists(Path.Combine(config.scriptBasePath, "Hunyuan3D-2", "hy3dgen")))
                {
                    hunyuan3dPath = Path.Combine(config.scriptBasePath, "Hunyuan3D-2");
                }
            }

            // Configurar PYTHONPATH
            string pythonPath = startInfo.EnvironmentVariables["PYTHONPATH"] ?? "";
            List<string> paths = new List<string>();

            if (!string.IsNullOrEmpty(hunyuan3dPath))
            {
                paths.Add(hunyuan3dPath);
            }

            // Afegir el directori dels scripts
            if (!string.IsNullOrEmpty(config.scriptBasePath))
            {
                paths.Add(config.scriptBasePath);
            }

            // Si tenim un venv, afegir site-packages
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
                var depManager = new Hunyuan3DDependencyManager();
                depManager.SetCudaEnvironmentVariables(startInfo);
            }
        }

        private void UpdateStatusFromOutput(string output)
        {
            // Actualitzar el missatge d'estat basat en el output del script Python
            if (output.Contains("Verificant dependències"))
            {
                statusMessage = "Verificant dependències FBX...";
                progress = 0.35f;
            }
            else if (output.Contains("Inicialitzant Hunyuan3D"))
            {
                statusMessage = "Inicialitzant Hunyuan3D...";
                progress = 0.4f;
            }
            else if (output.Contains("Carregant Background Remover"))
            {
                statusMessage = "Carregant Background Remover...";
                progress = 0.45f;
            }
            else if (output.Contains("Carregant pipeline de generació 3D"))
            {
                statusMessage = "Carregant model 3D...";
                progress = 0.5f;
            }
            else if (output.Contains("Models carregats correctament"))
            {
                statusMessage = "Models carregats!";
                progress = 0.55f;
            }
            else if (output.Contains("Carregant imatge"))
            {
                statusMessage = "Processant imatge...";
                progress = 0.6f;
            }
            else if (output.Contains("Generant forma 3D"))
            {
                statusMessage = "Generant model 3D...";
                progress = 0.7f;
            }
            else if (output.Contains("Post-processament"))
            {
                statusMessage = "Post-processant model...";
                progress = 0.8f;
            }
            else if (output.Contains("Generant textura"))
            {
                statusMessage = "Generant textures...";
                progress = 0.85f;
            }
            else if (output.Contains("Generant preview"))
            {
                statusMessage = "Generant previews...";
                progress = 0.9f;
            }
            else if (output.Contains("Completat en"))
            {
                statusMessage = "Completat!";
                progress = 0.95f;
            }
            else if (output.Contains("✓"))
            {
                // Missatges d'èxit genèrics
                statusMessage = "Processant...";
                if (progress < 0.9f) progress += 0.05f;
            }
            else if (output.Contains("Error") || output.Contains("✗"))
            {
                // Errors
                statusMessage = "Error detectat!";
            }

            // Forçar actualització de la UI
            Repaint();
        }

        private async Task<bool> VerifyHunyuan3DInstallation()
        {
            try
            {
                AddLogMessage("🔍 Verificant instal·lació de Hunyuan3D...");

                // Detectar venv
                string venvPath = DetectVirtualEnvironment();
                string pythonExe = config.pythonExecutablePath;
                string actualCommand = pythonExe;
                string actualArguments = "";

                // Comprovar si UV està disponible
                bool useUV = await CheckUVAvailable();

                if (!string.IsNullOrEmpty(venvPath))
                {
                    pythonExe = Path.Combine(venvPath, "Scripts", "python.exe");
                }

                // Script de verificació
                string verifyScript = @"
import sys
print(f'Python: {sys.executable}')
print(f'Version: {sys.version}')
try:
    import hy3dgen
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
    print('✅ Hunyuan3D trobat i accessible')
    sys.exit(0)
except ImportError as e:
    print(f'❌ ERROR: {e}')
    sys.exit(1)
";

                string tempScript = Path.Combine(Path.GetTempPath(), "verify_hunyuan3d.py");
                File.WriteAllText(tempScript, verifyScript);

                if (useUV)
                {
                    actualCommand = "uv.exe";
                    actualArguments = $"run python \"{tempScript}\"";
                }
                else
                {
                    actualCommand = pythonExe;
                    actualArguments = $"\"{tempScript}\"";
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

                    // Netejar
                    try { File.Delete(tempScript); } catch { }

                    AddLogMessage(output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        AddLogMessage($"Errors: {error}");
                    }

                    return output.Contains("✅ Hunyuan3D trobat");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Error verificant: {ex.Message}");
                return false;
            }
        }

        private async Task SelectGeneratedModels(string outputPath)
        {
            await Task.Delay(500); // Esperar que Unity processi els assets
            
            try
            {
                // Buscar fitxers generats
                string[] extensions = { $".{config.fileType}", ".png", ".jpg" };
                List<string> generatedFiles = new List<string>();
                
                foreach (string ext in extensions)
                {
                    generatedFiles.AddRange(Directory.GetFiles(outputPath, $"*{ext}", SearchOption.AllDirectories));
                }
                
                if (generatedFiles.Count > 0)
                {
                    // Convertir paths absoluts a relatius d'Assets
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
                                AddLogMessage($"✓ Asset importat: {relativePath}");
                            }
                        }
                    }
                    
                    // Seleccionar els assets a l'Inspector
                    if (objectsToSelect.Count > 0)
                    {
                        Selection.objects = objectsToSelect.ToArray();
                        EditorGUIUtility.PingObject(objectsToSelect[0]);
                        AddLogMessage($"✓ {objectsToSelect.Count} assets seleccionats a l'Inspector.");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"Error seleccionant models: {ex.Message}");
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
            
            // Mantenir només els últims 100 missatges
            if (logMessages.Count > 100)
            {
                logMessages.RemoveAt(0);
            }
            
            // Forçar repaint per actualitzar la UI
            Repaint();
            
            // També imprimir a la consola d'Unity
            UnityEngine.Debug.Log($"Hunyuan3D: {message}");
        }
        #endregion
    }
}
