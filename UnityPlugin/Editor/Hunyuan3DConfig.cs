using System;
using System.IO;
using UnityEngine;

namespace Hunyuan3D.Editor
{
    /// <summary>
    /// Configuració i utilitats per al plugin Hunyuan3D
    /// </summary>
    [Serializable]
    public class Hunyuan3DConfig
    {
        [Header("Paths")]
        public string pythonExecutablePath = "python";
        public string scriptBasePath = "";
        
        [Header("Model Settings")]
        public string modelPath = "tencent/Hunyuan3D-2mini";
        public string subfolder = "hunyuan3d-dit-v2-mini-turbo";
        public string texgenModelPath = "tencent/Hunyuan3D-2";
        public string device = "cuda";
        public string mcAlgo = "mc";
        
        [Header("Generation Parameters")]
        public int steps = 30;
        public float guidanceScale = 7.5f;
        public int seed = 1234;
        public int octreeResolution = 256;
        public int numChunks = 200000;
        public string fileType = "obj";
        
        [Header("Options")]
        public bool enableT23D = false;
        public bool disableTexture = false;
        public bool enableFlashVDM = false;
        public bool compile = false;
        public bool lowVramMode = false;
        public bool removeBackground = true;
        
        /// <summary>
        /// Carrega la configuració des del directori persistent
        /// </summary>
        public static Hunyuan3DConfig Load()
        {
            string configPath = GetConfigPath();
            
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    return JsonUtility.FromJson<Hunyuan3DConfig>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error carregant configuració Hunyuan3D: {ex.Message}");
                }
            }
            
            return new Hunyuan3DConfig();
        }
        
        /// <summary>
        /// Guarda la configuració al directori persistent
        /// </summary>
        public void Save()
        {
            try
            {
                string configPath = GetConfigPath();
                string configDir = Path.GetDirectoryName(configPath);
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error guardant configuració Hunyuan3D: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obté el path del fitxer de configuració
        /// </summary>
        private static string GetConfigPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Unity",
                "Hunyuan3D",
                "config.json"
            );
        }
        
        /// <summary>
        /// Valida la configuració actual
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = "";
            
            // Validar path de Python
            if (string.IsNullOrEmpty(pythonExecutablePath))
            {
                errorMessage = "Python executable path no pot estar buit.";
                return false;
            }
            
            // Validar path dels scripts
            if (string.IsNullOrEmpty(scriptBasePath) || !Directory.Exists(scriptBasePath))
            {
                errorMessage = "Script base path no és vàlid.";
                return false;
            }
            
            // Verificar existència dels scripts
            string batchScript = Path.Combine(scriptBasePath, "batch_hunyuan3d.py");
            if (!File.Exists(batchScript))
            {
                errorMessage = "batch_hunyuan3d.py no trobat en el script base path.";
                return false;
            }
            
            // Validar paràmetres numèrics
            if (steps <= 0)
            {
                errorMessage = "Steps ha de ser major que 0.";
                return false;
            }
            
            if (guidanceScale <= 0)
            {
                errorMessage = "Guidance Scale ha de ser major que 0.";
                return false;
            }
            
            if (octreeResolution <= 0)
            {
                errorMessage = "Octree Resolution ha de ser major que 0.";
                return false;
            }
            
            if (numChunks <= 0)
            {
                errorMessage = "Num Chunks ha de ser major que 0.";
                return false;
            }
            
            return true;
        }
    }
    
    /// <summary>
    /// Utilitats diverses per al plugin
    /// </summary>
    public static class Hunyuan3DUtils
    {
        /// <summary>
        /// Extensions d'imatge suportades
        /// </summary>
        public static readonly string[] SupportedImageExtensions = 
        {
            "jpg", "jpeg", "png", "bmp", "webp", "tiff"
        };
        
        /// <summary>
        /// Extensions de model 3D suportades
        /// </summary>
        public static readonly string[] SupportedModelExtensions = 
        {
            "obj", "fbx", "glb", "ply", "stl"
        };
        
        /// <summary>
        /// Comprova si un fitxer és una imatge suportada
        /// </summary>
        public static bool IsImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
                
            string extension = Path.GetExtension(filePath).ToLower().TrimStart('.');
            return Array.Exists(SupportedImageExtensions, ext => ext == extension);
        }
        
        /// <summary>
        /// Comprova si un fitxer és un model 3D suportat
        /// </summary>
        public static bool IsModelFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
                
            string extension = Path.GetExtension(filePath).ToLower().TrimStart('.');
            return Array.Exists(SupportedModelExtensions, ext => ext == extension);
        }
        
        /// <summary>
        /// Converteix un path absolut a un path relatiu d'Assets
        /// </summary>
        public static string GetRelativeAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return null;
                
            string dataPath = Application.dataPath;
            if (absolutePath.StartsWith(dataPath))
            {
                return "Assets" + absolutePath.Substring(dataPath.Length).Replace('\\', '/');
            }
            
            return null;
        }
        
        /// <summary>
        /// Genera un nom únic per un asset basat en el nom de la imatge original
        /// </summary>
        public static string GenerateAssetName(string imagePath, string suffix = "")
        {
            if (string.IsNullOrEmpty(imagePath))
                return "Generated3DModel";
                
            string baseName = Path.GetFileNameWithoutExtension(imagePath);
            return string.IsNullOrEmpty(suffix) ? baseName : $"{baseName}_{suffix}";
        }
        
        /// <summary>
        /// Obté totes les imatges d'una carpeta
        /// </summary>
        public static string[] GetImagesInFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return new string[0];
                
            var imageFiles = new System.Collections.Generic.List<string>();
            
            foreach (string extension in SupportedImageExtensions)
            {
                string[] files = Directory.GetFiles(folderPath, $"*.{extension}", SearchOption.TopDirectoryOnly);
                imageFiles.AddRange(files);
            }
            
            return imageFiles.ToArray();
        }
        
        /// <summary>
        /// Formata una durada en segons a un format llegible
        /// </summary>
        public static string FormatDuration(float seconds)
        {
            if (seconds < 60)
                return $"{seconds:F1}s";
            
            int minutes = (int)(seconds / 60);
            float remainingSeconds = seconds % 60;
            
            return $"{minutes}m {remainingSeconds:F1}s";
        }
        
        /// <summary>
        /// Formata una mida de fitxer a un format llegible
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int suffixIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            
            return $"{size:F1} {suffixes[suffixIndex]}";
        }
    }
}
