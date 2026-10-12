using System;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace HM.Core
{
    [Serializable]
    public sealed class PerfCaptureResult
    {
        public string note;
        public string scene;
        public string gpu;
        public string cpu;
        public string os;
        public int screenWidth, screenHeight;
        public int targetFps;
        public bool developmentBuild;
        public float warmupSeconds;
        public float captureSeconds;
        public int warmupFrames;
        public float warmupMaxMs;
        public int warmupFramesOver100Ms;
        public int frames;
        public float avgMs, p95Ms, p99Ms, maxMs;
        public float avgFps;
        public int overBudgetFrames;
        public bool renderCountersAvailable;
        public float drawCallsAvg;
        public int drawCallsMax;
        public float setPassAvg;
        public int setPassMax;
        public float trianglesAvg;
        public long trianglesMax;
    }

    /// <summary>
    /// Plain C# capture session: discards frames inside the warmup window, then records frame time and
    /// render counters until the capture duration elapses. Time is the sum of frame deltas.
    /// </summary>
    public sealed class PerfCaptureSession
    {
        readonly FrameTimeStats stats = new FrameTimeStats();
        readonly float warmup, duration;
        float elapsed;
        int warmupFrames, warmupOver100;
        float warmupMax;
        int counterFrames;
        double drawSum, setPassSum, triSum;
        int drawMax, setPassMax;
        long triMax;

        public PerfCaptureSession(float warmupSeconds, float captureSeconds)
        {
            warmup = Math.Max(0f, warmupSeconds);
            duration = Math.Max(0.01f, captureSeconds);
        }

        public bool InWarmup => elapsed < warmup;
        public bool Done => elapsed >= warmup + duration;
        public int Frames => stats.Count;
        public int WarmupFrames => warmupFrames;

        /// <summary>Add one frame. Counters are the render stats for that frame (0 when unavailable).</summary>
        public void AddFrame(float frameSeconds, int drawCalls = 0, int setPass = 0, long triangles = 0)
        {
            if (Done || frameSeconds < 0f || float.IsNaN(frameSeconds)) return;
            float ms = frameSeconds * 1000f;
            bool warm = elapsed < warmup;
            elapsed += frameSeconds;
            if (warm)
            {
                warmupFrames++;
                if (ms > warmupMax) warmupMax = ms;
                if (ms > 100f) warmupOver100++;
                return;
            }
            stats.Add(ms);
            if (drawCalls > 0)
            {
                counterFrames++;
                drawSum += drawCalls; setPassSum += setPass; triSum += triangles;
                if (drawCalls > drawMax) drawMax = drawCalls;
                if (setPass > setPassMax) setPassMax = setPass;
                if (triangles > triMax) triMax = triangles;
            }
        }

        public PerfCaptureResult Result()
        {
            var r = new PerfCaptureResult
            {
                warmupSeconds = warmup,
                captureSeconds = duration,
                warmupFrames = warmupFrames,
                warmupMaxMs = warmupMax,
                warmupFramesOver100Ms = warmupOver100,
                frames = stats.Count,
                avgMs = stats.Average,
                p95Ms = stats.Percentile(95),
                p99Ms = stats.Percentile(99),
                maxMs = stats.Max,
                overBudgetFrames = stats.OverBudget,
                renderCountersAvailable = counterFrames > 0,
            };
            r.avgFps = r.avgMs > 0f ? 1000f / r.avgMs : 0f;
            if (counterFrames > 0)
            {
                r.drawCallsAvg = (float)(drawSum / counterFrames); r.drawCallsMax = drawMax;
                r.setPassAvg = (float)(setPassSum / counterFrames); r.setPassMax = setPassMax;
                r.trianglesAvg = (float)(triSum / counterFrames); r.trianglesMax = triMax;
            }
            return r;
        }

        public string ToJson() => JsonUtility.ToJson(Result(), true);
    }

    /// <summary>
    /// Player flag driven capture. -hm-perf-capture SECONDS (required), -hm-perf-warmup SECONDS (default 5),
    /// -hm-perf-out PATH (default: perf-capture.json beside the log), -hm-perf-note TEXT (free text, record
    /// the machine load here). Writes JSON then quits. Never writes into the repository by default.
    /// </summary>
    public sealed class PerfCapture : MonoBehaviour
    {
        PerfCaptureSession session;
        string outPath, note;
        ProfilerRecorder drawCalls, triangles, setPass;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-hm-perf-capture") < 0) return;
            float seconds = ReadFloat(args, "-hm-perf-capture", 30f);
            float warmup = ReadFloat(args, "-hm-perf-warmup", 5f);
            var go = new GameObject("PerfCapture");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<PerfCapture>();
            c.session = new PerfCaptureSession(warmup, seconds);
            c.note = ReadString(args, "-hm-perf-note", "");
            c.outPath = ReadString(args, "-hm-perf-out", Path.Combine(Application.persistentDataPath, "perf-capture.json"));
        }

        static string ReadString(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        static float ReadFloat(string[] args, string name, float fallback)
        {
            string s = ReadString(args, name, null);
            return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }

        void OnDisable() { drawCalls.Dispose(); triangles.Dispose(); setPass.Dispose(); }

        void Update()
        {
            if (session == null) return;
            session.AddFrame(Time.unscaledDeltaTime,
                drawCalls.Valid ? (int)drawCalls.LastValue : 0,
                setPass.Valid ? (int)setPass.LastValue : 0,
                triangles.Valid ? triangles.LastValue : 0);
            if (!session.Done) return;

            var r = session.Result();
            r.note = note;
            r.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            r.gpu = SystemInfo.graphicsDeviceName; r.cpu = SystemInfo.processorType; r.os = SystemInfo.operatingSystem;
            r.screenWidth = Screen.width; r.screenHeight = Screen.height;
            r.targetFps = Application.targetFrameRate; r.developmentBuild = Debug.isDebugBuild;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllText(outPath, JsonUtility.ToJson(r, true));
                Debug.Log("[HM-PERF] capture written to " + outPath);
            }
            catch (Exception e) { Debug.LogError("[HM-PERF] could not write " + outPath + ": " + e.Message); }
            session = null;
            Application.Quit();
        }
    }
}
