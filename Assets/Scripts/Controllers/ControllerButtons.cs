using System;

namespace MotionControllers
{
    // Game-neutral input: bowling maps Primary to holding a ball.
    public enum ControllerButton { Primary }
    public enum ButtonPhase { Pressed, Released, Canceled }

    public struct ControllerButtonEvent
    {
        public string ControllerId;
        public ControllerButton Button;
        public ButtonPhase Phase;
        public long Sequence; // Independent of the motion-frame sequence.
        public double TimestampMs; // Phone event time, not PC arrival time.
        public double ReceivedAtSeconds;
        public bool HasSnapshot;
        public MotionFrame Snapshot;
    }

    public interface IControllerButtonSource : IMotionInputSource
    {
        event Action<ControllerButtonEvent> ButtonChanged;
    }
}
