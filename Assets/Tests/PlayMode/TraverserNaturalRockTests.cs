using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tozan.Tests
{
    public class TraverserNaturalRockTests
    {
        [UnityTest]
        public IEnumerator NaturalRockSandbox_HasNoTraverserMarkers()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return null;

            var rocks = GameObject.Find("NaturalRocks");
            Assert.IsNotNull(rocks);
            Assert.IsNotNull(GameObject.Find("TraverserPlayer"), "STEP 14 uses TraverserPlayer");
            Assert.IsNull(GameObject.Find("PlayerModel"), "DPS player must not be in the natural-rock gate");

            var player = GameObject.Find("TraverserPlayer");
            Assert.IsNotNull(player.transform.Find("PlayerCameraRoot"), "third-person camera target");
            Assert.IsNotNull(GameObject.Find("TraverserFollowCamera"), "Cinemachine follow camera");
            Assert.IsNotNull(FindComponent(player, "PlayerInput"), "PlayerInput wired to Traverser");

            foreach (var t in rocks.GetComponentsInChildren<Transform>(true))
            {
                Assert.AreNotEqual("Vault", t.gameObject.tag, t.name);
                Assert.AreEqual(0, t.gameObject.layer, t.name + " stays Default");
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null)
                        continue;
                    var n = c.GetType().Name;
                    Assert.AreNotEqual("TraverserClimbingObject", n, t.name);
                    Assert.AreNotEqual("TraverserParkourObject", n, t.name);
                    Assert.AreNotEqual("HandlePoints", n, t.name);
                    Assert.AreNotEqual("Point", n, t.name);
                }
            }
        }

        [UnityTest]
        public IEnumerator NaturalRockSandbox_ThirdPersonCameraFollowsPlayer()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            var player = GameObject.Find("TraverserPlayer");
            var cam = Camera.main;
            Assert.IsNotNull(player);
            Assert.IsNotNull(cam);
            Assert.Greater(Vector3.Distance(cam.transform.position, player.transform.position), 1.5f,
                "Main Camera must sit behind the player, not at the feet");
            Assert.Greater(cam.transform.position.y, 0.8f);
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator NaturalRockSandbox_MoveIsResponsiveAndJumpDoesNotStick()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return null;
            yield return new WaitForFixedUpdate();

            var player = GameObject.Find("TraverserPlayer");
            Assert.IsNotNull(player);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -6f), Quaternion.identity);

            var start = player.transform.position;
            yield return Hold(player, new Vector2(0f, 1f), east: false, north: false, 1.0f);
            var walked = player.transform.position.z - start.z;
            Assert.Greater(walked, 1.2f, "WASD should cover more than a crawl in 1s, moved " + walked);

            SetInput(player, Vector2.zero, east: false, north: true);
            yield return new WaitForFixedUpdate();
            SetInput(player, Vector2.zero, east: false, north: false);

            var peakY = player.transform.position.y;
            var stillRisingAtEnd = 0;
            for (var i = 0; i < 90; i++)
            {
                SetInput(player, Vector2.zero, east: false, north: true);
                yield return new WaitForFixedUpdate();
                var y = player.transform.position.y;
                if (y > peakY)
                    peakY = y;
                if (i > 50 && y > start.y + 0.6f)
                    stillRisingAtEnd++;
            }

            Assert.Less(player.transform.position.y, start.y + 0.5f,
                "Jump must land instead of holding Space forever. y=" + player.transform.position.y);
            Assert.Less(stillRisingAtEnd, 10, "Jump must not keep launching while Space is held");
        }

        [UnityTest]
        public IEnumerator NaturalRockSandbox_ConcaveMesh_DoesNotSpamClosestPoint()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return null;
            yield return new WaitForFixedUpdate();

            var player = GameObject.Find("TraverserPlayer");
            var irregular = GameObject.Find("Rock_Irregular");
            var trap = GameObject.Find("Rock_VariableWidthLedge");
            Assert.IsNotNull(player);
            Assert.IsNotNull(irregular);
            Assert.IsNotNull(trap);

            player.transform.position = irregular.transform.position + Vector3.up * 2.2f;
            for (var i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();

            player.transform.position = trap.transform.position + Vector3.up * 2.0f;
            for (var i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        [Timeout(25000)]
        public IEnumerator NaturalRockSandbox_Traverser_GrabHangTraverseMantleJumpGrab()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/NaturalRockSandbox.unity");
            yield return null;
            yield return new WaitForFixedUpdate();

            var player = GameObject.Find("TraverserPlayer");
            Assert.IsNotNull(player);

            var start = GameObject.Find("TraversalStart");
            var spawn = start != null ? start.transform.position : new Vector3(0f, 0.1f, 0.2f);
            player.transform.SetPositionAndRotation(spawn, Quaternion.identity);

            var findLedge = false;
            var grabbed = false;
            var hung = false;
            var traversed = false;
            var mantled = false;
            var jumpGrab = false;
            var hangX = player.transform.position.x;
            var hangY = player.transform.position.y;

            yield return Hold(player, new Vector2(0f, 1f), east: true, north: false, 1.4f);
            Sample(player, ref findLedge, ref grabbed, ref hung, ref traversed, ref mantled, ref jumpGrab, ref hangX, ref hangY);

            var hangDeadline = Time.time + 2.0f;
            while (Time.time < hangDeadline)
            {
                SetInput(player, new Vector2(0f, 1f), east: true, north: false);
                Sample(player, ref findLedge, ref grabbed, ref hung, ref traversed, ref mantled, ref jumpGrab, ref hangX, ref hangY);
                if (hung)
                    break;
                yield return null;
            }

            if (hung)
            {
                var traverseDeadline = Time.time + 1.6f;
                while (Time.time < traverseDeadline)
                {
                    SetInput(player, new Vector2(1f, 0f), east: false, north: false);
                    Sample(player, ref findLedge, ref grabbed, ref hung, ref traversed, ref mantled, ref jumpGrab, ref hangX, ref hangY);
                    if (traversed)
                        break;
                    yield return null;
                }

                var mantleDeadline = Time.time + 2.5f;
                while (Time.time < mantleDeadline)
                {
                    SetInput(player, Vector2.zero, east: false, north: true);
                    Sample(player, ref findLedge, ref grabbed, ref hung, ref traversed, ref mantled, ref jumpGrab, ref hangX, ref hangY);
                    if (mantled)
                        break;
                    yield return null;
                }
            }
            else
            {
                yield return Hold(player, new Vector2(0f, 1f), east: false, north: true, 0.7f);
                var jumpDeadline = Time.time + 1.5f;
                while (Time.time < jumpDeadline)
                {
                    SetInput(player, new Vector2(0f, 1f), east: true, north: false);
                    Sample(player, ref findLedge, ref grabbed, ref hung, ref traversed, ref mantled, ref jumpGrab, ref hangX, ref hangY);
                    if (jumpGrab || hung)
                        break;
                    yield return null;
                }
            }

            var report = BuildReport(player, findLedge, grabbed, hung, traversed, mantled, jumpGrab);
            Debug.Log(report);
            Assert.IsTrue(grabbed && hung && traversed && mantled,
                "Traverser did not complete Grab→Hang→Traverse→Mantle on unmarked natural mesh. " + report);
        }

        static IEnumerator Hold(GameObject player, Vector2 move, bool east, bool north, float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end)
            {
                SetInput(player, move, east, north);
                yield return null;
            }
        }

        static void Sample(GameObject player, ref bool findLedge, ref bool grabbed, ref bool hung,
            ref bool traversed, ref bool mantled, ref bool jumpGrab, ref float hangX, ref float hangY)
        {
            var tcc = FindComponent(player, "TraverserCharacterController");
            var climb = FindComponent(player, "TraverserClimbingAbility");
            var state = climb != null ? ReadField(climb, "state") : null;
            var stateName = state != null ? state.ToString() : "null";
            var hit = GetContactCollider(tcc);

            if (hit is BoxCollider)
                findLedge = true;

            if (stateName == "Mounting" || stateName == "LedgeToLedge")
            {
                grabbed = true;
                jumpGrab = jumpGrab || stateName == "LedgeToLedge";
            }
            if (stateName == "Climbing")
            {
                grabbed = true;
                hung = true;
                if (Mathf.Abs(player.transform.position.x - hangX) > 0.15f)
                    traversed = true;
            }
            else if (!hung)
            {
                hangX = player.transform.position.x;
                hangY = player.transform.position.y;
            }

            if (stateName == "PullUp" || (hung && player.transform.position.y > hangY + 0.8f && stateName == "Suspended"))
                mantled = true;
        }

        static void SetInput(GameObject player, Vector2 movement, bool east, bool north)
        {
            var input = FindComponent(player, "TraverserInputController");
            if (input == null)
                return;
            SetField(input, "inputMovement", movement);
            var flags = 0;
            if (east)
                flags |= 1 << 3;
            if (north)
                flags |= 1 << 2;
            var enumType = input.GetType().GetNestedType("InputInteraction", BindingFlags.NonPublic);
            if (enumType != null)
                SetField(input, "inputInteraction", System.Enum.ToObject(enumType, flags));
        }

        static string BuildReport(GameObject player, bool findLedge, bool grabbed, bool hung, bool traversed, bool mantled, bool jumpGrab)
        {
            var sb = new StringBuilder();
            sb.Append("STEP14 findLedge=").Append(findLedge);
            sb.Append(" grab=").Append(grabbed);
            sb.Append(" hang=").Append(hung);
            sb.Append(" traverse=").Append(traversed);
            sb.Append(" mantle=").Append(mantled);
            sb.Append(" jumpGrab=").Append(jumpGrab);
            sb.Append(" pos=").Append(player.transform.position);

            var climb = FindComponent(player, "TraverserClimbingAbility");
            sb.Append(" climbState=").Append(climb != null ? ReadField(climb, "state") : "missing");

            var tcc = FindComponent(player, "TraverserCharacterController");
            var hit = GetContactCollider(tcc);
            sb.Append(" contact=").Append(hit != null ? hit.name : "none");
            sb.Append(" box=").Append(hit is BoxCollider);
            sb.Append(" climbingObject=").Append(HasNamedComponent(hit, "TraverserClimbingObject"));

            var rocks = GameObject.Find("NaturalRocks");
            var markers = 0;
            if (rocks != null)
            {
                foreach (var c in rocks.GetComponentsInChildren<Component>(true))
                {
                    if (c != null && (c.GetType().Name == "TraverserClimbingObject" || c.GetType().Name == "TraverserParkourObject"))
                        markers++;
                }
            }
            sb.Append(" rockMarkers=").Append(markers);
            return sb.ToString();
        }

        static Collider GetContactCollider(Component tcc)
        {
            var state = ReadField(tcc, "state");
            if (state == null)
                return null;
            var current = state.GetType().GetField("currentCollision", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (current == null)
                return null;
            var collision = current.GetValue(state);
            if (collision == null)
                return null;
            var colliderField = collision.GetType().GetField("collider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return colliderField != null ? colliderField.GetValue(collision) as Collider : null;
        }

        static bool HasNamedComponent(Component host, string typeName)
        {
            if (host == null)
                return false;
            foreach (var c in host.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name == typeName)
                    return true;
            }
            return false;
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
