using System;
using HM.Dialogue;
using UnityEngine;

namespace HM.Gameplay
{
    /// <summary>NPC that opens a HM.Dialogue conversation. Flags are shared with FarmWorld so choices can react to farming.</summary>
    public sealed class NpcDialogue : Interactable
    {
        public static NpcDialogue Active { get; private set; }
        public TextAsset dialogueJson;
        public string npcName = "Mira";

        public DialogueRunner Runner { get; private set; }
        public event Action Changed;
        public override string Prompt => "E: Talk to " + npcName;

        public override void Interact(PlayerInteractor who) => Open();

        public void Open()
        {
            var data = DialogueData.FromJson(dialogueJson.text);
            var flags = FarmWorld.Instance != null ? FarmWorld.Instance.Flags : null;
            Runner = new DialogueRunner(data, flags);
            Runner.Begin();
            Active = this;
            Changed?.Invoke();
        }

        /// <summary>Pick choice i if the line has choices, otherwise advance. Closes when finished.</summary>
        public void Select(int i)
        {
            if (Runner == null) return;
            if (Runner.HasChoices) { if (i < 0 || i >= Runner.VisibleChoices().Count) return; Runner.Choose(i); }
            else Runner.Advance();
            if (Runner.Finished) Close(); else Changed?.Invoke();
        }

        public void Close()
        {
            Runner = null;
            if (Active == this) Active = null;
            Changed?.Invoke();
        }

        protected override void OnDisable() { base.OnDisable(); if (Active == this) Active = null; }
    }
}
