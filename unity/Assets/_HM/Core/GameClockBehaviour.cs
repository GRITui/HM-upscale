using UnityEngine;

namespace HM.Core
{
    /// <summary>Scene host for the GameClock. Persists across scene loads.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameClockBehaviour : MonoBehaviour
    {
        [SerializeField] float minutesPerRealSecond = 2f;
        [SerializeField] float startHour = 6f;

        public static GameClockBehaviour Instance { get; private set; }
        public GameClock Clock { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Clock = new GameClock(minutesPerRealSecond, startHour * 60.0);
            DontDestroyOnLoad(gameObject);
        }

        void Update() => Clock.Tick(Time.deltaTime);

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
