using System;
using UnityEngine;
namespace MotionControllers.Bowling
{
    // Physics timing only; the match decides what this roll means and which rack to set next.
    [Serializable] public sealed class BowlingRollResolution
    {
        [Min(0.1f)] public float quietSeconds = 1;
        [Min(2)] public float maximumSeconds = 20;
        [Min(0.01f)] public float pinLinearThreshold = .08f;
        [Min(0.01f)] public float pinAngularThreshold = .15f;
        [Tooltip("Optional coordinate reference for ball play bounds; null uses world space.")]
        public Transform playAreaReference;
        public Bounds ballPlayBounds = new Bounds(new Vector3(0, 2, 10), new Vector3(6, 6, 24));
        [Min(.01f)] public float ballLinearThreshold = .2f;
        [Min(.01f)] public float ballAngularThreshold = 1.5f;
        private float elapsed, quiet;
        public bool TimedOut { get; private set; }
        public void Begin() { elapsed = quiet = 0; TimedOut = false; }
        public bool Tick(float delta, BowlingBall ball, BowlingPinRack rack)
        {
            elapsed += delta;
            rack.CleanupOutOfPlay();
            var p = playAreaReference == null ? ball.Body.position : playAreaReference.InverseTransformPoint(ball.Body.position);
            if (!ball.OutOfPlay && !ballPlayBounds.Contains(p)) ball.RemoveFromPlay();
            bool ballDone = ball.OutOfPlay ||
                (ball.Body.linearVelocity.sqrMagnitude < ballLinearThreshold * ballLinearThreshold &&
                 ball.Body.angularVelocity.sqrMagnitude < ballAngularThreshold * ballAngularThreshold);
            bool settled = elapsed >= 2 && ballDone && rack.IsSettled(pinLinearThreshold, pinAngularThreshold);
            quiet = settled ? quiet + delta : 0;
            TimedOut = elapsed >= Mathf.Max(2, maximumSeconds);
            return quiet >= quietSeconds || TimedOut;
        }
    }
}
