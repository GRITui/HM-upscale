using System.Collections.Generic;
using System.Text;
using HM.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HM.Gameplay
{
    /// <summary>
    /// uGUI HUD built in code: clock and day, money, held tool, stamina, interaction prompt,
    /// dialogue panel and the sleep fade. Text is only rebuilt when its inputs change (no per-frame garbage).
    /// Replaced the UI Toolkit version: with no theme the labels drew as empty pills on screen.
    /// </summary>
    public sealed class SliceHud : MonoBehaviour
    {
        Text status, toast, prompt, dlgSpeaker, dlgText, dlgChoices;
        GameObject toastPanel, promptPanel, dialoguePanel;
        Image fade;
        Font font;
        readonly StringBuilder sb = new StringBuilder(128);
        string lastStatus = "", lastPrompt = "";
        int lastMinute = -1, lastVersion = -1;
        float toastUntil;
        PlayerInteractor player;
        bool built;

        static readonly Color PanelBg = new Color(0.08f, 0.10f, 0.12f, 0.82f);

        void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var statusPanel = Panel(root, "Status", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), PanelBg);
            status = Label(statusPanel, 18, Color.white, TextAnchor.UpperLeft);

            toastPanel = Panel(root, "Toast", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -112), PanelBg);
            toast = Label(toastPanel.transform, 16, Color.white, TextAnchor.UpperLeft);
            toastPanel.SetActive(false);

            promptPanel = Panel(root, "Prompt", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), PanelBg);
            prompt = Label(promptPanel.transform, 18, Color.white, TextAnchor.MiddleCenter);
            promptPanel.SetActive(false);

            dialoguePanel = Panel(root, "Dialogue", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Color(0.08f, 0.10f, 0.12f, 0.94f));
            var dlgRt = (RectTransform)dialoguePanel.transform;
            dlgRt.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            dlgRt.sizeDelta = new Vector2(720, 0);
            dlgSpeaker = Label(dialoguePanel.transform, 20, new Color(0.95f, 0.78f, 0.35f), TextAnchor.UpperLeft);
            dlgText = Label(dialoguePanel.transform, 18, Color.white, TextAnchor.UpperLeft);
            dlgChoices = Label(dialoguePanel.transform, 16, new Color(0.8f, 0.9f, 1f), TextAnchor.UpperLeft);
            dialoguePanel.SetActive(false);

            var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            fadeGo.transform.SetParent(root, false);
            var frt = (RectTransform)fadeGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            fade = fadeGo.GetComponent<Image>();
            fade.color = new Color(0, 0, 0, 0);
            fade.raycastTarget = false;

            player = FindFirstObjectByType<PlayerInteractor>();
            built = true;
        }

        static GameObject Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>(); img.color = bg; img.raycastTarget = false;
            var lg = go.GetComponent<VerticalLayoutGroup>();
            lg.padding = new RectOffset(12, 12, 8, 8);
            lg.childControlWidth = lg.childControlHeight = true;
            lg.childForceExpandWidth = lg.childForceExpandHeight = false;
            var fit = go.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        Text Label(Transform parent, int size, Color colour, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = colour; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Text Label(GameObject panel, int size, Color colour, TextAnchor anchor) => Label(panel.transform, size, colour, anchor);

        void Update()
        {
            if (!built) return;
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
                        toastPanel.SetActive(!string.IsNullOrEmpty(world.LastMessage));
                        toastUntil = Time.unscaledTime + 3f;
                    }
                    lastMinute = minute; lastVersion = world.Version;
                }
            }
            if (toastPanel.activeSelf && Time.unscaledTime > toastUntil) toastPanel.SetActive(false);

            var npc = NpcDialogue.Active;
            var evBridge = FarmEventDirector.Instance != null ? FarmEventDirector.Instance.Bridge : null;
            if (npc != null && npc.Runner != null && !npc.Runner.Finished) ShowDialogue(npc.Runner);
            else if (evBridge != null && evBridge.Active) ShowDialogue(evBridge.Runner);
            else if (dialoguePanel.activeSelf) dialoguePanel.SetActive(false);

            string p = "";
            if (npc == null)
            {
                if (player == null) player = FindFirstObjectByType<PlayerInteractor>();
                if (player != null && player.Focus != null) p = player.Focus.Prompt;
            }
            if (p != lastPrompt)
            {
                prompt.text = p; lastPrompt = p;
                promptPanel.SetActive(p.Length != 0);
            }
            var sleep = SleepController.Instance;
            float fa = sleep != null ? sleep.Fade : 0f;
            if (!Mathf.Approximately(fade.color.a, fa)) fade.color = new Color(0, 0, 0, fa);
        }

        void ShowDialogue(HM.Dialogue.DialogueRunner runner)
        {
            if (!dialoguePanel.activeSelf) dialoguePanel.SetActive(true);
            var n = runner.Current;
            dlgSpeaker.text = n.speaker;
            dlgText.text = n.text;
            sb.Clear();
            var choices = runner.VisibleChoices();
            if (choices.Count == 0) sb.Append("E: continue");
            for (int i = 0; i < choices.Count; i++) sb.Append(i + 1).Append(". ").Append(choices[i].text).Append('\n');
            dlgChoices.text = sb.ToString();
        }
    }
}
