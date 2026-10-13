using System.Collections.Generic;
using NUnit.Framework;

namespace HM.Events.Tests
{
    sealed class GameHost : EventHostBase
    {
        public readonly Dictionary<int, int[]> Pos = new Dictionary<int, int[]>();
        public readonly Dictionary<int, int> Values = new Dictionary<int, int>();
        public readonly List<string> Calls = new List<string>();
        public int Pref = 3;
        public int Dir = 1;

        public override bool GetActorPos(int actor, out int y, out int x, out int z)
        {
            y = x = z = 0;
            if (!Pos.TryGetValue(actor, out var p)) return false;
            y = p[0]; x = p[1]; z = p[2];
            return true;
        }
        public override void OffsetActorPos(int actor, int dy, int dx, int dz)
        {
            var p = Pos[actor]; p[0] += dy; p[1] += dx; p[2] += dz;
        }
        public override int DirectionBetween(int a, int b) => Dir;
        public override int ItemPreference(int item, int npc) => Pref;
        public override int EngineValue(int id) => Values.TryGetValue(id, out var v) ? v : 0;
        public override bool Engine(int op, int p1, int p2, int p3, int p4, bool begin, EventState state)
        { Calls.Add("engine " + op + " " + p1); return true; }
    }

    public class ScriptVMGameOpsTests
    {
        static ScriptCommand C(int op, int p1 = 0, int p2 = 0, int p3 = 0, int p4 = 0, int done = 255, int wait = 255)
            => new ScriptCommand { op = op, done = done, wait = wait, p = new[] { p1, p2, p3, p4 } };

        static ScriptVM Run(GameHost h, EventState s, params ScriptCommand[] cmds)
        {
            var vm = new ScriptVM(EventFile.Single(cmds).scripts[0], s, h);
            for (int i = 0; i < 100 && !vm.Finished; i++) vm.Tick();
            return vm;
        }

        [Test]
        public void ActorPosToVarsSkipsMinusOne()
        {
            var h = new GameHost();
            h.Pos[5] = new[] { 10, 20, 30 };
            var s = new EventState();
            s.Vars[2] = 99;
            Run(h, s, C(73, 5, 0, -1, 1), C(37));
            Assert.AreEqual(10, s.Vars[0]);
            Assert.AreEqual(99, s.Vars[2]);
            Assert.AreEqual(30, s.Vars[1]);
        }

        [Test]
        public void ActorPosToVarsRejectsBadActor()
        {
            var s = new EventState();
            Run(new GameHost(), s, C(73, 0x30, 0, 1, 2, done: 1, wait: 2), C(21, 0, 0, 1, done: 3), C(21, 0, 0, 2, done: 3), C(37));
            Assert.AreEqual(2, s.Vars[0]);   // bad actor id returns 0 -> wait branch
        }

        [Test]
        public void ActorMoveRelDoublesOffsets()
        {
            var h = new GameHost();
            h.Pos[3] = new[] { 0, 0, 0 };
            Run(h, new EventState(), C(104, 3, 1, 2, 3), C(37));
            CollectionAssert.AreEqual(new[] { 2, 4, 6 }, h.Pos[3]);
        }

        [Test]
        public void SaveBitsSetTestClear()
        {
            var s = new EventState();
            // set 0x30, then test 0x10 (true -> v0=1 via done=3), else v0=2
            Run(new GameHost(), s,
                C(69, 0, 1, 0x30, 0),
                C(69, 1, 0, 0x10, 0, done: 2, wait: 3),
                C(21, 0, 0, 1, done: 4),
                C(21, 0, 0, 2, done: 4),
                C(69, 0, 0, 0x10, 0),
                C(37));
            Assert.AreEqual(1, s.Vars[0]);
            Assert.AreEqual(0x20, s.Bank[EventState.BankMode]);
        }

        [Test]
        public void SaveBitsWideUsesPlayerBank()
        {
            var s = new EventState();
            Run(new GameHost(), s, C(69, 0, 1, 0x1FFFF, 1), C(37));
            Assert.AreEqual(0xFFFF, s.Bank[EventState.BankPlayer]);
            Assert.AreEqual(0, s.Bank[EventState.BankMode]);
        }

        [Test]
        public void RegisterPairFillsFiveSlotsThenFails()
        {
            var s = new EventState();
            Run(new GameHost(), s,
                C(100, 1, 11), C(100, 2, 12), C(100, 3, 13), C(100, 4, 14), C(100, 5, 15),
                C(100, 1, 21),                 // same key overwrites
                C(100, 6, 16, 0, 0, done: 7, wait: 8),   // table full -> wait branch
                C(21, 0, 0, 1, done: 9),
                C(21, 0, 0, 2, done: 9),
                C(37));
            Assert.AreEqual(21, s.Pairs[0, 1]);
            Assert.AreEqual(15, s.Pairs[4, 1]);
            Assert.AreEqual(2, s.Vars[0]);
        }

        [Test]
        public void FlagTripletSetsThreeFlags()
        {
            var s = new EventState();
            Run(new GameHost(), s, C(84, 0), C(37));
            Assert.AreEqual(0, s.GetFlag(4));
            Assert.AreEqual(1, s.GetFlag(5));
            Assert.AreEqual(1, s.GetFlag(0xB0));
            Run(new GameHost(), s, C(84, 1), C(37));
            Assert.AreEqual(1, s.GetFlag(4));
            Assert.AreEqual(0, s.GetFlag(5));
        }

        [Test]
        public void SlotOffsetUsesDirection()
        {
            var h = new GameHost();
            h.Pos[7] = new[] { 500, 1000, 2000 };
            var s = new EventState();
            Run(h, s, C(54, 0, 7, 3, 0), C(37));
            Assert.AreEqual(400, s.Slots[0, 0]);
            Assert.AreEqual(1100, s.Slots[0, 1]);
            Assert.AreEqual(2000, s.Slots[0, 2]);
        }

        [Test]
        public void ReturnAddressAndJump()
        {
            var s = new EventState();
            // 0: v0 = return address (1), skip to 3; 3: v3 = 5; 4: return to v0; 1: v2 = 1, goto 5; 5: end
            Run(new GameHost(), s,
                C(79, 0, done: 3),
                C(21, 2, 0, 1, done: 5),
                C(37),
                C(21, 3, 0, 5),
                C(80, 0),
                C(37));
            Assert.AreEqual(5, s.Vars[3]);
            Assert.AreEqual(1, s.Vars[2]);
            Assert.AreEqual(1, s.Vars[0]);
        }

        [Test]
        public void ReturnJumpRedirectsOwnDone()
        {
            var s = new EventState();
            s.Vars[0] = 3;
            Run(new GameHost(), s,
                C(80, 0),                 // goes to command 3
                C(21, 1, 0, 111),
                C(37),
                C(21, 2, 0, 222),
                C(37));
            Assert.AreEqual(0, s.Vars[1]);
            Assert.AreEqual(222, s.Vars[2]);
        }

        [Test]
        public void GameModeCheckUsesEngineValue()
        {
            var h = new GameHost();
            h.Values[EngineId.GameMode] = 3;
            var s = new EventState();
            Run(h, s, C(56, 0, 0, 0, 0, done: 1, wait: 2), C(21, 0, 0, 1, done: 3), C(21, 0, 0, 2, done: 3), C(37));
            Assert.AreEqual(1, s.Vars[0]);
        }

        [Test]
        public void NpcPreferenceStoresPlusOne()
        {
            var h = new GameHost();
            h.Values[EngineId.HeldItem] = 12;
            h.Pref = 3;
            var s = new EventState();
            Run(h, s, C(57, 4, 2, 0), C(37));
            Assert.AreEqual(4, s.Vars[2]);
        }

        [Test]
        public void EngineOpsDelegateToHost()
        {
            var h = new GameHost();
            Run(h, new EventState(), C(62, 1), C(63, 2), C(83, 3), C(37));
            CollectionAssert.AreEqual(new[] { "engine 62 1", "engine 63 2", "engine 83 3" }, h.Calls);
        }

        [Test]
        public void RegionIdLoadsMapScriptViaLoader()
        {
            var target = EventFile.Single(C(21, 0, 0, 77), C(37));
            var r = new EventRunner(new GameHost()) { EventMapLoader = id => id == 9 ? target : null };
            r.SetMap(EventFile.Single(C(37)));
            Assert.IsTrue(r.OnRegionEntered(9));
            r.Tick();
            Assert.AreEqual(77, r.State.Vars[0]);
        }
    }
}
