using MotionControllers.Bowling;
using NUnit.Framework;
using UnityEngine;
namespace MotionControllers.Tests
{
    public class BowlingPinTests
    {
        [Test] public void EscapedPinsStopBlockingButCountExactlyOnceAndReset()
        {
            var root = new GameObject("Translated pin deck"); root.transform.position = new Vector3(30, 5, 10);
            try {
                var rack = root.AddComponent<BowlingPinRack>(); rack.pins = new Rigidbody[3];
                for (int i = 0; i < 3; i++) {
                    var pin = new GameObject("Pin"); pin.transform.SetParent(root.transform, false);
                    pin.transform.localPosition = Vector3.right * i * .2f; rack.pins[i] = pin.AddComponent<Rigidbody>();
                }
                rack.CaptureStartingPoses(); var survivor = rack.pins[2].position;
                rack.pins[0].position += Vector3.down * 4; rack.pins[0].linearVelocity = Vector3.down * 30;
                rack.pins[1].position += Vector3.right * 4; rack.pins[1].angularVelocity = Vector3.one * 20;
                rack.pins[2].Sleep();
                Assert.That(rack.IsSettled(.08f, .15f), Is.True);
                Assert.That(rack.pins[0].gameObject.activeSelf, Is.False);
                Assert.That(rack.CollectNewlyFallen(), Is.EqualTo(2));
                Assert.That(rack.CollectNewlyFallen(), Is.Zero);
                Assert.That(rack.pins[2].position, Is.EqualTo(survivor));
                rack.ResetPins(); Assert.That(rack.StandingCount, Is.EqualTo(3));
                Assert.That(rack.pins[0].gameObject.activeSelf, Is.True);
                Assert.That(rack.pins[0].position, Is.EqualTo(root.transform.position));
            } finally { Object.DestroyImmediate(root); }
        }
        [Test] public void BallBeyondPlayBoundsAndEscapedPinResolveWithoutTimeout()
        {
            var root = new GameObject("Resolution");
            try {
                var rack = root.AddComponent<BowlingPinRack>();
                var pin = new GameObject("Pin"); pin.transform.SetParent(root.transform);
                rack.pins = new[] { pin.AddComponent<Rigidbody>() }; rack.CaptureStartingPoses();
                var ballObject = new GameObject("Ball"); ballObject.transform.SetParent(root.transform);
                var ball = ballObject.AddComponent<BowlingBall>(); ball.releasePoint = root.transform;
                ball.ResetBall(); ball.Launch(Vector3.forward * 8); ball.Body.position = Vector3.forward * 23;
                rack.pins[0].position = Vector3.down * 10; rack.pins[0].linearVelocity = Vector3.down * 20;
                var resolution = new BowlingRollResolution(); resolution.Begin();
                bool done = false;
                for (int i = 0; i < 35 && !done; i++) done = resolution.Tick(.1f, ball, rack);
                Assert.That(done, Is.True); Assert.That(resolution.TimedOut, Is.False);
                Assert.That(ball.OutOfPlay, Is.True); Assert.That(ball.gameObject.activeSelf, Is.False);
                Assert.That(rack.CollectNewlyFallen(), Is.EqualTo(1));
                ball.ResetBall(); Assert.That(ball.OutOfPlay, Is.False); Assert.That(ball.gameObject.activeSelf, Is.True);
            } finally { Object.DestroyImmediate(root); }
        }
        [Test] public void CountsOnlyNewFallenPinsKeepsStandingPoseAndRestoresRack() {
            var root=new GameObject("Rack test");
            try {
                var rack=root.AddComponent<BowlingPinRack>();rack.pins=new Rigidbody[10];
                for(int i=0;i<10;i++){var pin=new GameObject("Pin");pin.transform.SetParent(root.transform);pin.transform.position=new Vector3(i,1,0);rack.pins[i]=pin.AddComponent<Rigidbody>();}
                rack.CaptureStartingPoses();
                rack.pins[0].rotation=Quaternion.Euler(70,0,0);rack.pins[1].position+=Vector3.down;
                var standingPose=rack.pins[2].position+Vector3.right*.1f;rack.pins[2].position=standingPose;
                Assert.That(rack.CollectNewlyFallen(),Is.EqualTo(2));Assert.That(rack.StandingCount,Is.EqualTo(8));
                Assert.That(rack.CollectNewlyFallen(),Is.Zero);Assert.That(rack.pins[0].gameObject.activeSelf,Is.False);
                Assert.That(rack.pins[2].position,Is.EqualTo(standingPose));
                rack.CaptureStartingPoses();rack.ResetPins();Assert.That(rack.StandingCount,Is.EqualTo(10));
                Assert.That(rack.pins[2].position,Is.EqualTo(new Vector3(2,1,0)));Assert.That(rack.pins[0].gameObject.activeSelf,Is.True);
                rack.pins[0].WakeUp();rack.pins[0].linearVelocity=Vector3.one;Assert.That(rack.IsSettled(.08f,.15f),Is.False);
            } finally {Object.DestroyImmediate(root);}
        }
    }
}
