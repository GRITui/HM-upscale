using UnityEngine;

namespace HM.Gameplay
{
    /// <summary>Swings arm_l/arm_r/leg_l/leg_r child nodes of a glb character while the parent moves. No-op if nodes are missing.</summary>
    public sealed class WalkBob : MonoBehaviour
    {
        public float swingDegrees = 28f;
        public float cyclesPerMetre = 0.9f;
        Transform armL, armR, legL, legR;
        Vector3 last;
        float phase;
        bool bound;

        void Start() { last = transform.position; }

        void Bind()
        {
            bound = true;
            armL = Find("arm_l"); armR = Find("arm_r"); legL = Find("leg_l"); legR = Find("leg_r");
        }

        Transform Find(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        void LateUpdate()
        {
            if (!bound) Bind();
            var p = transform.position;
            var d = p - last; d.y = 0f; last = p;
            float speed = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
            bool moving = speed > 0.2f;
            if (moving) phase += d.magnitude * cyclesPerMetre * Mathf.PI * 2f;
            float a = moving ? Mathf.Sin(phase) * swingDegrees : 0f;
            Swing(armL, a); Swing(armR, -a); Swing(legL, -a); Swing(legR, a);
        }

        static void Swing(Transform t, float deg)
        {
            if (t == null) return;
            t.localRotation = Quaternion.Euler(deg, 0f, 0f);
        }
    }
}
