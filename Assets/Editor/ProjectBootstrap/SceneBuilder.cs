using System.IO;
using Nightwall;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectBootstrap
{
    /// <summary>
    /// Headless scaffold builder for the Nightwall prototype. Creates a <see cref="MapConfig"/>
    /// ScriptableObject (the single source of truth for map size), placeholder materials and
    /// prefabs (enemy, wall, trap), and a playable scene: an orthographic iso Cinemachine rig, a
    /// flat ground sized to the map with a baked NavMesh, a central HQ core, edge spawn points, a
    /// grid system and the wired game-state / wave / building systems. There are NO player-controlled
    /// units — defence is purely architectural. Primitives are intentional placeholders.
    ///
    /// Run headless with:
    ///   -executeMethod ProjectBootstrap.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        const string MatDir = "Assets/Art/Materials";
        const string PrefabDir = "Assets/Prefabs";
        const string SceneDir = "Assets/Scenes";
        const string ConfigDir = "Assets/ScriptableObjects";
        const string ScenePath = SceneDir + "/Nightwall.unity";
        const string MapConfigPath = ConfigDir + "/MapConfig.asset";

        static int _ground, _building;

        [MenuItem("Nightwall/Rebuild Prototype Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(SceneDir);
            EnsureFolder(ConfigDir);

            _ground = EnsureLayer("Ground");
            _building = EnsureLayer("Building");

            MapConfig map = MakeMapConfig();

            Material groundMat = MakeMat("Ground", new Color(0.16f, 0.18f, 0.22f));
            Material hqMat = MakeMat("HQ", new Color(0.25f, 0.5f, 0.95f));
            Material enemyMat = MakeMat("Enemy", new Color(0.9f, 0.25f, 0.25f));
            Material wallMat = MakeMat("Wall", new Color(0.55f, 0.55f, 0.6f));
            Material trapMat = MakeMat("Trap", new Color(0.85f, 0.7f, 0.2f));
            Material ghostMat = MakeMat("Ghost", new Color(0.4f, 0.9f, 1f));

            GameObject enemyPrefab = BuildEnemyPrefab(enemyMat);
            GameObject wallPrefab = BuildWallPrefab(wallMat);
            GameObject trapPrefab = BuildTrapPrefab(trapMat);

            AssetDatabase.SaveAssets();

            BuildScene(map, groundMat, hqMat, enemyPrefab, wallPrefab, trapPrefab, ghostMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneBuilder] Done.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ---------- Config ----------

        static MapConfig MakeMapConfig()
        {
            var map = AssetDatabase.LoadAssetAtPath<MapConfig>(MapConfigPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<MapConfig>();
                AssetDatabase.CreateAsset(map, MapConfigPath);
            }
            var so = new SerializedObject(map);
            so.FindProperty("width").intValue = 60;
            so.FindProperty("height").intValue = 60;
            so.FindProperty("cellSize").floatValue = 1f;
            so.FindProperty("origin").vector3Value = Vector3.zero;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
            return map;
        }

        // ---------- Prefabs ----------

        static GameObject BuildEnemyPrefab(Material body)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Enemy";
            root.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            Paint(root, body);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f; agent.height = 1.8f; agent.speed = 3.5f; agent.angularSpeed = 720f; agent.acceleration = 20f;

            // Kinematic body so the NavMesh-driven agent still raises trigger events (traps).
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 40f);
            root.AddComponent<NavAgentMotor>();
            root.AddComponent<Enemy>();

            return SavePrefab(root, "Enemy");
        }

        static GameObject BuildWallPrefab(Material body)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Wall";
            root.transform.localScale = new Vector3(1f, 1.6f, 1f);
            SetLayerRecursive(root, _building);
            Paint(root, body);

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 200f);
            root.AddComponent<Buildable>();
            AddCarvingObstacle(root, new Vector3(1f, 1.6f, 1f));

            return SavePrefab(root, "Wall");
        }

        static GameObject BuildTrapPrefab(Material body)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Trap";
            // A flat floor tile; left on the Default layer so the breach AI (Building mask) ignores it.
            root.transform.localScale = new Vector3(1f, 0.1f, 1f);
            Paint(root, body);

            var col = root.GetComponent<BoxCollider>();
            col.isTrigger = true;

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 100f);
            root.AddComponent<Buildable>();
            root.AddComponent<Trap>();

            return SavePrefab(root, "Trap");
        }

        // ---------- Scene ----------

        static void BuildScene(MapConfig map, Material groundMat, Material hqMat,
            GameObject enemyPrefab, GameObject wallPrefab, GameObject trapPrefab, Material ghostMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Vector2 worldSize = map.WorldSize;
            Vector2 worldMin = map.WorldMin;
            Vector2 worldMax = map.WorldMax;

            // Sun
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.86f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Flat ground sized to the map (a Unity plane is 10x10 units at scale 1).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = map.Origin;
            ground.transform.localScale = new Vector3(worldSize.x / 10f, 1f, worldSize.y / 10f);
            ground.layer = _ground;
            Paint(ground, groundMat);
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // HQ core at the map centre — blocks the bake so agents path to its edge.
            var hq = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hq.name = "HQ";
            hq.transform.localScale = new Vector3(4f, 3f, 4f);
            hq.transform.position = map.Origin + new Vector3(0f, 1.5f, 0f);
            hq.layer = _building;
            Paint(hq, hqMat);
            var hqHealth = hq.AddComponent<Health>();
            SetFloat(hqHealth, "maxHealth", 1000f);
            var hqComp = hq.AddComponent<Hq>();

            // Bake now that the static blockers (ground + HQ) exist.
            surface.BuildNavMesh();

            // Grid system (spatial source of truth) referencing the MapConfig.
            var gridGo = new GameObject("GridSystem");
            var grid = gridGo.AddComponent<GridSystem>();
            SetObject(grid, "config", map);

            // Camera rig: Main Camera (Brain) + an orthographic iso CinemachineCamera.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            var vcamGo = new GameObject("IsoCamera");
            vcamGo.transform.position = map.Origin + new Vector3(-28f, 32f, -28f);
            vcamGo.transform.rotation = Quaternion.LookRotation(map.Origin - vcamGo.transform.position, Vector3.up);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            LensSettings lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = 16f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 500f;
            vcam.Lens = lens;
            var camCtrl = vcamGo.AddComponent<IsoCameraController>();
            SetObject(camCtrl, "map", map);

            // Spawn points at the four map edges (inset slightly so they sit on the NavMesh).
            var spawnRoot = new GameObject("SpawnPoints");
            const float inset = 2f;
            Vector3[] spots =
            {
                new Vector3(map.Origin.x, 0f, worldMax.y - inset),
                new Vector3(worldMax.x - inset, 0f, map.Origin.z),
                new Vector3(map.Origin.x, 0f, worldMin.y + inset),
                new Vector3(worldMin.x + inset, 0f, map.Origin.z),
            };
            var spawns = new Transform[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var sp = new GameObject($"Spawn {i + 1}");
                sp.transform.SetParent(spawnRoot.transform, false);
                sp.transform.position = spots[i];
                spawns[i] = sp.transform;
            }

            // Game systems (no SelectionManager — the player never commands units).
            var systems = new GameObject("GameSystems");
            var gameManager = systems.AddComponent<GameManager>();
            var waveSpawner = systems.AddComponent<WaveSpawner>();
            var placer = systems.AddComponent<BuildingPlacer>();

            // ----- Wire references via SerializedObject (robust for private [SerializeField]) -----
            SetObject(gameManager, "hq", hqComp);
            SetObject(gameManager, "waveSpawner", waveSpawner);

            SetObject(waveSpawner, "enemyPrefab", enemyPrefab);
            SetObject(waveSpawner, "hq", hq.transform);
            SetArray(waveSpawner, "spawnPoints", spawns);

            SetArray(placer, "buildables", new Object[] { wallPrefab, trapPrefab });
            SetMask(placer, "groundMask", _ground);
            SetObject(placer, "ghostMaterial", ghostMat);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Make Nightwall the first (enabled) scene in the build.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false),
            };
        }

        // ---------- Helpers ----------

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Material MakeMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Paint(GameObject go, Material mat)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        static void AddCarvingObstacle(GameObject go, Vector3 size)
        {
            var o = go.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Box;
            o.size = size;
            o.carving = true;
        }

        static GameObject SavePrefab(GameObject root, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        static int EnsureLayer(string name)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var so = new SerializedObject(asset);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = name;
                    so.ApplyModifiedProperties();
                    return i;
                }
            }
            Debug.LogWarning($"[SceneBuilder] No free layer slot for '{name}'; using Default.");
            return 0;
        }

        static void SetFloat(Object comp, string prop, float value)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).floatValue = value;
            so.ApplyModifiedProperties();
        }

        static void SetObject(Object comp, string prop, Object value)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        static void SetMask(Object comp, string prop, int layer)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).intValue = 1 << layer;
            so.ApplyModifiedProperties();
        }

        static void SetArray(Object comp, string prop, Object[] values)
        {
            var so = new SerializedObject(comp);
            var p = so.FindProperty(prop);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
        }
    }
}
