using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hunyuan3D.Editor
{
    /// <summary>
    /// Exemple d'ús programàtic del plugin Hunyuan3D
    /// Aquest script mostra com utilitzar les funcionalitats del plugin des del codi
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
            EditorGUILayout.LabelField("Hunyuan3D Plugin - Exemples d'Ús", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Configuració Ràpida per CPU", GUILayout.Height(30)))
            {
                SetupForCPU();
            }

            if (GUILayout.Button("Configuració per GPU NVIDIA", GUILayout.Height(30)))
            {
                SetupForGPU();
            }

            if (GUILayout.Button("Configuració Mode Ràpid", GUILayout.Height(30)))
            {
                SetupFastMode();
            }

            if (GUILayout.Button("Configuració Alta Qualitat", GUILayout.Height(30)))
            {
                SetupHighQuality();
            }

            EditorGUILayout.Space(20);
            
            EditorGUILayout.LabelField("Ús Programàtic:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Obrir Generador Principal"))
            {
                Hunyuan3DGenerator.ShowWindow();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Aquest exemple mostra configuracions preestablertes per diferents escenaris d'ús. " +
                "Cada botó configura automàticament els paràmetres òptims per al cas d'ús específic.",
                MessageType.Info
            );
        }

        /// <summary>
        /// Configuració optimitzada per ús amb CPU
        /// </summary>
        private void SetupForCPU()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.device = "cpu";
            config.steps = 20;  // Menys passos per CPU
            config.octreeResolution = 128;  // Resolució més baixa
            config.lowVramMode = true;
            config.compile = false;  // No compilar per CPU
            config.enableFlashVDM = false;
            config.disableTexture = true;  // Desactivar textures per rapidesa
            
            config.Save();
            
            Debug.Log("✓ Configuració optimitzada per CPU aplicada");
            ShowNotification(new GUIContent("Configuració CPU aplicada!"));
        }

        /// <summary>
        /// Configuració optimitzada per GPU NVIDIA amb CUDA
        /// </summary>
        private void SetupForGPU()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.device = "cuda";
            config.steps = 30;
            config.octreeResolution = 256;
            config.lowVramMode = false;
            config.compile = true;  // Compilar per millor rendiment
            config.enableFlashVDM = true;  // Acceleració
            config.disableTexture = false;
            
            config.Save();
            
            Debug.Log("✓ Configuració optimitzada per GPU aplicada");
            ShowNotification(new GUIContent("Configuració GPU aplicada!"));
        }

        /// <summary>
        /// Configuració per processat ràpid amb qualitat acceptable
        /// </summary>
        private void SetupFastMode()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.steps = 15;  // Pocs passos
            config.guidanceScale = 5.0f;  // Guidance més baix
            config.octreeResolution = 128;  // Resolució baixa
            config.fileType = "obj";  // Format ràpid
            config.disableTexture = true;  // Sense textures
            config.lowVramMode = true;
            
            config.Save();
            
            Debug.Log("✓ Mode ràpid activat");
            ShowNotification(new GUIContent("Mode ràpid activat!"));
        }

        /// <summary>
        /// Configuració per màxima qualitat
        /// </summary>
        private void SetupHighQuality()
        {
            var config = Hunyuan3DConfig.Load();
            
            config.steps = 50;  // Molts passos
            config.guidanceScale = 10.0f;  // Guidance alt
            config.octreeResolution = 384;  // Alta resolució
            config.fileType = "fbx";  // Format complet
            config.disableTexture = false;  // Amb textures
            config.enableFlashVDM = true;
            config.compile = true;
            
            config.Save();
            
            Debug.Log("✓ Mode alta qualitat activat");
            ShowNotification(new GUIContent("Mode alta qualitat activat!"));
        }
    }

    /// <summary>
    /// Utilitat per automatzar tasques comunes
    /// </summary>
    public static class Hunyuan3DAutomation
    {
        /// <summary>
        /// Processa automàticament totes les imatges d'una carpeta
        /// Exemple d'ús des d'altres scripts
        /// </summary>
        // [MenuItem("Tools/Hunyuan3D/Auto Process Selected Folder")] // Removed as requested
        public static void AutoProcessSelectedFolder()
        {
            string folderPath = EditorUtility.OpenFolderPanel("Seleccionar carpeta d'imatges", "", "");
            
            if (string.IsNullOrEmpty(folderPath))
                return;

            string[] imageFiles = Hunyuan3DUtils.GetImagesInFolder(folderPath);
            
            if (imageFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Cap imatge trobada", "No s'han trobat imatges a la carpeta seleccionada.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Processar imatges",
                $"S'han trobat {imageFiles.Length} imatges. Vols processar-les totes?",
                "Sí", "Cancel·lar"
            );

            if (proceed)
            {
                // Obrir la finestra principal amb la carpeta preseleccionada
                var window = EditorWindow.GetWindow<Hunyuan3DGenerator>();
                // Aquí normalment passaríem el path, però com que les variables són privades,
                // l'usuari haurà de seleccionar manualment la carpeta a la interfície
                Debug.Log($"Processa les {imageFiles.Length} imatges de: {folderPath}");
                
                EditorUtility.DisplayDialog(
                    "Instruccions",
                    "S'ha obert la finestra del generador 3D. " +
                    "Activa 'Mode Batch' i selecciona la carpeta per processar totes les imatges.",
                    "Entès"
                );
            }
        }

        /// <summary>
        /// Neteja fitxers temporals generats pel plugin
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
                    Debug.LogWarning($"No s'ha pogut eliminar {filePath}: {ex.Message}");
                }
            }

            Debug.Log($"✓ {deletedCount} fitxers temporals eliminats");
            EditorUtility.DisplayDialog("Neteja completada", $"S'han eliminat {deletedCount} fitxers temporals.", "OK");
        }

        /// <summary>
        /// Valida la configuració actual
        /// </summary>
        // [MenuItem("Tools/Hunyuan3D/Validate Configuration")] // Removed as requested
        public static void ValidateConfiguration()
        {
            var config = Hunyuan3DConfig.Load();
            string errorMessage;
            
            if (config.IsValid(out errorMessage))
            {
                EditorUtility.DisplayDialog("Configuració vàlida", "La configuració actual és correcta i està llesta per utilitzar.", "OK");
                Debug.Log("✓ Configuració validada correctament");
            }
            else
            {
                EditorUtility.DisplayDialog("Error de configuració", $"Problema trobat: {errorMessage}", "OK");
                Debug.LogError($"✗ Error de configuració: {errorMessage}");
            }
        }

        /// <summary>
        /// Obre la carpeta de configuració
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
                EditorUtility.DisplayDialog("Carpeta no trobada", "La carpeta de configuració encara no s'ha creat. Prova obrir el plugin primer.", "OK");
            }
        }
    }
}
