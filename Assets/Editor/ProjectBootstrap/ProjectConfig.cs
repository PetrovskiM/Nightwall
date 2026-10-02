using Nightwall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class ProjectConfig
    {
        // Add NavAgentMotor to the Enemy prefab and restrict its breach mask to the Building layer.
        public static void PatchEnemyPrefab()
        {
            const string path = "Assets/Prefabs/Enemy.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);

            if (go.GetComponent<NavAgentMotor>() == null) go.AddComponent<NavAgentMotor>();

            var enemy = go.GetComponent<Enemy>();
            int building = LayerMask.NameToLayer("Building");
            if (enemy != null && building >= 0)
            {
                var so = new SerializedObject(enemy);
                var mask = so.FindProperty("structureMask");
                if (mask != null) { mask.intValue = 1 << building; so.ApplyModifiedProperties(); }
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            PrefabUtility.UnloadPrefabContents(go);
            Debug.Log("[ProjectConfig] Enemy prefab patched (NavAgentMotor + Building mask).");
            EditorApplication.Exit(0);
        }

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
