using System;
using UnityEngine;

namespace MotionControllers.Bowling
{
    [Serializable]
    public sealed class BowlingReleaseSettings
    {
        [Min(0.01f), Tooltip("Minimum effective angular speed in radians/second.")]
        public float minimumSwingSpeed = 0.75f;
        [Min(0.02f), Tooltip("Effective radians/second mapped to maximum ball speed.")]
        public float maximumSwingSpeed = 10f;
        [Min(0.1f)] public float minimumBallVelocity = 3.5f;
        [Min(0.1f)] public float maximumBallVelocity = 12f;
        [Range(0.1f, 0.3f)] public float releaseSamplingWindow = 0.2f;
        [Range(0.1f, 4f)] public float sensitivity = 1.5f;
    }

    public struct BowlingRelease
    {
        public bool Valid;
        public string Reason;
        public double TimestampMs;
        public float CurrentAngularSpeed, PeakAngularSpeed, EffectiveSwingSpeed, BallSpeed;
        public Vector3 AngularVelocity, Acceleration;
        public bool HasAcceleration;
        public Quaternion PhoneOrientation;
        public float WristRollDegrees; // Extension point: not applied as hook in this prototype.
    }

    public static class BowlingReleaseCalculator
    {
        public static BowlingRelease Calculate(ControllerSession session, double heldSinceMs, double releaseMs, BowlingReleaseSettings settings)
        {
            var result = new BowlingRelease { TimestampMs = releaseMs, Reason = "No fresh gyroscope sample at release." };
            double start = Math.Max(heldSinceMs, releaseMs - Mathf.Clamp(settings.releaseSamplingWindow, 0.1f, 0.3f) * 1000);
            double newestGyro = -1;
            for (int i = 0; i < session.HistoryCount; i++)
            {
                var frame = session.GetHistoryFromNewest(i);
                // Sensor time, not packet arrival time; excludes pre-hold peaks and future samples.
                if (frame.TimestampMs > releaseMs || !frame.HasAngularVelocity ||
                    frame.MotionTimestampMs < start || frame.MotionTimestampMs > releaseMs) continue;
                float speed = frame.AngularVelocity.magnitude;
                result.PeakAngularSpeed = Mathf.Max(result.PeakAngularSpeed, speed);
                if (frame.MotionTimestampMs > newestGyro)
                {
                    newestGyro = frame.MotionTimestampMs;
                    result.CurrentAngularSpeed = speed;
                    result.AngularVelocity = frame.AngularVelocity;
                    result.Acceleration = frame.Acceleration;
                    result.HasAcceleration = frame.HasAcceleration;
                }
            }
            if (!session.HasFrame || session.Latest.TimestampMs > releaseMs || releaseMs - session.Latest.TimestampMs > 120 ||
                newestGyro < 0 || releaseMs - newestGyro > 120) return result;
            result.PhoneOrientation = session.RawRotation;
            Vector3 right = result.PhoneOrientation * Vector3.right;
            result.WristRollDegrees = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
            // Peak tolerates event cadence near lift-off; current speed keeps the release relevant.
            result.EffectiveSwingSpeed = (0.65f * result.PeakAngularSpeed + 0.35f * result.CurrentAngularSpeed) * Mathf.Max(0.1f, settings.sensitivity);
            float minimum = Mathf.Max(0.01f, settings.minimumSwingSpeed);
            if (result.EffectiveSwingSpeed < minimum) { result.Reason = "Swing too slow. Hold, swing, then release again."; return result; }
            float t = Mathf.InverseLerp(minimum, Mathf.Max(minimum + 0.01f, settings.maximumSwingSpeed), result.EffectiveSwingSpeed);
            float low = Mathf.Max(0.1f, settings.minimumBallVelocity);
            result.BallSpeed = Mathf.Lerp(low, Mathf.Max(low, settings.maximumBallVelocity), t);
            result.Valid = true; result.Reason = "Released";
            return result;
        }
    }
}
