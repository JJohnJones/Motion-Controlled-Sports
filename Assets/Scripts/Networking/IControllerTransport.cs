namespace MotionControllers
{
    // Delivery only. No gameplay, sensor conversion, or future online replication here.
    public interface IControllerTransport
    {
        void Send(string peerId, string json);
        void Close(string peerId);
    }
    public enum ControllerConnectionState
    { Idle, CreatingSession, WaitingForController, Signaling, Connecting, Connected, Disconnected, Failed }
}
