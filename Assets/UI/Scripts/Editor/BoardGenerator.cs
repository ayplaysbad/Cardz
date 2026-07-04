#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastFreeCity.UI.Editor
{
    public static class BoardGenerator
    {
        [MenuItem("Last Free City/Clear Scene GameObjects")]
        public static void ClearSceneObjects()
        {
            // Find and delete the GameBoard GameObject container in the active scene
            GameObject boardContainer = GameObject.Find("GameBoard");
            if (boardContainer != null)
            {
                Undo.DestroyObjectImmediate(boardContainer);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("Successfully cleaned up old 2D World GameObjects from the scene. The board is now rendered inside the UI scroll container.");
            }
            else
            {
                Debug.Log("No old GameBoard container found in the scene to clear.");
            }
        }
    }
}
#endif
