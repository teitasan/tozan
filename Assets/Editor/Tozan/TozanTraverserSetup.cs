using Traverser;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Tozan.Editor
{
    public static class TozanTraverserSetup
    {
        public const string DataFolder = "Assets/ThirdParty/Traverser/Runtime/Data";
        public const string ControllerPath = DataFolder + "/TozanTraverser.controller";
        public const string ClimbingDataPath = DataFolder + "/TozanClimbingData.asset";
        public const string LocomotionDataPath = DataFolder + "/TozanLocomotionData.asset";
        public const string ParkourDataPath = DataFolder + "/TozanParkourData.asset";
        const string InputActionsPath = "Assets/StarterAssets/InputSystem/StarterAssets.inputactions";
        const string FollowCameraName = "TraverserFollowCamera";

        public static GameObject CreatePlayer(Vector3 position)
        {
            EnsureData();

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "TraverserPlayer";
            player.tag = "Untagged";
            player.layer = 0;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);

            var capsuleCol = player.GetComponent<CapsuleCollider>();
            if (capsuleCol != null)
                Object.DestroyImmediate(capsuleCol);
            var renderer = player.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
            var filter = player.GetComponent<MeshFilter>();
            if (filter != null)
                Object.DestroyImmediate(filter);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var animator = player.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var ual2Avatar = AssetDatabase.LoadAssetAtPath<Avatar>(TozanCharacterSetup.Ual2Path);
            if (ual2Avatar != null && ual2Avatar.isHuman)
                animator.avatar = ual2Avatar;

            AttachUal2Visual(player);

            player.AddComponent<TraverserInputController>();
            var tcc = player.AddComponent<TraverserCharacterController>();
            tcc.characterCollisionMask = LayerMask.GetMask("Default") == 0 ? 1 : LayerMask.GetMask("Default");

            var animCtrl = player.AddComponent<TraverserAnimationController>();
            BindDummyRig(player.transform, animCtrl);

            player.AddComponent<TraverserAbilityController>();

            var loco = player.AddComponent<TraverserLocomotionAbility>();
            loco.locomotionData = AssetDatabase.LoadAssetAtPath<TraverserLocomotionData>(LocomotionDataPath);
            loco.fIKOn = false;
            loco.iterations = 1;
            loco.stepping = 1.0f;
            loco.initialJumpSpeed = 6.0f;
            if (Camera.main != null)
                loco.cameraTransform = Camera.main.transform;

            var climb = player.AddComponent<TraverserClimbingAbility>();
            climb.climbingData = AssetDatabase.LoadAssetAtPath<TraverserClimbingData>(ClimbingDataPath);
            climb.fIKOn = false;
            climb.hIKOn = false;

            var parkour = player.AddComponent<TraverserParkourAbility>();
            parkour.parkourData = AssetDatabase.LoadAssetAtPath<TraverserParkourData>(ParkourDataPath);

            EnsurePlayerPresentation(player);
            return player;
        }

        public static void EnsurePlayerPresentation(GameObject player)
        {
            if (player == null)
                return;

            var cameraRoot = player.transform.Find("PlayerCameraRoot");
            if (cameraRoot == null)
            {
                var rootGo = new GameObject("PlayerCameraRoot");
                cameraRoot = rootGo.transform;
                cameraRoot.SetParent(player.transform, false);
                cameraRoot.localPosition = new Vector3(0f, 1.375f, 0f);
                cameraRoot.localRotation = Quaternion.identity;
            }

            var camCtrl = player.GetComponent<TraverserCameraController>();
            if (camCtrl == null)
                camCtrl = player.AddComponent<TraverserCameraController>();
            camCtrl.cameraTarget = cameraRoot;
            camCtrl.topClamp = 40.0f;
            camCtrl.bottomClamp = -20.0f;

            var follow = GameObject.Find(FollowCameraName);
            if (follow == null)
                follow = new GameObject(FollowCameraName);

            var cmCam = follow.GetComponent<CinemachineCamera>();
            if (cmCam == null)
                cmCam = follow.AddComponent<CinemachineCamera>();
            cmCam.Follow = cameraRoot;
            cmCam.Priority = 20;

            var orbit = follow.GetComponent<CinemachineThirdPersonFollow>();
            if (orbit == null)
                orbit = follow.AddComponent<CinemachineThirdPersonFollow>();
            orbit.CameraDistance = 4.0f;
            orbit.ShoulderOffset = new Vector3(0.5f, 0.0f, 0.0f);
            orbit.VerticalArmLength = 0.4f;
            orbit.CameraSide = 1.0f;
            orbit.Damping = new Vector3(0.1f, 0.25f, 0.3f);

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var playerInput = player.GetComponent<PlayerInput>();
            if (playerInput == null)
                playerInput = player.AddComponent<PlayerInput>();
            if (actions != null)
                playerInput.actions = actions;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;

            var loco = player.GetComponent<TraverserLocomotionAbility>();
            if (loco != null && Camera.main != null)
                loco.cameraTransform = Camera.main.transform;
        }

        static void AttachUal2Visual(GameObject player)
        {
            if (player.transform.Find("UAL2_Mannequin") != null)
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TozanCharacterSetup.Ual2Path);
            if (prefab == null)
                return;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
            visual.name = "UAL2_Mannequin";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var childAnimator in visual.GetComponentsInChildren<Animator>(true))
            {
                if (childAnimator.gameObject != player)
                    childAnimator.enabled = false;
            }
        }

        static void BindDummyRig(Transform root, TraverserAnimationController animCtrl)
        {
            var rigRoot = new GameObject("TraverserRig");
            rigRoot.transform.SetParent(root, false);
            var spine = MakeRig(rigRoot.transform, "SpineRig");
            var legs = MakeRig(rigRoot.transform, "LegsRig");
            var arms = MakeRig(rigRoot.transform, "ArmsRig");
            animCtrl.spineRig = spine;
            animCtrl.legsRig = legs;
            animCtrl.armsRig = arms;
            animCtrl.hipsRigEffector = MakeEmpty(rigRoot.transform, "HipsEffector");
            animCtrl.spineRigEffector = MakeEmpty(rigRoot.transform, "SpineEffector");
            animCtrl.leftLegRigEffector = MakeEmpty(rigRoot.transform, "LeftLegEffector");
            animCtrl.rightLegRigEffector = MakeEmpty(rigRoot.transform, "RightLegEffector");
            animCtrl.aimRigEffector = MakeEmpty(rigRoot.transform, "AimEffector");
            animCtrl.hipsRef = root;
        }

        static Rig MakeRig(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<Rig>();
        }

        static GameObject MakeEmpty(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void EnsureData()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ThirdParty"))
                AssetDatabase.CreateFolder("Assets", "ThirdParty");
            if (!AssetDatabase.IsValidFolder("Assets/ThirdParty/Traverser"))
                AssetDatabase.CreateFolder("Assets/ThirdParty", "Traverser");
            if (!AssetDatabase.IsValidFolder("Assets/ThirdParty/Traverser/Runtime"))
                AssetDatabase.CreateFolder("Assets/ThirdParty/Traverser", "Runtime");
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets/ThirdParty/Traverser/Runtime", "Data");

            var climbing = EnsureSo<TraverserClimbingData>(ClimbingDataPath);
            FillClimbingDummyAnims(climbing);
            var locomotion = EnsureSo<TraverserLocomotionData>(LocomotionDataPath);
            FillLocomotionDummyAnims(locomotion);
            EnsureSo<TraverserParkourData>(ParkourDataPath);
            EnsureAnimator();
        }

        static T EnsureSo<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static TraverserAnimationController.AnimationData IdleAnim()
        {
            return new TraverserAnimationController.AnimationData
            {
                animationStateName = "Idle",
                transitionDuration = 0.1f
            };
        }

        static TraverserTransition.TraverserTransitionData IdleTransition()
        {
            return new TraverserTransition.TraverserTransitionData
            {
                transitionAnim = "Idle",
                targetAnim = "Idle",
                triggerTransitionAnim = string.Empty,
                triggerTargetAnim = string.Empty
            };
        }

        static void FillClimbingDummyAnims(TraverserClimbingData data)
        {
            if (data == null)
                return;
            if (!string.IsNullOrEmpty(data.ledgeIdleAnimation.animationStateName))
                return;
            var trans = IdleTransition();
            var anim = IdleAnim();
            data.mountTransitionData = trans;
            data.jumpHangTransitionData = trans;
            data.jumpHangShortTransitionData = trans;
            data.dropDownTransitionData = trans;
            data.HopUpTransitionData = trans;
            data.HopRightTransitionData = trans;
            data.HopLeftTransitionData = trans;
            data.HopDownTransitionData = trans;
            data.dismountTransitionData = trans;
            data.pullUpTransitionData = trans;
            data.jumpBackTransitionData = trans;
            data.fallTransitionAnimation = anim;
            data.locomotionOnAnimation = anim;
            data.ledgeIdleAnimation = anim;
            data.fallLoopAnimation = anim;
            data.ledgeRightAnimation = anim;
            data.ledgeLeftAnimation = anim;
            EditorUtility.SetDirty(data);
        }

        static TraverserAnimationController.AnimationData Anim(string stateName, float duration)
        {
            return new TraverserAnimationController.AnimationData
            {
                animationStateName = stateName,
                transitionDuration = duration
            };
        }

        static void FillLocomotionDummyAnims(TraverserLocomotionData data)
        {
            if (data == null)
                return;
            var trans = IdleTransition();
            data.fallToRollTransitionData = trans;
            data.hardLandingTransitionData = trans;
            data.fallTransitionAnimation = Anim("Fall", 0.15f);
            data.jumpAnimation = Anim("Jump", 0.05f);
            data.jumpForwardAnimation = Anim("JumpForward", 0.05f);
            data.locomotionONAnimation = Anim("Locomotion", 0.15f);
            data.locomotionOFFAnimation = Anim("Idle", 0.2f);
            data.fallToLandAnimation = Anim("Land", 0.05f);
            data.fallToRunAnimation = Anim("LandRun", 0.05f);
            EditorUtility.SetDirty(data);
        }

        const string DpsAnimFolder = "Assets/ThirdParty/DynamicParkourSystem/Model/Animations/";

        static void EnsureAnimator()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            EnsureBool(controller, "Move");
            EnsureFloat(controller, "Speed");
            EnsureFloat(controller, "Heading");
            EnsureFloat(controller, "DirectionX");
            EnsureFloat(controller, "FreeHangWeight");
            EnsureFloat(controller, "IKLeftFootWeight");
            EnsureFloat(controller, "IKRightFootWeight");
            EnsureFloat(controller, "IKLeftHandWeight");
            EnsureFloat(controller, "IKRightHandWeight");

            var sm = controller.layers[0].stateMachine;
            var idle = GetOrAddState(sm, "Idle", new Vector3(200f, 0f, 0f));
            var loco = GetOrAddState(sm, "Locomotion", new Vector3(200f, 120f, 0f));
            var jump = GetOrAddState(sm, "Jump", new Vector3(480f, 0f, 0f));
            var jumpFwd = GetOrAddState(sm, "JumpForward", new Vector3(480f, 80f, 0f));
            var fall = GetOrAddState(sm, "Fall", new Vector3(480f, 160f, 0f));
            var land = GetOrAddState(sm, "Land", new Vector3(760f, 0f, 0f));
            var landRun = GetOrAddState(sm, "LandRun", new Vector3(760f, 80f, 0f));
            sm.defaultState = idle;

            idle.motion = LoadDpsClip("Idle.fbx", "Idle");
            jump.motion = LoadDpsClip("Jump.fbx", "Jump");
            jumpFwd.motion = LoadDpsClip("Big Jump.fbx", "Big Jump");
            fall.motion = LoadDpsClip("Fall Idle.fbx", "Fall A Loop");
            land.motion = LoadDpsClip("Falling To Landing.fbx", "Falling To Landing");
            landRun.motion = LoadDpsClip("Land To Run Forward.fbx", "Fall A Land To Run Forward");

            var walk = LoadDpsClip("Walk.fbx", "Walk");
            var jog = LoadDpsClip("Jog Forward.fbx", "Jog Forward");
            var run = LoadDpsClip("Run.fbx", "Run");
            var blend = GetOrAddBlendTree(controller, "LocomotionBlend");
            blend.blendType = BlendTreeType.Simple1D;
            blend.blendParameter = "Speed";
            blend.useAutomaticThresholds = false;
            blend.children = new ChildMotion[0];
            if (walk != null)
                blend.AddChild(walk, 1.0f);
            if (jog != null)
                blend.AddChild(jog, 3.9f);
            if (run != null)
                blend.AddChild(run, 5.5f);
            loco.motion = blend;

            EnsureBoolTransition(idle, loco, AnimatorConditionMode.If, "Move", false, 0.15f);
            EnsureBoolTransition(loco, idle, AnimatorConditionMode.IfNot, "Move", false, 0.2f);
            EnsureBoolTransition(land, idle, AnimatorConditionMode.IfNot, "Move", true, 0.15f);
            EnsureBoolTransition(land, loco, AnimatorConditionMode.If, "Move", true, 0.15f);
            EnsureBoolTransition(landRun, idle, AnimatorConditionMode.IfNot, "Move", true, 0.15f);
            EnsureBoolTransition(landRun, loco, AnimatorConditionMode.If, "Move", true, 0.15f);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static AnimatorState GetOrAddState(AnimatorStateMachine sm, string name, Vector3 position)
        {
            foreach (var child in sm.states)
            {
                if (child.state.name == name)
                    return child.state;
            }
            return sm.AddState(name, position);
        }

        static BlendTree GetOrAddBlendTree(AnimatorController controller, string name)
        {
            var path = AssetDatabase.GetAssetPath(controller);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var existing = obj as BlendTree;
                if (existing != null && existing.name == name)
                    return existing;
            }
            var blend = new BlendTree { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(blend, controller);
            return blend;
        }

        static void EnsureBoolTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string param, bool exitTime, float duration)
        {
            foreach (var t in from.transitions)
            {
                if (t.destinationState == to)
                    return;
            }
            var tr = from.AddTransition(to);
            tr.hasExitTime = exitTime;
            tr.hasFixedDuration = true;
            tr.duration = duration;
            if (exitTime)
                tr.exitTime = 0.85f;
            tr.AddCondition(mode, 0, param);
        }

        static AnimationClip LoadDpsClip(string fileName, string clipName)
        {
            AnimationClip named = null;
            AnimationClip any = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(DpsAnimFolder + fileName))
            {
                var clip = obj as AnimationClip;
                if (clip == null || clip.name.StartsWith("__"))
                    continue;
                if (clip.name == clipName)
                    named = clip;
                if (any == null)
                    any = clip;
            }
            return named != null ? named : any;
        }

        static void EnsureFloat(AnimatorController controller, string name)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name)
                    return;
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Float);
        }

        static void EnsureBool(AnimatorController controller, string name)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name)
                    return;
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        }
    }
}
