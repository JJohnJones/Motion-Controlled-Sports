using System;
using UnityEngine;

namespace MotionControllers
{
    [Serializable]
    public sealed class ControllerMessage
    {
        public int version;
        public string type, token, controllerId;
        public long sequence;
        public double timestamp, motionTimestamp;
        public Quaternion orientation;
        public Vector3 angularVelocity, acceleration, accelerationIncludingGravity;
        public bool hasAngularVelocity, hasAcceleration, hasGravity, absoluteOrientation;
        public float screenAngle;
        public string button, phase;
        public long buttonSequence;
        public double eventTimestamp;
        public bool hasSnapshot;
    }

    public static class MotionJsonCodec
    {
        public static bool TryDecode(string json, out ControllerMessage message)
        {
            message = null;
            try { message = JsonUtility.FromJson<ControllerMessage>(json); }
            catch (ArgumentException) { return false; }
            return message != null && message.version == 1 && message.type != null;
        }

        public static bool TryFrame(ControllerMessage p, string assignedId, double received, out MotionFrame frame)
        {
            frame = default;
            if (p.controllerId != assignedId || p.sequence < 1 || p.sequence > 9007199254740991L ||
                !Finite(p.timestamp) || p.timestamp < 0 || !Finite(p.motionTimestamp) || p.motionTimestamp < 0 || !Finite(p.screenAngle) ||
                Mathf.Abs(p.screenAngle) > 360 || !Finite(p.orientation.x) || !Finite(p.orientation.y) ||
                !Finite(p.orientation.z) || !Finite(p.orientation.w)) return false;
            float magnitude = p.orientation.x * p.orientation.x + p.orientation.y * p.orientation.y +
                p.orientation.z * p.orientation.z + p.orientation.w * p.orientation.w;
            if (magnitude < 0.5f || magnitude > 1.5f ||
                (p.hasAngularVelocity && !Valid(p.angularVelocity)) ||
                (p.hasAcceleration && !Valid(p.acceleration)) || (p.hasGravity && !Valid(p.accelerationIncludingGravity)))
                return false;
            frame = new MotionFrame
            {
                ControllerId = assignedId, Sequence = p.sequence, TimestampMs = p.timestamp, MotionTimestampMs = p.motionTimestamp,
                ReceivedAtSeconds = received, Orientation = DeviceCoordinates.Orientation(p.orientation.normalized, p.screenAngle),
                AngularVelocity = p.hasAngularVelocity ? DeviceCoordinates.AngularVelocity(p.angularVelocity, p.screenAngle) : Vector3.zero,
                Acceleration = p.hasAcceleration ? DeviceCoordinates.Acceleration(p.acceleration, p.screenAngle) : Vector3.zero,
                AccelerationIncludingGravity = p.hasGravity ? DeviceCoordinates.Acceleration(p.accelerationIncludingGravity, p.screenAngle) : Vector3.zero,
                HasAngularVelocity = p.hasAngularVelocity, HasAcceleration = p.hasAcceleration, HasGravity = p.hasGravity,
                AbsoluteOrientation = p.absoluteOrientation, ScreenAngle = p.screenAngle
            };
            return true;
        }
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static bool TryButton(ControllerMessage p, string assignedId, double received, out ControllerButtonEvent input)
        {
            input = default;
            if (p.controllerId != assignedId || p.button != "primary" || p.buttonSequence < 1 ||
                p.buttonSequence > 9007199254740991L || !Finite(p.eventTimestamp) || p.eventTimestamp < 0) return false;
            ButtonPhase phase;
            switch (p.phase)
            {
                case "pressed": phase = ButtonPhase.Pressed; break;
                case "released": phase = ButtonPhase.Released; break;
                case "canceled": phase = ButtonPhase.Canceled; break;
                default: return false;
            }
            MotionFrame snapshot = default;
            if (p.hasSnapshot && (!TryFrame(p, assignedId, received, out snapshot) ||
                snapshot.TimestampMs > p.eventTimestamp || p.eventTimestamp - snapshot.TimestampMs > 250 ||
                ((snapshot.HasAngularVelocity || snapshot.HasAcceleration || snapshot.HasGravity) &&
                 (snapshot.MotionTimestampMs > p.eventTimestamp || p.eventTimestamp - snapshot.MotionTimestampMs > 250)))) return false;
            input = new ControllerButtonEvent { ControllerId = assignedId, Button = ControllerButton.Primary,
                Phase = phase, Sequence = p.buttonSequence, TimestampMs = p.eventTimestamp, ReceivedAtSeconds = received,
                HasSnapshot = p.hasSnapshot, Snapshot = snapshot };
            return true;
        }
        private static bool Valid(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z) &&
            Math.Abs(v.x) < 100000 && Math.Abs(v.y) < 100000 && Math.Abs(v.z) < 100000;
    }
}
