#if UNITY_EDITOR
using LastFreeCity.Gameplay;
using UnityEditor;
using UnityEngine;

namespace LastFreeCity.UI.Editor
{
    [CustomEditor(typeof(LocalMultiplayerTestBootstrap))]
    public class LocalMultiplayerTestBootstrapEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Local Multiplayer Controls", EditorStyles.boldLabel);

            var bootstrap = (LocalMultiplayerTestBootstrap)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Host"))
                {
                    bootstrap.StartHostMode();
                }

                if (GUILayout.Button("Start Client"))
                {
                    bootstrap.StartClientMode();
                }
            }

            if (GUILayout.Button("Start Dedicated Server"))
            {
                bootstrap.StartDedicatedServerMode();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Shutdown Host"))
                {
                    bootstrap.ShutdownHost();
                }

                if (GUILayout.Button("Shutdown Client"))
                {
                    bootstrap.ShutdownClient();
                }
            }

            if (GUILayout.Button("Shutdown Multiplayer"))
            {
                bootstrap.ShutdownMultiplayer();
            }
        }
    }
}
#endif
