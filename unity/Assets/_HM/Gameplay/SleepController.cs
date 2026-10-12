using System.Collections;
using HM.Core;
using UnityEngine;

namespace HM.Gameplay
{
    /// <summary>End-of-day sleep: fade, jump the clock to the wake hour (raises DayChanged when midnight is crossed), fade back in.</summary>
    public sealed class SleepController : MonoBehaviour
    {
        public static SleepController Instance { get; private set; }
        public float fadeSeconds = 0.4f;
        public float Fade { get; private set; }
        public bool Sleeping { get; private set; }
        public int SleepCount { get; private set; }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var host = GameClockBehaviour.Instance;
            if (host == null || Sleeping) return;
            if (DayCycle.ShouldForceSleep(host.Clock)) Sleep("You collapsed from exhaustion.");
        }

        public void Sleep(string message)
        {
            if (!Sleeping && GameClockBehaviour.Instance != null) StartCoroutine(Run(message));
        }

        /// <summary>Immediate sleep without a fade (tests, dev console).</summary>
        public int SleepNow()
        {
            var host = GameClockBehaviour.Instance;
            if (host == null) return 0;
            int d = DayCycle.Sleep(host.Clock);
            SleepCount++;
            return d;
        }

        IEnumerator Run(string message)
        {
            Sleeping = true;
            for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime) { Fade = t / fadeSeconds; yield return null; }
            Fade = 1f;
            SleepNow();
            FarmWorld.Instance?.Notify(message);
            for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime) { Fade = 1f - t / fadeSeconds; yield return null; }
            Fade = 0f;
            Sleeping = false;
        }
    }
}
