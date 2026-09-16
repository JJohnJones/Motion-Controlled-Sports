using UnityEngine;
namespace MotionControllers.Tennis
{
    [System.Serializable] public sealed class TennisMovementSettings
    {
        public float movementSpeed = 7.5f, contactRadius = 1.9f;
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
        public Vector3 Contact => Avatar.position + Vector3.up * 1.25f + Forward * .5f;
        public Vector3 Forward => Team == 0 ? Vector3.forward : Vector3.back;
        public bool Miss;
        public Quaternion Face = Quaternion.identity;
        public Vector3 Home(bool doubles) => new Vector3(doubles ? (Slot < 2 ? -2.5f : 2.5f) * (Team == 0 ? -1 : 1) : 0, 0, Team == 0 ? -8.5f : 8.5f);
        public void Move(Vector3 ball, bool incoming, bool doubles, float dt, TennisMovementSettings settings)
        {
            Vector3 target = Home(doubles);
            if (incoming) {
                bool coversLeft = (Slot < 2) == (Team == 1);
                float min = doubles && !coversLeft ? -.3f : -5;
                float max = doubles && coversLeft ? .3f : 5;
                target.x = Mathf.Clamp(ball.x, min, max);
                target.z = Team == 0 ? Mathf.Clamp(ball.z, -11, -2) : Mathf.Clamp(ball.z, 2, 11);
            }
            Avatar.position = Vector3.MoveTowards(Avatar.position, target, settings.movementSpeed * dt);
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
            if (!session.IsCalibrated) return;
            Face = RacketRelativeRotation(session);
            var desired = Quaternion.SlerpUnclamped(Quaternion.identity, Face, settings.orientationSensitivity);
            desired = Quaternion.RotateTowards(Quaternion.identity,desired,settings.maximumVisualRotation);
            Face = desired;
            var world = Quaternion.LookRotation(Forward) * desired;
            Racket.rotation = Quaternion.Slerp(Racket.rotation, world, 1 - Mathf.Exp(-settings.responseSpeed * dt));
        }
    }
}
