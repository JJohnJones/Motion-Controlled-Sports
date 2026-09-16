using UnityEngine;

namespace MotionControllers.Bowling
{
    public sealed class BowlingPinRack : MonoBehaviour
    {
        public Rigidbody[] pins;
        [Range(0.1f, 0.3f), Tooltip("Local center-of-mass height; a modestly higher center lets slow impacts topple pins instead of only sliding them.")]
        public float centerOfMassHeight = 0.22f;
        [Range(15, 85)] public float knockdownAngle = 45;
        [Min(.05f)] public float fallenDistance = .2f;
        [Tooltip("Extra local-space clearance around the starting rack. Bounds move with this rack's transform.")]
        public Vector3 deckClearance = new Vector3(1.2f, 1f, 2.5f);
        private Bounds deckBounds;
        private bool[] outOfPlay;
        private bool[] standing;
        public Bounds LocalDeckBounds => deckBounds;
        public void CleanupOutOfPlay()
        {
            CaptureStartingPoses();
            for (int i = 0; i < pins.Length; i++)
                if (standing[i] && !outOfPlay[i] && !deckBounds.Contains(transform.InverseTransformPoint(pins[i].position)))
                {
                    outOfPlay[i] = true;
                    pins[i].linearVelocity = Vector3.zero; pins[i].angularVelocity = Vector3.zero;
                    pins[i].Sleep(); pins[i].gameObject.SetActive(false);
                }
        }

        public int StandingCount { get { int count = 0; if (standing != null) foreach (bool pin in standing) if (pin) count++; return count; } }
        private Vector3[] positions;
        private Quaternion[] rotations;
        public void CaptureStartingPoses()
        {
            if (positions != null) return;
            standing = new bool[pins.Length]; outOfPlay = new bool[pins.Length];
            positions = new Vector3[pins.Length]; rotations = new Quaternion[pins.Length];
            for (int i = 0; i < pins.Length; i++)
            { standing[i] = true; pins[i].centerOfMass = new Vector3(0, centerOfMassHeight, 0); positions[i] = pins[i].position; rotations[i] = pins[i].rotation; }
            if (pins.Length == 0) return;
            deckBounds = new Bounds(transform.InverseTransformPoint(positions[0]), Vector3.zero);
            foreach (var position in positions) deckBounds.Encapsulate(transform.InverseTransformPoint(position));
            deckBounds.Expand(new Vector3(Mathf.Max(.1f, deckClearance.x), Mathf.Max(.1f, deckClearance.y), Mathf.Max(.1f, deckClearance.z)) * 2);

        }
        private void OnDrawGizmosSelected()
        {
            if (positions == null) return;
            Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(deckBounds.center, deckBounds.size);
        }
        public bool IsSettled(float linearThreshold, float angularThreshold)
        {
            CleanupOutOfPlay();
            for (int i = 0; i < pins.Length; i++) if (standing[i] && !outOfPlay[i] && !pins[i].IsSleeping() &&
                (pins[i].linearVelocity.sqrMagnitude > linearThreshold * linearThreshold || pins[i].angularVelocity.sqrMagnitude > angularThreshold * angularThreshold)) return false;
            return true;
        }
        public int CollectNewlyFallen()
        {
            CleanupOutOfPlay(); int count = 0;
            for (int i = 0; i < pins.Length; i++) if (standing[i])
            {
                var pin = pins[i];
                bool fallen = outOfPlay[i] || Vector3.Angle(pin.rotation * Vector3.up, rotations[i] * Vector3.up) >= knockdownAngle ||
                    pin.position.y < positions[i].y - fallenDistance;
                // At the timeout boundary freeze residual motion before advancing the roll.
                pin.linearVelocity = Vector3.zero; pin.angularVelocity = Vector3.zero; pin.Sleep();
                if (fallen) { standing[i] = false; pin.gameObject.SetActive(false); count++; }
            }
            return count;
        }
        public void ResetPins()
        {
            if (positions == null) CaptureStartingPoses();
            for (int i = 0; i < pins.Length; i++)
            {
                var pin = pins[i]; standing[i] = true; outOfPlay[i] = false; pin.gameObject.SetActive(true);
                pin.linearVelocity = Vector3.zero; pin.angularVelocity = Vector3.zero;
                pin.position = positions[i]; pin.rotation = rotations[i];
                pin.transform.SetPositionAndRotation(positions[i], rotations[i]);
                pin.Sleep();
            }
        }
    }
}
