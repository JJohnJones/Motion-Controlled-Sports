namespace MotionControllers.Core
{
    // Optional game-to-shell presentation boundary; no scene, transport or sport rules here.
    public interface IGameCompletion { bool IsComplete { get; } }
    public interface IGameScoreboard { GameScoreboard Scoreboard { get; } }
    public sealed class GameScoreboard
    {
        public string Turn, Instruction;
        public int ActivePlayer;
        public ScoreboardPlayer[] Players;
    }
    public sealed class ScoreboardPlayer
    {
        public string Name, Total;
        public ScoreboardFrame[] Frames;
    }
    public sealed class ScoreboardFrame
    {
        public string Heading, Rolls, Score;
        public bool Current;
    }
}
