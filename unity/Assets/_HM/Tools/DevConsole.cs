using HM.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HM.Tools
{
    /// <summary>
    /// In-game developer console (backquote toggles). Compiled into the editor and development
    /// builds only; in release builds the component destroys itself. IMGUI on purpose: no scene UI to maintain.
    /// </summary>
    public sealed class DevConsole : MonoBehaviour
    {
        bool open;
        string input = "";
        string output = "backquote toggles; type help";
        const string FieldName = "hm_dev_input";

        void Awake()
        {
            if (!Debug.isDebugBuild) Destroy(this);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) open = !open;
        }

        public string Run(string line)
        {
            var clock = GameClockBehaviour.Instance != null ? GameClockBehaviour.Instance.Clock : null;
            output = DevCommands.Execute(clock, line);
            return output;
        }

        void OnGUI()
        {
            if (!open) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.BackQuote) { e.Use(); return; }
            float w = Mathf.Min(480f, Screen.width - 16f);
            GUILayout.BeginArea(new Rect(8, 8, w, 200), GUI.skin.box);
            GUILayout.Label(output);
            GUI.SetNextControlName(FieldName);
            input = GUILayout.TextField(input);
            GUI.FocusControl(FieldName);
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                Run(input);
                input = "";
                e.Use();
            }
            GUILayout.EndArea();
        }
    }
}
