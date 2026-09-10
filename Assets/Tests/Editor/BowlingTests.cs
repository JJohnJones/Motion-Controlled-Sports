using NUnit.Framework;
using UnityEngine;
using MotionControllers.Bowling;

namespace MotionControllers.Tests
{
    public sealed class BowlingTests
    {
        private static MotionFrame Frame(long sequence, double time, float speed, Quaternion? rotation = null) => new MotionFrame
        { ControllerId = "phone", Sequence = sequence, TimestampMs = time, MotionTimestampMs = time,
            ReceivedAtSeconds = Time.realtimeSinceStartupAsDouble, Orientation = rotation ?? Quaternion.identity,
            HasAngularVelocity = true, AngularVelocity = Vector3.right * speed };
        [Test] public void FasterSwingsMapToFasterBoundedBallSpeedsAndHoldTimeIsNotPower()
        {
            var session = new ControllerSession("phone"); var settings = new BowlingReleaseSettings();
            session.Accept(Frame(1, 1000, 0), true); session.Accept(Frame(2, 1100, 2));
            var slow = BowlingReleaseCalculator.Calculate(session, 1000, 1100, settings);
            session.Accept(Frame(3, 1300, 6)); var fast = BowlingReleaseCalculator.Calculate(session, 1000, 1300, settings);
            Assert.That(slow.Valid && fast.Valid, Is.True); Assert.That(fast.BallSpeed, Is.GreaterThan(slow.BallSpeed));
            Assert.That(BowlingReleaseCalculator.Calculate(session, 1250, 1300, settings).BallSpeed, Is.EqualTo(fast.BallSpeed));
            session.Accept(Frame(4, 1500, 999));
            Assert.That(BowlingReleaseCalculator.Calculate(session, 1000, 1500, settings).BallSpeed, Is.EqualTo(settings.maximumBallVelocity));
        }
        [Test] public void OldPreHoldAndFuturePeaksCannotPowerAReleaseAndStaleInputIsRejected()
        {
            var session = new ControllerSession("phone"); var settings = new BowlingReleaseSettings();
            session.Accept(Frame(1, 1000, 99), true); session.Accept(Frame(2, 1200, 0));
            Assert.That(BowlingReleaseCalculator.Calculate(session, 1100, 1200, settings).Valid, Is.False);
            session.Accept(Frame(3, 1250, 4));
            Assert.That(BowlingReleaseCalculator.Calculate(session, 1100, 1250, settings).PeakAngularSpeed, Is.EqualTo(4));
            Assert.That(BowlingReleaseCalculator.Calculate(session, 1100, 1400, settings).Valid, Is.False);
            session.Accept(Frame(4, 1500, 50));
            Assert.That(BowlingReleaseCalculator.Calculate(session, 1100, 1300, settings).Valid, Is.False);
        }
        [Test] public void ButtonProtocolRejectsSpoofingReplayAndOrphanRelease()
        {
            var session = new ControllerSession("phone");
            var pressed = new ControllerButtonEvent { ControllerId="phone", Button=ControllerButton.Primary, Phase=ButtonPhase.Pressed, Sequence=1, TimestampMs=10 };
            Assert.That(session.AcceptButton(pressed), Is.True);
            Assert.That(session.AcceptButton(pressed), Is.False);
            pressed.Sequence=2; pressed.Phase=ButtonPhase.Canceled;
            Assert.That(session.AcceptButton(pressed), Is.True); Assert.That(session.PrimaryHeld, Is.False);
            pressed.Sequence=3; pressed.Phase=ButtonPhase.Released;
            Assert.That(session.AcceptButton(pressed), Is.False);
            var packet = new ControllerMessage { controllerId="other", button="primary", phase="pressed", buttonSequence=1, eventTimestamp=10 };
            Assert.That(MotionJsonCodec.TryButton(packet,"phone",0,out _), Is.False);
            packet.controllerId="phone"; packet.eventTimestamp=double.NaN;
            Assert.That(MotionJsonCodec.TryButton(packet,"phone",0,out _), Is.False);
        }
        [Test] public void HoldLocksAimOnlyReleaseLaunchesAndRollingIgnoresAnotherThrow()
        {
            WithGame((manager, game, ball) =>
            {
                manager.Submit(Frame(1,1000,0,Quaternion.Euler(0,10,0)),true);
                manager.Submit(Frame(2,1010,0,Quaternion.Euler(0,20,0)));
                Assert.That(manager.SubmitButton(Button(1,ButtonPhase.Pressed,Frame(3,1020,0,Quaternion.Euler(0,20,0)))), Is.True);
                Assert.That(game.State, Is.EqualTo(BowlingState.Holding)); float aim = game.AimDegrees;
                manager.Submit(Frame(4,1050,20,Quaternion.Euler(80,-70,0))); Tick(game);
                Assert.That(game.State, Is.EqualTo(BowlingState.Holding)); Assert.That(ball.Body.isKinematic, Is.True);
                Assert.That(game.AimDegrees, Is.EqualTo(aim));
                manager.SubmitButton(Button(2,ButtonPhase.Released,Frame(5,1060,5,Quaternion.Euler(80,-70,0))));
                Assert.That(game.State, Is.EqualTo(BowlingState.Rolling)); Assert.That(ball.Body.linearVelocity.magnitude, Is.GreaterThan(0));
                var velocity=ball.LastLaunchVelocity;
                manager.SubmitButton(Button(3,ButtonPhase.Pressed,Frame(6,1070,10)));
                manager.SubmitButton(Button(4,ButtonPhase.Released,Frame(7,1080,10)));
                Assert.That(ball.LastLaunchVelocity, Is.EqualTo(velocity));
                ball.Body.position=new Vector3(0,-2,0); Tick(game); Assert.That(game.State,Is.EqualTo(BowlingState.Resetting));
                game.resetDelaySeconds=0; Tick(game); Assert.That(game.State,Is.EqualTo(BowlingState.Ready));
                Assert.That(ball.Body.position,Is.EqualTo(ball.releasePoint.position)); Assert.That(ball.Body.isKinematic,Is.True);
            });
        }
        [Test] public void TouchCancellationAndDisconnectDoNotLaunch()
        {
            WithGame((manager,game,ball) =>
            {
                manager.Submit(Frame(1,1000,0),true); manager.SubmitButton(Button(1,ButtonPhase.Pressed,Frame(2,1010,8)));
                manager.SubmitButton(new ControllerButtonEvent {ControllerId="phone",Sequence=2,TimestampMs=1020,Phase=ButtonPhase.Canceled});
                Assert.That(game.State,Is.EqualTo(BowlingState.Ready)); Assert.That(ball.LastLaunchVelocity,Is.EqualTo(Vector3.zero));
                manager.SubmitButton(Button(3,ButtonPhase.Pressed,Frame(3,1030,8))); manager.Remove("phone"); Tick(game);
                Assert.That(game.State,Is.EqualTo(BowlingState.Ready)); Assert.That(ball.Body.isKinematic,Is.True);
            });
        }
        private static ControllerButtonEvent Button(long seq, ButtonPhase phase, MotionFrame snapshot) => new ControllerButtonEvent
        { ControllerId="phone", Sequence=seq, Phase=phase, TimestampMs=snapshot.TimestampMs, ReceivedAtSeconds=Time.realtimeSinceStartupAsDouble, HasSnapshot=true, Snapshot=snapshot };
        private static void Tick(BowlingThrowController game) => typeof(BowlingThrowController).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(game,null);
        private static void WithGame(System.Action<ControllerManager,BowlingThrowController,BowlingBall> check)
        {
            var root=new GameObject("Bowling test"); root.SetActive(false);
            try
            {
                var manager=root.AddComponent<ControllerManager>(); manager.Register("phone");
                var spawn=new GameObject("Spawn"); spawn.transform.SetParent(root.transform); spawn.transform.position=new Vector3(0,0.2f,0);
                var ballObject=new GameObject("Ball"); ballObject.transform.SetParent(root.transform); var ball=ballObject.AddComponent<BowlingBall>(); ball.releasePoint=spawn.transform;
                var rack=root.AddComponent<BowlingPinRack>(); rack.pins=new Rigidbody[0];
                var game=root.AddComponent<BowlingThrowController>(); game.inputSource=manager; game.ball=ball; game.pinRack=rack;
                root.SetActive(true); game.Initialize();
                check(manager,game,ball);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
