using UnityEngine;

namespace MotionControllers.Bowling
{
    public sealed class BowlingPinRack : MonoBehaviour
    {
        public Rigidbody[] pins;
        [Range(0.1f, 0.3f), Tooltip("Local center-of-mass height; a modestly higher center lets slow impacts topple pins instead of only sliding them.")]
        public float centerOfMassHeight = 0.22f;
        private Vector3[] positions;
        private Quaternion[] rotations;
        public void CaptureStartingPoses()
        {
            positions = new Vector3[pins.Length]; rotations = new Quaternion[pins.Length];
            for (int i = 0; i < pins.Length; i++)
            { pins[i].centerOfMass = new Vector3(0, centerOfMassHeight, 0); positions[i] = pins[i].position; rotations[i] = pins[i].rotation; }
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
