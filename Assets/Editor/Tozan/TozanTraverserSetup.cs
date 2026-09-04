using Traverser;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
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
            if (Camera.main != null)
                loco.cameraTransform = Camera.main.transform;

            var climb = player.AddComponent<TraverserClimbingAbility>();
            climb.climbingData = AssetDatabase.LoadAssetAtPath<TraverserClimbingData>(ClimbingDataPath);
            climb.fIKOn = false;
            climb.hIKOn = false;

            var parkour = player.AddComponent<TraverserParkourAbility>();
            parkour.parkourData = AssetDatabase.LoadAssetAtPath<TraverserParkourData>(ParkourDataPath);

            return player;
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

        static void FillLocomotionDummyAnims(TraverserLocomotionData data)
        {
            if (data == null)
                return;
            if (!string.IsNullOrEmpty(data.locomotionONAnimation.animationStateName))
                return;
            var trans = IdleTransition();
            var anim = IdleAnim();
            data.fallToRollTransitionData = trans;
            data.hardLandingTransitionData = trans;
            data.fallTransitionAnimation = anim;
            data.jumpAnimation = anim;
            data.jumpForwardAnimation = anim;
            data.locomotionONAnimation = anim;
            data.locomotionOFFAnimation = anim;
            data.fallToLandAnimation = anim;
            data.fallToRunAnimation = anim;
            EditorUtility.SetDirty(data);
        }

        static void EnsureAnimator()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
                return;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            EnsureBool(controller, "Move");
            EnsureFloat(controller, "Speed");
            EnsureFloat(controller, "Heading");
            EnsureFloat(controller, "DirectionX");
            EnsureFloat(controller, "FreeHangWeight");
            EnsureFloat(controller, "IKLeftFootWeight");
            EnsureFloat(controller, "IKRightFootWeight");
            EnsureFloat(controller, "IKLeftHandWeight");
            EnsureFloat(controller, "IKRightHandWeight");

            var root = controller.layers[0].stateMachine;
            var idle = root.AddState("Idle");
            root.defaultState = idle;
            EditorUtility.SetDirty(controller);
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
