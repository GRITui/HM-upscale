using System.Collections.Generic;
using HM.Dialogue;
using NUnit.Framework;

namespace HM.Events.Tests
{
    sealed class RecHost : EventHostBase
    {
        public readonly List<string> Calls = new List<string>();
        public readonly Dictionary<int, int> Fields = new Dictionary<int, int>();
        public int DialogPolls = 3;   // dialog closes after this many polls
        int polls;
        public int Result = 7;
        public long Minutes;
        public int Rand = 5;
        public int MovePolls = 2;
        int moveCount;

        public override int GetField(int kind, int index) => Fields.TryGetValue(kind, out var v) ? v : 0;
        public override void SetField(int kind, int index, int value) { Fields[kind] = value; Calls.Add("setfield " + kind + "=" + value); }
        public override long TotalMinutes() => Minutes;
        public override void DialogBegin(int channel, int entry) { polls = 0; Calls.Add("dialog " + channel + ":" + entry); }
        public override bool DialogPoll(out int result) { result = Result; return ++polls >= DialogPolls; }
        public override bool MoveActor(int actor, int dir, int target, int speed, int anim, bool begin)
        { if (begin) moveCount = 0; Calls.Add("move " + actor); return ++moveCount >= MovePolls; }
        public override void SetActorPos(int actor, int y, int x, int z) => Calls.Add("pos " + actor + " " + y + "," + x + "," + z);
        public override void GiveItem(int item, bool notify) => Calls.Add("give " + item + (notify ? "n" : ""));
        public override int Random(int range) => Rand;
        public override void ScriptEnded(int variant, int flag, int cleanup) => Calls.Add("end " + variant);
        public override void SetPlayerPos(int mode, int y, int x, int z) => Calls.Add("player " + mode + " " + y + "," + x + "," + z);
    }

    public class ScriptVMTests
    {
        static ScriptCommand C(int op, int p1 = 0, int p2 = 0, int p3 = 0, int p4 = 0, int done = 255, int wait = 255, int yld = 0)
            => new ScriptCommand { op = op, done = done, wait = wait, @yield = yld, p = new[] { p1, p2, p3, p4 } };

        static ScriptVM Run(RecHost h, EventState s, params ScriptCommand[] cmds)
        {
            var vm = new ScriptVM(EventFile.Single(cmds).scripts[0], s, h);
            for (int i = 0; i < 100 && !vm.Finished; i++) vm.Tick();
            return vm;
        }

        [Test]
        public void VarSetAndArithModes()
        {
            var s = new EventState();
            Run(new RecHost(), s,
                C(21, 0, 0, 10),          // v0 = 10
                C(21, 0, 0, 5, 1),        // += 5
                C(21, 0, 0, 3, 3),        // *= 3
                C(21, 0, 0, 7, 2),        // -= 7
                C(21, 1, 0, 4, 0),        // v1 = 4
                C(21, 0, 1, 1, 5),        // v0 %= v[1] via is_var
                C(37));
            Assert.AreEqual(38 % 4, s.Vars[0]);
            Assert.AreEqual(4, s.Vars[1]);
        }

        [TestCase(0, 5, 5, true)]
        [TestCase(0, 4, 5, false)]
        [TestCase(1, 5, 5, false)]
        [TestCase(2, 5, 5, true)]
        [TestCase(3, 5, 5, false)]
        [TestCase(4, 5, 5, true)]
        [TestCase(5, 1, 99, true)]
        public void CompareModes(int mode, int lhs, int rhs, bool expected)
        {
            Assert.AreEqual(expected, ScriptVM.Compare(lhs, mode, rhs));
        }

        [Test]
        public void ConditionBranchesOnDoneAndWait()
        {
            var s = new EventState();
            s.Vars[0] = 3;
            // 0: v0 >= 5 ? goto 1 : goto 2
            Run(new RecHost(), s,
                C(15, 0, 0, 0, 5, done: 1, wait: 2),
                C(21, 5, 0, 111, done: 3),   // taken if true
                C(21, 5, 0, 222),            // taken if false
                C(37));
            Assert.AreEqual(222, s.Vars[5]);
            s = new EventState(); s.Vars[0] = 9;
            Run(new RecHost(), s,
                C(15, 0, 0, 0, 5, done: 1, wait: 2),
                C(21, 5, 0, 111, done: 3),
                C(21, 5, 0, 222),
                C(37));
            Assert.AreEqual(111, s.Vars[5]);
        }

        [Test]
        public void EventFlagsRoundTripAndFlagFieldKinds()
        {
            var s = new EventState();
            Run(new RecHost(), s,
                C(66, 0, 1, 0x20),             // set flag 0x20 = 1
                C(13, 0x50, 0, 0, 1, done: 3, wait: 2),   // kind 0x50 = flag 0x20; == mode 0 (>=) 1
                C(21, 0, 0, 99, done: 4),                  // false branch
                C(21, 0, 0, 42, done: 4),                  // true branch
                C(19, 0x31, 0, 8, 0),                      // flag 1 = 8
                C(37));
            Assert.AreEqual(42, s.Vars[0]);
            Assert.AreEqual(1, s.Flags[0x20]);
            Assert.AreEqual(8, s.Flags[1]);
        }

        [Test]
        public void ClockGateReadsHostFields()
        {
            var h = new RecHost();
            h.Fields[10] = 14;    // hour
            var s = new EventState();
            Run(h, s,
                C(17, 10, 0, 12, 18, done: 1, wait: 2),   // 12 <= hour <= 18
                C(21, 0, 0, 1, done: 3),
                C(21, 0, 0, 2),
                C(37));
            Assert.AreEqual(1, s.Vars[0]);
            h.Fields[10] = 20;
            s = new EventState();
            Run(h, s, C(17, 10, 0, 12, 18, done: 1, wait: 2), C(21, 0, 0, 1, done: 3), C(21, 0, 0, 2), C(37));
            Assert.AreEqual(2, s.Vars[0]);
        }

        [Test]
        public void WaitIdiomHoldsUntilHostFinishes()
        {
            var h = new RecHost { MovePolls = 3 };
            var s = new EventState();
            var vm = new ScriptVM(EventFile.Single(
                C(0, 1, 0, 100, 4, wait: 0, yld: 1),    // move; wait on self
                C(21, 0, 0, 1),
                C(37)).scripts[0], s, h);
            vm.Tick(); Assert.AreEqual(0, vm.Pc); Assert.AreEqual(0, s.Vars[0]);
            vm.Tick(); Assert.AreEqual(0, vm.Pc);
            vm.Tick(); Assert.AreEqual(1, vm.Pc); Assert.IsFalse(vm.Finished);   // move done, yield ends frame
            vm.Tick(); Assert.IsTrue(vm.Finished); Assert.AreEqual(1, s.Vars[0]);
            Assert.AreEqual(3, h.Calls.FindAll(x => x == "move 1").Count);
        }

        [Test]
        public void YieldEndsFrame()
        {
            var s = new EventState();
            var vm = new ScriptVM(EventFile.Single(C(21, 0, 0, 1, yld: 1), C(21, 1, 0, 2), C(37)).scripts[0], s, new RecHost());
            vm.Tick();
            Assert.AreEqual(1, s.Vars[0]); Assert.AreEqual(0, s.Vars[1]); Assert.AreEqual(1, vm.Pc);
            vm.Tick();
            Assert.AreEqual(2, s.Vars[1]); Assert.IsTrue(vm.Finished);
        }

        [Test]
        public void FrameTimerWaitsGivenFrames()
        {
            var s = new EventState();
            var vm = new ScriptVM(EventFile.Single(C(3, 3, wait: 0, yld: 1), C(21, 0, 0, 1), C(37)).scripts[0], s, new RecHost());
            int ticks = 0;
            while (!vm.Finished && ticks < 20) { vm.Tick(); ticks++; }
            Assert.AreEqual(5, ticks);   // 3 counting frames, 1 to see zero, 1 for the remainder
            Assert.AreEqual(1, s.Vars[0]);
        }

        [Test]
        public void DialogWaitsAndStoresResult()
        {
            var h = new RecHost { DialogPolls = 3, Result = 7 };
            var s = new EventState();
            var vm = new ScriptVM(EventFile.Single(C(2, 1, 305, 4, 0, wait: 0, yld: 1), C(37)).scripts[0], s, h);
            vm.Tick(); vm.Tick(); Assert.IsFalse(vm.Finished); Assert.AreEqual(0, s.Vars[4]);
            vm.Tick();
            Assert.AreEqual(7, s.Vars[4]);
            vm.Tick();
            Assert.IsTrue(vm.Finished);
            Assert.AreEqual(7, s.Vars[4]);
            Assert.AreEqual(1, h.Calls.FindAll(x => x == "dialog 1:305").Count);
        }

        [Test]
        public void DialogEntryFromVariable()
        {
            var h = new RecHost { DialogPolls = 1 };
            var s = new EventState(); s.Vars[2] = 777;
            Run(h, s, C(2, 0, 2, -1, 1), C(37));
            Assert.Contains("dialog 0:777", h.Calls);
        }

        [Test]
        public void UnknownAndLowOpsAreLoggedNotFatal()
        {
            var s = new EventState();
            var h = new RecHost();
            var vm = Run(h, s, C(18), C(200), C(45), C(21, 0, 0, 9), C(37));
            Assert.AreEqual(9, s.Vars[0]);
            Assert.AreEqual(1, s.Unhandled[18]);
            Assert.AreEqual(1, s.Unhandled[200]);
            Assert.AreEqual(1, s.Unhandled[45]);
            Assert.GreaterOrEqual(vm.Log.Count, 3);
        }

        [Test]
        public void RandomStoresAndCompares()
        {
            var h = new RecHost { Rand = 6 };
            var s = new EventState();
            Run(h, s, C(38, 10, 3, 0, 4, done: 1, wait: 2), C(21, 1, 0, 1, done: 3), C(21, 1, 0, 2), C(37));
            Assert.AreEqual(6, s.Vars[3]);
            Assert.AreEqual(1, s.Vars[1]);   // 6 >= 4
        }

        [Test]
        public void SetPlayerPosUsesVariablesInMode1()
        {
            var h = new RecHost();
            var s = new EventState(); s.Vars[0] = 1; s.Vars[1] = 2; s.Vars[2] = 3;
            Run(h, s, C(61, 1, 0, 1, 2), C(37));
            Assert.Contains("player 1 1,2,3", h.Calls);
        }

        [Test]
        public void ScriptEndStopsAndNotifiesHost()
        {
            var h = new RecHost();
            var s = new EventState();
            Run(h, s, C(37, 2), C(21, 0, 0, 5));
            Assert.AreEqual(0, s.Vars[0]);
            Assert.Contains("end 2", h.Calls);
        }

        [Test]
        public void EndOpByteStopsScript()
        {
            var s = new EventState();
            Run(new RecHost(), s, C(21, 0, 0, 1), C(0xFF), C(21, 0, 0, 2));
            Assert.AreEqual(1, s.Vars[0]);
        }

        [Test]
        public void InfiniteLoopIsBoundedPerTick()
        {
            var s = new EventState();
            var vm = new ScriptVM(EventFile.Single(C(21, 0, 0, 1, 1, done: 0)).scripts[0], s, new RecHost());
            vm.Tick();
            Assert.IsTrue(vm.Truncated);
            Assert.IsFalse(vm.Finished);
        }

        [Test]
        public void WaitUntilTimeRelativeUsesTotalMinutes()
        {
            var h = new RecHost { Minutes = 100 };
            var s = new EventState();
            var vm = new ScriptVM(EventFile.Single(C(76, 1, 30, 0, 0, wait: 0, yld: 1), C(37)).scripts[0], s, h);
            vm.Tick(); h.Minutes = 189; vm.Tick(); Assert.IsFalse(vm.Finished);
            h.Minutes = 190; vm.Tick(); vm.Tick(); Assert.IsTrue(vm.Finished);
        }

        [Test]
        public void SameScriptSameHostIsDeterministic()
        {
            string Trace()
            {
                var h = new RecHost(); var s = new EventState();
                Run(h, s, C(38, 10, 0, 0, 0), C(21, 1, 0, 3, 1), C(4, 5, 1, 2, 3), C(32, 12), C(37));
                return string.Join("|", h.Calls) + string.Join(",", s.Vars);
            }
            Assert.AreEqual(Trace(), Trace());
        }
    }

    public class EventRunnerTests
    {
        static ScriptCommand C(int op, int p1 = 0, int p2 = 0, int p3 = 0, int p4 = 0)
            => new ScriptCommand { op = op, p = new[] { p1, p2, p3, p4 } };

        static EventFile TwoScripts()
        {
            var f = new EventFile { schema = 1 };
            f.scripts = new[]
            {
                new EventScript { block = 4, commands = new[] { C(21, 0, 0, 11), C(37) } },
                new EventScript { block = 9, commands = new[] { C(21, 0, 0, 22), C(37) } },
            };
            return f;
        }

        [Test]
        public void RegionTriggerRunsScriptByBlockNumber()
        {
            var r = new EventRunner(new RecHost());
            r.SetMap(TwoScripts());
            Assert.IsTrue(r.OnRegionEntered(9));
            r.Tick();
            Assert.AreEqual(22, r.State.Vars[0]);
            Assert.IsFalse(r.Busy);
        }

        [Test]
        public void MissingDataOrUnknownEventDoesNothing()
        {
            var r = new EventRunner(new RecHost());
            Assert.IsFalse(r.OnRegionEntered(4));   // no map set
            r.SetMap(TwoScripts());
            Assert.IsFalse(r.OnRegionEntered(50));
            Assert.IsFalse(r.OnRegionEntered(-1));
        }

        [Test]
        public void CanFireGatesTrigger()
        {
            var r = new EventRunner(new RecHost()) { CanFire = id => id != 4 };
            r.SetMap(TwoScripts());
            Assert.IsFalse(r.OnRegionEntered(4));
            Assert.IsTrue(r.OnRegionEntered(9));
        }

        [Test]
        public void BusyRunnerIgnoresOrQueuesTriggers()
        {
            var h = new RecHost { DialogPolls = 2 };
            var f = new EventFile { schema = 1, scripts = new[]
            {
                new EventScript { block = 1, commands = new[] { new ScriptCommand { op = 2, wait = 0, @yield = 1, p = new[] { 0, 5, -1, 0 } } } },
                new EventScript { block = 2, commands = new[] { C(21, 0, 0, 5) } },
            } };
            var r = new EventRunner(h) { QueueWhileBusy = true };
            r.SetMap(f);
            r.OnRegionEntered(1);
            r.Tick();
            Assert.IsTrue(r.Busy);
            Assert.IsTrue(r.OnRegionEntered(2));
            for (int i = 0; i < 8; i++) r.Tick();
            Assert.AreEqual(5, r.State.Vars[0]);
        }
    }

    public class DialogueBridgeTests
    {
        sealed class Cat : IMessageCatalog
        {
            public DialogueData Find(MessageId id) => id.Entry == 1 ? DialogueData.FromJson(
                "{\"schema\":1,\"start\":\"a\",\"nodes\":[{\"id\":\"a\",\"speaker\":\"x\",\"text\":\"t\",\"next\":\"\"," +
                "\"choices\":[{\"text\":\"c0\",\"next\":\"\"},{\"text\":\"c1\",\"next\":\"\"}]}]}") : null;
        }

        sealed class BridgeHost : EventHostBase
        {
            public DialogueBridge Bridge;
            public override void DialogBegin(int channel, int entry) => Bridge.Begin(channel, entry);
            public override bool DialogPoll(out int result) => Bridge.Poll(out result);
        }

        [Test]
        public void ChoiceIndexFlowsIntoScriptVariable()
        {
            var host = new BridgeHost { Bridge = new DialogueBridge(new Cat()) };
            var r = new EventRunner(host);
            r.SetMap(new EventFile { schema = 1, scripts = new[] { new EventScript { block = 0, commands = new[] {
                new ScriptCommand { op = 2, wait = 0, @yield = 1, p = new[] { 0, 1, 6, 0 } } } } } });
            r.OnRegionEntered(0);
            r.Tick();
            Assert.IsTrue(host.Bridge.Active);
            host.Bridge.Choose(1);
            r.Tick(); r.Tick();
            Assert.AreEqual(1, r.State.Vars[6]);
            Assert.IsFalse(r.Busy);
        }

        [Test]
        public void MissingMessageCompletesAndIsRecorded()
        {
            var host = new BridgeHost { Bridge = new DialogueBridge(new Cat()) };
            var r = new EventRunner(host);
            r.SetMap(new EventFile { schema = 1, scripts = new[] { new EventScript { block = 0, commands = new[] {
                new ScriptCommand { op = 2, wait = 0, @yield = 1, p = new[] { 3, 999, -1, 0 } } } } } });
            r.OnRegionEntered(0);
            r.Tick(); r.Tick();
            Assert.IsFalse(r.Busy);
            Assert.AreEqual(1, host.Bridge.Missing.Count);
            Assert.AreEqual("msg:3:999", host.Bridge.Missing[0].ToString());
        }
    }

    public class EventLoaderTests
    {
        [Test]
        public void AbsentDirectoryFallsBackCleanly()
        {
            string old = EventLoader.OverrideDir;
            EventLoader.OverrideDir = "/nonexistent/hm-events-dir";
            try
            {
                Assert.IsFalse(EventLoader.Available);
                Assert.IsFalse(EventLoader.TryLoadMap(1, out var f));
                Assert.IsNull(f);
            }
            finally { EventLoader.OverrideDir = old; }
        }

        [Test]
        public void ParsesSchema1AndRejectsGarbage()
        {
            var f = EventLoader.Parse("{\"schema\":1,\"scripts\":[{\"block\":4,\"commands\":[{\"i\":0,\"op\":21,\"done\":255,\"wait\":255,\"yield\":1,\"p\":[1,0,5,0],\"name\":\"var_set\"}],\"time_gates\":[]}]}");
            Assert.NotNull(f);
            Assert.AreEqual(4, f.scripts[0].block);
            Assert.IsTrue(f.scripts[0].commands[0].Yields);
            Assert.AreEqual(5, f.scripts[0].commands[0].P(2));
            Assert.IsNull(EventLoader.Parse("not json"));
            Assert.IsNull(EventLoader.Parse("{\"schema\":2}"));
            Assert.IsNull(EventLoader.Parse(""));
        }

        [Test]
        public void RealConvertedScriptsRunWithoutThrowingWhenPresent()
        {
            if (!EventLoader.Available) Assert.Ignore("converted events not present locally");
            int ran = 0;
            for (int map = 0; map < 450; map++)
            {
                if (!EventLoader.TryLoadMap(map, out var file)) continue;
                foreach (var s in file.scripts)
                {
                    var vm = new ScriptVM(s, new EventState(), new EventHostBase());
                    for (int t = 0; t < 50 && !vm.Finished; t++) vm.Tick();
                    ran++;
                }
            }
            Assert.Greater(ran, 0);
        }
    }
}
