using NUnit.Framework;
using UnityEngine;

namespace MotionControllers.Tests
{
    public sealed class MotionPipelineTests
    {
        [Test] public void ReflectionMapsBasisVectorsAndAxialVelocity()
        {
            var q = DeviceCoordinates.Orientation(Quaternion.AngleAxis(90, Vector3.right), 0);
            Assert.That(Vector3.Distance(q * Vector3.up, Vector3.back), Is.LessThan(0.0001));
            Assert.That(DeviceCoordinates.Acceleration(Vector3.forward, 0), Is.EqualTo(Vector3.back));
            Assert.That(Vector3.Distance(DeviceCoordinates.AngularVelocity(Vector3.right * 180, 0), Vector3.left * Mathf.PI), Is.LessThan(0.0001));
            Assert.That(Vector3.Distance(DeviceCoordinates.Acceleration(Vector3.up, 90), Vector3.left), Is.LessThan(0.0001));
        }
        [Test] public void LandscapeUiCompensatesPhysicalCounterClockwiseRoll()
        {
            var upright = Quaternion.AngleAxis(90, Vector3.right);
            var landscape = upright * Quaternion.AngleAxis(90, Vector3.forward);
            Assert.That(Quaternion.Angle(DeviceCoordinates.Orientation(upright, 0),
                DeviceCoordinates.Orientation(landscape, 90)), Is.LessThan(0.01));
        }
        [Test] public void CalibrationUsesLocalDeltaForNonCommutingRotations()
        {
            var session = new ControllerSession("one");
            var reference = Quaternion.Euler(25, 75, -30);
            var delta = Quaternion.AngleAxis(35, Vector3.right);
            session.Accept(Frame("one", 1, reference), true);
            session.Accept(Frame("one", 2, reference * delta));
            Assert.That(Quaternion.Angle(session.RawRotation, delta), Is.LessThan(0.01));
            Assert.That(session.Accept(Frame("one", 1, Quaternion.identity)), Is.False);
            session.Calibrate();
            Assert.That(Quaternion.Angle(session.RawRotation, Quaternion.identity), Is.LessThan(0.01));
        }
        [Test] public void SessionsAreIndependentAndHistoryIsBounded()
        {
            var a = new ControllerSession("a"); var b = new ControllerSession("b");
            a.Accept(Frame("a", 1, Quaternion.identity), true);
            b.Accept(Frame("b", 1, Quaternion.Euler(20, 30, 40)), true);
            for (int i = 2; i <= 150; i++) a.Accept(Frame("a", i, Quaternion.Euler(i, 0, 0)));
            Assert.That(a.HistoryCount, Is.EqualTo(120));
            Assert.That(a.GetHistoryFromNewest(119).Sequence, Is.EqualTo(31));
            Assert.That(b.ReceivedFrames, Is.EqualTo(1));
            Assert.That(Quaternion.Angle(b.RawRotation, Quaternion.identity), Is.LessThan(0.01));
            var landscape = Frame("a", 151, Quaternion.identity); landscape.ScreenAngle = 90;
            a.Accept(landscape);
            Assert.That(a.NeedsCalibration, Is.True);
        }
        [Test] public void SmoothingIsFrameRateIndependentAndRawCanBeSelected()
        {
            var a = new ControllerSession("a"); var b = new ControllerSession("b");
            a.Accept(Frame("a", 1, Quaternion.identity), true); b.Accept(Frame("b", 1, Quaternion.identity), true);
            a.Accept(Frame("a", 2, Quaternion.Euler(0, 90, 0))); b.Accept(Frame("b", 2, Quaternion.Euler(0, 90, 0)));
            a.UpdateSmoothing(0.02f, 0.05f); b.UpdateSmoothing(0.01f, 0.05f); b.UpdateSmoothing(0.01f, 0.05f);
            Assert.That(Quaternion.Angle(a.SmoothedRotation, b.SmoothedRotation), Is.LessThan(0.05));
            a.UpdateSmoothing(0.01f, 0);
            Assert.That(Quaternion.Angle(a.SmoothedRotation, a.RawRotation), Is.LessThan(0.01));
        }
        [Test] public void CodecRejectsInvalidQuaternionAndSpoofedController()
        {
            var p = new ControllerMessage { controllerId = "a", sequence = 1, orientation = Quaternion.identity };
            Assert.That(MotionJsonCodec.TryFrame(p, "b", 0, out _), Is.False);
            p.orientation = new Quaternion(0, 0, 0, 0);
            Assert.That(MotionJsonCodec.TryFrame(p, "a", 0, out _), Is.False);
            p.orientation = Quaternion.identity; p.timestamp = double.NaN;
            Assert.That(MotionJsonCodec.TryFrame(p, "a", 0, out _), Is.False);
        }
        private static MotionFrame Frame(string id, long sequence, Quaternion q) => new MotionFrame
            { ControllerId = id, Sequence = sequence, TimestampMs = sequence * 16, Orientation = q };
    }
}
