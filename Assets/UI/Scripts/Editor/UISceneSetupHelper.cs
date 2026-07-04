#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastFreeCity.UI.Editor
{
    public static class UISceneSetupHelper
    {
        [MenuItem("Last Free City/Setup UI Scene")]
        public static void SetupUIScene()
        {
            // 1. Locate visual assets
            string uxmlPath = "Assets/UI/UXML/MainHUD.uxml";
            string thumbPath = "Assets/UI/UXML/CardThumbnail.uxml";
            
            VisualTreeAsset mainHUDUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            VisualTreeAsset cardThumbUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(thumbPath);

            if (mainHUDUxml == null)
            {
                Debug.LogError($"Could not find MainHUD UXML at '{uxmlPath}'. Make sure files are in the right folders.");
                return;
            }

            // 2. Load or Create PanelSettings asset configured for portrait mobile
            string panelSettingsPath = "Assets/UI/LastFreeCityPanelSettings.asset";
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);

            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                
                // Set Scale With Screen Size properties for mobile portrait (1080x1920)
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1080, 1920);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;

                AssetDatabase.CreateAsset(panelSettings, panelSettingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created PanelSettings asset at: '{panelSettingsPath}' configured for 1080x1920 Mobile Portrait.");
            }
            else
            {
                // Force update resolution settings
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1080, 1920);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                EditorUtility.SetDirty(panelSettings);
                AssetDatabase.SaveAssets();
            }

            // 3. Find or Create GameObject in the current scene
            GameObject hudGo = GameObject.Find("GameHUD");
            if (hudGo == null)
            {
                hudGo = new GameObject("GameHUD");
                Undo.RegisterCreatedObjectUndo(hudGo, "Create GameHUD");
            }

            // 4. Setup UI Document component
            UIDocument uiDoc = hudGo.GetComponent<UIDocument>();
            if (uiDoc == null)
            {
                uiDoc = hudGo.AddComponent<UIDocument>();
            }
            uiDoc.panelSettings = panelSettings;
            uiDoc.visualTreeAsset = mainHUDUxml;

            // 5. Setup UIManager component
            UIManager manager = hudGo.GetComponent<UIManager>();
            if (manager == null)
            {
                manager = hudGo.AddComponent<UIManager>();
            }
            manager.cardThumbnailTemplate = cardThumbUxml;

            // Mark Scene as dirty to prompt saving
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            
            Selection.activeGameObject = hudGo;
            
            Debug.Log("Successfully setup the UI Scene! HUD and Mobile Portrait Panel Settings are configured. Check the Inspector of the GameHUD GameObject.");
        }
    }
}
#endif
