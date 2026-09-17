using UnityEngine;
namespace MotionControllers.Tennis
{
    [System.Serializable] public sealed class TennisMovementSettings
    {
        public float movementSpeed = 7.5f, contactRadius = 1.9f;
        public float acceleration=16, braking=22, predictionSeconds=.45f;
        [Range(0,1)] public float aiMissChance = .12f;
        public float aiSwingSpeed = 7;
    }
    public sealed class TennisPlayer
    {
        public string Id, Name;
        public int Slot;
        public int Team => Slot % 2;
        public bool AI => Id == null;
        public Transform Avatar, Racket;
        public readonly TennisSwing Swing = new TennisSwing();
        public Vector3 Contact => Avatar.TransformPoint(new Vector3(0,1.25f,Team==0?.5f:-.5f));
        public Vector3 MovementVelocity {get;private set;}
        public void ResetMovement()=>MovementVelocity=Vector3.zero;
        public Vector3 Forward => Team == 0 ? Vector3.forward : Vector3.back;
        public bool Miss;
        public Quaternion Face = Quaternion.identity;
        private Quaternion previousPhone, continuousTarget;
        private int orientationRevision = -1;
        private bool wasFresh;
        public Vector3 Home(bool doubles) => new Vector3(doubles ? (Slot < 2 ? -2.5f : 2.5f) * (Team == 0 ? -1 : 1) : 0, 0, Team == 0 ? -8.5f : 8.5f);
        public void Move(Vector3 ball, bool incoming, bool doubles, float dt, TennisMovementSettings settings, Vector3 ballVelocity=default)
        {
            Vector3 target = Home(doubles);
            if (incoming) {
                bool onOurSide=TennisCourt.Side(ball)==Team;
                float intercept= Mathf.Abs(ballVelocity.z)>.1f ? Mathf.Clamp((Avatar.position.z-ball.z)/ballVelocity.z,0,settings.predictionSeconds) : 0;
                ball+=ballVelocity*intercept;
                bool coversLeft = (Slot < 2) == (Team == 1);
                float min = doubles && !coversLeft ? -.3f : -5;
                float max = doubles && coversLeft ? .3f : 5;
                target.x = Mathf.Clamp(ball.x, min, max);
                if(onOurSide)target.z = Team == 0 ? Mathf.Clamp(ball.z, -11, -2) : Mathf.Clamp(ball.z, 2, 11);
            }
            if(dt<=0)return;
            var offset=target-Avatar.position;offset.y=0;
            float speed=Mathf.Min(settings.movementSpeed,Mathf.Sqrt(2*Mathf.Max(.1f,settings.braking)*offset.magnitude));
            var desired=offset.magnitude<.08f?Vector3.zero:offset.normalized*speed;
            float rate=Vector3.Dot(MovementVelocity,desired)<0 || desired.sqrMagnitude<MovementVelocity.sqrMagnitude?settings.braking:settings.acceleration;
            MovementVelocity=Vector3.MoveTowards(MovementVelocity,desired,Mathf.Max(.1f,rate)*dt);
            var next=Avatar.position+MovementVelocity*dt;
            next.x=Mathf.Clamp(next.x,-5,5);next.z=Team==0?Mathf.Clamp(next.z,-11.8f,-1.8f):Mathf.Clamp(next.z,1.8f,11.8f);
            Avatar.position=next;
        }
        public static Quaternion RacketRelativeRotation(ControllerSession session)
        {
            if (!session.HasCalibrationDeviceAngles) return session.RawRotation;
            var angles = session.CalibrationDeviceAngles;
            // Preserve calibrated heading, but use an upright portrait phone as Tennis's grip.
            var browserReference = Quaternion.AngleAxis(angles.x, Vector3.forward) *
                Quaternion.AngleAxis(angles.y, Vector3.right) * Quaternion.AngleAxis(angles.z, Vector3.up);
            var browserUpright = Quaternion.AngleAxis(angles.x, Vector3.forward) * Quaternion.AngleAxis(90, Vector3.right);
            var reference = DeviceCoordinates.Orientation(browserReference, session.Latest.ScreenAngle);
            var upright = DeviceCoordinates.Orientation(browserUpright, session.Latest.ScreenAngle);
            var neutralRelative = Quaternion.Inverse(reference) * upright;
            return Quaternion.Inverse(neutralRelative) * session.RawRotation;
        }
        public void Orient(ControllerSession session, double now, float dt, TennisSwingSettings settings)
        {
            Swing.Read(session,now,settings);
            bool fresh = session.IsCalibrated && session.HasFrame && now - session.Latest.ReceivedAtSeconds <= .35;
            if (!fresh) { wasFresh = false; return; }
            var phone = RacketRelativeRotation(session);
            if (!wasFresh || orientationRevision != session.CalibrationRevision)
            { previousPhone = continuousTarget = phone; orientationRevision = session.CalibrationRevision; }
            else
            {
                // Scale small relative steps, never clamp the shortest absolute angle at 180 degrees.
                var delta = Quaternion.Inverse(previousPhone) * phone;
                continuousTarget = (continuousTarget * Quaternion.SlerpUnclamped(Quaternion.identity, delta, settings.orientationSensitivity)).normalized;
                previousPhone = phone;
            }
            wasFresh = true;
            Face = continuousTarget;
            var world = Quaternion.LookRotation(Forward) * Face;
            var smooth = Quaternion.Slerp(Racket.rotation, world, 1 - Mathf.Exp(-settings.responseSpeed * dt));
            Racket.rotation = Quaternion.RotateTowards(Racket.rotation, smooth, settings.maximumVisualDegreesPerSecond * dt);
            var anchor = Racket.localPosition;
            anchor.x = (session.LeftHanded ? -.65f : .65f) * (Team == 0 ? 1 : -1);
            Racket.localPosition = anchor;
        }
    }
}
