using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tozan.Tests
{
    public static class PlatformerTestHelpers
    {
        public static IEnumerator WaitForState(string expected, float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    var character = FindCharacter(world.EntityManager);
                    if (character != Entity.Null && ReadCurrentState(world.EntityManager, character) == expected)
                        yield break;
                }
                yield return null;
            }

            Assert.Fail("Timed out waiting for " + expected);
        }

        public static Entity FindCharacter(EntityManager em)
        {
            var type = FindType("PlatformerCharacterStateMachine");
            if (type == null)
                return Entity.Null;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly(type));
            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        public static void EnsureGeometryConfig(EntityManager em, Entity character)
        {
            var type = FindType("TozanPlatformerGeometryConfig");
            Assert.IsNotNull(type, "TozanPlatformerGeometryConfig");
            if (!em.HasComponent(character, type))
            {
                var defaultProperty = type.GetProperty(
                    "DefaultNaturalRock",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var boxed = defaultProperty != null
                    ? defaultProperty.GetValue(null)
                    : System.Activator.CreateInstance(type);
                var modeField = type.GetField("DetectionMode");
                modeField.SetValue(boxed, System.Enum.ToObject(modeField.FieldType, 1)); // GeometryOnly
                em.AddComponent(character, type);
                SetComponent(em, character, type, boxed);
            }
            else
            {
                var boxed = GetComponent(em, character, type);
                var modeField = type.GetField("DetectionMode");
                modeField.SetValue(boxed, System.Enum.ToObject(modeField.FieldType, 1)); // GeometryOnly
                SetComponent(em, character, type, boxed);
            }
        }

        public static void ClearControlOverrides(EntityManager em, Entity character)
        {
            var type = FindType("TozanPlatformerTestDrive");
            if (type != null && em.HasComponent(character, type))
                em.RemoveComponent(character, type);
        }

        public static void PlaceFallingAtUnmarkedShelf(EntityManager em, Entity character)
        {
            PlaceCharacter(em, character, new float3(-5.5f, 1.42f, 4.58f), quaternion.identity, new float3(0f, -1.2f, 0.8f));
        }

        public static void PlaceCharacter(EntityManager em, Entity character, float3 position, quaternion rotation, float3 linearVelocity)
        {
            ZeroBodyVelocity(em, character);
            var lt = em.GetComponentData<Unity.Transforms.LocalTransform>(character);
            lt.Position = position;
            lt.Rotation = rotation;
            em.SetComponentData(character, lt);
            SetBodyVelocity(em, character, linearVelocity);
        }

        public static void SetBodyVelocity(EntityManager em, Entity character, float3 linear)
        {
            var bodyType = FindType("Unity.CharacterController.KinematicCharacterBody");
            if (bodyType != null && em.HasComponent(character, bodyType))
            {
                var boxed = GetComponent(em, character, bodyType);
                var field = bodyType.GetField("RelativeVelocity");
                var grounded = bodyType.GetField("IsGrounded");
                if (field != null)
                    field.SetValue(boxed, linear);
                if (grounded != null && math.lengthsq(linear) > 0.01f)
                    grounded.SetValue(boxed, false);
                SetComponent(em, character, bodyType, boxed);
            }

            var velType = FindType("Unity.Physics.PhysicsVelocity");
            if (velType != null && em.HasComponent(character, velType))
            {
                var boxed = GetComponent(em, character, velType);
                var linearField = velType.GetField("Linear");
                var angular = velType.GetField("Angular");
                if (linearField != null)
                    linearField.SetValue(boxed, linear);
                if (angular != null)
                    angular.SetValue(boxed, float3.zero);
                SetComponent(em, character, velType, boxed);
            }
        }

        public static void ZeroBodyVelocity(EntityManager em, Entity character)
        {
            SetBodyVelocity(em, character, float3.zero);
        }

        public static string ReadCurrentState(EntityManager em, Entity character)
        {
            var type = FindType("PlatformerCharacterStateMachine");
            var data = GetComponent(em, character, type);
            var state = type.GetField("CurrentState").GetValue(data);
            return state != null ? state.ToString() : "null";
        }

        public static object GetComponent(EntityManager em, Entity entity, System.Type type)
        {
            foreach (var m in typeof(EntityManager).GetMethods())
            {
                if (m.Name != "GetComponentData" || !m.IsGenericMethod)
                    continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(Entity))
                    return m.MakeGenericMethod(type).Invoke(em, new object[] { entity });
            }
            return null;
        }

        public static void SetComponent(EntityManager em, Entity entity, System.Type type, object boxed)
        {
            foreach (var m in typeof(EntityManager).GetMethods())
            {
                if (m.Name != "SetComponentData" || !m.IsGenericMethod)
                    continue;
                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType == typeof(Entity))
                {
                    m.MakeGenericMethod(type).Invoke(em, new[] { entity, boxed });
                    return;
                }
            }
        }

        public static System.Type FindType(string name)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null)
                    return t;
            }
            return null;
        }

        public static float3 ReadBodyVelocity(EntityManager em, Entity character)
        {
            var bodyType = FindType("Unity.CharacterController.KinematicCharacterBody");
            if (bodyType != null && em.HasComponent(character, bodyType))
            {
                var boxed = GetComponent(em, character, bodyType);
                var field = bodyType.GetField("RelativeVelocity");
                if (field != null)
                    return (float3)field.GetValue(boxed);
            }
            return float3.zero;
        }

        public static float3 ReadCameraRight(EntityManager em)
        {
            var playerType = FindType("PlatformerPlayer");
            if (playerType == null)
                return math.right();

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly(playerType));
            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
                return math.right();

            var player = GetComponent(em, entities[0], playerType);
            var cameraField = playerType.GetField("ControlledCamera");
            if (cameraField == null)
                return math.right();

            var camera = (Entity)cameraField.GetValue(player);
            if (camera == Entity.Null || !em.HasComponent<Unity.Transforms.LocalTransform>(camera))
                return math.right();

            return math.mul(em.GetComponentData<Unity.Transforms.LocalTransform>(camera).Rotation, math.right());
        }

        public static int ReadHybridClipIndex()
        {
            var animator = FindHybridCharacterMeshAnimator();
            return animator != null ? animator.GetInteger("ClipIndex") : -1;
        }

        public static Animator FindHybridCharacterMeshAnimator()
        {
            foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include))
            {
                if (animator != null && animator.gameObject.name.IndexOf("CharacterMesh") >= 0)
                    return animator;
            }

            return null;
        }

        public static bool HasErikaRendererIdentity(Animator animator)
        {
            if (animator == null)
                return false;

            foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null)
                    continue;
                var meshName = renderer.sharedMesh.name;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    meshName.IndexOf("Erika", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public static (float footY, float headY, float height) MeasureHybridVisualExtents(GameObject meshRoot)
        {
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var renderer in meshRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null)
                    continue;

                var bounds = renderer.localBounds;
                var ext = bounds.extents;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center;
                    corner.x += (i & 1) == 0 ? -ext.x : ext.x;
                    corner.y += (i & 2) == 0 ? -ext.y : ext.y;
                    corner.z += (i & 4) == 0 ? -ext.z : ext.z;
                    var meshLocal = meshRoot.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (meshLocal.y < minY)
                        minY = meshLocal.y;
                    if (meshLocal.y > maxY)
                        maxY = meshLocal.y;
                }
            }

            if (float.IsPositiveInfinity(minY))
                return (0f, 0f, 0f);

            return (minY, maxY, maxY - minY);
        }

        public static bool HasClipIndexParameter(Animator animator)
        {
            if (animator == null)
                return false;

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == "ClipIndex" && parameter.type == AnimatorControllerParameterType.Int)
                    return true;
            }

            return false;
        }

        public static float ReadHybridAnimatorSpeed()
        {
            var animator = FindHybridCharacterMeshAnimator();
            return animator != null ? animator.speed : -1f;
        }

        public static bool TryFindVerticalWallAabb(EntityManager em, out Aabb wallAabb)
        {
            wallAabb = default;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            var physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
            foreach (var body in physicsWorld.StaticBodies)
            {
                var aabb = body.CalculateAabb();
                var size = aabb.Max - aabb.Min;
                var center = (aabb.Max + aabb.Min) * 0.5f;
                if (size.x >= 11f && size.y >= 7f && size.z > 0.4f && size.z < 0.8f &&
                    math.abs(center.x) < 0.25f && center.y > 3.5f && center.y < 4.5f &&
                    center.z > 1.3f && center.z < 1.8f)
                {
                    wallAabb = aabb;
                    return true;
                }
            }

            return false;
        }

        public static void PlaceAtVerticalWall(EntityManager em, Entity character, float height = 1.1f)
        {
            // Keep the character just inside the reliable distance-query band
            // used by the official capsule geometry.
            PlaceCharacter(em, character, new float3(0f, height, 1.3f), quaternion.identity, float3.zero);
        }

    }
}
