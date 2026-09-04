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
    /// PlayMode gate for the Erika hybrid visual on the official ECS Platformer player.
    /// </summary>
    public class NaturalRockSandboxErikaVisualTests
    {
        static Keyboard s_sharedKeyboard;
        Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            if (s_sharedKeyboard == null || !s_sharedKeyboard.added)
                s_sharedKeyboard = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();
            _keyboard = s_sharedKeyboard;

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
        public IEnumerator NaturalRockSandbox_ErikaHybridVisual_EntersClimbingWithClipIndex10()
        {
            yield return LoadSandboxAndWaitForGroundMove();

            var hybridAnimator = PlatformerTestHelpers.FindHybridCharacterMeshAnimator();
            Assert.IsNotNull(hybridAnimator, "ECS hybrid link must instantiate CharacterMesh visual");
            Assert.IsNotNull(hybridAnimator.avatar, "Erika visual must have an avatar");
            Assert.IsTrue(hybridAnimator.avatar.isHuman, "Erika visual must use a Humanoid avatar");
            Assert.IsFalse(hybridAnimator.applyRootMotion, "ECS owns movement; root motion must stay disabled");
            Assert.IsTrue(PlatformerTestHelpers.HasClipIndexParameter(hybridAnimator),
                "PlatformerCharacterAnimationHandler requires ClipIndex on the hybrid Animator");
            Assert.IsTrue(PlatformerTestHelpers.HasErikaRendererIdentity(hybridAnimator),
                "Hybrid visual must render Erika skinned meshes, not ProtoCharacter");

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var character = PlatformerTestHelpers.FindCharacter(em);
            PlatformerTestHelpers.EnsureGeometryConfig(em, character);
            PlatformerTestHelpers.ClearControlOverrides(em, character);
            PlatformerTestHelpers.PlaceAtVerticalWall(em, character);

            yield return HoldKeysUntilState(em, character, "Climbing", 4f, Key.S);
            yield return HoldStateWithInput(em, character, "Climbing", 20, Key.W);

            var clip = PlatformerTestHelpers.ReadHybridClipIndex();
            var speed = PlatformerTestHelpers.ReadHybridAnimatorSpeed();
            var velocity = PlatformerTestHelpers.ReadBodyVelocity(em, character);
            var report = "clip=" + clip + " speed=" + speed + " velocity=" + velocity;
            Assert.AreEqual(10, clip, "ClimbingMoveClip (ClipIndex 10) must play on Erika while climbing. " + report);
            Assert.IsTrue(math.lengthsq(velocity) > 0.01f || speed > 0.05f,
                "climbing with W must show motion via velocity or animator speed. " + report);
        }

        IEnumerator LoadSandboxAndWaitForGroundMove()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return PlatformerTestHelpers.WaitForState("GroundMove", 10f);
            QueueKeyboardState(new KeyboardState());
            yield return new WaitForFixedUpdate();
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
                QueueKeyboardState(new KeyboardState(keys));
                yield return new WaitForFixedUpdate();
                if (PlatformerTestHelpers.ReadCurrentState(em, character) == expected)
                    yield break;
            }

            var position = em.GetComponentData<Unity.Transforms.LocalTransform>(character).Position;
            Assert.Fail("Timed out waiting for " + expected + " last=" + PlatformerTestHelpers.ReadCurrentState(em, character)
                + " pos=" + position);
        }

        IEnumerator HoldStateWithInput(EntityManager em, Entity character, string expected, int frames, Key heldKey)
        {
            for (var i = 0; i < frames; i++)
            {
                if (heldKey == Key.None)
                    QueueKeyboardState(new KeyboardState());
                else
                    QueueKeyboardState(new KeyboardState(heldKey));

                yield return new WaitForFixedUpdate();
                var state = PlatformerTestHelpers.ReadCurrentState(em, character);
                if (state != expected)
                    Assert.Fail("lost " + expected + " after " + i + " frames (last=" + state + ")");
            }
        }
    }
}
