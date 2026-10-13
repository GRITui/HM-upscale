using System.Collections.Generic;
using HM.Dialogue;

namespace HM.Events
{
    /// <summary>Resolves a message id to a dialogue graph. Game text lives only in local, git-ignored data.</summary>
    public interface IMessageCatalog
    {
        DialogueData Find(MessageId id);
    }

    /// <summary>Message id as used by script op 2: channel 0-4 (messege0..4) and entry number. No text.</summary>
    public readonly struct MessageId
    {
        public readonly int Channel;
        public readonly int Entry;
        public MessageId(int channel, int entry) { Channel = channel; Entry = entry; }
        public override string ToString() => "msg:" + Channel + ":" + Entry;
    }

    /// <summary>
    /// Bridges script dialog ops to HM.Dialogue by message id. UI code reads <see cref="Runner"/>
    /// (Current node, VisibleChoices), then calls Advance or Choose. When the catalog has no entry for an id
    /// (for example text not extracted locally) the dialog completes at once with result 0 and the id is
    /// recorded in <see cref="Missing"/>.
    /// </summary>
    public sealed class DialogueBridge
    {
        readonly IMessageCatalog catalog;
        readonly HashSet<string> flags;
        int lastChoice;

        public DialogueRunner Runner { get; private set; }
        public MessageId Current { get; private set; }
        public bool Active => Runner != null && !Runner.Finished;
        public readonly List<MessageId> Missing = new List<MessageId>();

        public DialogueBridge(IMessageCatalog catalog, HashSet<string> flags = null)
        {
            this.catalog = catalog;
            this.flags = flags;
        }

        public void Begin(int channel, int entry)
        {
            Current = new MessageId(channel, entry);
            lastChoice = 0;
            var data = catalog?.Find(Current);
            if (data == null) { Runner = null; Missing.Add(Current); return; }
            Runner = new DialogueRunner(data, flags);
            Runner.Begin();
        }

        public void Advance() { if (Active) Runner.Advance(); }

        public void Choose(int index)
        {
            if (!Active) return;
            Runner.Choose(index);
            lastChoice = index;
        }

        /// <summary>True once the conversation has closed; result is the last chosen index (0 if none).</summary>
        public bool Poll(out int result)
        {
            result = lastChoice;
            return !Active;
        }
    }
}
