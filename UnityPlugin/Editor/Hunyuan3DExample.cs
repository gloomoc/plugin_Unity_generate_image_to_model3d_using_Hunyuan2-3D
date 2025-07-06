using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hunyuan3D.Editor
{
    /// <summary>
    /// Example of programmatic use of the Hunyuan3D plugin
    /// This script shows how to use the plugin's functionalities from code
    /// </summary>
    public class Hunyuan3DExample : EditorWindow
    {
        // [MenuItem("Tools/Hunyuan3D/Example Usage")] // Removed as requested
        public static void ShowExampleWindow()
        {
            var window = GetWindow<Hunyuan3DExample>("Hunyuan3D Example");
            window.minSize = new Vector2(400, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Hunyuan3D Plugin - Usage Examples", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Quick Setup for CPU", GUILayout.Height(30)))
            {
                SetupForCPU();
            }

            if (GUILayout.Button("Setup for NVIDIA GPU", GUILayout.Height(30)))
            {
                SetupForGPU();
            }

            if (GUILayout.Button("Setup Fast Mode", GUILayout.Height(30)))
            {
                SetupFastMode();
            }

            if (GUILayout.Button("Setup High Quality", GUILayout.Height(30)))
            {
                SetupHighQuality();
            }

            EditorGUILayout.Space(20);
            
            EditorGUILayout.LabelField("Programmatic Usage:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Open Main Generator"))
            {
                Hunyuan3DGenerator.ShowWindow();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "This example shows preset configurations for different usage scenarios. " +
                "Each button automatically configures the optimal parameters for the specific use case.",
                MessageType.Info
            );
        }

        /// <summary>
        /// Optimized configuration for CPU usage
        /// </summary>
        private void SetupForCPU()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.device = "cpu";
            config.steps = 20;  // Fewer steps for CPU
            config.octreeResolution = 128;  // Lower resolution
            config.lowVramMode = true;
            config.compile = false;  // Do not compile for CPU
            config.enableFlashVDM = false;
            config.disableTexture = true;  // Disable textures for speed
            
            config.Save();
            
            Debug.Log("✓ Optimized configuration for CPU applied");
            ShowNotification(new GUIContent("CPU configuration applied!"));
        }

        /// <summary>
        /// Optimized configuration for NVIDIA GPU with CUDA
        /// </summary>
        private void SetupForGPU()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.device = "cuda";
            config.steps = 30;
            config.octreeResolution = 256;
            config.lowVramMode = false;
            config.compile = true;  // Compile for better performance
            config.enableFlashVDM = true;  // Acceleration
            config.disableTexture = false;
            
            config.Save();
            
            Debug.Log("✓ Optimized configuration for GPU applied");
            ShowNotification(new GUIContent("GPU configuration applied!"));
        }

        /// <summary>
        /// Configuration for fast processing with acceptable quality
        /// </summary>
        private void SetupFastMode()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.steps = 15;  // Few steps
            config.guidanceScale = 5.0f;  // Lower guidance
            config.octreeResolution = 128;  // Low resolution
            config.fileType = "obj";  // Fast format
            config.disableTexture = true;  // No textures
            config.lowVramMode = true;
            
            config.Save();
            
            Debug.Log("✓ Fast mode activated");
            ShowNotification(new GUIContent("Fast mode activated!"));
        }

        /// <summary>
        /// Configuration for maximum quality
        /// </summary>
        private void SetupHighQuality()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.steps = 50;  // Many steps
            config.guidanceScale = 10.0f;  // High guidance
            config.octreeResolution = 384;  // High resolution
            config.fileType = "fbx";  // Complete format
            config.disableTexture = false;  // With textures
            config.enableFlashVDM = true;
            config.compile = true;
            
            config.Save();
            
            Debug.Log("✓ High quality mode activated");
            ShowNotification(new GUIContent("High quality mode activated!"));
        }
    }

    /// <summary>
    /// Utility to automate common tasks
    /// </summary>
    public static class Hunyuan3DAutomation
    {
        /// <summary>
        /// Automatically processes all images in a folder
        /// Example of use from other scripts
        /// </summary>
        // [MenuItem("Tools/Hunyuan3D/Auto Process Selected Folder")] // Removed as requested
        public static void AutoProcessSelectedFolder()
        {
            string folderPath = EditorUtility.OpenFolderPanel("Select image folder", "", "");
            
            if (string.IsNullOrEmpty(folderPath))
                return;

            string[] imageFiles = Hunyuan3DUtils.GetImagesInFolder(folderPath);
            
            if (imageFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("No images found", "No images were found in the selected folder.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Process images",
                $"Found {imageFiles.Length} images. Do you want to process them all?",
                "Yes", "Cancel"
            );

            if (proceed)
            {
                // Open the main window with the folder preselected
                var window = EditorWindow.GetWindow<Hunyuan3DGenerator>();
                // Here we would normally pass the path, but since the variables are private,
                // the user will have to manually select the folder in the interface
                Debug.Log($"Process the {imageFiles.Length} images from: {folderPath}");
                
                EditorUtility.DisplayDialog(
                    "Instructions",
                    "The 3D generator window has been opened. " +
                    "Activate 'Batch Mode' and select the folder to process all images.",
                    "Understood"
                );
            }
        }

        /// <summary>
        /// Cleans temporary files generated by the plugin
        /// </summary>
        // [MenuItem("Tools/Hunyuan3D/Clean Temp Files")] // Removed as requested
        public static void CleanTempFiles()
        {
            string tempPath = Path.GetTempPath();
            string[] tempFiles = Directory.GetFiles(tempPath, "*_nobg.*", SearchOption.TopDirectoryOnly);
            
            int deletedCount = 0;
            foreach (string filePath in tempFiles)
            {
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Could not delete {filePath}: {ex.Message}");
                }
            }

            Debug.Log($"✓ {deletedCount} temporary files deleted");
            EditorUtility.DisplayDialog("Cleanup complete", $"{deletedCount} temporary files have been deleted.", "OK");
        }

        /// <summary>
        /// Validates the current configuration
        /// </summary>
        // [MenuItem("Tools/Hunyuan3D/Validate Configuration")] // Removed as requested
        public static void ValidateConfiguration()
        {
            var config = Hunyuan3DConfig.Load();
            string errorMessage;
            
            if (config.IsValid(out errorMessage))
            {
                EditorUtility.DisplayDialog("Valid configuration", "The current configuration is correct and ready to use.", "OK");
                Debug.Log("✓ Configuration validated successfully");
            }
            else
            {
                EditorUtility.DisplayDialog("Configuration error", $"Problem found: {errorMessage}", "OK");
                Debug.LogError($"✗ Configuration error: {errorMessage}");
            }
        }

        /// <summary>
        /// Opens the configuration folder
        /// </summary>
        // [MenuItem("Tools/Hunyuan3D/Open Config Folder")] // Removed as requested
        public static void OpenConfigFolder()
        {
            string configPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "Unity",
                "Hunyuan3D"
            );

            if (Directory.Exists(configPath))
            {
                EditorUtility.RevealInFinder(configPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Folder not found", "The configuration folder has not been created yet. Try opening the plugin first.", "OK");
            }
        }
    }
}
