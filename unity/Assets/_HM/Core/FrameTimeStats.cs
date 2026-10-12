using System;
using System.Collections.Generic;

namespace HM.Core
{
    /// <summary>Plain C# frame time accumulator: average, percentiles, max and count over budget.</summary>
    public sealed class FrameTimeStats
    {
        public const float BudgetMs = 1000f / 60f;

        readonly List<float> samples = new List<float>(4096);

        public int Count => samples.Count;
        public int OverBudget { get; private set; }

        public void Add(float frameMs)
        {
            if (frameMs < 0f || float.IsNaN(frameMs)) return;
            samples.Add(frameMs);
            if (frameMs > BudgetMs * 1.05f) OverBudget++;
        }

        public void Clear() { samples.Clear(); OverBudget = 0; }

        public float Average
        {
            get
            {
                if (samples.Count == 0) return 0f;
                double sum = 0;
                for (int i = 0; i < samples.Count; i++) sum += samples[i];
                return (float)(sum / samples.Count);
            }
        }

        public float Max
        {
            get
            {
                float m = 0f;
                for (int i = 0; i < samples.Count; i++) if (samples[i] > m) m = samples[i];
                return m;
            }
        }

        /// <summary>Nearest-rank percentile, p in 0..100.</summary>
        public float Percentile(float p)
        {
            if (samples.Count == 0) return 0f;
            var sorted = samples.ToArray();
            Array.Sort(sorted);
            int rank = (int)Math.Ceiling(Math.Max(0f, Math.Min(100f, p)) / 100f * sorted.Length);
            return sorted[Math.Max(0, rank - 1)];
        }

        public string Summary() =>
            $"frames={Count} avg={Average:0.00}ms ({(Average > 0 ? 1000f / Average : 0):0.0}fps) p95={Percentile(95):0.00}ms p99={Percentile(99):0.00}ms max={Max:0.00}ms over_budget={OverBudget}";
    }
}
