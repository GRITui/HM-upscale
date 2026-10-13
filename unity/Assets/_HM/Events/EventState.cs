using System.Collections.Generic;

namespace HM.Events
{
    /// <summary>
    /// State shared by every script run: 20 script variables, event flags and scratch slots.
    /// Mirrors the original globals (int[20] variables; flags via get/set helpers 0x7aa8/0x7af4).
    /// </summary>
    public sealed class EventState
    {
        public const int VarCount = 20;
        public const int FlagCount = 0x400;
        public const int SlotCount = 64;

        public readonly int[] Vars = new int[VarCount];
        public readonly int[] Flags = new int[FlagCount];
        public readonly int[,] Slots = new int[SlotCount, 3];     // y, x, z
        public readonly bool[] SlotSet = new bool[SlotCount];

        /// <summary>Opcodes that ran without a real implementation, with hit counts.</summary>
        public readonly Dictionary<int, int> Unhandled = new Dictionary<int, int>();

        public int GetVar(int i) => i >= 0 && i < VarCount ? Vars[i] : 0;
        public void SetVar(int i, int v) { if (i >= 0 && i < VarCount) Vars[i] = v; }
        public int GetFlag(int id) => id >= 0 && id < FlagCount ? Flags[id] : 0;
        public void SetFlag(int id, int v) { if (id >= 0 && id < FlagCount) Flags[id] = v; }

        public void CountUnhandled(int op)
        {
            Unhandled.TryGetValue(op, out int n);
            Unhandled[op] = n + 1;
        }
    }
}
