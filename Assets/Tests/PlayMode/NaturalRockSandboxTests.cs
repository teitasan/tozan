using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tozan.Tests
{
    public class NaturalRockSandboxTests
    {
        static readonly string[] RequiredRocks =
        {
            "Rock_VerticalWall",
            "Rock_Slope80",
            "Rock_OverhangShelf",
            "Rock_Overhang",
            "Rock_VariableWidthLedge",
            "Rock_ConvexCorner",
            "Rock_ConcaveCorner",
            "Rock_SteppedLedges",
            "Rock_Irregular"
        };

        [UnityTest]
        public IEnumerator NaturalRockSandbox_HasUnmarkedNaturalGeometry()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return null;

            var rocks = GameObject.Find("NaturalRocks");
            Assert.IsNotNull(rocks, "NaturalRocks root");
            foreach (var name in RequiredRocks)
                Assert.IsNotNull(GameObject.Find(name), name);

            Assert.IsNull(GameObject.Find("Climb_Ledge"));
            Assert.IsNull(GameObject.Find("Climb_Wall"));
            Assert.IsNull(GameObject.Find("Climb_SmallLedge"));
            Assert.IsNull(GameObject.Find("Vault_Box"));
            Assert.IsNull(GameObject.Find("Jump_Reach"));

            foreach (var t in rocks.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreNotEqual("Vault", t.gameObject.tag, t.name + " must not use Vault tag");
                Assert.AreEqual(0, t.gameObject.layer, t.name + " must stay on Default (no Ledge/Wall layer)");
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null)
                        continue;
                    var typeName = c.GetType().Name;
                    Assert.AreNotEqual("HandlePoints", typeName, t.name);
                    Assert.AreNotEqual("Point", typeName, t.name);
                    Assert.AreNotEqual("TraverserClimbingObject", typeName, t.name);
                    Assert.AreNotEqual("TraverserParkourObject", typeName, t.name);
                }
            }

            var player = GameObject.Find("TraverserPlayer");
            Assert.IsNotNull(player, "STEP 14 Traverser player");
            Assert.IsTrue(HasComponent(player, "TraverserClimbingAbility"));
        }

        [UnityTest]
        [Timeout(25000)]
        [Ignore("STEP 13 already recorded DPS failure on this scene. STEP 14 hosts TraverserPlayer instead.")]
        public IEnumerator NaturalRockSandbox_UnmarkedMesh_GrabHangTraverseMantle()
        {
            // Adoption gate. Do not make this pass by adding Vault tags, Ledge layer,
            // HandlePoints, or DPS environment prefabs. A fail means DPS does not
            // read unmarked natural meshes.
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return null;
            yield return new WaitForFixedUpdate();

            var player = FindClimbPlayer();
            Assert.IsNotNull(player, "DPS player");

            var start = GameObject.Find("TraversalStart");
            var spawn = start != null ? start.transform.position : new Vector3(0f, 0.1f, 0.2f);
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            DisablePlayerInput(player);
            yield return new WaitForSeconds(0.4f);

            var grabbed = false;
            var hung = false;
            var traversed = false;
            var mantled = false;
            var hangX = player.transform.position.x;
            var hangY = player.transform.position.y;

            yield return HoldInput(player, Vector2.zero, true, 0.45f);

            var grabDeadline = Time.time + 2.2f;
            while (Time.time < grabDeadline)
            {
                SetDpsInput(player, Vector2.zero, true);
                if (IsGrabbed(player))
                    grabbed = true;
                if (IsHanging(player))
                {
                    hung = true;
                    hangX = player.transform.position.x;
                    hangY = player.transform.position.y;
                    break;
                }
                yield return null;
            }

            if (hung)
            {
                var traverseDeadline = Time.time + 1.6f;
                while (Time.time < traverseDeadline)
                {
                    SetDpsInput(player, new Vector2(1f, 0f), false);
                    if (IsHanging(player) && Mathf.Abs(player.transform.position.x - hangX) > 0.15f)
                    {
                        traversed = true;
                        break;
                    }
                    yield return null;
                }

                var preMantleY = player.transform.position.y;
                var mantleDeadline = Time.time + 3.2f;
                while (Time.time < mantleDeadline)
                {
                    SetDpsInput(player, new Vector2(0f, 1f), true);
                    if (IsMantling(player) || (player.transform.position.y > preMantleY + 0.8f && !IsHanging(player)))
                    {
                        mantled = true;
                        break;
                    }
                    yield return null;
                }
            }

            var report = BuildReport(player, grabbed, hung, traversed, mantled);
            Debug.Log(report);
            Assert.IsTrue(grabbed && hung && traversed && mantled,
                "DPS did not complete Grab→Hang→Traverse→Mantle on unmarked natural mesh. " + report);
        }

        static IEnumerator HoldInput(GameObject player, Vector2 move, bool jump, float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
                SetDpsInput(player, move, jump);
                yield return null;
            }
        }

        static GameObject FindClimbPlayer()
        {
            var model = GameObject.Find("PlayerModel");
            if (model != null && HasComponent(model, "ClimbController"))
                return model;
            var player = GameObject.Find("Player");
            if (player != null && HasComponent(player, "ClimbController"))
                return player;
            return model != null ? model : player;
        }

        static bool HasComponent(GameObject go, string typeName)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == typeName)
                    return true;
            }
            return false;
        }

        static void DisablePlayerInput(GameObject player)
        {
            var input = FindComponent(player, "InputCharacterController") as Behaviour;
            if (input != null)
                input.enabled = false;
            var playerInput = FindComponent(player, "PlayerInput") as Behaviour;
            if (playerInput != null)
                playerInput.enabled = false;
        }

        static void SetDpsInput(GameObject player, Vector2 movement, bool jump)
        {
            var input = FindComponent(player, "InputCharacterController");
            if (input == null)
                return;
            SetField(input, "movement", movement);
            SetField(input, "jump", jump);
            SetField(input, "drop", false);
        }

        static bool IsGrabbed(GameObject player)
        {
            if (ReadBool(player, "ThirdPersonController", "dummy"))
                return true;
            if (ReadBool(player, "ClimbController", "toLedge"))
                return true;
            if (ReadBool(player, "ClimbController", "onLedge"))
                return true;
            return AnimatorMatches(player, "Idle To Braced Hang", "Idle To Freehang");
        }

        static bool IsHanging(GameObject player)
        {
            if (ReadBool(player, "ClimbController", "onLedge"))
                return true;
            var state = ReadField(player, "ClimbController", "curClimbState");
            if (state != null)
            {
                var name = state.ToString();
                if (name == "BHanging" || name == "FHanging")
                    return true;
            }
            return AnimatorMatches(player, "Hanging Movement");
        }

        static bool IsMantling(GameObject player)
        {
            return AnimatorMatches(player, "Braced Hang To Crouch", "Freehang Climb");
        }

        static bool AnimatorMatches(GameObject player, params string[] states)
        {
            var animator = player.GetComponent<Animator>();
            if (animator == null)
                return false;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            foreach (var state in states)
            {
                if (info.IsName(state))
                    return true;
            }
            return false;
        }

        static string BuildReport(GameObject player, bool grabbed, bool hung, bool traversed, bool mantled)
        {
            var sb = new StringBuilder();
            sb.Append("STEP13 grab=").Append(grabbed);
            sb.Append(" hang=").Append(hung);
            sb.Append(" traverse=").Append(traversed);
            sb.Append(" mantle=").Append(mantled);
            sb.Append(" pos=").Append(player.transform.position);
            sb.Append(" dummy=").Append(ReadBool(player, "ThirdPersonController", "dummy"));
            sb.Append(" onLedge=").Append(ReadBool(player, "ClimbController", "onLedge"));
            sb.Append(" toLedge=").Append(ReadBool(player, "ClimbController", "toLedge"));
            sb.Append(" climbState=").Append(ReadField(player, "ClimbController", "curClimbState"));

            var det = FindComponent(player, "DetectionCharacterController");
            if (det != null)
            {
                var layer = ReadField(det, "ledgeLayer");
                sb.Append(" ledgeLayer=").Append(layer);
                var method = det.GetType().GetMethod("FindLedgeCollision", BindingFlags.Instance | BindingFlags.Public);
                if (method != null)
                {
                    var args = new object[] { new RaycastHit() };
                    var found = (bool)method.Invoke(det, args);
                    sb.Append(" FindLedgeCollision=").Append(found);
                    if (found)
                    {
                        var hit = (RaycastHit)args[0];
                        sb.Append(" hit=").Append(hit.collider != null ? hit.collider.name : "null");
                    }
                }
            }

            var rocks = GameObject.Find("NaturalRocks");
            var handlePoints = 0;
            if (rocks != null)
            {
                foreach (var c in rocks.GetComponentsInChildren<Component>(true))
                {
                    if (c != null && c.GetType().Name == "HandlePoints")
                        handlePoints++;
                }
            }
            sb.Append(" rockHandlePoints=").Append(handlePoints);
            return sb.ToString();
        }

        static bool ReadBool(GameObject go, string typeName, string field)
        {
            var c = FindComponent(go, typeName);
            if (c == null)
                return false;
            var value = ReadField(c, field);
            return value is bool b && b;
        }

        static object ReadField(GameObject go, string typeName, string field)
        {
            var c = FindComponent(go, typeName);
            return c == null ? null : ReadField(c, field);
        }

        static Component FindComponent(GameObject go, string typeName)
        {
            if (go == null)
                return null;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == typeName)
                    return c;
            }
            return null;
        }

        static object ReadField(object target, string name)
        {
            if (target == null)
                return null;
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field.GetValue(target);
                type = type.BaseType;
            }
            return null;
        }

        static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
        }
    }
}
