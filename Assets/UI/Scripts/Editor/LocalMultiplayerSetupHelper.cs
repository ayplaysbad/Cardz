#if UNITY_EDITOR
using LastFreeCity.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastFreeCity.UI.Editor
{
    public static class LocalMultiplayerSetupHelper
    {
        [MenuItem("Last Free City/Setup Local Multiplayer Test")]
        public static void SetupLocalMultiplayerTest()
        {
            GameObject hudGo = GameObject.Find("GameHUD");
            if (hudGo == null)
            {
                Debug.LogError("Couldn't find 'GameHUD' in the active scene. Run 'Last Free City/Setup UI Scene' first.");
                return;
            }

            UIManager uiManager = hudGo.GetComponent<UIManager>();
            if (uiManager == null)
            {
                Debug.LogError("'GameHUD' is missing UIManager. Run 'Last Free City/Setup UI Scene' first.");
                return;
            }

            GameObject bootstrapGo = GameObject.Find("LocalMultiplayerBootstrap");
            if (bootstrapGo == null)
            {
                bootstrapGo = new GameObject("LocalMultiplayerBootstrap");
                Undo.RegisterCreatedObjectUndo(bootstrapGo, "Create Local Multiplayer Bootstrap");
            }

            LocalMultiplayerTestBootstrap bootstrap = bootstrapGo.GetComponent<LocalMultiplayerTestBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = bootstrapGo.AddComponent<LocalMultiplayerTestBootstrap>();
            }

            MatchPrototypeDefinition prototype = FindDefaultPrototype();
            SerializedObject serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("uiManager").objectReferenceValue = uiManager;
            serializedBootstrap.FindProperty("prototypeMatch").objectReferenceValue = prototype;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            if (uiManager.prototypeMatch == null && prototype != null)
            {
                SerializedObject serializedUi = new SerializedObject(uiManager);
                serializedUi.FindProperty("prototypeMatch").objectReferenceValue = prototype;
                serializedUi.ApplyModifiedPropertiesWithoutUndo();
            }

            Selection.activeGameObject = bootstrapGo;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log(prototype != null
                ? "Local multiplayer test setup is ready. Select 'LocalMultiplayerBootstrap' to review the host/client settings."
                : "Local multiplayer bootstrap was created, but no MatchPrototypeDefinition was found automatically. Assign one in the inspector.");
        }

        private static MatchPrototypeDefinition FindDefaultPrototype()
        {
            string[] guids = AssetDatabase.FindAssets("t:MatchPrototypeDefinition", new[] { "Assets/UI/GameData/MatchPrototypes" });
            if (guids.Length == 0)
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<MatchPrototypeDefinition>(path);
        }
    }
}
#endif
