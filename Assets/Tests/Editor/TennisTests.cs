using NUnit.Framework;
using UnityEngine;
using MotionControllers.Tennis;
namespace MotionControllers.Tests
{
    public class TennisTests
    {
        private static void Game(TennisMatch match,int team) { for(int i=0;i<4;i++)match.AwardPoint(team); }
        [TestCase(0f)] [TestCase(90f)]
        public void UprightRacketGripWorksFromFlatOrUprightCalibration(float calibrationPitch)
        {
            var session = new ControllerSession("grip");
            session.Accept(new MotionFrame { Sequence=1, TimestampMs=1, HasDeviceAngles=true,
                DeviceAnglesDegrees=new Vector3(30,calibrationPitch,0),
                Orientation=DeviceCoordinates.Orientation(Quaternion.AngleAxis(30,Vector3.forward)*Quaternion.AngleAxis(calibrationPitch,Vector3.right),0) },true);
            session.Accept(new MotionFrame { Sequence=2, TimestampMs=2, HasDeviceAngles=true,
                DeviceAnglesDegrees=new Vector3(30,90,0),
                Orientation=DeviceCoordinates.Orientation(Quaternion.AngleAxis(30,Vector3.forward)*Quaternion.AngleAxis(90,Vector3.right),0) });
            Assert.That(Quaternion.Angle(TennisPlayer.RacketRelativeRotation(session),Quaternion.identity),Is.LessThan(.01f));
        }
        [Test] public void DeuceAdvantageAndOneGameCompletion() {
            var m=new TennisMatch(2,TennisFormat.OneGame);
            for(int i=0;i<3;i++){m.AwardPoint(0);m.AwardPoint(1);}
            Assert.That(m.PointLabel(0),Is.EqualTo("40"));m.AwardPoint(0);Assert.That(m.PointLabel(0),Is.EqualTo("AD"));
            m.AwardPoint(1);Assert.That(m.Complete,Is.False);m.AwardPoint(1);m.AwardPoint(1);
            Assert.That(m.Complete,Is.True);Assert.That(m.Winner,Is.EqualTo(1));
        }
        [TestCase(1,2)][TestCase(2,2)][TestCase(3,4)][TestCase(4,4)]
        public void ServiceRotationAndDoubleFault(int humans,int slots) {
            var m=new TennisMatch(humans,TennisFormat.StandardSet);
            for(int i=0;i<4;i++){Assert.That(m.ServerSlot,Is.EqualTo(i%slots));Game(m,i%2);}
            int server=m.ServerTeam;Assert.That(m.Fault(),Is.False);Assert.That(m.Faults,Is.EqualTo(1));
            Assert.That(m.Fault(),Is.True);Assert.That(m.Points[1-server],Is.EqualTo(1));Assert.That(m.Faults,Is.Zero);
        }
        [Test] public void StandardSetTieBreakAndServiceSequence() {
            var m=new TennisMatch(4,TennisFormat.StandardSet);
            for(int i=0;i<12;i++)Game(m,i%2);
            Assert.That(m.TieBreak,Is.True);
            foreach(int expected in new[]{0,1,1,2,2,3,3}) {Assert.That(m.ServerSlot,Is.EqualTo(expected));m.AwardPoint(0);}
            Assert.That(m.Complete,Is.True);Assert.That(m.Games[0],Is.EqualTo(7));
        }
        [Test] public void ShortFormatsAndServiceBoxBoundaries() {
            var m=new TennisMatch(1);Game(m,0);Game(m,1);Game(m,0);Assert.That(m.Complete,Is.False);Game(m,0);Assert.That(m.Complete,Is.True);
            Assert.That(TennisCourt.InCourt(new Vector3(5,0,5),false),Is.False);
            Assert.That(TennisCourt.InCourt(new Vector3(5,0,5),true),Is.True);
            Assert.That(TennisCourt.InServiceBox(new Vector3(-2,0,4),0,true),Is.True);
            Assert.That(TennisCourt.InServiceBox(new Vector3(2,0,4),0,true),Is.False);
            Assert.That(TennisCourt.InServiceBox(new Vector3(2,0,-4),1,true),Is.True);
        }
        [Test] public void SwingRequiresSustainedMotionAndRearm() {
            var s=new TennisSwing();var settings=new TennisSwingSettings();
            s.Sample(1,0,0,Vector3.up,Quaternion.identity,settings);Assert.That(s.Active(0,settings),Is.False);
            s.Sample(2,.01,.01,Vector3.up*5,Quaternion.identity,settings);Assert.That(s.Active(.01,settings),Is.False);
            s.Sample(3,.06,.06,Vector3.up*8,Quaternion.identity,settings);Assert.That(s.Active(.06,settings),Is.True);Assert.That(s.Peak,Is.EqualTo(8));
            s.Consume();s.Sample(4,1,1,Vector3.up*9,Quaternion.identity,settings);Assert.That(s.Active(1,settings),Is.False);
            s.Sample(5,1.02,1.02,Vector3.zero,Quaternion.identity,settings);s.Sample(6,1.04,1.04,Vector3.down*8,Quaternion.identity,settings);
            s.Sample(7,1.1,1.1,Vector3.down*8,Quaternion.identity,settings);Assert.That(s.Active(1.1,settings),Is.True);Assert.That(s.Backhand,Is.True);
        }
        [Test] public void RestingPhoneAndAlternatingShakeCannotReturnBall()
        {
            var swing=new TennisSwing();var settings=new TennisSwingSettings();
            swing.Sample(1,0,0,Vector3.zero,Quaternion.identity,settings);
            for(int i=1;i<20;i++){
                swing.Sample(i+1,i*.02,i*.02,(i%2==0?Vector3.right:Vector3.left)*8,Quaternion.identity,settings);
                Assert.That(swing.Active(i*.02,settings),Is.False);
            }
            swing.Sample(30,1,1,Vector3.zero,Quaternion.identity,settings);
            swing.Sample(31,1.02,1.02,Vector3.right*8,Quaternion.identity,settings);
            swing.Sample(32,1.08,1.08,Vector3.right*8,Quaternion.identity,settings);
            Assert.That(swing.Active(1.08,settings),Is.True);
            swing.Sample(33,1.10,1.10,Vector3.zero,Quaternion.identity,settings);
            Assert.That(swing.Active(1.10,settings),Is.False,"A completed swing must not leave an automatic-return window open");
        }
        [Test] public void ShotPowerFaceTimingAndSpinChangeTrajectory() {
            var settings=new TennisShotSettings();var p=new Vector3(0,1.5f,-8);
            var slow=TennisShots.Calculate(p,0,3,Quaternion.identity,Vector3.up,0,Vector3.zero,false,true,settings);
            var fast=TennisShots.Calculate(p,0,12,Quaternion.identity,Vector3.up,0,Vector3.zero,false,true,settings);
            Assert.That(fast.Velocity.z,Is.GreaterThan(slow.Velocity.z));
            var angled=TennisShots.Calculate(p,0,12,Quaternion.Euler(0,30,20),Vector3.right,.4f,Vector3.right,false,true,settings);
            Assert.That(angled.Velocity.x,Is.Not.EqualTo(fast.Velocity.x));Assert.That(angled.Spin,Is.Not.Zero);
        }
        [Test] public void AirDragAndBounceDissipateEnergy()
        {
            var flight=new TennisFlight();flight.Launch(new Vector3(0,10,3),new TennisShot {Velocity=Vector3.forward*20});flight.Step(.1f);
            Assert.That(flight.Velocity.z,Is.LessThan(20));
            flight.Launch(new Vector3(0,.2f,3),new TennisShot {Velocity=new Vector3(4,-12,20),Spin=.6f});float before=flight.Velocity.sqrMagnitude;
            flight.Step(.02f);Assert.That(flight.Ground,Is.True);Assert.That(flight.Velocity.sqrMagnitude,Is.LessThan(before));
        }
        [Test] public void PlayersAccelerateAndStayOnTheirSide()
        {
            var go=new GameObject("movement");try {
                var player=new TennisPlayer {Avatar=go.transform,Slot=0};go.transform.position=player.Home(false);
                var settings=new TennisMovementSettings();player.Move(new Vector3(4,1,-3),true,false,.02f,settings);
                Assert.That(player.MovementVelocity.magnitude,Is.LessThanOrEqualTo(settings.acceleration*.02f+.001f));
                for(int i=0;i<300;i++)player.Move(new Vector3(30,1,4),true,false,.02f,settings);
                Assert.That(go.transform.position.z,Is.LessThanOrEqualTo(-1.8f));Assert.That(go.transform.position.x,Is.LessThanOrEqualTo(5));
                player.ResetMovement();Assert.That(player.MovementVelocity,Is.EqualTo(Vector3.zero));
            }finally{Object.DestroyImmediate(go);}
        }
        [Test] public void SweptNetAndGroundAvoidTunneling() {
            var f=new TennisFlight();f.Launch(new Vector3(0,.5f,-1),new TennisShot {Velocity=Vector3.forward*100});f.Step(.02f);Assert.That(f.Net,Is.True);
            f.Launch(new Vector3(0,.2f,4),new TennisShot {Velocity=Vector3.down*20});f.Step(.02f);Assert.That(f.Ground,Is.True);Assert.That(f.Velocity.y,Is.GreaterThan(0));
            Assert.That(TennisCourt.SegmentDistance(new Vector3(-4,1,0),new Vector3(4,1,0),Vector3.up),Is.Zero);
        }
    }
}
