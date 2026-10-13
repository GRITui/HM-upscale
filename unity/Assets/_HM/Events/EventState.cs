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

        /// <summary>
        /// Engine bit-field words that scripts read and modify (ops 10, 11, 69, 81, 99). Index with the
        /// <c>Bank*</c> constants; the comments give the original globals.
        /// </summary>
        public const int BankMode = 0;        // 0x1aef1c, 32-bit mode word (bit 0x800000 = scripted, 2 = camera busy, 8 = mode wait)
        public const int BankPlayer = 1;      // 0x1aeee6, 16-bit player word
        public const int BankScratch = 2;     // 0x1ae76e, op 81 case 6 bits
        public const int BankEngine = 3;      // 0x1ae1e0
        public const int BankEngine2 = 4;     // 0x1aeea8
        public const int BankUi = 5;          // 0x1ae138
        public const int BankToken = 6;       // 0x1b36c0, op 81 case 0x12
        public const int BankCounter = 7;     // 0x1aef22, op 81 case 0x16 (max 15)
        public const int BankCount = 8;
        public readonly int[] Bank = new int[BankCount];

        /// <summary>Pending sleep-trigger id (0x1aeed2, short); -1 = none. Set by the world, consumed by op 11.</summary>
        public int Pending = -1;

        /// <summary>Op 100 registry: five (key, value) pairs, empty key = -1.</summary>
        public const int PairCount = 5;
        public readonly int[,] Pairs = NewPairs();
        static int[,] NewPairs()
        {
            var p = new int[PairCount, 2];
            for (int i = 0; i < PairCount; i++) p[i, 0] = -1;
            return p;
        }

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
