using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tozan.Tests
{
    public class PrototypeSmokeTests
    {
        [Test]
        public void ClimbingSandbox_AssetExists()
        {
            Assert.IsTrue(System.IO.File.Exists("Assets/Scenes/ClimbingSandbox.unity"));
        }

        [Test]
        public void StarterAssetsPlayground_AssetExists()
        {
            Assert.IsTrue(System.IO.File.Exists("Assets/Scenes/StarterAssetsPlayground.unity"));
        }

        [Test]
        public void TerrainSandbox_AssetExists()
        {
            Assert.IsTrue(System.IO.File.Exists("Assets/Scenes/TerrainSandbox.unity"));
        }

        [Test]
        public void DynamicParkourPlayerPrefab_Exists()
        {
            Assert.IsTrue(System.IO.File.Exists("Assets/ThirdParty/DynamicParkourSystem/Prefabs/Player.prefab"));
        }

        [Test]
        public void QuaterniusCharacter_Exists()
        {
            Assert.IsTrue(System.IO.File.Exists("Assets/Characters/Quaternius/Casual_Male.fbx"));
        }

        [Test]
        public void UalAnimations_Exist()
        {
            Assert.IsTrue(System.IO.File.Exists("Assets/Characters/UAL/AnimationLibrary_Unity_Standard.fbx"));
            Assert.IsTrue(System.IO.File.Exists("Assets/Characters/UAL/UAL2_Standard.fbx"));
        }

        [UnityTest]
        public IEnumerator ClimbingSandbox_PlayModeHasPlayerAndBoxes()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/ClimbingSandbox.unity");
            yield return null;

            var player = GameObject.Find("PlayerModel");
            if (player == null)
                player = GameObject.Find("Player");
            if (player == null)
                player = GameObject.Find("PlayerArmature");
            Assert.IsNotNull(player, "Player should exist in ClimbingSandbox");
            Assert.IsTrue(HasComponent(player, "ClimbController"), "ClimbController");
            Assert.IsTrue(HasComponent(player, "VaultingController"), "VaultingController");
            Assert.IsTrue(HasComponent(player, "JumpPredictionController"), "JumpPredictionController");

            var boxes = GameObject.Find("Boxes");
            Assert.IsNotNull(boxes, "Boxes root should exist");
            Assert.GreaterOrEqual(boxes.transform.childCount, 7, "Need a small box course");
            Assert.IsNotNull(GameObject.Find("Climb_Ledge"), "Ledge for grab/hang");
            Assert.IsNotNull(GameObject.Find("Climb_Wall"), "Wall for traverse");
            Assert.IsNotNull(GameObject.Find("Jump_Reach"), "Reach for jump grab");
            yield return new WaitForSeconds(0.25f);
        }

        [UnityTest]
        public IEnumerator StarterAssetsPlayground_PlayerMoves()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/StarterAssetsPlayground.unity");
            yield return null;

            var player = GameObject.Find("PlayerArmature");
            Assert.IsNotNull(player, "Starter Assets player");
            Assert.IsNotNull(player.GetComponent<CharacterController>());
            Assert.IsTrue(HasComponent(player, "ThirdPersonController"), "StarterAssets ThirdPersonController");
            Assert.IsTrue(HasComponent(player, "StarterAssetsInputs"), "StarterAssetsInputs");

            var start = player.transform.position;
            SetStarterMove(player, new Vector2(0f, 1f));
            yield return new WaitForSeconds(0.6f);
            Assert.Greater((player.transform.position - start).sqrMagnitude, 0.01f, "Starter player should move");
        }

        [UnityTest]
        public IEnumerator TerrainSandbox_HasTerrainTexturesAndVegetation()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/TerrainSandbox.unity");
            yield return null;

            var terrain = Object.FindFirstObjectByType<Terrain>();
            Assert.IsNotNull(terrain);
            Assert.GreaterOrEqual(terrain.terrainData.terrainLayers.Length, 2, "Need grass + dirt layers");
            Assert.Greater(GameObject.Find("PlaceholderVegetation").transform.childCount, 0, "Need placeholder trees");
            Assert.Greater(terrain.terrainData.detailPrototypes.Length, 0, "Need grass details");
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

        static void SetMovement(GameObject player, Vector2 movement)
        {
            var input = player.GetComponent("InputCharacterController");
            if (input == null)
                return;
            var field = input.GetType().GetField("movement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field?.SetValue(input, movement);
        }

        static void SetStarterMove(GameObject player, Vector2 movement)
        {
            var input = player.GetComponent("StarterAssetsInputs");
            if (input == null)
                return;
            var field = input.GetType().GetField("move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field?.SetValue(input, movement);
        }
    }
}
