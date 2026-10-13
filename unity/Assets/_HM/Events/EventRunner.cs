using System;
using System.Collections.Generic;

namespace HM.Events
{
    /// <summary>Picks the script to run for a region event id (a map id; see EventRunner.DefaultResolver).</summary>
    public delegate EventScript ScriptResolver(EventFile file, int eventId, out int scriptIndex);

    /// <summary>
    /// Owns the active script, ticks it, and turns region triggers (scene-header event ids) into runs.
    /// One script runs at a time (as in the original); triggers that arrive meanwhile are ignored unless
    /// <see cref="QueueWhileBusy"/> is set. Wire <see cref="OnRegionEntered"/> to the world's region
    /// enter callback and call <see cref="Tick"/> once per frame.
    /// </summary>
    public sealed class EventRunner
    {
        readonly IEventHost host;
        readonly Queue<int> queue = new Queue<int>();

        public EventState State { get; }
        public EventFile File { get; private set; }
        public ScriptVM Active { get; private set; }
        public bool Busy => Active != null && !Active.Finished;
        public bool QueueWhileBusy;

        /// <summary>Optional predicate for whether an event may fire (FUN_00054354 equivalent). Null means yes.</summary>
        public Func<int, bool> CanFire;
        public ScriptResolver Resolver = DefaultResolver;
        public event Action<int> EventStarted;
        public event Action<int> EventFinished;
        int activeEvent = -1;

        public EventRunner(IEventHost host, EventState state = null)
        {
            this.host = host;
            State = state ?? new EventState();
        }

        /// <summary>Set the event file of the current map; null (data absent) disables triggers cleanly.</summary>
        public void SetMap(EventFile file)
        {
            File = file;
            Active = null;
            queue.Clear();
        }

        /// <summary>
        /// Resolves the event file that holds an event's script. In the original a region event id is a
        /// map id: the game loads that map's single resident script image and runs it from command 0
        /// (FUN_00058da0). Null means the current <see cref="File"/> is used.
        /// </summary>
        public Func<int, EventFile> EventMapLoader;

        /// <summary>
        /// Default resolver: the event id names a map, whose file holds exactly one script (all 439
        /// converted maps do). Pass the target map's file; a file with several scripts falls back to
        /// matching the block number, then the index.
        /// </summary>
        public static EventScript DefaultResolver(EventFile file, int eventId, out int scriptIndex)
        {
            scriptIndex = -1;
            if (file?.scripts == null) return null;
            if (file.scripts.Length == 1) { scriptIndex = 0; return file.scripts[0]; }
            for (int i = 0; i < file.scripts.Length; i++)
                if (file.scripts[i].block == eventId) { scriptIndex = i; return file.scripts[i]; }
            if (eventId >= 0 && eventId < file.scripts.Length) { scriptIndex = eventId; return file.scripts[eventId]; }
            return null;
        }

        /// <summary>Region trigger. Returns true if a script was started or queued.</summary>
        public bool OnRegionEntered(int eventId)
        {
            if (eventId < 0 || File == null) return false;
            if (CanFire != null && !CanFire(eventId)) return false;
            if (Busy)
            {
                if (!QueueWhileBusy) return false;
                queue.Enqueue(eventId);
                return true;
            }
            return Start(eventId);
        }

        bool Start(int eventId)
        {
            var f = EventMapLoader?.Invoke(eventId) ?? File;
            var s = Resolver(f, eventId, out int idx);
            if (s == null) return false;
            Active = new ScriptVM(s, State, host, idx);
            activeEvent = eventId;
            EventStarted?.Invoke(eventId);
            return true;
        }

        public void Tick()
        {
            if (Active != null && !Active.Finished)
            {
                Active.Tick();
                if (Active.Finished) EventFinished?.Invoke(activeEvent);
            }
            if (!Busy && queue.Count > 0) Start(queue.Dequeue());
        }
    }
}
