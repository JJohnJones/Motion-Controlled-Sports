using UnityEngine;

namespace MotionControllers.Bowling
{
    public enum BowlingState { Ready, Holding, Rolling, Resetting }

    public sealed class BowlingThrowController : MonoBehaviour, IControllerGameDebug
    {
        [Tooltip("A component implementing IControllerButtonSource (ControllerManager).")]
        public MonoBehaviour inputSource;
        public BowlingBall ball;
        public BowlingPinRack pinRack;
        public LineRenderer aimIndicator;
        public BowlingReleaseSettings release = new BowlingReleaseSettings();
        [Range(0.05f, 1f)] public float aimSensitivity = 0.3f;
        [Range(1, 12)] public float maximumAimDegrees = 6f;
        [Min(2)] public float maximumRollSeconds = 12f;
        [Min(0.1f)] public float resetDelaySeconds = 2f;
        public BowlingState State { get; private set; } = BowlingState.Ready;
        public string ActiveControllerId { get; private set; }
        public string Message { get; private set; } = "Connect and calibrate, then aim and hold.";
        public float AimDegrees { get; private set; }
        public BowlingRelease LastRelease { get; private set; }
        public float CurrentAngularSpeed { get; private set; }
        public float RecentPeakSpeed { get; private set; }
        public string ButtonState { get; private set; } = "Not pressed";
        private IControllerButtonSource source;
        private double heldSince, stateSince;
        private int calibrationRevision;
        private float stoppedSeconds;
        private bool initialized;

        private void Start() { Initialize(); }
        private void OnEnable() { if (initialized) Subscribe(); }
        private void OnDisable()
        {
            if (source != null) source.ButtonChanged -= HandleButton;
            if (State == BowlingState.Holding) CancelHold("Hold canceled: game disabled.");
        }
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (ball == null || pinRack == null || inputSource == null)
            { Message = "Assign input source, ball and pin rack in the Inspector."; enabled = false; return; }
            pinRack.CaptureStartingPoses(); ball.ResetBall(); Subscribe();
        }
        private void Subscribe()
        {
            source = inputSource as IControllerButtonSource;
            if (source == null) { Message = "Input source does not support controller buttons."; return; }
            source.ButtonChanged -= HandleButton; source.ButtonChanged += HandleButton;
        }
        private bool Fresh(ControllerSession session) => session.HasFrame && session.IsCalibrated &&
            Time.realtimeSinceStartupAsDouble - session.Latest.ReceivedAtSeconds <= 0.5;
        private float ReadAim(ControllerSession session)
        {
            var forward = session.RawRotation * Vector3.forward;
            return Mathf.Clamp(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg * aimSensitivity, -maximumAimDegrees, maximumAimDegrees);
        }
        public void HandleButton(ControllerButtonEvent input)
        {
            if (source == null || input.Button != ControllerButton.Primary) return;
            if (State == BowlingState.Rolling || State == BowlingState.Resetting) return;
            if (ActiveControllerId != null && ActiveControllerId != input.ControllerId) return;
            if (!source.TryGetController(input.ControllerId, out var session)) return;
            ActiveControllerId = input.ControllerId;
            ButtonState = input.Phase.ToString();
            if (input.Phase == ButtonPhase.Canceled) { CancelHold("Touch canceled. Press again when ready."); return; }
            if (input.Phase == ButtonPhase.Pressed && State == BowlingState.Ready)
            {
                if (!input.HasSnapshot || !Fresh(session)) { Message = "Calibrate and wait for fresh motion before holding."; return; }
                AimDegrees = ReadAim(session);
                heldSince = input.TimestampMs; calibrationRevision = session.CalibrationRevision;
                State = BowlingState.Holding; RecentPeakSpeed = 0;
                Message = "Aim locked. Swing, then lift your finger to release.";
            }
            else if (input.Phase == ButtonPhase.Released && State == BowlingState.Holding)
            {
                if (!input.HasSnapshot || !Fresh(session) || calibrationRevision != session.CalibrationRevision)
                { CancelHold("Release canceled: stale input or calibration changed."); return; }
                LastRelease = BowlingReleaseCalculator.Calculate(session, heldSince, input.TimestampMs, release);
                RecentPeakSpeed = LastRelease.PeakAngularSpeed;
                if (!LastRelease.Valid) { CancelHold(LastRelease.Reason); return; }
                Vector3 direction = Quaternion.AngleAxis(AimDegrees, Vector3.up) * ball.releasePoint.forward;
                ball.Launch(direction.normalized * LastRelease.BallSpeed);
                State = BowlingState.Rolling; stateSince = Time.realtimeSinceStartupAsDouble; stoppedSeconds = 0;
                Message = "Ball rolling. Wait for reset.";
            }
        }
        private void CancelHold(string reason)
        {
            State = BowlingState.Ready; Message = reason;
            if (ball != null && ball.releasePoint != null) ball.ResetBall();
        }
        private void Update()
        {
            if (source == null || ball == null || pinRack == null) return;
            ControllerSession session = null;
            if (ActiveControllerId != null && !source.TryGetController(ActiveControllerId, out session))
            {
                ActiveControllerId = null;
                if (State == BowlingState.Holding) CancelHold("Controller disconnected. Reconnect and calibrate.");
            }
            if (ActiveControllerId == null)
                foreach (var candidate in source.Sessions.Values)
                    if (Fresh(candidate)) { session = candidate; ActiveControllerId = candidate.Id; break; }
            if (session != null)
            {
                CurrentAngularSpeed = session.Latest.HasAngularVelocity ? session.Latest.AngularVelocity.magnitude : 0;
                if (State == BowlingState.Ready && Fresh(session)) AimDegrees = ReadAim(session);
                if (State == BowlingState.Holding)
                {
                    if (!Fresh(session) || session.CalibrationRevision != calibrationRevision)
                        CancelHold("Hold canceled: motion stopped or calibration changed. Press again.");
                    else RecentPeakSpeed = BowlingReleaseCalculator.Calculate(session, heldSince, session.Latest.TimestampMs, release).PeakAngularSpeed;
                }
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (State == BowlingState.Rolling)
            {
                Vector3 p = ball.Body.position;
                stoppedSeconds = ball.Body.linearVelocity.sqrMagnitude < 0.04f ? stoppedSeconds + Time.unscaledDeltaTime : 0;
                bool outside = p.y < -1 || Mathf.Abs(p.x) > 3 || p.z > 22 || p.z < -2;
                if (outside || now - stateSince > maximumRollSeconds || (now - stateSince > 2 && stoppedSeconds > 1))
                { State = BowlingState.Resetting; stateSince = now; Message = "Resetting ball and pins…"; }
            }
            else if (State == BowlingState.Resetting && now - stateSince >= resetDelaySeconds)
            {
                ball.ResetBall(); pinRack.ResetPins(); State = BowlingState.Ready;
                Message = "Ready. Aim, press and hold, swing, then release.";
            }
            if (aimIndicator != null)
            {
                aimIndicator.enabled = State == BowlingState.Ready || State == BowlingState.Holding;
                Vector3 origin = ball.releasePoint.position; origin.y = 0.025f;
                Vector3 direction = Quaternion.AngleAxis(AimDegrees, Vector3.up) * ball.releasePoint.forward;
                aimIndicator.SetPosition(0, origin); aimIndicator.SetPosition(1, origin + direction * 16);
            }
        }
        public void DrawControllerDebug()
        {
            GUILayout.Label("BOWLING · " + State);
            GUILayout.Label(Message);
            GUILayout.Label($"Button: {ButtonState} | Aim: {AimDegrees:F1}°");
            GUILayout.Label($"Angular: {CurrentAngularSpeed:F2} rad/s | Recent peak: {RecentPeakSpeed:F2} rad/s");
            GUILayout.Label($"Release: {LastRelease.TimestampMs:F1} ms (phone clock)");
            GUILayout.Label($"Swing: {LastRelease.EffectiveSwingSpeed:F2} rad/s | Ball: {LastRelease.BallSpeed:F2} m/s");
            GUILayout.Label("Launch velocity: " + (ball != null ? ball.LastLaunchVelocity.ToString("F2") : "—"));
            GUILayout.Label($"Wrist roll: {LastRelease.WristRollDegrees:F1}° (hook not applied)");
        }
    }
}
