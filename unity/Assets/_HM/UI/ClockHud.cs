using HM.Core;
using UnityEngine;

namespace HM.UI
{
    /// <summary>Minimal placeholder HUD showing date and time. Replaced by UI Toolkit later.</summary>
    public sealed class ClockHud : MonoBehaviour
    {
        GUIStyle style;

        void OnGUI()
        {
            var host = GameClockBehaviour.Instance;
            if (host == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var c = host.Clock;
            GUI.Label(new Rect(16, 12, 520, 36), $"{c.Date}   {c.FormatTime()}   {c.Phase}", style);
        }
    }
}
