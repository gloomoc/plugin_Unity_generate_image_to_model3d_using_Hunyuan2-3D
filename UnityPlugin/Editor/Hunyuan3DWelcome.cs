using UnityEditor;
using UnityEngine;

namespace Hunyuan3D.Editor
{
    /// <summary>
    /// Pantalla de benvinguda i guia d'instal·lació per Hunyuan3D
    /// </summary>
    public class Hunyuan3DWelcome : EditorWindow
    {
        private Vector2 scrollPosition = Vector2.zero;
        private bool showOnStartup = true;
        
        // Icones i estils
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle stepStyle;
        
        private const string SHOW_ON_STARTUP_KEY = "Hunyuan3D_ShowWelcomeOnStartup";
        
        // [MenuItem("Tools/Hunyuan3D/Welcome & Setup Guide")] // Removed as requested
        public static void ShowWindow()
        {
            var window = GetWindow<Hunyuan3DWelcome>("Hunyuan3D Welcome");
            window.minSize = new Vector2(600, 500);
            window.maxSize = new Vector2(800, 700);
            window.Show();
        }
        
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(SHOW_ON_STARTUP_KEY, true))
                {
                    ShowWindow();
                }
            };
        }
        
        private void OnEnable()
        {
            showOnStartup = EditorPrefs.GetBool(SHOW_ON_STARTUP_KEY, true);
            SetupStyles();
        }
        
        private void SetupStyles()
        {
            titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.6f, 1f) }
            };
            
            subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            
            stepStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true
            };
        }
        
        private void OnGUI()
        {
            if (titleStyle == null) SetupStyles();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(20);
            
            DrawQuickStart();
            EditorGUILayout.Space(15);
            
            DrawInstallationSteps();
            EditorGUILayout.Space(15);
            
            DrawTroubleshooting();
            EditorGUILayout.Space(15);
            
            DrawFooter();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("🎯 Benvingut a Hunyuan3D Unity Plugin", titleStyle);
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField(
                "Aquest plugin et permet generar models 3D a partir d'imatges utilitzant " +
                "la potent IA Hunyuan3D-2 de Tencent, directament des de Unity.",
                stepStyle
            );
            
            EditorGUILayout.Space(10);
            
            // Estadístiques del repositori
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("⭐ 10.5k GitHub Stars | 🍴 947 Forks | 📅 Actualitzat 2025", stepStyle);
                GUILayout.FlexibleSpace();
            }
        }
        
        private void DrawQuickStart()
        {
            EditorGUILayout.LabelField("🚀 Inici Ràpid", subtitleStyle);
            
            EditorGUILayout.HelpBox(
                "Per començar ràpidament, segueix aquests 3 passos:",
                MessageType.Info
            );
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1️⃣ <b>Instal·la Dependències</b>", stepStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("   Obre el gestor automàtic de dependències:", stepStyle);
                    if (GUILayout.Button("Dependency Manager", GUILayout.Width(150)))
                    {
                        Hunyuan3DDependencyManager.ShowWindow();
                    }
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("2️⃣ <b>Configura Scripts</b>", stepStyle);
                EditorGUILayout.LabelField("   Col·loca batch_hunyuan3d.py i remove_background.py al directori del projecte", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("3️⃣ <b>Comença a Generar</b>", stepStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("   Obre el generador principal:", stepStyle);
                    if (GUILayout.Button("3D Model Generator", GUILayout.Width(150)))
                    {
                        Hunyuan3DGenerator.ShowWindow();
                    }
                }
            }
        }
        
        private void DrawInstallationSteps()
        {
            EditorGUILayout.LabelField("📋 Guia Detallada d'Instal·lació", subtitleStyle);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("<b>Pas 1: Requisits del Sistema</b>", stepStyle);
                EditorGUILayout.LabelField("• Python 3.8 o superior", stepStyle);
                EditorGUILayout.LabelField("• 6 GB VRAM per generació de forma", stepStyle);
                EditorGUILayout.LabelField("• 16 GB VRAM total per forma + textura", stepStyle);
                EditorGUILayout.LabelField("• Unity 2020.3 LTS o superior", stepStyle);
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("<b>Pas 2: Instal·lació de Dependències Python</b>", stepStyle);
                EditorGUILayout.LabelField("Opció A (Recomanada): Usa el nostre Dependency Manager automàtic", stepStyle);
                EditorGUILayout.LabelField("Opció B: Instal·lació manual seguint README.md", stepStyle);
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("<b>Pas 3: Scripts de Python</b>", stepStyle);
                EditorGUILayout.LabelField("• Descarrega els scripts del repositori oficial", stepStyle);
                EditorGUILayout.LabelField("• Col·loca'ls al directori del projecte Unity", stepStyle);
                EditorGUILayout.LabelField("• El plugin els detectarà automàticament", stepStyle);
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("<b>Pas 4: Configuració Unity</b>", stepStyle);
                EditorGUILayout.LabelField("• Configura paths de Python i scripts", stepStyle);
                EditorGUILayout.LabelField("• Ajusta paràmetres segons el teu hardware", stepStyle);
                EditorGUILayout.LabelField("• Prova amb una imatge d'exemple", stepStyle);
            }
        }
        
        private void DrawTroubleshooting()
        {
            EditorGUILayout.LabelField("🔧 Resolució de Problemes Comuns", subtitleStyle);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("<b>❌ Python no trobat</b>", stepStyle);
                EditorGUILayout.LabelField("→ Assegura't que Python estigui instal·lat i sigui accessible des del PATH", stepStyle);
                EditorGUILayout.LabelField("→ Usa el botó 'Detectar' al Dependency Manager", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("<b>❌ Dependències no instal·lades</b>", stepStyle);
                EditorGUILayout.LabelField("→ Usa 'Instal·lar Tot' al Dependency Manager", stepStyle);
                EditorGUILayout.LabelField("→ Si falla, prova crear un Conda Environment", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("<b>❌ Error de CUDA</b>", stepStyle);
                EditorGUILayout.LabelField("→ Selecciona mode 'CPU' si no tens targeta NVIDIA", stepStyle);
                EditorGUILayout.LabelField("→ Actualitza drivers NVIDIA si tens targeta compatible", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("<b>❌ Models no s'importen</b>", stepStyle);
                EditorGUILayout.LabelField("→ Verifica que la carpeta de sortida estigui dins d'Assets/", stepStyle);
                EditorGUILayout.LabelField("→ Comprova permisos d'escriptura de Unity", stepStyle);
            }
        }
        
        private void DrawFooter()
        {
            EditorGUILayout.Space(10);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("📚 Recursos Addicionals:", stepStyle);
                
                if (GUILayout.Button("GitHub Oficial", GUILayout.Width(100)))
                {
                    Application.OpenURL("https://github.com/Tencent-Hunyuan/Hunyuan3D-2");
                }
                
                if (GUILayout.Button("Demo Online", GUILayout.Width(100)))
                {
                    Application.OpenURL("https://3d.hunyuan.tencent.com/");
                }
                
                if (GUILayout.Button("HuggingFace", GUILayout.Width(100)))
                {
                    Application.OpenURL("https://huggingface.co/spaces/tencent/Hunyuan3D-2");
                }
            }
            
            EditorGUILayout.Space(10);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                showOnStartup = EditorGUILayout.Toggle("Mostrar aquesta finestra a l'inici", showOnStartup);
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Tancar", GUILayout.Width(80)))
                {
                    Close();
                }
            }
            
            // Guardar preferència
            EditorPrefs.SetBool(SHOW_ON_STARTUP_KEY, showOnStartup);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("© 2025 Hunyuan3D Unity Plugin - Basat en Tencent Hunyuan3D-2", EditorStyles.centeredGreyMiniLabel);
        }
    }
}
