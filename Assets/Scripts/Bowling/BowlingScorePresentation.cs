using System.Linq;
using MotionControllers.Core;
namespace MotionControllers.Bowling
{
    public static class BowlingScorePresentation
    {
        public static GameScoreboard Build(BowlingMatch match, string instruction)
        {
            var result = new GameScoreboard { ActivePlayer = match.PlayerIndex, Instruction = instruction,
                Turn = match.Complete ? "BOWLING COMPLETE" : $"{match.CurrentPlayer.DisplayName.ToUpperInvariant()}  ·  F{match.FrameIndex + 1}  ·  R{match.CurrentFrame.Rolls.Count + 1}",
                Players = new ScoreboardPlayer[match.Players.Count] };
            for (int p = 0; p < match.Players.Count; p++) {
                var player = match.Players[p];
                var row = result.Players[p] = new ScoreboardPlayer { Name = player.DisplayName, Total = player.Total.ToString(), Frames = new ScoreboardFrame[10] };
                for (int f = 0; f < 10; f++) {
                    var frame = player.Frames[f];
                    row.Frames[f] = new ScoreboardFrame { Heading = (f + 1).ToString(),
                        Rolls = string.Join(" ", Enumerable.Range(0, frame.Tenth ? 3 : 2).Select(frame.Notation)),
                        Score = frame.Cumulative?.ToString() ?? "", Current = !match.Complete && p == match.PlayerIndex && f == match.FrameIndex };
                }
            }
            return result;
        }
        public static GameResult Results(BowlingMatch match)
        {
            var ordered = match.Players.OrderByDescending(p => p.Total).ToArray();
            var winners = ordered.Where(p => p.Total == ordered[0].Total).Select(p => p.DisplayName).ToArray();
            string summary = winners.Length > 1 ? "Tie: " + string.Join(" & ", winners) : winners[0] + " wins!";
            var lines = new string[ordered.Length]; int rank = 1;
            for (int i = 0; i < ordered.Length; i++) {
                if (i > 0 && ordered[i].Total != ordered[i - 1].Total) rank = i + 1;
                lines[i] = $"{rank}.  {ordered[i].DisplayName}     {ordered[i].Total} points";
            }
            return new GameResult(summary, string.Join("\n", lines));
        }
    }
}
