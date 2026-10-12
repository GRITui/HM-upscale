using System.Collections.Generic;
using UnityEngine;

namespace HM.Gameplay
{
    /// <summary>Something the player can use with the interact key when standing close.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();
        public float radius = 2f;
        public abstract string Prompt { get; }
        public abstract void Interact(PlayerInteractor who);

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public static Interactable Nearest(Vector3 from)
        {
            Interactable best = null;
            float bestD = float.MaxValue;
            foreach (var i in All)
            {
                var d = i.transform.position - from; d.y = 0f;
                float m = d.sqrMagnitude;
                if (m <= i.radius * i.radius && m < bestD) { best = i; bestD = m; }
            }
            return best;
        }
    }
}
