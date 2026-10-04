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
        // MapConfig lives under Resources so GridSystem can recover it at runtime even when the
        // binary-saved scene drops the serialized reference.
        const string ConfigDir = "Assets/Resources";
        const string ScenePath = SceneDir + "/Nightwall.unity";
        const string MapConfigPath = ConfigDir + "/MapConfig.asset";

        static int _ground, _building;
        static EnemyDefinition _basicDef, _runnerDef, _bruteDef, _swarmDef;

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
            Material reinforcedMat = MakeMat("ReinforcedWall", new Color(0.32f, 0.36f, 0.46f));
            Material gateMat = MakeMat("Gate", new Color(0.6f, 0.45f, 0.2f));
            Material trapMat = MakeMat("Trap", new Color(0.85f, 0.7f, 0.2f));
            Material ghostMat = MakeTransparentMat("Ghost", new Color(0.2f, 0.9f, 0.2f, 0.5f));
            Material gridMat = MakeUnlitTransparentMat("GridLines", new Color(0.55f, 0.75f, 1f, 0.14f));

            // Enemy archetypes (data assets; the one Enemy prefab reads these at spawn). Under
            // Resources so WaveSpawner can recover the default even if the binary scene drops the ref.
            MakeEnemyDefinitions();

            GameObject enemyPrefab = BuildEnemyPrefab(enemyMat);
            GameObject wallPrefab = BuildWallPrefab(wallMat);
            GameObject reinforcedPrefab = BuildReinforcedWallPrefab(reinforcedMat);
            GameObject gatePrefab = BuildGatePrefab(gateMat);
            GameObject trapPrefab = BuildTrapPrefab(trapMat);

            AssetDatabase.SaveAssets();

            BuildScene(map, groundMat, hqMat, enemyPrefab, wallPrefab, reinforcedPrefab, gatePrefab,
                trapPrefab, ghostMat, gridMat);

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
            // Smoother crowd motion: good-quality avoidance plus a small stopping distance keeps
            // agents from grinding into the exact same point.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.GoodQualityObstacleAvoidance;
            agent.stoppingDistance = 0.3f;

            // Kinematic body so the NavMesh-driven agent still raises trigger events (traps).
            // Interpolate so the rendered capsule doesn't snap between the agent's per-frame moves.
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 40f);
            root.AddComponent<NavAgentMotor>();
            root.AddComponent<AttackEffect>();
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
            SetFloat(health, "maxHealth", 120f);
            root.AddComponent<Buildable>();
            var structure = root.AddComponent<DefensiveStructure>();
            SetString(structure, "displayName", "Wall");
            SetInt(structure, "cost", 10);
            // Carve slightly past the cell so two diagonally-placed walls overlap at their shared
            // corner and seal the pinch — otherwise the horde slips through the diagonal gap.
            AddCarvingObstacle(root, new Vector3(1.1f, 1.6f, 1.1f));

            return SavePrefab(root, "Wall");
        }

        static GameObject BuildReinforcedWallPrefab(Material body)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "ReinforcedWall";
            // A touch taller/stockier so it reads as the heavy-duty wall at phone scale.
            root.transform.localScale = new Vector3(1f, 2.0f, 1f);
            SetLayerRecursive(root, _building);
            Paint(root, body);

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 500f);
            root.AddComponent<Buildable>();
            var structure = root.AddComponent<DefensiveStructure>();
            SetString(structure, "displayName", "Reinforced");
            SetInt(structure, "cost", 40);
            AddCarvingObstacle(root, new Vector3(1.1f, 2.0f, 1.1f));

            return SavePrefab(root, "ReinforcedWall");
        }

        static GameObject BuildGatePrefab(Material body)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Gate";
            root.transform.localScale = new Vector3(1f, 1.4f, 1f);
            SetLayerRecursive(root, _building);
            Paint(root, body);

            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", 200f);
            root.AddComponent<Buildable>();
            // Carve like a wall while closed; Gate lifts the obstacle (and sinks the visual) when open.
            AddCarvingObstacle(root, new Vector3(1.1f, 1.4f, 1.1f));
            var gate = root.AddComponent<Gate>();
            SetString(gate, "displayName", "Gate");
            SetInt(gate, "cost", 25);

            return SavePrefab(root, "Gate");
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
            var structure = root.AddComponent<DefensiveStructure>();
            SetString(structure, "displayName", "Trap");
            SetInt(structure, "cost", 15);
            root.AddComponent<Trap>();

            return SavePrefab(root, "Trap");
        }

        // ---------- Scene ----------

        static void BuildScene(MapConfig map, Material groundMat, Material hqMat,
            GameObject enemyPrefab, GameObject wallPrefab, GameObject reinforcedPrefab,
            GameObject gatePrefab, GameObject trapPrefab, Material ghostMat, Material gridMat)
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
            // Placeholder base health bar: reads the HQ's Health only, floats above the core.
            var hqBar = hq.AddComponent<HqHealthBar>();
            SetFloat(hqBar, "heightOffset", 3.5f);

            // Bake now that the static blockers (ground + HQ) exist.
            surface.BuildNavMesh();

            // Grid system (spatial source of truth) referencing the MapConfig.
            var gridGo = new GameObject("GridSystem");
            var grid = gridGo.AddComponent<GridSystem>();
            SetObject(grid, "config", map);

            // Visible in-game grid overlay (gizmos only show in the Scene view).
            var overlayGo = new GameObject("GridOverlay");
            overlayGo.transform.position = Vector3.zero;
            overlayGo.AddComponent<MeshFilter>();
            var overlayRenderer = overlayGo.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = gridMat;
            overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            var overlay = overlayGo.AddComponent<GridOverlay>();
            SetObject(overlay, "config", map);

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

            // Entrances around the whole perimeter — four edge midpoints and four corners — so the
            // horde can be made to attack from any combination of directions (inset so they sit on
            // the NavMesh). Each carries a SpawnPoint component; waves pick which ones are active.
            var spawnRoot = new GameObject("SpawnPoints");
            const float inset = 2f;
            float ex = worldMax.x - inset, nx = worldMin.x + inset;
            float ez = worldMax.y - inset, sz = worldMin.y + inset;
            (Vector3 pos, Color color, string name)[] spots =
            {
                (new Vector3(map.Origin.x, 0f, ez), new Color(0.90f, 0.30f, 0.20f), "N"),
                (new Vector3(ex, 0f, ez),           new Color(0.95f, 0.60f, 0.20f), "NE"),
                (new Vector3(ex, 0f, map.Origin.z), new Color(0.90f, 0.85f, 0.25f), "E"),
                (new Vector3(ex, 0f, sz),           new Color(0.40f, 0.85f, 0.30f), "SE"),
                (new Vector3(map.Origin.x, 0f, sz), new Color(0.30f, 0.80f, 0.80f), "S"),
                (new Vector3(nx, 0f, sz),           new Color(0.35f, 0.55f, 0.95f), "SW"),
                (new Vector3(nx, 0f, map.Origin.z), new Color(0.60f, 0.40f, 0.95f), "W"),
                (new Vector3(nx, 0f, ez),           new Color(0.95f, 0.45f, 0.80f), "NW"),
            };
            var spawns = new SpawnPoint[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var sp = new GameObject($"Spawn {i} ({spots[i].name})");
                sp.transform.SetParent(spawnRoot.transform, false);
                sp.transform.position = spots[i].pos;
                var comp = sp.AddComponent<SpawnPoint>();
                SetColor(comp, "gizmoColor", spots[i].color);
                spawns[i] = comp;
            }

            // Game systems (no SelectionManager — the player never commands units).
            // AttackDirectionSelector must be added before WaveSpawner (RequireComponent order).
            var systems = new GameObject("GameSystems");
            var selector = systems.AddComponent<AttackDirectionSelector>();
            var waveSpawner = systems.AddComponent<WaveSpawner>();
            var gameManager = systems.AddComponent<GameManager>();
            var placer = systems.AddComponent<BuildingPlacer>();
            var buildBar = systems.AddComponent<BuildBar>();
            systems.AddComponent<DevHud>();

            // Configure AttackDirectionSelector difficulty curves.
            // Min active sides: 1 all the way through (even late waves can be 1-side).
            // Max active sides: ramps from 1 at wave 1 to 4 at wave 8+.
            SetAnimCurve(selector, "minSidesCurve",
                new Keyframe[] { new Keyframe(1, 1), new Keyframe(10, 1) });
            SetAnimCurve(selector, "maxSidesCurve",
                new Keyframe[] { new Keyframe(1, 1), new Keyframe(4, 2), new Keyframe(7, 3), new Keyframe(10, 4) });
            SetObject(selector, "map", map);

            // ----- Wire references via SerializedObject (robust for private [SerializeField]) -----
            SetObject(gameManager, "hq", hqComp);
            SetObject(gameManager, "waveSpawner", waveSpawner);
            SetObject(gameManager, "attackDirectionSelector", selector);

            SetObject(waveSpawner, "enemyPrefab", enemyPrefab);
            SetObject(waveSpawner, "hq", hq.transform);
            SetObject(waveSpawner, "defaultDefinition", _basicDef);
            SetObject(waveSpawner, "levelConfig", MakeLevelConfig());

            SetArray(placer, "buildables",
                new Object[] { wallPrefab, reinforcedPrefab, gatePrefab, trapPrefab });
            SetMask(placer, "groundMask", _ground);
            SetMask(placer, "buildingMask", _building);
            SetObject(placer, "ghostMaterial", ghostMat);

            SetObject(buildBar, "placer", placer);

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

        static Material MakeTransparentMat(string name, Color color)
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
            // URP transparent surface setup.
            mat.SetFloat("_Surface", 1f);       // 1 = Transparent
            mat.SetFloat("_Blend", 0f);         // Alpha blend
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetShaderPassEnabled("ShadowCaster", false);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material MakeUnlitTransparentMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);       // 1 = Transparent
            mat.SetFloat("_Blend", 0f);         // Alpha blend
            mat.SetFloat("_Cull", 0f);          // draw both faces
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
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

        // LevelConfig lives under Resources (like MapConfig) so WaveSpawner can recover it at
        // runtime even if the binary-saved scene drops the serialized reference.
        const string LevelConfigPath = ConfigDir + "/LevelConfig.asset";

        /// <summary>
        /// Author a handful of escalating nights, then leave the rest to the procedural fallback.
        /// Groups use <see cref="AttackSide"/> rather than spawn-point indices; the
        /// <see cref="AttackDirectionSelector"/> may override which sides are actually active at
        /// runtime, but the authored counts and archetypes are always honoured.
        /// </summary>
        static LevelConfig MakeLevelConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<LevelConfig>();
                AssetDatabase.CreateAsset(cfg, LevelConfigPath);
            }

            // (interval, [(side, count, archetype), ...]) per authored night.
            var waves = new (float interval, (AttackSide side, int count, EnemyDefinition def)[] groups)[]
            {
                // night 1: single-side, plain Basics.
                (0.60f, new[] { (AttackSide.North, 6, _basicDef) }),
                // night 2: two-side pincer.
                (0.55f, new[] { (AttackSide.North, 5, _basicDef), (AttackSide.South, 6, _runnerDef) }),
                // night 3: three edges — Brute arrives to crack walls.
                (0.50f, new[] { (AttackSide.North, 5, _basicDef), (AttackSide.East, 6, _runnerDef), (AttackSide.West, 2, _bruteDef) }),
                // night 4: all four edges, every archetype.
                (0.40f, new[] { (AttackSide.North, 5, _basicDef), (AttackSide.East, 8, _runnerDef), (AttackSide.South, 16, _swarmDef), (AttackSide.West, 3, _bruteDef) }),
            };

            var so = new SerializedObject(cfg);
            var wavesProp = so.FindProperty("waves");
            wavesProp.arraySize = waves.Length;
            for (int w = 0; w < waves.Length; w++)
            {
                var wp = wavesProp.GetArrayElementAtIndex(w);
                wp.FindPropertyRelative("spawnInterval").floatValue = waves[w].interval;
                var gp = wp.FindPropertyRelative("groups");
                var groups = waves[w].groups;
                gp.arraySize = groups.Length;
                for (int g = 0; g < groups.Length; g++)
                {
                    var ep = gp.GetArrayElementAtIndex(g);
                    ep.FindPropertyRelative("side").enumValueIndex = (int)groups[g].side;
                    ep.FindPropertyRelative("count").intValue = groups[g].count;
                    ep.FindPropertyRelative("enemyDefinition").objectReferenceValue = groups[g].def;
                }
            }
            so.FindProperty("proceduralBaseCount").intValue = 8;
            so.FindProperty("proceduralCountPerWave").intValue = 4;
            so.FindProperty("proceduralSpawnInterval").floatValue = 0.45f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        /// <summary>
        /// Author the four starting archetypes as data assets under Resources. Only stats differ;
        /// behaviour is shared in the Enemy component. No special abilities yet. Each tints the base
        /// capsule (no visual prefab) so the archetypes read apart at phone scale.
        /// </summary>
        static void MakeEnemyDefinitions()
        {
            // (file, name, hp, speed, wallDps, cooldown, size, color, cost)
            _basicDef = MakeEnemyDefinition("Enemy_Basic", "Basic",
                40f, 3.5f, 15f, 1.0f, 1.0f, new Color(0.90f, 0.25f, 0.25f), 1);
            _runnerDef = MakeEnemyDefinition("Enemy_Runner", "Runner",
                16f, 6.5f, 6f, 0.8f, 0.75f, new Color(0.95f, 0.85f, 0.25f), 1);
            _bruteDef = MakeEnemyDefinition("Enemy_Brute", "Brute",
                450f, 1.6f, 60f, 1.2f, 1.7f, new Color(0.45f, 0.25f, 0.55f), 5);
            _swarmDef = MakeEnemyDefinition("Enemy_Swarm", "Swarm",
                8f, 4.2f, 2f, 0.6f, 0.5f, new Color(0.80f, 0.80f, 0.85f), 1);
        }

        static EnemyDefinition MakeEnemyDefinition(string file, string displayName,
            float hp, float speed, float wallDps, float cooldown, float size, Color color, int cost)
        {
            string path = $"{ConfigDir}/{file}.asset";
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            var so = new SerializedObject(def);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("maxHealth").floatValue = hp;
            so.FindProperty("moveSpeed").floatValue = speed;
            so.FindProperty("wallDamagePerSecond").floatValue = wallDps;
            so.FindProperty("attackCooldown").floatValue = cooldown;
            so.FindProperty("size").floatValue = size;
            so.FindProperty("bodyColor").colorValue = color;
            so.FindProperty("spawnCost").intValue = cost;
            so.FindProperty("visualPrefab").objectReferenceValue = null;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(def);
            return def;
        }

        static void SetColor(Object comp, string prop, Color value)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).colorValue = value;
            so.ApplyModifiedProperties();
        }

        static void SetFloat(Object comp, string prop, float value)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).floatValue = value;
            so.ApplyModifiedProperties();
        }

        static void SetInt(Object comp, string prop, int value)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).intValue = value;
            so.ApplyModifiedProperties();
        }

        static void SetString(Object comp, string prop, string value)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).stringValue = value;
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

        static void SetAnimCurve(Object comp, string prop, Keyframe[] keys)
        {
            var so = new SerializedObject(comp);
            so.FindProperty(prop).animationCurveValue = new AnimationCurve(keys);
            so.ApplyModifiedProperties();
        }
    }
}
