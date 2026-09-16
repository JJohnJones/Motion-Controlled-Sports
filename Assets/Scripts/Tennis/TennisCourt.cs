using UnityEngine;
namespace MotionControllers.Tennis
{
    public static class TennisCourt
    {
        public const float HalfLength = 11.885f, SinglesWidth = 4.115f, DoublesWidth = 5.485f, ServiceLength = 6.4f, NetHeight = .94f;
        public static bool InCourt(Vector3 p, bool doubles) => Mathf.Abs(p.x) <= (doubles ? DoublesWidth : SinglesWidth) + .04f && Mathf.Abs(p.z) <= HalfLength + .04f;
        public static int Side(Vector3 p) => p.z < 0 ? 0 : 1;
        public static bool InServiceBox(Vector3 p, int servingTeam, bool deuce) =>
            Side(p) != servingTeam && Mathf.Abs(p.z) <= ServiceLength + .04f && Mathf.Abs(p.x) <= SinglesWidth + .04f &&
            ((deuce ? -1 : 1) * (servingTeam == 0 ? 1 : -1) * p.x >= -.04f);
        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        { var d=b-a; float t=d.sqrMagnitude < .00001f ? 0 : Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude); return Vector3.Distance(a+d*t,p); }
    }
    // Fixed-step ballistic model with swept net/ground events, independent of tiny colliders.
    public sealed class TennisFlight
    {
        public Vector3 Position, Previous, Velocity;
        public float Spin;
        public bool Ground, Net;
        public void Launch(Vector3 p, TennisShot shot) { Position=Previous=p; Velocity=shot.Velocity; Spin=shot.Spin; }
        public void Step(float dt)
        {
            Ground=Net=false; Previous=Position;
            Velocity += new Vector3(0, -9.81f - Spin * Mathf.Abs(Velocity.z) * .2f, 0) * dt;
            Position += Velocity * dt;
            if (Previous.z * Position.z < 0) {
                float t = -Previous.z / (Position.z-Previous.z);
                var cross = Vector3.Lerp(Previous,Position,t);
                Net = Mathf.Abs(cross.x) <= 6.1f && cross.y <= TennisCourt.NetHeight + .13f;
            }
            if (Position.y <= .13f && Previous.y > .13f) {
                float t = (Previous.y-.13f)/(Previous.y-Position.y);
                Position = Vector3.Lerp(Previous,Position,t); Position.y=.13f;
                Velocity.y=Mathf.Abs(Velocity.y)*.78f; Velocity.x *= .94f; Velocity.z *= .94f + Spin*.04f;
                Ground=true;
            }
        }
    }
}
