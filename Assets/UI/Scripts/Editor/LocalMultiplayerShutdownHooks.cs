#if UNITY_EDITOR
using LastFreeCity.Gameplay;
using UnityEditor;
using UnityEngine;

namespace LastFreeCity.UI.Editor
{
    [InitializeOnLoad]
    public static class LocalMultiplayerShutdownHooks
    {
        static LocalMultiplayerShutdownHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ShutdownAllSessions;
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownAllSessions;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode)
            {
                ShutdownAllSessions();
            }
        }

        private static void ShutdownAllSessions()
        {
            var bootstraps = Object.FindObjectsByType<LocalMultiplayerTestBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bootstraps.Length; i++)
            {
                if (bootstraps[i] != null)
                {
                    bootstraps[i].ShutdownMultiplayer();
                }
            }
        }
    }
}
#endif
