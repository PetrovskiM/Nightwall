using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class ProjectConfig
    {
        // Force-reserialize the scene file to text while it is NOT the active scene.
        public static void ResaveScene()
        {
            Debug.Log($"[ProjectConfig] mode before = {(int)EditorSettings.serializationMode}");
            EditorSettings.serializationMode = SerializationMode.ForceText;
            Debug.Log($"[ProjectConfig] mode after = {(int)EditorSettings.serializationMode}");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.ForceReserializeAssets(
                new[] { "Assets/Scenes/Nightwall.unity" },
                ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectConfig] Scene force-reserialized to text.");
            EditorApplication.Exit(0);
        }

        // Force text (YAML) asset serialization — essential for readable git diffs and merges.
        public static void ForceTextSerialization()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets();
            Debug.Log("[ProjectConfig] Serialization = ForceText; assets reserialized.");
            EditorApplication.Exit(0);
        }
    }
}
