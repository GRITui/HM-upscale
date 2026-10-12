using System.Collections.Generic;
using System.Text;
using HM.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HM.Gameplay
{
    /// <summary>
    /// UI Toolkit HUD built in code: clock and day, money, held tool, stamina, interaction prompt,
    /// dialogue panel and the sleep fade. Text is only rebuilt when its inputs change (no per-frame garbage).
    /// </summary>
    public sealed class SliceHud : MonoBehaviour
    {
        Label status, toast, prompt, dlgSpeaker, dlgText, dlgChoices;
        VisualElement dialoguePanel, fade;
        readonly StringBuilder sb = new StringBuilder(128);
        string lastStatus = "", lastPrompt = "";
        int lastMinute = -1, lastVersion = -1;
        float toastUntil;
        PlayerInteractor player;

        public VisualElement Root { get; private set; }

        void Start()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>(); // empty theme: all styling is inline
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            Root = doc.rootVisualElement;
            Root.pickingMode = PickingMode.Ignore;
            Root.style.flexGrow = 1;

            status = Make(Root, 18, new Color(0.08f, 0.1f, 0.12f, 0.78f));
            status.style.position = Position.Absolute; status.style.left = 16; status.style.top = 12;
            toast = Make(Root, 16, new Color(0.08f, 0.1f, 0.12f, 0.78f));
            toast.style.position = Position.Absolute; toast.style.left = 16; toast.style.top = 108;
            prompt = Make(Root, 18, new Color(0.08f, 0.1f, 0.12f, 0.85f));
            prompt.style.position = Position.Absolute; prompt.style.bottom = 28; prompt.style.alignSelf = Align.Center;
            prompt.style.left = new Length(40, LengthUnit.Percent);

            dialoguePanel = new VisualElement();
            var ds = dialoguePanel.style;
            ds.position = Position.Absolute; ds.left = new Length(20, LengthUnit.Percent); ds.right = new Length(20, LengthUnit.Percent);
            ds.bottom = 24; ds.paddingTop = ds.paddingBottom = 14; ds.paddingLeft = ds.paddingRight = 18;
            ds.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 0.92f);
            ds.borderTopLeftRadius = ds.borderTopRightRadius = ds.borderBottomLeftRadius = ds.borderBottomRightRadius = 6;
            dialoguePanel.style.display = DisplayStyle.None;
            dlgSpeaker = new Label(); dlgSpeaker.style.color = new Color(0.95f, 0.78f, 0.35f); dlgSpeaker.style.fontSize = 20;
            dlgText = new Label(); dlgText.style.color = Color.white; dlgText.style.fontSize = 18; dlgText.style.whiteSpace = WhiteSpace.Normal;
            dlgChoices = new Label(); dlgChoices.style.color = new Color(0.8f, 0.9f, 1f); dlgChoices.style.fontSize = 16; dlgChoices.style.marginTop = 8;
            dialoguePanel.Add(dlgSpeaker); dialoguePanel.Add(dlgText); dialoguePanel.Add(dlgChoices);
            Root.Add(dialoguePanel);

            fade = new VisualElement { pickingMode = PickingMode.Ignore };
            fade.style.position = Position.Absolute;
            fade.style.left = fade.style.right = fade.style.top = fade.style.bottom = 0;
            fade.style.backgroundColor = Color.black; fade.style.opacity = 0f;
            Root.Add(fade);
            player = FindFirstObjectByType<PlayerInteractor>();
        }

        static Label Make(VisualElement parent, int size, Color bg)
        {
            var l = new Label();
            var s = l.style;
            s.fontSize = size; s.color = Color.white; s.backgroundColor = bg;
            s.paddingTop = s.paddingBottom = 6; s.paddingLeft = s.paddingRight = 12;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 5;
            s.whiteSpace = WhiteSpace.Normal;
            parent.Add(l);
            return l;
        }

        void Update()
        {
            if (Root == null) return;
            var host = GameClockBehaviour.Instance;
            var world = FarmWorld.Instance;
            if (host != null && world != null)
            {
                var c = host.Clock;
                int minute = (int)c.TotalMinutes;
                if (minute != lastMinute || world.Version != lastVersion)
                {
                    sb.Clear();
                    sb.Append(c.Date).Append("  ").Append(c.FormatTime()).Append("  ").Append(c.Phase)
                      .Append("\nGold ").Append(world.Sim.Gold)
                      .Append("   Stamina ").Append(world.Sim.Stamina).Append('/').Append(world.Sim.MaxStamina)
                      .Append("\nTool: ").Append(world.HeldTool);
                    if (world.HeldTool == Farming.Tool.SeedBag)
                        sb.Append(" (").Append(world.SeedCrop).Append(" x").Append(world.Sim.SeedCount(world.SeedCrop)).Append(')');
                    string s = sb.ToString();
                    if (s != lastStatus) { status.text = s; lastStatus = s; }
                    if (world.Version != lastVersion)
                    {
                        toast.text = world.LastMessage;
                        toast.style.display = string.IsNullOrEmpty(world.LastMessage) ? DisplayStyle.None : DisplayStyle.Flex;
                        toastUntil = Time.unscaledTime + 3f;
                    }
                    lastMinute = minute; lastVersion = world.Version;
                }
            }
            if (toast.style.display == DisplayStyle.Flex && Time.unscaledTime > toastUntil) toast.style.display = DisplayStyle.None;

            var npc = NpcDialogue.Active;
            if (npc != null && npc.Runner != null && !npc.Runner.Finished) ShowDialogue(npc); else dialoguePanel.style.display = DisplayStyle.None;

            string p = "";
            if (npc == null)
            {
                if (player == null) player = FindFirstObjectByType<PlayerInteractor>();
                if (player != null && player.Focus != null) p = player.Focus.Prompt;
            }
            if (p != lastPrompt)
            {
                prompt.text = p; lastPrompt = p;
                prompt.style.display = p.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }
            var sleep = SleepController.Instance;
            fade.style.opacity = sleep != null ? sleep.Fade : 0f;
        }

        void ShowDialogue(NpcDialogue npc)
        {
            dialoguePanel.style.display = DisplayStyle.Flex;
            var n = npc.Runner.Current;
            dlgSpeaker.text = n.speaker;
            dlgText.text = n.text;
            sb.Clear();
            var choices = npc.Runner.VisibleChoices();
            if (choices.Count == 0) sb.Append("E: continue");
            for (int i = 0; i < choices.Count; i++) sb.Append(i + 1).Append(". ").Append(choices[i].text).Append('\n');
            dlgChoices.text = sb.ToString();
        }
    }
}
