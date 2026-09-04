using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tozan.Editor
{
    /// <summary>
    /// STEP 15: official ECS Platformer Sample as NaturalRockSandbox player.
    /// Rocks stay unmarked. PhysicsShape is collision, not a climb marker.
    /// </summary>
    public static class TozanPlatformerSetup
    {
        public const string CharacterPrefabPath = "Assets/ThirdParty/UnityPlatformer/Prefabs/PlatformerCharacter.prefab";
        public const string PlayerPrefabPath = "Assets/ThirdParty/UnityPlatformer/Prefabs/PlatformerPlayer.prefab";
        public const string CameraPrefabPath = "Assets/ThirdParty/UnityPlatformer/Prefabs/OrbitCamera.prefab";
        public const string ContentScenePath = "Assets/Scenes/NaturalRockSandboxContent.unity";

        public static void ConvertActiveSceneToUnityPhysics()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    ConvertColliderObject(t.gameObject);
            }

            EnsurePhysicsStep();
            EnsureGlobalGravity();
            EnsureSceneInitialization();
        }

        public static void AttachSubSceneAndCamera()
        {
            var existing = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Include);
            foreach (var sub in existing)
                Object.DestroyImmediate(sub.gameObject);

            var subGo = new GameObject("PlatformerContent");
            subGo.SetActive(false);
            var subScene = subGo.AddComponent<SubScene>();
            subScene.AutoLoadScene = true;
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ContentScenePath);
            subGo.SetActive(true);
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();
            SubSceneInspectorUtility.ForceReimport(subScene);

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            if (cam.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            if (cam.GetComponent<MainGameObjectCamera>() == null)
                cam.gameObject.AddComponent<MainGameObjectCamera>();

            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null)
                Object.DestroyImmediate(brain);
        }

        static void ConvertColliderObject(GameObject go)
        {
            var filter = go.GetComponent<MeshFilter>();
            var hasCollider = go.GetComponent<UnityEngine.Collider>() != null;
            if (filter == null && !hasCollider)
                return;

            foreach (var col in go.GetComponents<UnityEngine.Collider>())
                Object.DestroyImmediate(col);

            var shape = go.GetComponent<PhysicsShapeAuthoring>();
            if (shape == null)
                shape = go.AddComponent<PhysicsShapeAuthoring>();

            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh != null && mesh.name == "Cube")
            {
                shape.SetBox(new BoxGeometry
                {
                    Center = float3.zero,
                    Size = new float3(1f, 1f, 1f),
                    Orientation = quaternion.identity,
                    BevelRadius = 0.02f
                });
            }
            else if (mesh != null)
            {
                shape.SetMesh(mesh);
            }
            else
            {
                shape.FitToEnabledRenderMeshes();
            }

            var body = go.GetComponent<PhysicsBodyAuthoring>();
            if (body == null)
                body = go.AddComponent<PhysicsBodyAuthoring>();
            body.MotionType = BodyMotionType.Static;
            body.CustomTags = CustomPhysicsBodyTags.Nothing;
        }

        static void EnsurePhysicsStep()
        {
            var existing = Object.FindObjectsByType<PhysicsStepAuthoring>(FindObjectsInactive.Include);
            if (existing.Length > 0)
                return;

            var go = new GameObject("PhysicsStep");
            var step = go.AddComponent<PhysicsStepAuthoring>();
            step.Gravity = new float3(0f, -9.81f, 0f);
        }

        static void EnsureGlobalGravity()
        {
            var existing = Object.FindObjectsByType<GlobalGravityZoneAuthoring>(FindObjectsInactive.Include);
            if (existing.Length > 0)
                return;

            var go = new GameObject("GlobalGravity");
            var zone = go.AddComponent<GlobalGravityZoneAuthoring>();
            zone.Gravity = new float3(0f, -9.81f, 0f);
        }

        static void EnsureSceneInitialization()
        {
            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
            if (characterPrefab == null || playerPrefab == null || cameraPrefab == null)
                throw new System.InvalidOperationException("Platformer prefabs missing under Assets/ThirdParty/UnityPlatformer/Prefabs");

            var start = GameObject.Find("TraversalStart");
            if (start == null)
            {
                start = new GameObject("TraversalStart");
                start.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, 0.2f), Quaternion.identity);
            }

            var initGo = GameObject.Find("SceneInitialization");
            if (initGo == null)
                initGo = new GameObject("SceneInitialization");

            var auth = initGo.GetComponent<SceneInitializationAuthoring>();
            if (auth == null)
                auth = initGo.AddComponent<SceneInitializationAuthoring>();
            auth.CharacterSpawnPointEntity = start;
            auth.CharacterPrefabEntity = characterPrefab;
            auth.CameraPrefabEntity = cameraPrefab;
            auth.PlayerPrefabEntity = playerPrefab;
        }
    }
}
