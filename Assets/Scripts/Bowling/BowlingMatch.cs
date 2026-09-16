using System;
using System.Collections.Generic;
using System.Linq;

namespace MotionControllers.Bowling
{
    // Pure ten-pin rules: no Unity, networking, physics, or UI dependencies.
    public sealed class BowlingFrame
    {
        private readonly List<int> rolls = new List<int>(3);
        public IReadOnlyList<int> Rolls => rolls.AsReadOnly();
        public bool Tenth { get; }
        public int? Score { get; internal set; }
        public int? Cumulative { get; internal set; }
        public bool Strike => rolls.Count > 0 && rolls[0] == 10;
        public bool Spare => rolls.Count > 1 && !Strike && rolls[0] + rolls[1] == 10;
        public bool Complete => Tenth ? rolls.Count == 3 || rolls.Count == 2 && !Strike && !Spare : Strike || rolls.Count == 2;
        internal BowlingFrame(bool tenth) { Tenth = tenth; }
        public int RemainingPins => FreshRack(rolls.Count) ? 10 : 10 - rolls[rolls.Count - 1];
        private bool FreshRack(int index) => index == 0 || Tenth &&
            (index == 1 && Strike || index == 2 && (!Strike || rolls[1] == 10));
        internal void Add(int pins)
        {
            if (Complete || pins < 0 || pins > RemainingPins) throw new ArgumentOutOfRangeException(nameof(pins), "Roll exceeds standing pins or frame is complete.");
            rolls.Add(pins);
        }
        public string Notation(int index)
        {
            if (index >= rolls.Count) return "";
            int pins = rolls[index];
            if (FreshRack(index) && pins == 10) return "X";
            if (!FreshRack(index) && pins + rolls[index - 1] == 10) return "/";
            return pins == 0 ? "-" : pins.ToString();
        }
    }
    public sealed class BowlingPlayer
    {
        public string ControllerId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<BowlingFrame> Frames { get; }
        public int Total { get; internal set; }
        public BowlingPlayer(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Controller ID required");
            ControllerId = id; DisplayName = name;
            var frames = new BowlingFrame[10]; for (int i = 0; i < 10; i++) frames[i] = new BowlingFrame(i == 9);
            Frames = Array.AsReadOnly(frames);
        }
    }
    public readonly struct BowlingRollOutcome
    {
        public readonly bool ResetRack, FrameComplete, GameComplete;
        public BowlingRollOutcome(bool reset, bool frameComplete, bool gameComplete)
        { ResetRack = reset; FrameComplete = frameComplete; GameComplete = gameComplete; }
    }
    public sealed class BowlingMatch
    {
        public IReadOnlyList<BowlingPlayer> Players { get; }
        public int PlayerIndex { get; private set; }
        public int FrameIndex { get; private set; }
        public bool Complete => FrameIndex == 10;
        public BowlingPlayer CurrentPlayer => Complete ? null : Players[PlayerIndex];
        public BowlingFrame CurrentFrame => CurrentPlayer?.Frames[FrameIndex];
        public BowlingMatch(IEnumerable<BowlingPlayer> players)
        {
            var list = players.ToArray();
            if (list.Length < 1 || list.Length > 4 || list.Any(p => p == null) || list.Select(p => p.ControllerId).Distinct().Count() != list.Length)
                throw new ArgumentException("Bowling requires 1–4 unique players.");
            Players = Array.AsReadOnly(list);
        }
        public BowlingRollOutcome RecordRoll(int pins)
        {
            if (Complete) throw new InvalidOperationException("Game is complete.");
            var frame = CurrentFrame; bool clearedRack = pins == frame.RemainingPins;
            frame.Add(pins); BowlingScoring.Calculate(CurrentPlayer);
            bool finished = frame.Complete;
            if (finished && ++PlayerIndex == Players.Count) { PlayerIndex = 0; FrameIndex++; }
            return new BowlingRollOutcome(finished || clearedRack, finished, Complete);
        }
    }
    public static class BowlingScoring
    {
        public static void Calculate(BowlingPlayer player)
        {
            var all = player.Frames.SelectMany(f => f.Rolls).ToArray(); int offset = 0, cumulative = 0; bool resolved = true;
            foreach (var frame in player.Frames)
            {
                int? score = null;
                if (frame.Complete)
                {
                    if (frame.Tenth) score = frame.Rolls.Sum();
                    else if (frame.Strike && all.Length >= offset + 3) score = 10 + all[offset + 1] + all[offset + 2];
                    else if (frame.Spare && all.Length >= offset + 3) score = 10 + all[offset + 2];
                    else if (!frame.Strike && !frame.Spare) score = frame.Rolls.Sum();
                }
                frame.Score = score; resolved &= score.HasValue;
                if (resolved) { cumulative += score.Value; frame.Cumulative = cumulative; } else frame.Cumulative = null;
                offset += frame.Rolls.Count;
            }
            player.Total = cumulative;
        }
    }
}
