using System;
using System.Collections;
using HM.Characters;
using HM.Gameplay;
using HM.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HM.Tests.PlayMode
{
    /// <summary>
    /// Scripted walk pace and door warps on the real converted farm. Both skip on a checkout without local converted data.
    /// </summary>
    public class FarmWalkWarpTests : InputTestFixture
    {
        Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            FarmOverlay.State.Season = 0; FarmOverlay.State.BuildingFlag = 0; FarmOverlay.State.HouseLevel = 0;
        }

        static IEnumerator LoadOverlay()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var op = SceneManager.LoadSceneAsync("SliceFarm", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            float t = 0f;
            while (!FarmOverlayBootstrap.GlbLoaded && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(FarmOverlayBootstrap.GlbLoaded, "overlay and GLB loaded");
            yield return new WaitForSeconds(0.4f);
        }

        static void Teleport(Transform p, float rawX, float rawZ)
        {
            var cc = p.GetComponent<CharacterController>();
            cc.enabled = false;
            var w = FarmCoords.ToWorld(rawX, 0f, rawZ);
            p.position = new Vector3(w.x, p.position.y, w.z);
            cc.enabled = true;
        }

        static IEnumerator WaitRow(FarmOverlayBootstrap ov, int row)
        {
            float t = 0f;
            while ((ov.CurrentRow != row || !FarmOverlayBootstrap.GlbLoaded) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(row, ov.CurrentRow, "warp reached row " + row);
            yield return new WaitForSeconds(1.0f); // clears the 0.75 s warp cooldown
        }

        [UnityTest]
        public IEnumerator HeldRightMovesPlusXAtTheDecompiledPace()
        {
            if (FarmOverlay.Find() == null) Assert.Ignore("converted farm not present locally");
            yield return LoadOverlay();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            FarmCoords.ToRaw(player.transform.position, out float x0, out float z0);
            Press(keyboard.dKey);
            float simTime = 0f; int frames = 0;
            while (simTime < 1.0f && frames < 100000) { yield return null; simTime += Time.deltaTime; frames++; }
            Release(keyboard.dKey);
            FarmCoords.ToRaw(player.transform.position, out float x1, out float z1);
            float travelled = x1 - x0;
            float original = FarmCoords.WalkUnitsPerSecond * simTime; // 41 raw units per 30 Hz update in the original
            // ADR 0007 keeps the pace at the decompiled metre value while the farm is 1.5x larger, so the raw pace is 1/WorldScale of the original.
            float expected = FarmCoords.DecompiledMetresPerSecond / FarmCoords.UnitsToMetres * simTime;
            Debug.Log("[HM-WALK] right: " + travelled.ToString("F1") + " raw units in " + simTime.ToString("F3") + " s (" + frames + " frames), expected " + expected.ToString("F1")
                + ", ratio " + (travelled / expected).ToString("F3") + ", z drift " + (z1 - z0).ToString("F1"));
            Assert.AreEqual(expected, travelled, expected * 0.06f, "walk pace matches the documented ADR 0007 pace (WorldScale-reduced fraction of 41 raw units per 30 Hz update)");
            Assert.AreEqual(0f, z1 - z0, 20f, "right does not drift in z (no diagonals)");
        }

        [UnityTest]
        public IEnumerator FarmhouseDoorEntersInteriorAndExitReturnsToTheYard()
        {
            if (FarmOverlay.Find() == null || FarmOverlay.Find(80) == null) Assert.Ignore("converted farm rows not present locally");
            yield return LoadOverlay();
            var ov = UnityEngine.Object.FindFirstObjectByType<FarmOverlayBootstrap>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            Assert.AreEqual(0, ov.CurrentRow);

            // Yard region 0: type 80, door into the farmhouse (rows 80+).
            Teleport(player.transform, -4776, 4034);
            yield return WaitRow(ov, FarmOverlay.HouseRow(0, 0));
            FarmCoords.ToRaw(player.transform.position, out float ix, out float iz);
            Assert.AreEqual(1377f, ix, 60f, "arrives at the door's arrival x in the interior");
            Assert.AreEqual(-842f, iz, 60f, "arrives at the door's arrival z in the interior");

            // Interior region 0 is the exit; its arrival is the yard spot in front of the door.
            Teleport(player.transform, 1685, -780);
            yield return WaitRow(ov, 0);
            FarmCoords.ToRaw(player.transform.position, out float ox, out float oz);
            Assert.AreEqual(-4464f, ox, 60f);
            Assert.AreEqual(4032f, oz, 60f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, ov.CurrentRow, "standing at the arrival does not bounce back through the door");
        }
    }
}
