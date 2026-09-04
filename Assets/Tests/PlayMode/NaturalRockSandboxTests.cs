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

            ZeroBodyVelocity(em, character);
            var lt = em.GetComponentData<LocalTransform>(character);
            // Shelf_Low: unmarked box, top ~y=1.075, front ~z=16.075.
            // Official CanGrabLedge runs only in AirMove and rejects grabs while moving up
            // the surface normal. Approach falling into the lip, not jumping onto the top.
            lt.Position = new float3(0f, 0.55f, 15.7f);
            lt.Rotation = quaternion.identity;
            em.SetComponentData(character, lt);
            SetBodyVelocity(em, character, new float3(0f, -2f, 4f));

            EnsureTestDrive(em, character);

            var grabbed = false;
            var states = "";
            var deadline = Time.time + 5f;
            var frames = 0;
            while (Time.time < deadline)
            {
                SetDrive(em, character, new float3(0f, 0f, 1f), false, false);
                var state = ReadCurrentState(em, character);
                if (frames % 8 == 0)
                    states += state + ",";
                if (state == "LedgeGrab" || state == "LedgeStandingUp")
                {
                    grabbed = true;
                    break;
                }
                frames++;
                yield return null;
            }

            var pos = em.GetComponentData<LocalTransform>(character).Position;
            var report = "stateTrail=" + states + " last=" + ReadCurrentState(em, character) + " pos=" + pos;
            Debug.Log("STEP15 LedgeGrab " + report);
            Assert.IsTrue(grabbed,
                "Official LedgeDetection did not enter LedgeGrab on unmarked natural mesh. " + report);
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
                if (field != null)
                {
                    field.SetValue(boxed, linear);
                    SetComponent(em, character, bodyType, boxed);
                }
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
