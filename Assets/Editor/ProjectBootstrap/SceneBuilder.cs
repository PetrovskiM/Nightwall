using System.Collections.Generic;
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
    /// Headless scaffold builder: creates placeholder materials + prefabs (unit, enemy, wall,
    /// tower) and a playable Nightwall scene (iso Cinemachine rig, ground + baked NavMesh, HQ,
    /// spawn points, wired game systems, a few starter units). Primitives are intentional
    /// placeholders — swap in real low-poly art later.
    ///
    /// Run headless with:
    ///   -executeMethod ProjectBootstrap.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        const string MatDir = "Assets/Art/Materials";
        const string PrefabDir = "Assets/Prefabs";
        const string SceneDir = "Assets/Scenes";
        const string ScenePath = SceneDir + "/Nightwall.unity";

        static int _ground, _building, _selectable;

        public static void Build()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(SceneDir);

            _ground = EnsureLayer("Ground");
            _building = EnsureLayer("Building");
            _selectable = EnsureLayer("Selectable");

            Material groundMat = MakeMat("Ground", new Color(0.16f, 0.18f, 0.22f));
            Material hqMat = MakeMat("HQ", new Color(0.25f, 0.5f, 0.95f));
            Material unitMat = MakeMat("Unit", new Color(0.3f, 0.8f, 0.4f));
            Material enemyMat = MakeMat("Enemy", new Color(0.9f, 0.25f, 0.25f));
            Material wallMat = MakeMat("Wall", new Color(0.55f, 0.55f, 0.6f));
            Material towerMat = MakeMat("Tower", new Color(0.95f, 0.6f, 0.2f));
            Material ringMat = MakeMat("SelectRing", new Color(0.9f, 1f, 0.4f));
            Material ghostMat = MakeMat("Ghost", new Color(0.4f, 0.9f, 1f));

            GameObject unitPrefab = BuildUnitPrefab(unitMat, ringMat);
            GameObject enemyPrefab = BuildEnemyPrefab(enemyMat);
            GameObject wallPrefab = BuildWallPrefab(wallMat);
            GameObject towerPrefab = BuildTowerPrefab(towerMat);

            AssetDatabase.SaveAssets();

            BuildScene(groundMat, hqMat, unitPrefab, enemyPrefab, wallPrefab, towerPrefab, ghostMat);

            Debug.Log("[SceneBuilder] Done.");
            EditorApplication.Exit(0);
        }

        // ---------- Prefabs ----------

        static GameObject BuildUnitPrefab(Material body, Material ring)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Unit";
            root.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            SetLayerRecursive(root, _selectable);
            Paint(root, body);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f; agent.height = 1.8f; agent.speed = 5f; agent.angularSpeed = 720f; agent.acceleration = 30f;

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 80f);
            root.AddComponent<UnitController>();
            var selectable = root.AddComponent<Selectable>();

            // Selection ring at the unit's feet, hidden by default.
            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "SelectionRing";
            Object.DestroyImmediate(ringGo.GetComponent<Collider>());
            ringGo.transform.SetParent(root.transform, false);
            ringGo.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            ringGo.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            Paint(ringGo, ring);
            SetObject(selectable, "selectionIndicator", ringGo);

            return SavePrefab(root, "Unit");
        }

        static GameObject BuildEnemyPrefab(Material body)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Enemy";
            root.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            Paint(root, body);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f; agent.height = 1.8f; agent.speed = 3.5f; agent.angularSpeed = 720f; agent.acceleration = 20f;

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 40f);
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

        static GameObject BuildTowerPrefab(Material body)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "Tower";
            root.transform.localScale = new Vector3(1f, 1.2f, 1f);
            SetLayerRecursive(root, _building);
            Paint(root, body);

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 150f);
            var buildable = root.AddComponent<Buildable>();
            SetVector2Int(buildable, "footprint", new Vector2Int(1, 1));
            AddCarvingObstacle(root, new Vector3(1f, 2.4f, 1f));

            return SavePrefab(root, "Tower");
        }

        // ---------- Scene ----------

        static void BuildScene(Material groundMat, Material hqMat, GameObject unitPrefab,
            GameObject enemyPrefab, GameObject wallPrefab, GameObject towerPrefab, Material ghostMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sun
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.86f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground (80x80), with a baked NavMeshSurface
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.layer = _ground;
            Paint(ground, groundMat);
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // HQ at center — blocks the bake so agents path to its edge.
            var hq = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hq.name = "HQ";
            hq.transform.localScale = new Vector3(4f, 3f, 4f);
            hq.transform.position = new Vector3(0f, 1.5f, 0f);
            hq.layer = _building;
            Paint(hq, hqMat);
            var hqHealth = hq.AddComponent<Health>();
            SetFloat(hqHealth, "maxHealth", 1000f);
            var hqComp = hq.AddComponent<Hq>();

            // Bake now that the static blockers (ground + HQ) exist.
            surface.BuildNavMesh();

            // Camera rig: Main Camera (Brain) + an orthographic iso CinemachineCamera.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            var vcamGo = new GameObject("IsoCamera");
            vcamGo.transform.position = new Vector3(-28f, 32f, -28f);
            vcamGo.transform.rotation = Quaternion.LookRotation(Vector3.zero - vcamGo.transform.position, Vector3.up);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            LensSettings lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = 16f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 500f;
            vcam.Lens = lens;
            vcamGo.AddComponent<RTSCameraController>();

            // Spawn points at the map edges.
            var spawnRoot = new GameObject("SpawnPoints");
            var spawns = new Transform[4];
            Vector3[] spots =
            {
                new Vector3(0f, 0f, 38f), new Vector3(38f, 0f, 0f),
                new Vector3(0f, 0f, -38f), new Vector3(-38f, 0f, 0f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var sp = new GameObject($"Spawn {i + 1}");
                sp.transform.SetParent(spawnRoot.transform, false);
                sp.transform.position = spots[i];
                spawns[i] = sp.transform;
            }

            // Game systems
            var systems = new GameObject("GameSystems");
            var gameManager = systems.AddComponent<GameManager>();
            var waveSpawner = systems.AddComponent<WaveSpawner>();
            var placer = systems.AddComponent<BuildingPlacer>();
            var selection = systems.AddComponent<SelectionManager>();

            // Starter units near the HQ.
            var unitsRoot = new GameObject("Units");
            Vector3[] unitSpots = { new Vector3(6f, 0f, 0f), new Vector3(8f, 0f, 1.5f), new Vector3(7f, 0f, -1.5f) };
            foreach (var pos in unitSpots)
            {
                var u = (GameObject)PrefabUtility.InstantiatePrefab(unitPrefab);
                u.transform.SetParent(unitsRoot.transform, false);
                u.transform.position = pos;
            }

            // ----- Wire references via SerializedObject (robust for private [SerializeField]) -----
            SetObject(gameManager, "hq", hqComp);
            SetObject(gameManager, "waveSpawner", waveSpawner);

            SetObject(waveSpawner, "enemyPrefab", enemyPrefab);
            SetObject(waveSpawner, "hq", hq.transform);
            SetArray(waveSpawner, "spawnPoints", spawns);

            SetMask(selection, "selectableMask", _selectable);
            SetMask(selection, "groundMask", _ground);
            SetObject(selection, "buildingPlacer", placer);

            SetArray(placer, "buildables", new Object[] { wallPrefab, towerPrefab });
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

        static void SetVector2Int(Object comp, string prop, Vector2Int value)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).vector2IntValue = value;
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
