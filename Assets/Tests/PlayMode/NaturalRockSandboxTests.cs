using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tozan.Tests
{
    public class NaturalRockSandboxTests
    {
        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NaturalRockSandbox_SpawnsPlatformerWithoutClimbMarkers()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return WaitForState("GroundMove", 10f);

            Assert.IsNull(GameObject.Find("TraverserPlayer"), "STEP 15 player is ECS Platformer, not Traverser");
            Assert.IsNull(GameObject.Find("PlayerModel"), "DPS player must not be in the natural-rock gate");
            Assert.IsNull(GameObject.Find("Climb_Ledge"));
            Assert.IsNull(GameObject.Find("Vault_Box"));

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                Assert.AreNotEqual("Vault", go.tag, go.name);
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null)
                        continue;
                    var n = c.GetType().Name;
                    Assert.AreNotEqual("HandlePoints", n, go.name);
                    Assert.AreNotEqual("TraverserClimbingObject", n, go.name);
                    Assert.AreNotEqual("TraverserParkourObject", n, go.name);
                }
            }

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world);
            var character = FindCharacter(world.EntityManager);
            Assert.AreNotEqual(Entity.Null, character);

            var geometryConfigType = PlatformerTestHelpers.FindType("TozanPlatformerGeometryConfig");
            Assert.IsNotNull(geometryConfigType, "Tozan geometry configuration type");
            Assert.IsTrue(world.EntityManager.HasComponent(character, geometryConfigType),
                "NaturalRockSandbox player prefab must bake geometry mode; tests must not add it as a fallback");
            var geometryConfig = PlatformerTestHelpers.GetComponent(world.EntityManager, character, geometryConfigType);
            var detectionMode = geometryConfigType.GetField("DetectionMode").GetValue(geometryConfig);
            Assert.AreEqual("GeometryOnly", detectionMode.ToString());
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NaturalRockSandbox_UnmarkedMesh_OfficialLedgeGrab()
        {
            // Adoption gate. Do not pass this by adding Climbable tags, HandlePoints,
            // TraverserClimbingObject, Vault tags, or Ledge layer.
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return WaitForState("GroundMove", 10f);

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = FindCharacter(em);
            Assert.AreNotEqual(Entity.Null, character, "Platformer character entity");

            PlaceFallingAtUnmarkedShelf(em, character);
            EnsureTestDrive(em, character);

            yield return DriveUntilState(em, character, "LedgeGrab", 5f, new float3(0f, 0f, 0.4f), false);
            yield return HoldState(em, character, "LedgeGrab", 10, float3.zero);

            var pos = em.GetComponentData<LocalTransform>(character).Position;
            var report = "last=" + ReadCurrentState(em, character) + " pos=" + pos;
            Debug.Log("STEP15 LedgeGrab " + report);
            Assert.AreEqual("LedgeGrab", ReadCurrentState(em, character),
                "Official LedgeDetection did not hang on unmarked overhang lip. " + report);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NaturalRockSandbox_UnmarkedMesh_OfficialLedgeStandUp()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return WaitForState("GroundMove", 10f);

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = FindCharacter(em);
            Assert.AreNotEqual(Entity.Null, character, "Platformer character entity");

            PlaceFallingAtUnmarkedShelf(em, character);
            EnsureTestDrive(em, character);
            yield return DriveUntilState(em, character, "LedgeGrab", 5f, new float3(0f, 0f, 0.4f), false);
            yield return HoldState(em, character, "LedgeGrab", 10, float3.zero);

            var hangClip = ReadHybridClipIndex();
            Assert.AreEqual(4, hangClip, "official LedgeGrabMove clip (ClipIndex 4) during hang, was " + hangClip);

            for (var i = 0; i < 12; i++)
            {
                var hanging = ReadCurrentState(em, character) == "LedgeGrab";
                SetDrive(em, character, float3.zero, false, hanging);
                yield return null;
                if (!hanging)
                    break;
            }
            SetDrive(em, character, float3.zero, false, false);

            var sawStandUp = false;
            var states = "";
            var deadline = Time.time + 4f;
            var frames = 0;
            while (Time.time < deadline)
            {
                SetDrive(em, character, float3.zero, false, false);
                var state = ReadCurrentState(em, character);
                if (frames % 6 == 0)
                    states += state + ",";
                if (state == "LedgeStandingUp")
                    sawStandUp = true;
                if (state == "GroundMove")
                    break;
                frames++;
                yield return null;
            }

            var pos = em.GetComponentData<LocalTransform>(character).Position;
            var last = ReadCurrentState(em, character);
            var report = "stateTrail=" + states + " last=" + last + " pos=" + pos + " sawStandUp=" + sawStandUp;
            Debug.Log("STEP15 LedgeStandUp " + report);
            Assert.AreEqual("GroundMove", last, "official stand-up should land on the unmarked shelf. " + report);
            Assert.GreaterOrEqual(pos.y, 2.0f, "should be on OverhangShelf Lip top (~2.5), " + report);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NaturalRockSandbox_VerticalWallFixture_IsLargeAndUnmarked()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return WaitForState("GroundMove", 10f);

            // NaturalRockSandbox is streamed into an Entities SubScene, so the
            // authoring GameObject is not present at runtime. Validate the
            // baked static physics body instead of searching the hierarchy.
            Assert.IsTrue(PlatformerTestHelpers.TryFindVerticalWallAabb(
                World.DefaultGameObjectInjectionWorld.EntityManager, out var wallAabb),
                "Rock_VerticalWall confirmation fixture must be baked into the static physics world");

            var size = wallAabb.Max - wallAabb.Min;
            Assert.GreaterOrEqual(size.x, 11f, "confirmation wall width should be ~12m");
            Assert.GreaterOrEqual(size.y, 7f, "confirmation wall height should be ~8m");
            Assert.Greater(size.z, 0.4f, "confirmation wall must have a solid collision depth");
            Assert.Less(size.z, 0.8f, "confirmation wall depth should remain the thin test fixture");

            // Front face z = center.z - depth/2 ≈ 1.275 for existing climb placement.
            Assert.AreEqual(1.275f, wallAabb.Min.z, 0.08f,
                "wall front face position for climb tests");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NaturalRockSandbox_GeometryClimb_VerticalWall()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return PlatformerTestHelpers.WaitForState("GroundMove", 10f);

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.EnsureGeometryConfig(em, character);

            PlatformerTestHelpers.PlaceCharacter(em, character,
                new float3(0f, 1.1f, 1.15f), quaternion.identity, float3.zero);
            EnsureTestDrive(em, character);

            yield return DriveUntilState(em, character, "Climbing", 4f, new float3(0f, 0f, 0.4f), false, climbPressed: true);
            yield return HoldState(em, character, "Climbing", 15, new float3(0f, 0.4f, 0f), climbPressed: false);

            var pos = em.GetComponentData<LocalTransform>(character).Position;
            Assert.Greater(pos.y, 1.05f, "geometry climb should raise character on vertical wall fixture");

            SetDrive(em, character, float3.zero, false, false, climbPressed: true);
            yield return PlatformerTestHelpers.WaitForState("AirMove", 3f);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NaturalRockSandbox_GeometryClimb_IrregularMeshCollider()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return PlatformerTestHelpers.WaitForState("GroundMove", 10f);

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.EnsureGeometryConfig(em, character);
            PlatformerTestHelpers.PlaceCharacter(em, character,
                new float3(0f, 1.1f, 20.85f), quaternion.identity, float3.zero);
            EnsureTestDrive(em, character);

            yield return DriveUntilState(em, character, "Climbing", 4f, float3.zero, false, climbPressed: true);
            yield return HoldState(em, character, "Climbing", 15, new float3(0f, 0.4f, 0f));

            var pos = em.GetComponentData<LocalTransform>(character).Position;
            Assert.IsTrue(math.all(math.isfinite(pos)), "irregular MeshCollider climb must not produce NaN");
            Assert.Greater(pos.y, 1.05f, "geometry climb should work on the unmarked irregular MeshCollider fixture");

            SetDrive(em, character, float3.zero, false, false, climbPressed: true);
            yield return PlatformerTestHelpers.WaitForState("AirMove", 3f);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NaturalRockSandbox_Mantle_NoTeleportSnap()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return PlatformerTestHelpers.WaitForState("GroundMove", 10f);

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.PlaceFallingAtUnmarkedShelf(em, character);
            EnsureTestDrive(em, character);
            yield return DriveUntilState(em, character, "LedgeGrab", 5f, new float3(0f, 0f, 0.4f), false);
            yield return HoldState(em, character, "LedgeGrab", 8, float3.zero);

            var hangPos = em.GetComponentData<LocalTransform>(character).Position;
            var maxStep = 0f;
            for (var i = 0; i < 60; i++)
            {
                // Keep the deterministic harness press alive for a few fixed
                // steps so the state-machine update cannot miss its edge.
                var requestMantle = i < 4 && ReadCurrentState(em, character) == "LedgeGrab";
                SetDrive(em, character, float3.zero, false, requestMantle);
                yield return null;
                var state = ReadCurrentState(em, character);
                if (state == "GroundMove")
                    break;
                var pos = em.GetComponentData<LocalTransform>(character).Position;
                maxStep = math.max(maxStep, math.distance(pos, hangPos));
                hangPos = pos;
            }

            Assert.Less(maxStep, 0.35f, "mantle should advance in small steps, not one-frame teleport");
            Assert.AreEqual("GroundMove", ReadCurrentState(em, character));
        }

        static void PlaceFallingAtUnmarkedShelf(EntityManager em, Entity character)
        {
            PlatformerTestHelpers.PlaceFallingAtUnmarkedShelf(em, character);
        }

        static IEnumerator DriveUntilState(EntityManager em, Entity character, string expected, float seconds, float3 move, bool jumpPressed, bool climbPressed = false)
        {
            var end = Time.time + seconds;
            var states = "";
            var frames = 0;
            while (Time.time < end)
            {
                SetDrive(em, character, move, false, jumpPressed, climbPressed);
                var state = ReadCurrentState(em, character);
                if (frames < 40)
                    states += state + ",";
                if (state == expected)
                    yield break;
                frames++;
                yield return null;
            }

            var pos = em.GetComponentData<LocalTransform>(character).Position;
            Assert.Fail("Timed out waiting for " + expected + " (trail=" + states + " last=" + ReadCurrentState(em, character) + " pos=" + pos + ")");
        }

        static IEnumerator HoldState(EntityManager em, Entity character, string expected, int frames, float3 move, bool climbPressed = false)
        {
            var trail = "";
            for (var i = 0; i < frames; i++)
            {
                SetDrive(em, character, move, false, false, climbPressed);
                var state = ReadCurrentState(em, character);
                trail += state + ",";
                if (state != expected)
                {
                    var pos = em.GetComponentData<LocalTransform>(character).Position;
                    Assert.Fail("lost " + expected + " after " + i + " hang frames (trail=" + trail + " pos=" + pos + ")");
                }
                yield return null;
            }
        }

        static int ReadHybridClipIndex()
        {
            foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include))
            {
                if (animator == null || animator.gameObject.name.IndexOf("CharacterMesh") < 0)
                    continue;
                return animator.GetInteger("ClipIndex");
            }
            return -1;
        }

        static IEnumerator WaitForState(string expected, float seconds)
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

            var last = "none";
            var w = World.DefaultGameObjectInjectionWorld;
            if (w != null && w.IsCreated)
            {
                var c = FindCharacter(w.EntityManager);
                last = c == Entity.Null ? "no-character" : ReadCurrentState(w.EntityManager, c);
            }
            Assert.Fail("Timed out waiting for " + expected + " (last=" + last + ")");
        }

        static Entity FindCharacter(EntityManager em)
        {
            var type = FindType("PlatformerCharacterStateMachine");
            if (type == null)
                return Entity.Null;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly(type));
            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }

        static void EnsureTestDrive(EntityManager em, Entity character)
        {
            var type = FindType("TozanPlatformerTestDrive");
            Assert.IsNotNull(type, "TozanPlatformerTestDrive");
            if (!em.HasComponent(character, type))
                em.AddComponent(character, type);
        }

        static void SetBodyVelocity(EntityManager em, Entity character, float3 linear)
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

        static void ZeroBodyVelocity(EntityManager em, Entity character)
        {
            SetBodyVelocity(em, character, float3.zero);
        }

        static void SetDrive(EntityManager em, Entity character, float3 move, bool jumpHeld, bool jumpPressed, bool climbPressed = false, bool crouchPressed = false)
        {
            var type = FindType("TozanPlatformerTestDrive");
            var boxed = System.Activator.CreateInstance(type);
            type.GetField("MoveVector").SetValue(boxed, move);
            type.GetField("JumpHeld").SetValue(boxed, jumpHeld);
            type.GetField("JumpPressed").SetValue(boxed, jumpPressed);
            type.GetField("ClimbPressed").SetValue(boxed, climbPressed);
            type.GetField("CrouchPressed").SetValue(boxed, crouchPressed);
            SetComponent(em, character, type, boxed);
        }

        static string ReadCurrentState(EntityManager em, Entity character)
        {
            var type = FindType("PlatformerCharacterStateMachine");
            var data = GetComponent(em, character, type);
            var state = type.GetField("CurrentState").GetValue(data);
            return state != null ? state.ToString() : "null";
        }

        static object GetComponent(EntityManager em, Entity entity, System.Type type)
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

        static void SetComponent(EntityManager em, Entity entity, System.Type type, object boxed)
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

        static System.Type FindType(string name)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null)
                    return t;
            }
            return null;
        }
    }
}
