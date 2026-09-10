using UnityEngine;

namespace MotionControllers.Bowling
{
    public sealed class BowlingPinRack : MonoBehaviour
    {
        public Rigidbody[] pins;
        private Vector3[] positions;
        private Quaternion[] rotations;
        public void CaptureStartingPoses()
        {
            positions = new Vector3[pins.Length]; rotations = new Quaternion[pins.Length];
            for (int i = 0; i < pins.Length; i++)
            { positions[i] = pins[i].position; rotations[i] = pins[i].rotation; }
        }
        public void ResetPins()
        {
            if (positions == null) CaptureStartingPoses();
            for (int i = 0; i < pins.Length; i++)
            {
                var pin = pins[i];
                pin.linearVelocity = Vector3.zero; pin.angularVelocity = Vector3.zero;
                pin.position = positions[i]; pin.rotation = rotations[i];
                pin.transform.SetPositionAndRotation(positions[i], rotations[i]);
                pin.Sleep();
            }
        }
    }
}
