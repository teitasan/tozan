using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tozan.Editor
{
    /// <summary>
    /// Wires Erika (DPS Humanoid) as the ECS Platformer hybrid visual for NaturalRockSandbox.
    /// Physics, input, and state machine stay on the official PlatformerCharacter prefab.
    /// ProtoCharacter CharacterMesh prefab is preserved as rollback reference.
    /// </summary>
    public static class TozanErikaPlatformerSetup
    {
        public const string ErikaFolder = "Assets/Characters/Erika";
        public const string ErikaModelPath = "Assets/ThirdParty/DynamicParkourSystem/Model/Erika.fbx";
        public const string AnimationsFolder = "Assets/ThirdParty/DynamicParkourSystem/Model/Animations";
        public const string GeneratedAnimationsFolder = ErikaFolder + "/Animations";
        public const string AnimatorPath = ErikaFolder + "/ErikaPlatformerAnimator.controller";
        public const string VisualPrefabPath = ErikaFolder + "/ErikaCharacterMesh.prefab";

        static readonly (string stateName, string clipFile, float speed)[] ClipMap =
        {
            ("Idle", "Idle.fbx", 1f),
            ("Run", "Run.fbx", 3f),
            ("Sprint", "Idle To Sprint.fbx", 4f),
            ("InAir", "Jump.fbx", 1f),
            ("LedgeGrabMove", "Hanging Idle.fbx", 1f),
            ("LedgeStandUp", "Step Up.fbx", 1f),
            ("WallRunLeft", "FreeHang Left Shimmy.fbx", 2f),
            ("WallRunRight", "Braced Hang Right Shimmy.fbx", 2f),
            ("CrouchIdle", "Jumping Crouch.fbx", 1f),
            ("CrouchMove", "Walk.fbx", 2f),
            ("ClimbingMove", "Freehang Climb.fbx", 3f),
            ("SwimmingIdle", "Fall Idle.fbx", 0.3f),
            ("SwimmingMove", "JumpingDown.fbx", 1f),
            ("Dash", "Jog Forward.fbx", 2f),
            ("RopeHang", "Freehang Idle.fbx", 1f),
            ("Sliding", "Slide.fbx", 2f),
        };

        [MenuItem("Tozan/Setup Erika Platformer Visual")]
        public static void SetupFromMenu()
        {
            Debug.Log(EnsureReady());
        }

        public static string EnsureReady()
        {
            EnsureFolder("Assets", "Characters");
            EnsureFolder("Assets/Characters", "Erika");
            EnsureFolder(ErikaFolder, "Animations");

            var controller = CreateOrUpdateAnimator();
            var prefabPath = CreateOrUpdateVisualPrefab(controller);
            WirePlatformerMeshPrefab(prefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"ok animator={AnimatorPath} visual={prefabPath} wired={TozanPlatformerSetup.CharacterPrefabPath}";
        }

        static AnimatorController CreateOrUpdateAnimator()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);

            EnsureIntParameter(controller, "ClipIndex");

            var root = controller.layers[0].stateMachine;
            while (root.anyStateTransitions.Length > 0)
                root.RemoveAnyStateTransition(root.anyStateTransitions[0]);
            while (root.states.Length > 0)
                root.RemoveState(root.states[0].state);

            AnimatorState defaultState = null;
            for (var i = 0; i < ClipMap.Length; i++)
            {
                var (stateName, clipFile, speed) = ClipMap[i];
                var clip = LoadClipForController(clipFile);
                if (clip == null)
                    throw new InvalidOperationException("Missing Mixamo clip: " + clipFile);

                var state = root.AddState(stateName, new Vector3(400f, 110f + i * 50f, 0f));
                state.motion = clip;
                state.speed = speed;
                if (i == 0)
                    defaultState = state;

                var transition = root.AddAnyStateTransition(state);
                transition.hasExitTime = false;
                transition.duration = i == 4 ? 0f : 0.15f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, i, "ClipIndex");
                if (i is 0 or 1 or 2)
                    transition.interruptionSource = TransitionInterruptionSource.Destination;
            }

            root.defaultState = defaultState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static string CreateOrUpdateVisualPrefab(AnimatorController controller)
        {
            var erikaSource = AssetDatabase.LoadAssetAtPath<GameObject>(ErikaModelPath);
            if (erikaSource == null)
                throw new InvalidOperationException("Missing Erika model: " + ErikaModelPath);

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(ErikaModelPath);
            if (avatar == null || !avatar.isHuman)
                throw new InvalidOperationException("Erika.fbx must import as a valid Humanoid avatar.");

            // HybridSystem attaches CharacterMesh at MeshRoot and expects the
            // Animator on this prefab root. Lift the imported rig so feet sit
            // on y=0; Erika's Mixamo bind pose otherwise sinks ~1m below root.
            var root = new GameObject("CharacterMesh");

            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;

                var model = Object.Instantiate(erikaSource, root.transform);
                model.name = "ErikaModel";
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                model.transform.localPosition = new Vector3(0f, ComputeFootLiftY(model), 0f);

                var legacyAnimator = model.GetComponent<Animator>();
                if (legacyAnimator != null)
                    Object.DestroyImmediate(legacyAnimator);

                var animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                PrefabUtility.SaveAsPrefabAsset(root, VisualPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return VisualPrefabPath;
        }

        internal static float ComputeFootLiftY(GameObject modelRoot)
        {
            var minY = float.PositiveInfinity;
            foreach (var renderer in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bounds = renderer.localBounds;
                var ext = bounds.extents;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center;
                    corner.x += (i & 1) == 0 ? -ext.x : ext.x;
                    corner.y += (i & 2) == 0 ? -ext.y : ext.y;
                    corner.z += (i & 4) == 0 ? -ext.z : ext.z;
                    var modelLocal = modelRoot.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (modelLocal.y < minY)
                        minY = modelLocal.y;
                }
            }

            return minY < -0.001f ? -minY : 0f;
        }

        static void WirePlatformerMeshPrefab(string erikaMeshPrefabPath)
        {
            var meshPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(erikaMeshPrefabPath);
            if (meshPrefab == null)
                throw new InvalidOperationException("Missing Erika visual prefab: " + erikaMeshPrefabPath);

            var characterPath = TozanPlatformerSetup.CharacterPrefabPath;
            var root = PrefabUtility.LoadPrefabContents(characterPath);
            try
            {
                var authoring = root.GetComponent<PlatformerCharacterAuthoring>();
                if (authoring == null)
                    throw new InvalidOperationException("PlatformerCharacterAuthoring missing on " + characterPath);

                var so = new SerializedObject(authoring);
                so.FindProperty("MeshPrefab").objectReferenceValue = meshPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, characterPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void EnsureFolder(string parent, string folderName)
        {
            var path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        static void EnsureIntParameter(AnimatorController controller, string name)
        {
            foreach (var parameter in controller.parameters)
            {
                if (parameter.name == name)
                    return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Int);
        }

        static AnimationClip LoadFirstClip(string fbxPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase))
                    return clip;
            }

            return null;
        }

        static AnimationClip LoadClipForController(string clipFile)
        {
            var sourcePath = AnimationsFolder + "/" + clipFile;
            var source = LoadFirstClip(sourcePath);
            if (source == null || source.events.Length == 0)
                return source;

            // DPS clips can contain callbacks for its legacy controller. The
            // ECS Platformer visual has no such receiver, so keep the motion
            // curves but make a local event-free copy instead of importing the
            // legacy controller just to consume an unrelated event.
            var generatedName = System.IO.Path.GetFileNameWithoutExtension(clipFile) + ".anim";
            var generatedPath = GeneratedAnimationsFolder + "/" + generatedName;
            var generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(generatedPath);
            if (generated == null)
            {
                generated = Object.Instantiate(source);
                generated.name = source.name + " (ECS Event Free)";
                AssetDatabase.CreateAsset(generated, generatedPath);
            }

            // Use the editor API rather than only the property setter. Unity
            // serializes imported animation events separately, and the editor
            // API makes the event-free result explicit and persistent for both
            // newly created and previously generated clips.
            AnimationUtility.SetAnimationEvents(generated, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(generated);

            return generated;
        }
    }
}
