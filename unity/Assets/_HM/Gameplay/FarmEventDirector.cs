using HM.Core;
using HM.Events;
using HM.World;
using UnityEngine;

namespace HM.Gameplay
{
    /// <summary>
    /// Runs the event VM on the farm: walking into a MapRegionTrigger with an event id runs that map's
    /// resident script (EventRunner.EventMapLoader), ticked once per frame. Owns the dialogue bridge the HUD reads.
    /// </summary>
    public sealed class FarmEventDirector : MonoBehaviour
    {
        public static FarmEventDirector Instance { get; private set; }

        public EventRunner Runner { get; private set; }
        public FarmEventHost Host { get; private set; }
        public DialogueBridge Bridge { get; private set; }

        /// <summary>True while a script runs, a dialogue is open or a script asked for an input lock.</summary>
        public bool InputLocked => (Runner != null && Runner.Busy && Host != null && Host.InputLocked) || (Bridge != null && Bridge.Active);

        /// <summary>Text source; null (default) means missing ids complete at once and are listed in Bridge.Missing.</summary>
        public static IMessageCatalog Catalog;

        public void Init(Transform player, int farmMapId)
        {
            Instance = this;
            Bridge = new DialogueBridge(Catalog, FarmWorld.Instance != null ? FarmWorld.Instance.Flags : null);
            Host = new FarmEventHost(player, Bridge, () => GameClockBehaviour.Instance != null ? GameClockBehaviour.Instance.Clock : null) { CurrentMap = farmMapId };
            Runner = new EventRunner(Host)
            {
                EventMapLoader = id => EventLoader.TryLoadMap(id, out var f) ? f : null
            };
            Runner.EventStarted += id => Debug.Log("[HM-EVENT] start region event " + id);
            Runner.EventFinished += id => Debug.Log("[HM-EVENT] finished region event " + id);
            // Region ids are map ids resolved through EventMapLoader, so the runner needs a (possibly empty) current file.
            bool have = EventLoader.TryLoadMap(farmMapId, out var file);
            Runner.SetMap(have ? file : new EventFile { schema = 1 });
            if (!have) Debug.Log("[HM-EVENT] no event data for map " + farmMapId + "; triggers are inert unless the target map has data");
            MapRegionTrigger.Entered += OnEntered;
        }

        /// <summary>Map number from an id such as "map080" (digits only); -1 if none.</summary>
        public static int MapNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            int n = 0; bool any = false;
            foreach (char c in id) if (c >= '0' && c <= '9') { n = n * 10 + (c - '0'); any = true; }
            return any ? n : -1;
        }

        void OnEntered(MapRegionTrigger t, Collider who)
        {
            if (t == null || t.eventId < 0 || Runner == null) return;
            Runner.OnRegionEntered(t.eventId);
        }

        void Update() => Runner?.Tick();

        void OnDestroy()
        {
            MapRegionTrigger.Entered -= OnEntered;
            if (Instance == this) Instance = null;
        }
    }
}
