using System.Collections;
using NUnit.Framework;
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
        const float MinClimbDelta = 0.08f;
        const float MaxWallNormalDrift = 0.2f;

        static Keyboard s_sharedKeyboard;
        Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            if (s_sharedKeyboard == null || !s_sharedKeyboard.added)
                s_sharedKeyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            _keyboard = s_sharedKeyboard;

            // The Unity Test Runner keeps devices created by earlier PlayMode
            // runs in the editor process. A stale keyboard multiplies every
            // keyboard binding during action resolution and can exceed the
            // Input System's per-binding control-count limit.
            var ignoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                for (var i = InputSystem.devices.Count - 1; i >= 0; i--)
                {
                    if (InputSystem.devices[i] is Keyboard keyboard && keyboard != _keyboard)
                        InputSystem.RemoveDevice(keyboard);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreFailingMessages;
            }

            _keyboard.MakeCurrent();
            QueueKeyboardState(new KeyboardState());
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null)
                QueueKeyboardState(new KeyboardState());
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
            PlatformerTestHelpers.PlaceAtVerticalWall(em, character);

            // Approach with S first, then press F once the capsule is against
            // the face. Holding both from the initial placement can consume
            // the official WasPressedThisFrame edge before contact exists.
            for (var i = 0; i < 8; i++)
            {
                PressKey(Key.S);
                yield return new WaitForFixedUpdate();
            }
            ReleaseAllKeys();
            yield return new WaitForFixedUpdate();
            yield return HoldKeysUntilState(em, character, "Climbing", 4f, Key.F);
            yield return HoldStateWithInput(em, character, "Climbing", 15, Key.W);

            var climbPos = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;
            Assert.Greater(climbPos.y, 1.05f, "should move upward while climbing");

            yield return ReleaseClimbAndWaitForExit(em, character, 3f);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator InputSystem_GeometryClimb_WallRelative_WASD()
        {
            // Use one real-input session so the four directions are verified
            // against the same camera, wall normal, and active climb state.
            yield return EnterClimbingOnVerticalWall(startHeight: 1.1f);

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            var start = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;

            yield return HoldStateWithInput(em, character, "Climbing", 30, Key.W);
            var afterW = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;

            yield return HoldStateWithInput(em, character, "Climbing", 2, Key.None);
            yield return HoldStateWithInput(em, character, "Climbing", 30, Key.S);
            var afterS = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;

            yield return HoldStateWithInput(em, character, "Climbing", 2, Key.None);
            yield return HoldStateWithInput(em, character, "Climbing", 30, Key.A);
            var afterA = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;

            yield return HoldStateWithInput(em, character, "Climbing", 2, Key.None);
            yield return HoldStateWithInput(em, character, "Climbing", 30, Key.D);
            var afterD = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;

            var cameraRight = PlatformerTestHelpers.ReadCameraRight(em);
            var wallRight = math.normalizesafe(new float3(cameraRight.x, 0f, cameraRight.z), math.right());
            var report = "start=" + start + " W=" + afterW + " S=" + afterS + " A=" + afterA + " D=" + afterD
                + " cameraRight=" + cameraRight + " wallRight=" + wallRight;
            Assert.Greater(afterW.y - start.y, MinClimbDelta, "W should ascend on the wall. " + report);
            Assert.Less(afterS.y - afterW.y, -MinClimbDelta, "S should descend on the wall. " + report);
            Assert.Less(math.dot(afterA - afterS, wallRight), -MinClimbDelta,
                "A should move toward screen-left along the wall. " + report);
            Assert.Greater(math.dot(afterD - afterA, wallRight), MinClimbDelta,
                "D should move toward screen-right along the wall. " + report);
            Assert.Less(math.abs(afterD.z - start.z), MaxWallNormalDrift,
                "WASD should keep the character on the wall face. " + report);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator InputSystem_GeometryClimb_ClimbingAnimationClipAndVelocity()
        {
            yield return EnterClimbingOnVerticalWall();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);

            yield return HoldStateWithInput(em, character, "Climbing", 20, Key.W);

            var clip = PlatformerTestHelpers.ReadHybridClipIndex();
            var speed = PlatformerTestHelpers.ReadHybridAnimatorSpeed();
            var velocity = PlatformerTestHelpers.ReadBodyVelocity(em, character);
            var report = "clip=" + clip + " speed=" + speed + " velocity=" + velocity;
            Assert.AreEqual(10, clip, "ClimbingMoveClip (ClipIndex 10) must play while climbing. " + report);
            Assert.IsTrue(math.lengthsq(velocity) > 0.01f || speed > 0.05f,
                "climbing with W must show motion via velocity or animator speed. " + report);
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

        IEnumerator EnterClimbingOnVerticalWall(float startHeight = 1.1f)
        {
            yield return LoadSandboxAndWaitForGroundMove();

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.EnsureGeometryConfig(em, character);
            PlatformerTestHelpers.ClearControlOverrides(em, character);
            PlatformerTestHelpers.PlaceAtVerticalWall(em, character, startHeight);

            // Approach with S first, then press F once the capsule is against
            // the face. Holding both from the initial placement can consume
            // the official WasPressedThisFrame edge before contact exists.
            for (var i = 0; i < 8; i++)
            {
                PressKey(Key.S);
                yield return new WaitForFixedUpdate();
            }
            ReleaseAllKeys();
            yield return new WaitForFixedUpdate();
            yield return HoldKeysUntilState(em, character, "Climbing", 4f, Key.F);
            // Consume the start-climb press before the movement direction is
            // changed. The subsequent wall traversal must be driven by WASD.
            ReleaseAllKeys();
            yield return new WaitForFixedUpdate();
        }

        IEnumerator LoadSandboxAndWaitForGroundMove()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return PlatformerTestHelpers.WaitForState("GroundMove", 10f);

            ReleaseAllKeys();
            yield return new WaitForFixedUpdate();
        }

        void PressKey(Key key)
        {
            QueueKeyboardState(new KeyboardState(key));
        }

        void PressKeys(params Key[] keys)
        {
            QueueKeyboardState(new KeyboardState(keys));
        }

        void ReleaseAllKeys()
        {
            QueueKeyboardState(new KeyboardState());
        }

        void QueueKeyboardState(KeyboardState state)
        {
            InputSystem.QueueStateEvent(_keyboard, state);
        }

        IEnumerator HoldKeysUntilState(EntityManager em, Entity character, string expected, float seconds, params Key[] keys)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
                PressKeys(keys);
                yield return new WaitForFixedUpdate();
                if (PlatformerTestHelpers.ReadCurrentState(em, character) == expected)
                    yield break;
            }

            var position = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;
            Assert.Fail("Timed out waiting for " + expected + " last=" + PlatformerTestHelpers.ReadCurrentState(em, character)
                + " pos=" + position);
        }

        IEnumerator ReleaseClimbAndWaitForExit(EntityManager em, Entity character, float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
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
                if (heldKey == Key.None)
                    ReleaseAllKeys();
                else
                    PressKey(heldKey);

                yield return new WaitForFixedUpdate();
                var state = PlatformerTestHelpers.ReadCurrentState(em, character);
                if (state != expected)
                    Assert.Fail("lost " + expected + " after " + i + " frames (last=" + state + ")");
            }
        }
    }
}
