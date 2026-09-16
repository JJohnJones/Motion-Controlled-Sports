using UnityEngine;

namespace MotionControllers
{
    public sealed class ControllerSession
    {
        private readonly MotionFrame[] history = new MotionFrame[120];
        private int nextHistory;
        private Quaternion reference = Quaternion.identity;
        private float calibrationScreenAngle;
        public string Id { get; }
        public MotionFrame Latest { get; private set; }
        public bool HasFrame { get; private set; }
        public bool IsCalibrated { get; private set; }
        public bool NeedsCalibration => !IsCalibrated;
        public Quaternion RawRotation { get; private set; } = Quaternion.identity;
        public Quaternion SmoothedRotation { get; private set; } = Quaternion.identity;
        public int HistoryCount { get; private set; }
        public long ReceivedFrames { get; private set; }
        public long SequenceGaps { get; private set; }
        public int CalibrationRevision { get; private set; }
        public bool PrimaryHeld { get; private set; }
        public ButtonPhase LastButtonPhase { get; private set; } = ButtonPhase.Canceled;
        public long LastButtonSequence { get; private set; }
        public double LastButtonTimestampMs { get; private set; }
        public Vector3 CalibrationDeviceAngles { get; private set; }
        public bool HasCalibrationDeviceAngles { get; private set; }
        public Vector3 CalibrationForwardAxis => Quaternion.AngleAxis(calibrationScreenAngle, Vector3.forward) * Vector3.up;
        public Vector3 AccelerationInCalibrationAxes(MotionFrame frame) =>
            Quaternion.Inverse(reference) * (frame.Orientation * frame.Acceleration);

        public ControllerSession(string id) { Id = id; }

        public bool Accept(MotionFrame frame, bool calibrate = false)
        {
            if (HasFrame && (frame.Sequence <= Latest.Sequence || frame.TimestampMs < Latest.TimestampMs))
                return false;
            if (HasFrame) SequenceGaps += System.Math.Max(0, frame.Sequence - Latest.Sequence - 1);
            Latest = frame;
            HasFrame = true;
            ReceivedFrames++;
            history[nextHistory] = frame;
            nextHistory = (nextHistory + 1) % history.Length;
            HistoryCount = System.Math.Min(HistoryCount + 1, history.Length);
            if (IsCalibrated && Mathf.Abs(Mathf.DeltaAngle(calibrationScreenAngle, frame.ScreenAngle)) > 1)
                IsCalibrated = false; // screen rotation changes local axes: explicitly recalibrate
            if (calibrate) Calibrate();
            if (IsCalibrated) RawRotation = Quaternion.Inverse(reference) * frame.Orientation;
            return true;
        }

        public bool Calibrate()
        {
            if (!HasFrame) return false;
            reference = Latest.Orientation;
            CalibrationDeviceAngles = Latest.DeviceAnglesDegrees;
            HasCalibrationDeviceAngles = Latest.HasDeviceAngles;
            calibrationScreenAngle = Latest.ScreenAngle;
            IsCalibrated = true;
            CalibrationRevision++;
            RawRotation = SmoothedRotation = Quaternion.identity;
            return true;
        }

        public bool AcceptButton(ControllerButtonEvent input)
        {
            if (input.ControllerId != Id || input.Button != ControllerButton.Primary ||
                input.Sequence <= LastButtonSequence || input.TimestampMs < LastButtonTimestampMs ||
                double.IsNaN(input.TimestampMs) || double.IsInfinity(input.TimestampMs) || input.TimestampMs < 0) return false;
            // Do not turn duplicate presses, orphan releases, or replayed packets into throws.
            if ((input.Phase == ButtonPhase.Pressed && PrimaryHeld) ||
                (input.Phase != ButtonPhase.Pressed && !PrimaryHeld)) return false;
            if (input.HasSnapshot && !Accept(input.Snapshot)) return false;
            PrimaryHeld = input.Phase == ButtonPhase.Pressed;
            LastButtonPhase = input.Phase;
            LastButtonSequence = input.Sequence;
            LastButtonTimestampMs = input.TimestampMs;
            return true;
        }

        public void UpdateSmoothing(float deltaTime, float timeConstant)
        {
            SmoothedRotation = timeConstant <= 0 ? RawRotation : Quaternion.Slerp(
                SmoothedRotation, RawRotation, 1 - Mathf.Exp(-deltaTime / timeConstant));
        }
        internal void CancelHeldInput() { PrimaryHeld = false; LastButtonPhase = ButtonPhase.Canceled; }

        public MotionFrame GetHistoryFromNewest(int offset)
        {
            if (offset < 0 || offset >= HistoryCount) throw new System.ArgumentOutOfRangeException(nameof(offset));
            return history[(nextHistory - 1 - offset + history.Length) % history.Length];
        }
    }
}
