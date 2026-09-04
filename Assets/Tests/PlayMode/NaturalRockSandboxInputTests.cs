using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tozan.Tests
{
    /// <summary>
    /// PlayMode gates using queued Input System device events (not TestDrive injection).
    /// Placement still uses TestDrive-free ECS writes where needed for deterministic spawn.
    /// </summary>
    public class NaturalRockSandboxInputTests
    {
        static Keyboard s_sharedKeyboard;
        Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            if (s_sharedKeyboard == null || !s_sharedKeyboard.added)
                s_sharedKeyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard = s_sharedKeyboard;
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null)
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator InputSystem_LedgeGrab_OnUnmarkedOverhangLip()
        {
            yield return LoadSandboxAndWaitForGroundMove();

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            Assert.AreNotEqual(Entity.Null, character);
            PlatformerTestHelpers.EnsureGeometryConfig(em, character);

            PlatformerTestHelpers.PlaceFallingAtUnmarkedShelf(em, character);
            PlatformerTestHelpers.ClearControlOverrides(em, character);

            PressKey(Key.W);
            yield return WaitForState(em, character, "LedgeGrab", 6f);
            yield return HoldStateWithInput(em, character, "LedgeGrab", 12, Key.None);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator InputSystem_Mantle_ReachesGroundOnShelf()
        {
            yield return LoadSandboxAndWaitForGroundMove();

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.PlaceFallingAtUnmarkedShelf(em, character);
            PlatformerTestHelpers.ClearControlOverrides(em, character);

            PressKey(Key.W);
            yield return WaitForState(em, character, "LedgeGrab", 6f);
            yield return HoldStateWithInput(em, character, "LedgeGrab", 8, Key.None);

            var startPos = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;
            PressKey(Key.Space);
            for (var i = 0; i < 90; i++)
            {
                // Let the fixed-step input bridge consume the press before
                // releasing the synthetic keyboard state.
                if (i > 0)
                    ReleaseAllKeys();
                yield return null;
            }

            var endPos = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;
            var last = PlatformerTestHelpers.ReadCurrentState(em, character);
            var report = "last=" + last + " start=" + startPos + " end=" + endPos;
            Assert.AreEqual("GroundMove", last, "mantle should finish grounded on shelf. " + report);
            Assert.GreaterOrEqual(endPos.y, 2.0f, report);
            Assert.Greater(math.distance(startPos, endPos), 0.05f, "mantle must move without teleport snap. " + report);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator InputSystem_GeometryClimb_VerticalWall_HoldAndRelease()
        {
            yield return LoadSandboxAndWaitForGroundMove();

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.EnsureGeometryConfig(em, character);
            PlatformerTestHelpers.ClearControlOverrides(em, character);

            // Rock_VerticalWall front face ~ z=1.275; place the standing
            // capsule at its contact point so the climb edge is not consumed
            // by a corrective overlap step.
            PlatformerTestHelpers.PlaceCharacter(em, character,
                new float3(0f, 1.1f, 0.98f), quaternion.identity, float3.zero);

            // Start climbing with the real climb binding while stationary;
            // apply W only after the state transition so ground movement
            // cannot carry the character away from the wall first.
            yield return HoldKeysUntilState(em, character, "Climbing", 4f, Key.F);
            yield return HoldStateWithInput(em, character, "Climbing", 15, Key.W);

            var climbPos = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;
            Assert.Greater(climbPos.y, 1.05f, "should move upward while climbing");

            yield return ReleaseClimbAndWaitForExit(em, character, 3f);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator InputSystem_CrouchRelease_FromLedgeGrab()
        {
            yield return LoadSandboxAndWaitForGroundMove();

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.PlaceFallingAtUnmarkedShelf(em, character);
            PlatformerTestHelpers.ClearControlOverrides(em, character);

            PressKey(Key.W);
            yield return WaitForState(em, character, "LedgeGrab", 6f);
            PressKey(Key.C);
            yield return WaitForState(em, character, "AirMove", 2f);
        }

        IEnumerator LoadSandboxAndWaitForGroundMove()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return PlatformerTestHelpers.WaitForState("GroundMove", 10f);

            // Apply the queued neutral state after the new player input map is
            // enabled. This prevents a key held by the preceding test from
            // suppressing the next WasPressedThisFrame edge.
            ReleaseAllKeys();
            yield return null;
        }

        void PressKey(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
        }

        void PressKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        }

        void ReleaseAllKeys()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        }

        IEnumerator HoldKeysUntilState(EntityManager em, Entity character, string expected, float seconds, params Key[] keys)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
                PressKeys(keys);
                if (PlatformerTestHelpers.ReadCurrentState(em, character) == expected)
                    yield break;
                yield return null;
            }

            var position = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;
            Assert.Fail("Timed out waiting for " + expected + " last=" + PlatformerTestHelpers.ReadCurrentState(em, character) + " pos=" + position);
        }

        IEnumerator ReleaseClimbAndWaitForExit(EntityManager em, Entity character, float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
                // Keep the real climb binding held until the fixed-step bridge
                // consumes the edge. A transient AirMove may immediately land
                // on the sandbox floor, so accept either post-climb state.
                PressKey(Key.F);
                var state = PlatformerTestHelpers.ReadCurrentState(em, character);
                if (state == "AirMove" || state == "GroundMove")
                    yield break;
                yield return null;
            }

            Assert.Fail("Timed out waiting for climb release last=" + PlatformerTestHelpers.ReadCurrentState(em, character));
        }

        static IEnumerator WaitForState(EntityManager em, Entity character, string expected, float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
                if (PlatformerTestHelpers.ReadCurrentState(em, character) == expected)
                    yield break;
                yield return null;
            }

            Assert.Fail("Timed out waiting for " + expected + " last=" + PlatformerTestHelpers.ReadCurrentState(em, character));
        }

        IEnumerator HoldStateWithInput(EntityManager em, Entity character, string expected, int frames, Key heldKey)
        {
            for (var i = 0; i < frames; i++)
            {
                if (heldKey != Key.None)
                {
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(heldKey));
                }

                var state = PlatformerTestHelpers.ReadCurrentState(em, character);
                if (state != expected)
                    Assert.Fail("lost " + expected + " after " + i + " frames (last=" + state + ")");
                yield return null;
            }
        }
    }
}
