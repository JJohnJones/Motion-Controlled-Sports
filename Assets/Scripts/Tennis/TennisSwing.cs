using System;
using UnityEngine;
namespace MotionControllers.Tennis
{
    [Serializable] public sealed class TennisSwingSettings
    {
        [Min(.1f)] public float minimumSpeed = 2.8f;
        [Min(.01f)] public float sustainSeconds = .035f;
        [Min(1)] public float minimumArcDegrees=12;
        [Range(0,1)] public float directionConsistency=.65f;
        [Min(.05f)] public float windowSeconds = .30f;
        [Min(.1f)] public float cooldownSeconds = .4f;
        [Min(.1f)] public float sensitivity = 1;
        [Range(.1f, 2)] public float orientationSensitivity = 1;
        [Min(180)] public float maximumVisualDegreesPerSecond = 1440;
        [Min(1)] public float responseSpeed = 25;
    }
    public sealed class TennisSwing
    {
        public float Peak { get; private set; }
        public Vector3 Direction { get; private set; }
        public Quaternion Face { get; private set; } = Quaternion.identity;
        public double Started { get; private set; } = -100;
        public bool Backhand => Direction.y < 0;
        private long sequence;
        private double previous, aboveSince = -1;
        private float arc;
        private Vector3 initiationDirection;
        private bool armed = true, consumed, skipHistory;
        private int revision = -1;
        public void Reset() { aboveSince = -1; arc=0; armed = false; consumed = true; Started = -100; skipHistory = true; }
        public bool Active(double now, TennisSwingSettings settings) => !consumed && now >= Started && now - Started <= settings.windowSeconds;
        public void Consume() { consumed = true; }
        public void Read(ControllerSession session, double now, TennisSwingSettings settings)
        {
            if (!session.IsCalibrated || !session.HasFrame || now - session.Latest.ReceivedAtSeconds > .35) { Reset(); return; }
            if (revision != session.CalibrationRevision) { revision = session.CalibrationRevision; Reset(); }
            if (skipHistory) { sequence = session.Latest.Sequence; skipHistory = false; return; }
            // Replay unseen recent samples; use phone timestamps only for duration, host clock for active window.
            for (int i = session.HistoryCount - 1; i >= 0; i--) {
                var frame = session.GetHistoryFromNewest(i);
                if (frame.Sequence <= sequence) continue;
                sequence = frame.Sequence;
                if (session.Latest.TimestampMs - frame.TimestampMs > 220 || !frame.HasAngularVelocity) continue;
                Sample(frame.Sequence, frame.TimestampMs / 1000, now, frame.AngularVelocity * session.MotionSensitivity, session.RawRotation, settings);
            }
        }
        public void Sample(long seq, double time, double now, Vector3 angular, Quaternion face, TennisSwingSettings settings)
        {
            float speed = angular.magnitude * settings.sensitivity;
            float dt=(float)(time-previous);
            if(dt<=0 || dt>.12f){aboveSince=-1;arc=0;}
            previous = time; Face = face;
            if (speed < settings.minimumSpeed * .5f) { armed = true; aboveSince = -1; arc=0; consumed=true; return; }
            if (Active(now, settings) && speed > Peak) { Peak = speed; Direction = angular.normalized; }
            if (speed < settings.minimumSpeed) { aboveSince = -1; arc=0; return; }
            if (!armed || now - Started < settings.cooldownSeconds) return;
            if (aboveSince < 0 || Vector3.Dot(initiationDirection,angular.normalized)<settings.directionConsistency)
            { aboveSince=time;arc=0;initiationDirection=angular.normalized; }
            if(dt>0 && dt<=.12f)arc+=angular.magnitude*dt*Mathf.Rad2Deg;
            if (time - aboveSince < settings.sustainSeconds || arc<settings.minimumArcDegrees) return;
            Started = now; Peak = speed; Direction = angular.normalized; consumed = false; armed = false; aboveSince = -1;
        }
    }
    [Serializable] public sealed class TennisShotSettings
    {
        public float minimumPace = 10, maximumPace = 23, maximumSwingSpeed = 14;
        [Range(0, 1)] public float spinSensitivity = .35f;
        [Range(0, 1)] public float faceInfluence = .75f;
    }
    public struct TennisShot { public Vector3 Velocity; public float Spin; }
    public static class TennisShots
    {
        public static TennisShot Calculate(Vector3 contact, int team, float peak, Quaternion face, Vector3 swingDirection,
            float timing, Vector3 incoming, bool serve, bool deuce, TennisShotSettings settings)
        {
            float pace = Mathf.Lerp(settings.minimumPace, settings.maximumPace, Mathf.Clamp01(peak / settings.maximumSwingSpeed));
            Vector3 normal = face * Vector3.forward;
            float lateral = Mathf.Clamp(normal.x * settings.faceInfluence + swingDirection.y * .25f, -1, 1);
            float sign = team == 0 ? 1 : -1;
            float targetX = serve ? (deuce ? -1 : 1) * sign * 2.1f + lateral * 2 * sign : lateral * 4.1f * sign;
            float lift = Mathf.Clamp(-normal.y, -.5f, 1);
            Vector3 target = new Vector3(targetX, .13f, sign * (serve ? 4.5f : 8.5f + lift * 2));
            float travel = Mathf.Clamp(Vector2.Distance(new Vector2(contact.x,contact.z), new Vector2(target.x,target.z)) / pace, .65f, 2.4f);
            travel *= 1 + Mathf.Max(0, lift) * .5f;
            var v = (target-contact) / travel; v.y += 4.905f * travel;
            // Early/late hits retain a small incoming component and introduce signed directional error.
            v.x += timing * 1.4f + incoming.x * .05f; v.z += incoming.z * .025f;
            float spin = Mathf.Clamp((swingDirection.x + (face * Vector3.up).x * .5f) * settings.spinSensitivity, -.6f, .6f);
            return new TennisShot { Velocity = v, Spin = spin };
        }
    }
}
