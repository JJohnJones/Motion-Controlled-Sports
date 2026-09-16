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
        private bool initialized, paused, matchManaged, turnInputEnabled = true;
        private double pauseStarted;
        private string assignedControllerId;
        public void AssignController(string id) {
            if (State == BowlingState.Holding && id != ActiveControllerId) CancelHold("Turn changed.");
            assignedControllerId = ActiveControllerId = id;
        }
        public void ManageMatch() { matchManaged = true; turnInputEnabled = false; }
        public void LockTurn() { turnInputEnabled = false; if (State == BowlingState.Holding) CancelHold("Wait for your turn."); }
        public void PrepareTurn(string id)
        {
            Initialize(); AssignController(id); ball.ResetBall(); State = BowlingState.Ready;
            AimDegrees = 0; ButtonState = "Not pressed"; turnInputEnabled = true;
            Message = "Your turn. Aim, hold, swing, and release.";
        }
        public event System.Action<BowlingRelease> ThrowLaunched;
        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            if (paused)
            {
                pauseStarted = Time.realtimeSinceStartupAsDouble;
                if (State == BowlingState.Holding) CancelHold("Hold canceled while paused. Start a new hold after resuming.");
            }
            else stateSince += Time.realtimeSinceStartupAsDouble - pauseStarted;
        }

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
            if (ball == null || pinRack == null || ControllerInput.Resolve(inputSource) == null)
            { Message = "Assign input source, ball and pin rack in the Inspector."; enabled = false; return; }
            pinRack.CaptureStartingPoses(); ball.ResetBall(); Subscribe();
        }
        private void Subscribe()
        {
            source = ControllerInput.Resolve(inputSource) as IControllerButtonSource;
            if (source == null) { Message = "Input source does not support controller buttons."; return; }
            source.ButtonChanged -= HandleButton; source.ButtonChanged += HandleButton;
        }
        private bool Fresh(ControllerSession session) => session.HasFrame && session.IsCalibrated &&
            Time.realtimeSinceStartupAsDouble - session.Latest.ReceivedAtSeconds <= 0.5;
        private float ReadAim(ControllerSession session)
        {
            if (!session.HasCalibrationDeviceAngles || !session.Latest.HasDeviceAngles) return 0;
            float alpha = Mathf.DeltaAngle(session.CalibrationDeviceAngles.x, session.Latest.DeviceAnglesDegrees.x);
            // Positive browser alpha turns the phone counter-clockwise (left when screen-up).
            return Mathf.Clamp(-alpha * aimSensitivity, -maximumAimDegrees, maximumAimDegrees);
        }
        public void HandleButton(ControllerButtonEvent input)
        {
            if (paused || !turnInputEnabled || source == null || input.Button != ControllerButton.Primary) return;
            if (State == BowlingState.Rolling || State == BowlingState.Resetting) return;
            if (assignedControllerId != null && assignedControllerId != input.ControllerId) return;
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
                ball.Launch(direction.normalized * LastRelease.BallSpeed, LastRelease.Spin, release.hookAcceleration);
                State = BowlingState.Rolling; stateSince = Time.realtimeSinceStartupAsDouble; stoppedSeconds = 0;
                Message = "Ball rolling. Wait for reset.";
                ThrowLaunched?.Invoke(LastRelease);
            }
        }
        private void CancelHold(string reason)
        {
            State = BowlingState.Ready; Message = reason;
            if (ball != null && ball.releasePoint != null) ball.ResetBall();
        }
        private void Update()
        {
            if (paused || source == null || ball == null || pinRack == null) return;
            ControllerSession session = null;
            if (ActiveControllerId != null && !source.TryGetController(ActiveControllerId, out session))
            {
                ActiveControllerId = null;
                if (State == BowlingState.Holding) CancelHold("Controller disconnected. Reconnect and calibrate.");
            }
            if (ActiveControllerId == null)
                foreach (var candidate in source.Sessions.Values)
                    if ((assignedControllerId == null || candidate.Id == assignedControllerId) && Fresh(candidate)) { session = candidate; ActiveControllerId = candidate.Id; break; }
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
            if (!matchManaged && State == BowlingState.Rolling)
            {
                Vector3 p = ball.Body.position;
                stoppedSeconds = ball.Body.linearVelocity.sqrMagnitude < 0.04f ? stoppedSeconds + Time.unscaledDeltaTime : 0;
                bool outside = p.y < -1 || Mathf.Abs(p.x) > 3 || p.z > 22 || p.z < -2;
                if (outside || now - stateSince > maximumRollSeconds || (now - stateSince > 2 && stoppedSeconds > 1))
                { State = BowlingState.Resetting; stateSince = now; Message = "Resetting ball and pins…"; }
            }
            else if (!matchManaged && State == BowlingState.Resetting && now - stateSince >= resetDelaySeconds)
            {
                ball.ResetBall(); pinRack.ResetPins(); State = BowlingState.Ready;
                Message = "Ready. Aim, press and hold, swing, then release.";
            }
            if (aimIndicator != null)
            {
                aimIndicator.enabled = turnInputEnabled && (State == BowlingState.Ready || State == BowlingState.Holding);
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
            GUILayout.Label($"Forward acceleration: {LastRelease.ForwardAcceleration:F2} m/s² | Beta speed: {LastRelease.ForwardBetaSpeed:F1}°/s");
            GUILayout.Label($"Wrist gamma: {LastRelease.WristRollDegrees:F1}° | Spin: {LastRelease.Spin:F2}");
        }
    }
}
