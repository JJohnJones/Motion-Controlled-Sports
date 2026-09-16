namespace MotionControllers.Core
{
    public interface IGameControllerFeedback { string ControllerState(string controllerId); }
    // Optional outbound presentation bridge. Scene flow does not know the transport.
    public interface IControllerUiPresenter
    {
        void PresentControllerUi(string mode, bool paused);
        void PresentControllerStates(IGameControllerFeedback feedback);
    }
}
