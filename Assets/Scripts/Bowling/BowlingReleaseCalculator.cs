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
        [Min(0), Tooltip("Minimum linear acceleration toward the phone TOP at calibration, in m/s². Calibrate pointing the phone down the lane.")]
        public float minimumForwardAcceleration = 0.25f;
        [Min(0), Tooltip("Minimum signed beta change near release, in degrees/second.")]
        public float minimumForwardBetaSpeed = 15f;
        [Tooltip("Beta increases during the forward swing for the current grip.")]
        public bool forwardBetaIncreases = true;
        [Range(0, 4), Tooltip("Maximum arcade hook lateral acceleration, in m/s². Zero disables hook.")]
        public float hookAcceleration = 0.25f;
        [Range(5, 80), Tooltip("Relative gamma wrist roll giving maximum hook.")]
        public float fullSpinRollDegrees = 35f;
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
        public float WristRollDegrees;
        public float ForwardAcceleration, ForwardBetaSpeed, Spin;
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
                    result.ForwardAcceleration = frame.HasAcceleration ? Vector3.Dot(session.AccelerationInCalibrationAxes(frame), session.CalibrationForwardAxis) : 0;
                }
            }
            if (!session.HasFrame || session.Latest.TimestampMs > releaseMs || releaseMs - session.Latest.TimestampMs > 120 ||
                newestGyro < 0 || releaseMs - newestGyro > 120) return result;
            result.PhoneOrientation = session.RawRotation;
            if (!session.HasCalibrationDeviceAngles || !session.Latest.HasDeviceAngles)
            { result.Reason = "Update the PWA and recalibrate: alpha/beta/gamma angles are required."; return result; }
            if (!result.HasAcceleration || result.ForwardAcceleration <= Mathf.Max(0, settings.minimumForwardAcceleration))
            { result.Reason = "Release blocked: acceleration must be forward, toward the calibrated phone top."; return result; }
            // Beta comes from the device's pitch angle, not Unity's reconstructed Euler angles.
            bool betaMeasured = false;
            var latest = session.Latest;
            for (int i = 1; i < session.HistoryCount; i++)
            {
                var older = session.GetHistoryFromNewest(i);
                double dt = latest.TimestampMs - older.TimestampMs;
                if (!older.HasDeviceAngles || older.TimestampMs < heldSinceMs || dt < 40 || dt > 150) continue;
                result.ForwardBetaSpeed = Mathf.DeltaAngle(older.DeviceAnglesDegrees.y, latest.DeviceAnglesDegrees.y) /
                    (float)(dt / 1000) * (settings.forwardBetaIncreases ? 1 : -1);
                betaMeasured = true; break;
            }
            if (!betaMeasured || result.ForwardBetaSpeed <= Mathf.Max(0, settings.minimumForwardBetaSpeed))
            { result.Reason = "Release blocked: beta must increase during the forward swing."; return result; }
            result.WristRollDegrees = Mathf.DeltaAngle(session.CalibrationDeviceAngles.z, latest.DeviceAnglesDegrees.z);
            result.Spin = Mathf.Clamp(result.WristRollDegrees / Mathf.Max(5, settings.fullSpinRollDegrees), -1, 1);
            // Peak tolerates event cadence near lift-off; current speed keeps the release relevant.
            result.EffectiveSwingSpeed = (0.65f * result.PeakAngularSpeed + 0.35f * result.CurrentAngularSpeed) * Mathf.Max(0.1f, settings.sensitivity) * session.MotionSensitivity;
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
