using UnityEngine;

namespace HM.World
{
    /// <summary>
    /// A decoded region as a trigger volume. It carries data only: the event and arrival values are the raw decoded
    /// ids and are deliberately NOT interpreted yet (the event interpreter is owned elsewhere).
    /// </summary>
    public sealed class MapRegionTrigger : MonoBehaviour
    {
        public int index, flags, type, eventId = -1, facing, condition;
        /// <summary>Arrival point in world metres (converted with FarmCoords).</summary>
        public Vector3 arrival;
        /// <summary>Raised when something with a CharacterController enters; payload is this region.</summary>
        public static event System.Action<MapRegionTrigger, Collider> Entered;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() != null) Entered?.Invoke(this, other);
        }
    }
}
