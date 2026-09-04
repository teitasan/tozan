using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

namespace Tozan.Editor
{
    /// <summary>
    /// Character roles (do not mix):
    /// Casual_Male — display / future retopo only. Blender IK; legs are not Hips descendants. Never Humanoid.
    /// UAL / UAL2 — Starter Assets Humanoid. Runtime visual prefers UAL2.
    /// Erika — DPS Humanoid avatar on Player.prefab; NaturalRockSandbox ECS visual via TozanErikaPlatformerSetup.
    /// </summary>
    public static class TozanCharacterSetup
    {
        public const string CharacterPath = "Assets/Characters/Quaternius/Casual_Male.fbx";
        public const string UalPath = "Assets/Characters/UAL/AnimationLibrary_Unity_Standard.fbx";
        public const string Ual2Path = "Assets/Characters/UAL/UAL2_Standard.fbx";
        public const string PlayerPrefabPath = "Assets/ThirdParty/DynamicParkourSystem/Prefabs/Player.prefab";
        public const string StarterControllerPath = "Assets/StarterAssets/Animations/StarterLocomotion.controller";

        [MenuItem("Tozan/Setup Characters And Rigging")]
        public static void SetupFromMenu()
        {
            Debug.Log(EnsureReady());
        }

        public static string EnsureReady()
        {
            KeepCasualMaleAsDisplayModel();
            EnsureHumanoid(UalPath, UalMap());
            EnsureHumanoid(Ual2Path, Ual2Map());
            var controllerPath = CreateStarterAnimator();
            var playerResult = ApplyErikaToDpsPlayer();
            AssetDatabase.SaveAssets();
            return $"ok controller={controllerPath} player={playerResult}";
        }

        public static void AttachVisualToStarterPlayer(GameObject player)
        {
            if (player == null)
                return;

            HidePrimitiveVisual(player);
            var leftoverCasual = player.transform.Find("Casual_Male");
            if (leftoverCasual != null)
                leftoverCasual.gameObject.SetActive(false);

            var visualPath = Ual2Path;
            var visualName = "UAL2_Mannequin";
            var existing = player.transform.Find(visualName);
            GameObject visual = existing != null ? existing.gameObject : null;
            if (visual == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
                if (prefab == null)
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UalPath);
                if (prefab != null)
                {
                    visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
                    visual.name = visualName;
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                }
            }

            var animator = player.GetComponent<Animator>();
            if (animator == null)
                animator = player.AddComponent<Animator>();
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(visualPath);
            if (avatar == null || !avatar.isHuman)
                avatar = AssetDatabase.LoadAssetAtPath<Avatar>(UalPath);
            if (animator != null && avatar != null)
                animator.avatar = avatar;
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(StarterControllerPath);
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            AddRig(player.transform, animator);
        }

        static void EnsureHumanoid(string path, HumanBone[] map)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("Missing model: " + path);
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            var alreadyMapped = false;
            if (map != null && map.Length > 0)
            {
                foreach (var bone in importer.humanDescription.human)
                {
                    if (bone.boneName == map[0].boneName)
                    {
                        alreadyMapped = true;
                        break;
                    }
                }
            }

            if (existing != null && existing.isValid && alreadyMapped && importer.animationType == ModelImporterAnimationType.Human)
                return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            if (map != null && map.Length > 0)
            {
                var desc = importer.humanDescription;
                desc.human = map;
                importer.humanDescription = desc;
            }

            importer.SaveAndReimport();
        }

        static HumanBone Bone(string boneName, string humanName)
        {
            return new HumanBone
            {
                boneName = boneName,
                humanName = humanName,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }

        static void KeepCasualMaleAsDisplayModel()
        {
            var importer = AssetImporter.GetAtPath(CharacterPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("Missing model: " + CharacterPath);
                return;
            }

            if (importer.animationType == ModelImporterAnimationType.Generic)
                return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();
        }

        static HumanBone[] UalMap()
        {
            return new[]
            {
                Bone("DEF-hips", "Hips"),
                Bone("DEF-spine.001", "Spine"),
                Bone("DEF-spine.002", "Chest"),
                Bone("DEF-spine.003", "UpperChest"),
                Bone("DEF-neck", "Neck"),
                Bone("DEF-head", "Head"),
                Bone("DEF-thigh.L", "LeftUpperLeg"),
                Bone("DEF-shin.L", "LeftLowerLeg"),
                Bone("DEF-foot.L", "LeftFoot"),
                Bone("DEF-toe.L", "LeftToes"),
                Bone("DEF-thigh.R", "RightUpperLeg"),
                Bone("DEF-shin.R", "RightLowerLeg"),
                Bone("DEF-foot.R", "RightFoot"),
                Bone("DEF-toe.R", "RightToes"),
                Bone("DEF-shoulder.L", "LeftShoulder"),
                Bone("DEF-upper_arm.L", "LeftUpperArm"),
                Bone("DEF-forearm.L", "LeftLowerArm"),
                Bone("DEF-hand.L", "LeftHand"),
                Bone("DEF-shoulder.R", "RightShoulder"),
                Bone("DEF-upper_arm.R", "RightUpperArm"),
                Bone("DEF-forearm.R", "RightLowerArm"),
                Bone("DEF-hand.R", "RightHand")
            };
        }

        static HumanBone[] Ual2Map()
        {
            return new[]
            {
                Bone("pelvis", "Hips"),
                Bone("spine_01", "Spine"),
                Bone("spine_02", "Chest"),
                Bone("spine_03", "UpperChest"),
                Bone("neck_01", "Neck"),
                Bone("Head", "Head"),
                Bone("thigh_l", "LeftUpperLeg"),
                Bone("calf_l", "LeftLowerLeg"),
                Bone("foot_l", "LeftFoot"),
                Bone("ball_l", "LeftToes"),
                Bone("thigh_r", "RightUpperLeg"),
                Bone("calf_r", "RightLowerLeg"),
                Bone("foot_r", "RightFoot"),
                Bone("ball_r", "RightToes"),
                Bone("clavicle_l", "LeftShoulder"),
                Bone("upperarm_l", "LeftUpperArm"),
                Bone("lowerarm_l", "LeftLowerArm"),
                Bone("hand_l", "LeftHand"),
                Bone("clavicle_r", "RightShoulder"),
                Bone("upperarm_r", "RightUpperArm"),
                Bone("lowerarm_r", "RightLowerArm"),
                Bone("hand_r", "RightHand")
            };
        }

        static string CreateStarterAnimator()
        {
            if (!AssetDatabase.IsValidFolder("Assets/StarterAssets/Animations"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/StarterAssets"))
                    AssetDatabase.CreateFolder("Assets", "StarterAssets");
                AssetDatabase.CreateFolder("Assets/StarterAssets", "Animations");
            }

            var idle = FindClip(UalPath, "Idle") ?? LoadFirstClip("Assets/ThirdParty/DynamicParkourSystem/Model/Animations/Idle.fbx");
            var walk = FindClip(UalPath, "Walk") ?? LoadFirstClip("Assets/ThirdParty/DynamicParkourSystem/Model/Animations/Walk.fbx");
            var run = FindClip(UalPath, "Run") ?? LoadFirstClip("Assets/ThirdParty/DynamicParkourSystem/Model/Animations/Run.fbx");
            var jump = FindClip(UalPath, "Jump") ?? LoadFirstClip("Assets/ThirdParty/DynamicParkourSystem/Model/Animations/Jump.fbx");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(StarterControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(StarterControllerPath);

            EnsureFloat(controller, "Speed");
            EnsureBool(controller, "Grounded");
            EnsureBool(controller, "Jump");

            var root = controller.layers[0].stateMachine;
            while (root.states.Length > 0)
                root.RemoveState(root.states[0].state);

            var idleState = root.AddState("Idle");
            idleState.motion = idle;
            root.defaultState = idleState;

            var loco = root.AddState("Locomotion");
            var blend = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(blend, controller);
            if (idle != null) blend.AddChild(idle, 0f);
            if (walk != null) blend.AddChild(walk, 2f);
            if (run != null) blend.AddChild(run, 5.3f);
            loco.motion = blend;

            var jumpState = root.AddState("Jump");
            jumpState.motion = jump;

            var idleToLoco = idleState.AddTransition(loco);
            idleToLoco.hasExitTime = false;
            idleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var locoToIdle = loco.AddTransition(idleState);
            locoToIdle.hasExitTime = false;
            locoToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var anyToJump = root.AddAnyStateTransition(jumpState);
            anyToJump.hasExitTime = false;
            anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.hasExitTime = false;
            jumpToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Jump");
            jumpToIdle.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

            EditorUtility.SetDirty(controller);
            return StarterControllerPath;
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

        static string ApplyErikaToDpsPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
                return "missing-player-prefab";

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var playerModel = FindChild(root.transform, "PlayerModel");
                if (playerModel == null)
                    return "missing-PlayerModel";

                foreach (Transform child in playerModel)
                {
                    if (child.name.IndexOf("Erika", StringComparison.OrdinalIgnoreCase) >= 0)
                        child.gameObject.SetActive(true);
                    if (child.name == "Casual_Male")
                        Object.DestroyImmediate(child.gameObject);
                }

                var animator = playerModel.GetComponent<Animator>();
                var erikaAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/ThirdParty/DynamicParkourSystem/Model/Erika.fbx");
                if (animator != null && erikaAvatar != null)
                    animator.avatar = erikaAvatar;

                BindClimbBones(playerModel.gameObject, animator);
                AddRig(playerModel, animator);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                return "patched-erika+rig";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void BindClimbBones(GameObject playerModel, Animator animator)
        {
            var climb = playerModel.GetComponent("ClimbController") as MonoBehaviour;
            if (climb == null)
                return;

            Transform leftHand = null;
            Transform rightHand = null;
            Transform leftFoot = null;
            Transform rightFoot = null;
            if (animator != null && animator.isHuman)
            {
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

            leftHand ??= FindBone(playerModel.transform, "LeftHand");
            rightHand ??= FindBone(playerModel.transform, "RightHand");
            leftFoot ??= FindBone(playerModel.transform, "LeftFoot");
            rightFoot ??= FindBone(playerModel.transform, "RightFoot");

            var so = new SerializedObject(climb);
            so.FindProperty("AutoSearchBones").boolValue = true;
            if (leftHand != null) so.FindProperty("LHand").objectReferenceValue = leftHand.gameObject;
            if (rightHand != null) so.FindProperty("RHand").objectReferenceValue = rightHand.gameObject;
            if (leftFoot != null) so.FindProperty("LFoot").objectReferenceValue = leftFoot.gameObject;
            if (rightFoot != null) so.FindProperty("RFoot").objectReferenceValue = rightFoot.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddRig(Transform characterRoot, Animator animator)
        {
            if (characterRoot.GetComponent<RigBuilder>() != null)
                return;

            var rigGo = new GameObject("AnimationRig");
            rigGo.transform.SetParent(characterRoot, false);
            var rig = rigGo.AddComponent<Rig>();
            var builder = characterRoot.gameObject.AddComponent<RigBuilder>();
            builder.layers.Clear();
            builder.layers.Add(new RigLayer(rig, true));
            EditorUtility.SetDirty(builder);
        }

        static void HidePrimitiveVisual(GameObject player)
        {
            var renderer = player.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
            var filter = player.GetComponent<MeshFilter>();
            if (filter != null)
                Object.DestroyImmediate(filter);
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }
            return null;
        }

        static Transform FindBone(Transform root, string boneName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.Replace("mixamorig:", string.Empty).Replace("mixamorig", string.Empty);
                if (n.Equals(boneName, StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith(boneName, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }

        static AnimationClip FindClip(string fbxPath, string needle)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip &&
                    !clip.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase) &&
                    clip.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip;
            }
            return null;
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
    }
}
