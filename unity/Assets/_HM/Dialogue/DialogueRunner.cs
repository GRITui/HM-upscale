using System;
using System.Collections.Generic;

namespace HM.Dialogue
{
    /// <summary>Walks a DialogueData graph. Pure C#; flags are a plain string set owned by the caller.</summary>
    public sealed class DialogueRunner
    {
        readonly Dictionary<string, NodeData> byId = new Dictionary<string, NodeData>();
        readonly DialogueData data;

        public HashSet<string> Flags { get; }
        public NodeData Current { get; private set; }
        public bool Finished => Current == null;

        public DialogueRunner(DialogueData data, HashSet<string> flags = null)
        {
            this.data = data;
            Flags = flags ?? new HashSet<string>();
            foreach (var n in data.nodes) byId[n.id] = n;
        }

        public void Begin(string nodeId = null) => Enter(nodeId ?? data.start);

        void Enter(string id)
        {
            if (string.IsNullOrEmpty(id)) { Current = null; return; }
            if (!byId.TryGetValue(id, out var n)) throw new KeyNotFoundException("dialogue node " + id);
            Current = n;
            if (n.setFlags != null) foreach (var f in n.setFlags) Flags.Add(f);
        }

        /// <summary>Choices currently visible (requireFlag satisfied).</summary>
        public List<ChoiceData> VisibleChoices()
        {
            var list = new List<ChoiceData>();
            if (Current?.choices == null) return list;
            foreach (var c in Current.choices)
                if (string.IsNullOrEmpty(c.requireFlag) || Flags.Contains(c.requireFlag)) list.Add(c);
            return list;
        }

        public bool HasChoices => VisibleChoices().Count > 0;

        /// <summary>Move past a line with no choices. Throws if choices are pending.</summary>
        public void Advance()
        {
            if (Finished) return;
            if (HasChoices) throw new InvalidOperationException("choose first");
            bool branch = !string.IsNullOrEmpty(Current.ifFlag) && Flags.Contains(Current.ifFlag);
            Enter(branch ? Current.ifNext : Current.next);
        }

        public void Choose(int index)
        {
            var vis = VisibleChoices();
            if (index < 0 || index >= vis.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var c = vis[index];
            if (c.setFlags != null) foreach (var f in c.setFlags) Flags.Add(f);
            Enter(c.next);
        }
    }
}
