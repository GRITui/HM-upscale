using System.Collections;
using HM.Dialogue;
using HM.Events;
using HM.Gameplay;
using HM.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HM.Tests.PlayMode
{
    /// <summary>Event VM on the farm: region trigger to script, dialogue by id, stubs. Synthetic scripts and text only.</summary>
    public class FarmEventTests
    {
        GameObject player, trigger, dirGo;
        FarmEventDirector dir;

        sealed class Cat : IMessageCatalog
        {
            public DialogueData Find(MessageId id) => id.Entry == 1 ? DialogueData.FromJson(
                "{\"schema\":1,\"start\":\"a\",\"nodes\":[{\"id\":\"a\",\"speaker\":\"s\",\"text\":\"t\",\"next\":\"\"," +
                "\"choices\":[{\"text\":\"c0\",\"next\":\"\"},{\"text\":\"c1\",\"next\":\"\"}]}]}") : null;
        }

        static ScriptCommand C(int op, int p1 = 0, int p2 = 0, int p3 = 0, int p4 = 0)
            => new ScriptCommand { op = op, done = 255, wait = 255, p = new[] { p1, p2, p3, p4 } };

        // Wait idiom from the real scripts: a waiting op loops on itself (wait 0 = this command) and yields.
        static ScriptCommand Dialog(int channel, int entry, int resultVar)
            => new ScriptCommand { op = 2, done = 255, wait = 0, @yield = 1, p = new[] { channel, entry, resultVar, 0 } };

        [TearDown]
        public void Cleanup()
        {
            FarmEventDirector.Catalog = null;
            foreach (var g in new[] { player, trigger, dirGo }) if (g != null) Object.Destroy(g);
        }

        void Build(int eventId, params ScriptCommand[] cmds)
        {
            player = new GameObject("TestPlayer");
            var cc = player.AddComponent<CharacterController>();
            player.transform.position = new Vector3(0, 1f, 0);
            dirGo = new GameObject("TestDirector");
            dir = dirGo.AddComponent<FarmEventDirector>();
            dir.Init(player.transform, 999);
            var file = EventFile.Single(cmds);
            dir.Runner.EventMapLoader = id => id == eventId ? file : null;
            trigger = new GameObject("TestTrigger");
            trigger.transform.position = new Vector3(0, 1f, 0);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(4, 4, 4);
            var t = trigger.AddComponent<MapRegionTrigger>();
            t.eventId = eventId;
        }

        IEnumerator Frames(int n)
        {
            var cc = player.GetComponent<CharacterController>();
            for (int i = 0; i < n; i++)
            {
                // A CharacterController only raises trigger events when it moves.
                if (cc != null && cc.enabled && trigger != null && player.transform.position.x == 0f) cc.Move(Vector3.zero + new Vector3(0, 0.001f, 0));
                yield return new WaitForFixedUpdate();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TriggerRunsScriptAndMovesPlayer()
        {
            // set_player_pos(mode 0, y 0, x 500, z 300), then set event flag 1.
            Build(7, C(61, 0, 0, 500, 300), C(19, 0x31, 0, 1, 0));
            yield return Frames(6);
            Assert.IsFalse(dir.Runner.Busy, "script finished");
            Assert.AreEqual(1, dir.Runner.State.GetFlag(1));
            Assert.AreEqual(-5f, player.transform.position.x, 0.02f, "x mirrored, 0.01 m per unit");
            Assert.AreEqual(3f, player.transform.position.z, 0.02f);
            Assert.IsFalse(dir.InputLocked);
        }

        [UnityTest]
        public IEnumerator DialogueGoesThroughBridgeAndLocksInput()
        {
            FarmEventDirector.Catalog = new Cat();
            Build(7, Dialog(0, 1, 3), C(19, 0x31, 0, 1, 0));
            yield return Frames(4);
            Assert.IsTrue(dir.Bridge.Active, "dialogue open");
            Assert.IsTrue(dir.InputLocked, "player locked while dialogue is open");
            Assert.AreEqual(0, dir.Runner.State.GetFlag(1), "script waits on the dialogue");
            dir.Bridge.Choose(1);
            yield return Frames(3);
            Assert.AreEqual(1, dir.Runner.State.GetFlag(1), "script resumed");
            Assert.AreEqual(1, dir.Runner.State.GetVar(3), "choice index lands in the result variable");
            Assert.IsFalse(dir.InputLocked);
        }

        [UnityTest]
        public IEnumerator MissingTextCompletesAndIsRecorded()
        {
            FarmEventDirector.Catalog = null;
            Build(7, Dialog(1, 42, 3));
            yield return Frames(4);
            Assert.IsFalse(dir.Runner.Busy);
            Assert.AreEqual(1, dir.Bridge.Missing.Count);
            Assert.AreEqual(42, dir.Bridge.Missing[0].Entry);
        }

        [UnityTest]
        public IEnumerator EngineOpsAreLoggedStubsNotStalls()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[HM-EVENT\] no event data"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[HM-EVENT\] start region event 7"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[HM-EVENT\] stub: engine op 8"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[HM-EVENT\] stub: camera_move"));
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[HM-EVENT\] finished region event 7"));
            Build(7, C(8, -2), C(9, 0, 0, 724, 1704), C(19, 0x31, 0, 1, 0));
            yield return Frames(6);
            Assert.IsFalse(dir.Runner.Busy);
            Assert.AreEqual(1, dir.Runner.State.GetFlag(1), "script continued past the stubs");
            CollectionAssert.Contains(dir.Host.StubLog, "engine op 8 (-2,0,0,0)");
            CollectionAssert.Contains(dir.Host.StubLog, "camera_move");
        }

        [Test]
        public void MapNumberParsesIds()
        {
            Assert.AreEqual(80, FarmEventDirector.MapNumber("map080"));
            Assert.AreEqual(-1, FarmEventDirector.MapNumber("farm"));
            Assert.AreEqual(-1, FarmEventDirector.MapNumber(null));
        }
    }
}
