using System;
using UnityEditor;
using UnityEngine;

namespace Hunyuan3D.Editor
{
    /// <summary>
    /// Welcome and installation guide screen for Hunyuan3D
    /// </summary>
    public class Hunyuan3DWelcome : EditorWindow
    {
        private Vector2 scrollPosition = Vector2.zero;
        private bool showOnStartup = true;
        
        // Icons and styles
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
            titleStyle = null;
            subtitleStyle = null;
            stepStyle = null;
        }
        
        private bool EnsureStyles()
        {
            if (titleStyle != null && subtitleStyle != null && stepStyle != null)
            {
                return true;
            }

            if (EditorStyles.label == null)
            {
                return false;
            }

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

            return true;
        }

        private void RunAfterGui(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                action?.Invoke();
            };

            GUIUtility.ExitGUI();
        }
        
        private void OnGUI()
        {
            if (!EnsureStyles())
            {
                EditorGUILayout.HelpBox("Unity is still initializing the editor UI. Reopen this window in a moment if needed.", MessageType.Info);
                Repaint();
                return;
            }

            bool scrollViewStarted = false;
            try
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                scrollViewStarted = true;

                DrawHeader();
                EditorGUILayout.Space(20);

                DrawQuickStart();
                EditorGUILayout.Space(15);

                DrawInstallationSteps();
                EditorGUILayout.Space(15);

                DrawTroubleshooting();
                EditorGUILayout.Space(15);

                DrawFooter();
            }
            finally
            {
                if (scrollViewStarted)
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("🎯 Welcome to Hunyuan3D Unity Plugin", titleStyle);
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField(
                "This plugin allows you to generate 3D models from images using " +
                "Tencent's powerful Hunyuan3D-2 AI, directly from Unity.",
                stepStyle
            );
            
            EditorGUILayout.Space(10);
            
            // Repository statistics
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("⭐ 10.5k GitHub Stars | 🍴 947 Forks | 📅 Updated 2025", stepStyle);
                GUILayout.FlexibleSpace();
            }
        }
        
        private void DrawQuickStart()
        {
            EditorGUILayout.LabelField("🚀 Quick Start", subtitleStyle);
            
            EditorGUILayout.HelpBox(
                "To get started quickly, follow these 3 steps:",
                MessageType.Info
            );
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1️⃣ <b>Install Dependencies</b>", stepStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("   Open the automatic dependency manager:", stepStyle);
                    if (GUILayout.Button("Dependency Manager", GUILayout.Width(150)))
                    {
                        RunAfterGui(() => Hunyuan3DDependencyManager.ShowWindow());
                    }
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("2️⃣ <b>Configure Scripts</b>", stepStyle);
                EditorGUILayout.LabelField("   Place batch_hunyuan3d.py and remove_background.py in the project directory", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("3️⃣ <b>Start Generating</b>", stepStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("   Open the main generator:", stepStyle);
                    if (GUILayout.Button("3D Model Generator", GUILayout.Width(150)))
                    {
                        RunAfterGui(() => Hunyuan3DGenerator.ShowWindow());
                    }
                }
            }
        }
        
        private void DrawInstallationSteps()
        {
            EditorGUILayout.LabelField("📋 Detailed Installation Guide", subtitleStyle);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("<b>Step 1: System Requirements</b>", stepStyle);
                EditorGUILayout.LabelField("• Python 3.8 or higher", stepStyle);
                EditorGUILayout.LabelField("• 6 GB VRAM for shape generation", stepStyle);
                EditorGUILayout.LabelField("• 16 GB VRAM total for shape + texture", stepStyle);
                EditorGUILayout.LabelField("• Unity 2020.3 LTS or higher", stepStyle);
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("<b>Step 2: Python Dependency Installation</b>", stepStyle);
                EditorGUILayout.LabelField("Option A (Recommended): Use our automatic Dependency Manager", stepStyle);
                EditorGUILayout.LabelField("Option B: Manual installation following README.md", stepStyle);
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("<b>Step 3: Python Scripts</b>", stepStyle);
                EditorGUILayout.LabelField("• Download the scripts from the official repository", stepStyle);
                EditorGUILayout.LabelField("• Place them in the Unity project directory", stepStyle);
                EditorGUILayout.LabelField("• The plugin will detect them automatically", stepStyle);
                
                EditorGUILayout.Space(10);
                
                EditorGUILayout.LabelField("<b>Step 4: Unity Configuration</b>", stepStyle);
                EditorGUILayout.LabelField("• Configure Python and script paths", stepStyle);
                EditorGUILayout.LabelField("• Adjust parameters according to your hardware", stepStyle);
                EditorGUILayout.LabelField("• Test with an example image", stepStyle);
            }
        }
        
        private void DrawTroubleshooting()
        {
            EditorGUILayout.LabelField("🔧 Common Troubleshooting", subtitleStyle);
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("<b>❌ Python not found</b>", stepStyle);
                EditorGUILayout.LabelField("→ Make sure Python is installed and accessible from the PATH", stepStyle);
                EditorGUILayout.LabelField("→ Use the 'Detect' button in the Dependency Manager", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("<b>❌ Dependencies not installed</b>", stepStyle);
                EditorGUILayout.LabelField("→ Use 'Install All' in the Dependency Manager", stepStyle);
                EditorGUILayout.LabelField("→ If it fails, try creating a Conda Environment", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("<b>❌ CUDA Error</b>", stepStyle);
                EditorGUILayout.LabelField("→ Select 'CPU' mode if you don't have an NVIDIA card", stepStyle);
                EditorGUILayout.LabelField("→ Update NVIDIA drivers if you have a compatible card", stepStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("<b>❌ Models are not imported</b>", stepStyle);
                EditorGUILayout.LabelField("→ Verify that the output folder is inside Assets/", stepStyle);
                EditorGUILayout.LabelField("→ Check Unity's write permissions", stepStyle);
            }
        }
        
        private void DrawFooter()
        {
            EditorGUILayout.Space(10);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("📚 Additional Resources:", stepStyle);
                
                if (GUILayout.Button("Official GitHub", GUILayout.Width(100)))
                {
                    Application.OpenURL("https://github.com/Tencent-Hunyuan/Hunyuan3D-2");
                }
                
                if (GUILayout.Button("Online Demo", GUILayout.Width(100)))
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
                showOnStartup = EditorGUILayout.Toggle("Show this window on startup", showOnStartup);
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Close", GUILayout.Width(80)))
                {
                    RunAfterGui(() => Close());
                }
            }
            
            // Save preference
            EditorPrefs.SetBool(SHOW_ON_STARTUP_KEY, showOnStartup);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("© 2025 Hunyuan3D Unity Plugin - Based on Tencent Hunyuan3D-2", EditorStyles.centeredGreyMiniLabel);
        }
    }
}
