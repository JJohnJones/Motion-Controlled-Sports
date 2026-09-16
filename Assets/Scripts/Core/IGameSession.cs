namespace MotionControllers.Core
{
    public struct GameResult
    {
        public string Summary;
        public string Detail;
        public GameResult(string summary, string detail) { Summary = summary; Detail = detail; }
    }
    // Game scenes implement this boundary. They do not own global navigation or networking.
    public interface IGameSession
    {
        string Status { get; }
        void SetControllers(System.Collections.Generic.IReadOnlyList<string> controllerIds);
        void SetPaused(bool paused);
        GameResult Finish();
    }
}
