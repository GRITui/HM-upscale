using System;
using Unity.Profiling;
using UnityEngine;

namespace HM.Core
{
    /// <summary>
    /// Logs frame time stats from a player build. Enable with the command line flag -hm-perf
    /// (optionally -hm-perf-seconds N to log once after N seconds and quit; -hm-perf-interval N for a
    /// periodic log). Press F3 to toggle the periodic log in any build. Draw calls and triangles are
    /// only reported when the platform exposes the render profiler counters (development builds).
    /// </summary>
    public sealed class FrameTimeLogger : MonoBehaviour
    {
        readonly FrameTimeStats stats = new FrameTimeStats();
        float interval = 5f;
        float quitAfter;
        float elapsed, sinceLog;
        bool active;
        ProfilerRecorder drawCalls, triangles, setPass;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-hm-perf") < 0) return;
            var go = new GameObject("FrameTimeLogger");
            DontDestroyOnLoad(go);
            var l = go.AddComponent<FrameTimeLogger>();
            l.active = true;
            l.interval = ReadFloat(args, "-hm-perf-interval", 5f);
            l.quitAfter = ReadFloat(args, "-hm-perf-seconds", 0f);
        }

        static float ReadFloat(string[] args, string name, float fallback)
        {
            int i = Array.IndexOf(args, name);
            if (i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
            return fallback;
        }

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }

        void OnDisable()
        {
            drawCalls.Dispose(); triangles.Dispose(); setPass.Dispose();
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame)
            {
                active = !active;
                stats.Clear();
                sinceLog = 0f;
            }
            if (!active) return;

            float dt = Time.unscaledDeltaTime;
            stats.Add(dt * 1000f);
            elapsed += dt;
            sinceLog += dt;

            if (quitAfter > 0f && elapsed >= quitAfter) { Log("final"); Application.Quit(); active = false; return; }
            if (sinceLog >= interval) { Log("periodic"); stats.Clear(); sinceLog = 0f; }
        }

        void Log(string tag)
        {
            string render = drawCalls.Valid && drawCalls.LastValue > 0
                ? $" draw_calls={drawCalls.LastValue} setpass={setPass.LastValue} tris={triangles.LastValue}"
                : " draw_calls=n/a";
            Debug.Log($"[HM-PERF] {tag} {stats.Summary()}{render} target_fps={Application.targetFrameRate} gfx={SystemInfo.graphicsDeviceName}");
        }
    }
}
