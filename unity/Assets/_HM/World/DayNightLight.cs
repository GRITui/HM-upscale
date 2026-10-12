using HM.Core;
using UnityEngine;

namespace HM.World
{
    /// <summary>Rotates and tints a directional light from the GameClock.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class DayNightLight : MonoBehaviour
    {
        [SerializeField] Gradient colour = DefaultGradient();
        [SerializeField] float maxIntensity = 1.1f;
        [SerializeField] float nightIntensity = 0.08f;

        Light sun;

        void Awake() => sun = GetComponent<Light>();

        void Update()
        {
            var host = GameClockBehaviour.Instance;
            if (host == null) return;
            Apply(host.Clock.DayFraction);
        }

        public void Apply(float dayFraction)
        {
            // 0.25 (06:00) sunrise, 0.5 noon, 0.75 (18:00) sunset.
            float pitch = (dayFraction - 0.25f) * 360f;
            transform.rotation = Quaternion.Euler(pitch, -30f, 0f);
            float elevation = Mathf.Sin((dayFraction - 0.25f) * Mathf.PI * 2f);
            // Offset so the 06:00 start reads as early morning light rather than night.
            float k = Mathf.Clamp01((elevation + 0.3f) * 2f);
            sun.intensity = Mathf.Lerp(nightIntensity, maxIntensity, k);
            sun.color = colour.Evaluate(dayFraction);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.05f, 0.06f, 0.12f), new Color(0.5f, 0.55f, 0.6f), k);
        }

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.35f, 0.4f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.65f, 0.4f), 0.27f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.9f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.35f), 0.73f),
                    new GradientColorKey(new Color(0.35f, 0.4f, 0.7f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
