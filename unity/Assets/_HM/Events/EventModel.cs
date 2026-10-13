using System;

namespace HM.Events
{
    /// <summary>One 16-byte map-script command (docs/formats/event.md). Field names match the converted JSON.</summary>
    [Serializable]
    public sealed class ScriptCommand
    {
        public int i;
        public int op;
        public int done = 255;   // jump target when the handler returns 1 (0xff = next)
        public int wait = 255;   // jump target when the handler returns 0 (0xff = next)
        public int @yield;       // bit 0 ends the frame after this command
        public int[] p = new int[4];

        public int P(int n) => p != null && n < p.Length ? p[n] : 0;
        public bool Yields => (@yield & 1) != 0;
    }

    /// <summary>A script block: an ordered command array.</summary>
    [Serializable]
    public sealed class EventScript
    {
        public int block;
        public ScriptCommand[] commands = new ScriptCommand[0];
    }

    /// <summary>Converted event file for one map (schema 1).</summary>
    [Serializable]
    public sealed class EventFile
    {
        public int schema;
        public EventScript[] scripts = new EventScript[0];

        /// <summary>Test and tool helper: build a one-script file from commands.</summary>
        public static EventFile Single(params ScriptCommand[] cmds)
        {
            for (int k = 0; k < cmds.Length; k++) cmds[k].i = k;
            return new EventFile { schema = 1, scripts = new[] { new EventScript { block = 0, commands = cmds } } };
        }
    }
}
