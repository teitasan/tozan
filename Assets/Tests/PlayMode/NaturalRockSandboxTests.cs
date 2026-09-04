using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
            Assert.AreNotEqual(Entity.Null, FindCharacter(world.EntityManager));
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

        static void PlaceFallingAtUnmarkedShelf(EntityManager em, Entity character)
        {
            ZeroBodyVelocity(em, character);
            var lt = em.GetComponentData<LocalTransform>(character);
            // Rock_OverhangShelf / Lip: unmarked boxes. Lip front z=4.95, top y=2.495, x=-5.5.
            // Standing capsule radius 0.3: z must stay < 4.65 or CanGrabLedge bails on overlap
            // and the character falls through, then walks under the lip on the ground.
            // Shelf_Low is only ~1.07m, so hang puts feet on the ground and drops immediately.
            // Official detection point is local (0, 1.084, 0.5). Fall into the lip, not jump onto it.
            lt.Position = new float3(-5.5f, 1.42f, 4.58f);
            lt.Rotation = quaternion.identity;
            em.SetComponentData(character, lt);
            SetBodyVelocity(em, character, new float3(0f, -1.2f, 0.8f));
        }

        static IEnumerator DriveUntilState(EntityManager em, Entity character, string expected, float seconds, float3 move, bool jumpPressed)
        {
            var end = Time.time + seconds;
            var states = "";
            var frames = 0;
            while (Time.time < end)
            {
                SetDrive(em, character, move, false, jumpPressed);
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

        static IEnumerator HoldState(EntityManager em, Entity character, string expected, int frames, float3 move)
        {
            var trail = "";
            for (var i = 0; i < frames; i++)
            {
                SetDrive(em, character, move, false, false);
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

        static void SetDrive(EntityManager em, Entity character, float3 move, bool jumpHeld, bool jumpPressed)
        {
            var type = FindType("TozanPlatformerTestDrive");
            var boxed = System.Activator.CreateInstance(type);
            type.GetField("MoveVector").SetValue(boxed, move);
            type.GetField("JumpHeld").SetValue(boxed, jumpHeld);
            type.GetField("JumpPressed").SetValue(boxed, jumpPressed);
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
