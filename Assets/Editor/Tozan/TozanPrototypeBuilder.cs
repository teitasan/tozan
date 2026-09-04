using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using StarterAssets;
using Object = UnityEngine.Object;

namespace Tozan.Editor
{
    public static class TozanPrototypeBuilder
    {
        const string ClimbingScenePath = "Assets/Scenes/ClimbingSandbox.unity";
        const string StarterScenePath = "Assets/Scenes/StarterAssetsPlayground.unity";
        const string TerrainScenePath = "Assets/Scenes/TerrainSandbox.unity";
        const string NaturalScenePath = "Assets/Scenes/NaturalRockSandbox.unity";
        const string PlayerPrefabPath = "Assets/ThirdParty/DynamicParkourSystem/Prefabs/Player.prefab";
        const string InputActionsPath = "Assets/StarterAssets/InputSystem/StarterAssets.inputactions";
        const string RockMaterialPath = "Assets/Materials/NaturalRock.mat";

        static readonly Vector3[] BoxLayout =
        {
            new(2f, 0.5f, 4f),
            new(4.2f, 1.0f, 4f),
            new(6.6f, 1.5f, 4f),
            new(9.2f, 0.75f, 4f),
            new(3f, 1.2f, 7.5f),
            new(5.5f, 2.0f, 8f),
            new(8.5f, 2.4f, 8.5f),
            new(12f, 1.0f, 6f),
            new(14f, 1.8f, 8f),
            new(16.5f, 0.6f, 5f)
        };

        [MenuItem("Tozan/Build Prototype Scenes")]
        public static void BuildAllFromMenu()
        {
            var result = BuildAll();
            Debug.Log(result);
        }

        public static string BuildAll()
        {
            EnsureFolders();
            ConvertBuiltInMaterialsToUrp();
            try { TozanCharacterSetup.EnsureReady(); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            string climbing;
            string starter;
            string terrain;
            string natural;
            try { climbing = BuildClimbingSandbox(); }
            catch (System.Exception ex)
            {
                climbing = "FAILED: " + ex;
                Debug.LogException(ex);
            }
            try { starter = BuildStarterAssetsPlayground(); }
            catch (System.Exception ex)
            {
                starter = "FAILED: " + ex;
                Debug.LogException(ex);
            }
            try { terrain = BuildTerrainSandbox(); }
            catch (System.Exception ex)
            {
                terrain = "FAILED: " + ex;
                Debug.LogException(ex);
            }
            try { natural = BuildNaturalRockSandbox(); }
            catch (System.Exception ex)
            {
                natural = "FAILED: " + ex;
                Debug.LogException(ex);
            }
            AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ClimbingScenePath, true),
                new EditorBuildSettingsScene(StarterScenePath, true),
                new EditorBuildSettingsScene(TerrainScenePath, true),
                new EditorBuildSettingsScene(NaturalScenePath, true)
            };
            return $"ok climbing={climbing} starter={starter} terrain={terrain} natural={natural}";
        }

        [MenuItem("Tozan/Build Natural Rock Sandbox")]
        public static void BuildNaturalFromMenu()
        {
            EnsureFolders();
            try { TozanCharacterSetup.EnsureReady(); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            Debug.Log(BuildNaturalRockSandbox());
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
        }

        static int ConvertBuiltInMaterialsToUrp()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
                return 0;

            var converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ThirdParty/DynamicParkourSystem" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                    continue;
                if (mat.shader.name.StartsWith("Universal Render Pipeline"))
                    continue;
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.gray;
                mat.shader = urpLit;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(mat);
                converted++;
            }

            return converted;
        }

        public static string BuildClimbingSandbox()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "ClimbingSandbox";

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            ground.isStatic = true;

            var boxes = new GameObject("Boxes");
            for (var i = 0; i < BoxLayout.Length; i++)
            {
                var pos = BoxLayout[i];
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Box_{i + 1}";
                var height = 0.5f + (i % 4) * 0.4f;
                box.transform.position = new Vector3(pos.x, height * 0.5f, pos.z);
                box.transform.localScale = new Vector3(1.6f, height, 1.6f);
                box.tag = "Vault";
                box.isStatic = true;
                box.transform.SetParent(boxes.transform, true);
            }

            PlacePrefab("Assets/ThirdParty/DynamicParkourSystem/Prefabs/Environment/Climb/Ledge.prefab",
                new Vector3(2f, 0f, 14f), Quaternion.identity, "Climb_Ledge");
            PlacePrefab("Assets/ThirdParty/DynamicParkourSystem/Prefabs/Environment/Climb/Wall.prefab",
                new Vector3(8f, 0f, 14f), Quaternion.identity, "Climb_Wall");
            PlacePrefab("Assets/ThirdParty/DynamicParkourSystem/Prefabs/Environment/Climb/Small Ledge.prefab",
                new Vector3(14f, 0f, 14f), Quaternion.identity, "Climb_SmallLedge");
            PlacePrefab("Assets/ThirdParty/DynamicParkourSystem/Prefabs/Environment/Vault/Box.prefab",
                new Vector3(-3f, 0f, 6f), Quaternion.identity, "Vault_Box");
            PlacePrefab("Assets/ThirdParty/DynamicParkourSystem/Prefabs/Environment/Vault/Obstacle.prefab",
                new Vector3(-6f, 0f, 6f), Quaternion.identity, "Vault_Obstacle");
            PlacePrefab("Assets/ThirdParty/DynamicParkourSystem/Prefabs/Environment/Jump/Reach Surface.prefab",
                new Vector3(20f, 0f, 8f), Quaternion.identity, "Jump_Reach");

            var playerRoot = PlaceDpsPlayer(new Vector3(0f, 0.1f, 0f));
            RemoveCamerasOutside(playerRoot);
            SetupMainCamera(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ClimbingScenePath);
            return ClimbingScenePath;
        }

        public static string BuildNaturalRockSandbox()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "NaturalRockSandbox";

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.tag = "Untagged";
            ground.layer = 0;
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.isStatic = true;
            UseSolidGroundCollider(ground);

            var rocks = new GameObject("NaturalRocks");
            rocks.tag = "Untagged";
            rocks.layer = 0;
            TozanNaturalRockGeometry.BuildCourse(rocks.transform, GetOrCreateRockMaterial());

            var start = new GameObject("TraversalStart");
            start.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, 0.2f), Quaternion.identity);

            var playerRoot = TozanTraverserSetup.CreatePlayer(start.transform.position);
            playerRoot.transform.rotation = Quaternion.identity;
            RemoveCamerasOutside(playerRoot);
            SetupMainCamera(false);
            var loco = playerRoot.GetComponent<Traverser.TraverserLocomotionAbility>();
            if (loco != null && Camera.main != null)
                loco.cameraTransform = Camera.main.transform;
            EnsureInBuildSettings(NaturalScenePath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, NaturalScenePath);
            return NaturalScenePath;
        }

        public static string BuildStarterAssetsPlayground()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "StarterAssetsPlayground";

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            for (var i = 0; i < 6; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Step_{i + 1}";
                cube.transform.position = new Vector3(2f + i * 1.5f, 0.25f + i * 0.15f, 3f);
                cube.transform.localScale = new Vector3(1.2f, 0.5f + i * 0.3f, 1.2f);
            }

            CreateStarterAssetsPlayer(new Vector3(0f, 0.1f, 0f));
            // Casual_Male is display-only (not Humanoid). Live Starter visual is UAL2.
            var casual = AssetDatabase.LoadAssetAtPath<GameObject>(TozanCharacterSetup.CharacterPath);
            if (casual != null)
            {
                var display = (GameObject)PrefabUtility.InstantiatePrefab(casual);
                display.name = "Quaternius_CasualMale";
                display.transform.position = new Vector3(-2.5f, 0f, 2f);
            }
            SetupMainCamera(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, StarterScenePath);
            return StarterScenePath;
        }

        public static string BuildTerrainSandbox()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "TerrainSandbox";

            var data = new TerrainData
            {
                heightmapResolution = 513,
                size = new Vector3(200f, 40f, 200f)
            };

            var heights = new float[data.heightmapResolution, data.heightmapResolution];
            for (var z = 0; z < data.heightmapResolution; z++)
            {
                for (var x = 0; x < data.heightmapResolution; x++)
                {
                    var nx = x / (float)data.heightmapResolution;
                    var nz = z / (float)data.heightmapResolution;
                    heights[z, x] = Mathf.PerlinNoise(nx * 4f, nz * 4f) * 0.35f
                                    + Mathf.PerlinNoise(nx * 12f, nz * 12f) * 0.08f;
                }
            }

            data.SetHeights(0, 0, heights);

            var grass = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var dirt = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            FillTex(grass, new Color(0.31f, 0.45f, 0.22f));
            FillTex(dirt, new Color(0.42f, 0.32f, 0.18f));
            AssetDatabase.CreateAsset(grass, "Assets/Materials/TerrainGrass.asset");
            AssetDatabase.CreateAsset(dirt, "Assets/Materials/TerrainDirt.asset");

            var grassTex = new TerrainLayer { diffuseTexture = grass, tileSize = new Vector2(8, 8) };
            var dirtTex = new TerrainLayer { diffuseTexture = dirt, tileSize = new Vector2(12, 12) };
            AssetDatabase.CreateAsset(grassTex, "Assets/Materials/TerrainLayerGrass.terrainlayer");
            AssetDatabase.CreateAsset(dirtTex, "Assets/Materials/TerrainLayerDirt.terrainlayer");
            data.terrainLayers = new[] { grassTex, dirtTex };

            var alphamap = new float[data.alphamapResolution, data.alphamapResolution, 2];
            for (var z = 0; z < data.alphamapResolution; z++)
            {
                for (var x = 0; x < data.alphamapResolution; x++)
                {
                    var h = data.GetHeight(
                        Mathf.RoundToInt(x / (float)data.alphamapResolution * (data.heightmapResolution - 1)),
                        Mathf.RoundToInt(z / (float)data.alphamapResolution * (data.heightmapResolution - 1)));
                    var t = Mathf.InverseLerp(2f, 18f, h);
                    alphamap[z, x, 0] = 1f - t;
                    alphamap[z, x, 1] = t;
                }
            }

            data.SetAlphamaps(0, 0, alphamap);
            AssetDatabase.CreateAsset(data, "Assets/Materials/TozanTerrain.asset");

            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Terrain";
            terrainGo.transform.position = new Vector3(-100f, 0f, -100f);

            ScatterPlaceholderTrees(terrainGo.GetComponent<Terrain>());
            AddTerrainGrass(terrainGo.GetComponent<Terrain>(), grass);
            CreateStarterAssetsPlayer(new Vector3(0f, 25f, 0f));
            SetupMainCamera(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TerrainScenePath);
            return TerrainScenePath;
        }

        static void ScatterPlaceholderTrees(Terrain terrain)
        {
            var parent = new GameObject("PlaceholderVegetation");
            var random = new System.Random(42);
            for (var i = 0; i < 40; i++)
            {
                var x = random.Next(-80, 80);
                var z = random.Next(-80, 80);
                var y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"Tree_{i}";
                trunk.transform.SetParent(parent.transform, true);
                trunk.transform.position = new Vector3(x, y + 1.2f, z);
                trunk.transform.localScale = new Vector3(0.35f, 1.2f, 0.35f);
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.transform.SetParent(trunk.transform, false);
                canopy.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                canopy.transform.localScale = new Vector3(3.2f, 2.2f, 3.2f);
            }
        }

        static void AddTerrainGrass(Terrain terrain, Texture2D grassTex)
        {
            var data = terrain.terrainData;
            data.SetDetailResolution(256, 16);
            var prototype = new DetailPrototype
            {
                prototypeTexture = grassTex,
                usePrototypeMesh = false,
                renderMode = DetailRenderMode.GrassBillboard,
                minWidth = 0.5f,
                maxWidth = 1.0f,
                minHeight = 0.4f,
                maxHeight = 0.9f,
                healthyColor = new Color(0.35f, 0.55f, 0.2f),
                dryColor = new Color(0.45f, 0.4f, 0.2f),
                noiseSpread = 0.2f
            };
            data.detailPrototypes = new[] { prototype };
            var map = new int[data.detailWidth, data.detailHeight];
            for (var z = 0; z < data.detailHeight; z++)
            {
                for (var x = 0; x < data.detailWidth; x++)
                {
                    map[z, x] = (x + z) % 7 == 0 ? 2 : 0;
                }
            }

            data.SetDetailLayer(0, 0, 0, map);
            terrain.detailObjectDistance = 80f;
        }

        static void FillTex(Texture2D tex, Color color)
        {
            var pixels = new Color[tex.width * tex.height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
        }

        static GameObject PlaceDpsPlayer(Vector3 position)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                var playerRoot = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerRoot.name = "Player";
                playerRoot.transform.position = position;
                return playerRoot;
            }

            Debug.LogWarning("DPS Player prefab missing; creating Starter Assets player instead.");
            return CreateStarterAssetsPlayer(position);
        }

        static Material GetOrCreateRockMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
            if (mat != null)
                return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.45f, 0.38f, 0.32f));
            AssetDatabase.CreateAsset(mat, RockMaterialPath);
            return mat;
        }

        static void EnsureInBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var existing in scenes)
            {
                if (existing.path == path)
                    return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static GameObject PlacePrefab(string path, Vector3 position, Quaternion rotation, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab: {path}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        static GameObject CreateStarterAssetsPlayer(Vector3 position)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "PlayerArmature";
            player.transform.position = position;
            var capsuleCollider = player.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
                Object.DestroyImmediate(capsuleCollider);

            var playerLayer = LayerMask.NameToLayer("Player");
            player.layer = playerLayer >= 0 ? playerLayer : 0;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.93f, 0f);

            var cameraRoot = new GameObject("PlayerCameraRoot");
            cameraRoot.transform.SetParent(player.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0f, 1.375f, 0f);

            player.AddComponent<StarterAssetsInputs>();
            var tpc = player.AddComponent<StarterAssets.ThirdPersonController>();
            tpc.CinemachineCameraTarget = cameraRoot;
            var defaultMask = LayerMask.GetMask("Default");
            tpc.GroundLayers = defaultMask != 0 ? defaultMask : 1;

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var playerInput = player.GetComponent<PlayerInput>() ?? player.AddComponent<PlayerInput>();
            if (actions != null)
                playerInput.actions = actions;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;

            var followCam = new GameObject("PlayerFollowCamera");
            var cmCam = followCam.AddComponent<CinemachineCamera>();
            cmCam.Follow = cameraRoot.transform;
            cmCam.Priority = 10;
            var follow = followCam.AddComponent<CinemachineThirdPersonFollow>();
            follow.CameraDistance = 4.0f;
            follow.ShoulderOffset = new Vector3(0.5f, 0.0f, 0.0f);
            follow.VerticalArmLength = 0.4f;
            follow.CameraSide = 1.0f;
            follow.Damping = new Vector3(0.1f, 0.25f, 0.3f);

            TozanCharacterSetup.AttachVisualToStarterPlayer(player);
            return player;
        }

        public static void UseSolidGroundCollider(GameObject ground)
        {
            if (ground == null)
                return;
            var mesh = ground.GetComponent<MeshCollider>();
            if (mesh != null)
                Object.DestroyImmediate(mesh);
            var box = ground.GetComponent<BoxCollider>();
            if (box == null)
                box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(10f, 1f, 10f);
            box.center = new Vector3(0f, -0.5f, 0f);
        }

        static void RemoveCamerasOutside(GameObject keepRoot)
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (keepRoot != null && cam.transform.IsChildOf(keepRoot.transform))
                    continue;
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        static void SetupMainCamera(bool addSwitchCameras)
        {
            var main = Camera.main;
            if (main == null)
            {
                var camGo = new GameObject("Main Camera");
                main = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            if (main.GetComponent<CinemachineBrain>() == null)
                main.gameObject.AddComponent<CinemachineBrain>();
            if (main.GetComponent<AudioListener>() == null)
                main.gameObject.AddComponent<AudioListener>();
            if (addSwitchCameras && main.GetComponent<Climbing.SwitchCameras>() == null)
                main.gameObject.AddComponent<Climbing.SwitchCameras>();
        }
    }
}
