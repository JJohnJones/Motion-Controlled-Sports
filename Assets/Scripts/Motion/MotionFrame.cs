using UnityEngine;

namespace MotionControllers
{
    // Transport-independent, converted input. Timestamp is phone monotonic milliseconds,
    // never directly comparable with the PC clock. Vectors are screen-local Unity axes.
    public struct MotionFrame
    {
        public string ControllerId;
        public long Sequence;
        public double TimestampMs;
        public double MotionTimestampMs;
        public double ReceivedAtSeconds;
        public Quaternion Orientation;
        public Vector3 AngularVelocity; // radians/second (axial vector)
        public Vector3 Acceleration; // m/s² (polar vector)
        public Vector3 AccelerationIncludingGravity;
        public bool HasAngularVelocity, HasAcceleration, HasGravity;
        public bool AbsoluteOrientation;
        public float ScreenAngle;
    }
}
