using System;
namespace MotionControllers.Tennis
{
    public enum TennisFormat { OneGame, FirstToThree, ShortSet, StandardSet }
    // Pure rules: no scene, sensor or transport dependencies. One set per match.
    public sealed class TennisMatch
    {
        public int[] Points { get; } = new int[2];
        public int[] Games { get; } = new int[2];
        public bool Complete { get; private set; }
        public int Winner { get; private set; } = -1;
        public int Faults { get; private set; }
        public int PointNumber { get; private set; }
        public int ServiceGame { get; private set; }
        public bool TieBreak { get; private set; }
        public int ServerSlot => (ServiceGame + (TieBreak ? (PointNumber + 1) / 2 : 0)) % slots;
        public int ServerTeam => ServerSlot % 2;
        public int ReceiverSlot => slots == 2 ? 1 - ServerTeam : (1 - ServerTeam) + (PointNumber % 2) * 2;
        public bool DeuceSide => PointNumber % 2 == 0;
        private readonly int slots, target;
        private readonly bool winByTwo;
        public TennisMatch(int humans, TennisFormat format = TennisFormat.FirstToThree)
        {
            if (humans < 1 || humans > 4) throw new ArgumentOutOfRangeException(nameof(humans));
            slots = humans <= 2 ? 2 : 4;
            target = format == TennisFormat.OneGame ? 1 : format == TennisFormat.FirstToThree ? 3 : format == TennisFormat.ShortSet ? 4 : 6;
            winByTwo = format == TennisFormat.ShortSet || format == TennisFormat.StandardSet;
        }
        public string PointLabel(int team)
        {
            if (TieBreak) return Points[team].ToString();
            if (Points[0] >= 3 && Points[1] >= 3) return Points[team] > Points[1-team] ? "AD" : "40";
            return new[] { "0", "15", "30", "40" }[Math.Min(3, Points[team])];
        }
        public bool Fault()
        {
            if (Complete) return false;
            if (++Faults < 2) return false;
            AwardPoint(1 - ServerTeam); return true;
        }
        public void AwardPoint(int team)
        {
            if (Complete) throw new InvalidOperationException("Match complete");
            if (team < 0 || team > 1) throw new ArgumentOutOfRangeException(nameof(team));
            Faults = 0; Points[team]++; PointNumber++;
            if (Points[team] < (TieBreak ? 7 : 4) || Points[team] - Points[1-team] < 2) return;
            Games[team]++;
            if (TieBreak || Games[team] >= target && (!winByTwo || Games[team] - Games[1-team] >= 2))
            { Complete = true; Winner = team; return; }
            ServiceGame++; PointNumber = 0; Points[0] = Points[1] = 0;
            TieBreak = winByTwo && Games[0] == target && Games[1] == target;
        }
    }
}
