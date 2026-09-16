using System;
using System.Collections.Generic;
using MotionControllers.Core;
using UnityEngine;
namespace MotionControllers.Bowling
{
    public enum BowlingMatchPhase { Waiting, Ready, Resolving, TurnTransition, Complete }
    // Coordinates rules/input/physics; neither scoring nor sensor interpretation lives here.
    public sealed class BowlingGameSession : MonoBehaviour, IGameSession, IGameCompletion, IGameScoreboard
    {
        public BowlingThrowController bowling;
        public BowlingRollResolution resolution = new BowlingRollResolution();
        [Min(.1f)] public float turnTransitionSeconds = 1.5f;
        public BowlingMatch Match { get; private set; }
        public BowlingMatchPhase Phase { get; private set; } = BowlingMatchPhase.Waiting;
        public GameScoreboard Scoreboard { get; private set; }
        public bool IsComplete => Phase == BowlingMatchPhase.Complete;
        private string status = "Preparing players…";
        public string Status => Phase == BowlingMatchPhase.Ready && bowling != null ? bowling.Message : status;
        public event Action<string> ActivePlayerChanged;
        private bool paused, resetRack;
        private float transitionRemaining;
        public string LastRollSummary { get; private set; } = "";
        private void OnEnable() {
            if (bowling != null) { bowling.ManageMatch(); bowling.ThrowLaunched += OnThrow; }
        }
        private void OnDisable() { if (bowling != null) { bowling.ThrowLaunched -= OnThrow; bowling.LockTurn(); } }
        private void OnThrow(BowlingRelease release) {
            if (Phase != BowlingMatchPhase.Ready) return;
            bowling.LockTurn(); resolution.Begin(); Phase = BowlingMatchPhase.Resolving;
            status = "Ball rolling / pins settling..."; RefreshScoreboard();
        }
        public void SetControllers(IReadOnlyList<string> ids)
        {
            if (Match != null) return;
            var players = new List<BowlingPlayer>();
            var manager = ControllerInput.Resolve(bowling.inputSource) as ControllerManager;
            foreach (var id in ids) {
                int number = manager != null ? manager.GetPlayerNumber(id) : players.Count + 1;
                players.Add(new BowlingPlayer(id, "Player " + (number > 0 ? number : players.Count + 1)));
            }
            Match = new BowlingMatch(players); bowling.Initialize(); bowling.ManageMatch();
            bowling.ThrowLaunched -= OnThrow; bowling.ThrowLaunched += OnThrow;
            bowling.pinRack.ResetPins(); bowling.ball.ResetBall(); BeginTransition(true);
        }
        public void SetPaused(bool value) { paused = value; if (bowling != null) bowling.SetPaused(value); }
        private void RefreshScoreboard() { if (Match != null) Scoreboard = BowlingScorePresentation.Build(Match, Phase == BowlingMatchPhase.Ready ? "Your turn" : status); }
        private void BeginTransition(bool fullRack) {
            resetRack = fullRack; bowling.LockTurn(); Phase = BowlingMatchPhase.TurnTransition;
            transitionRemaining = Mathf.Max(.1f, turnTransitionSeconds);
            status = $"{LastRollSummary} {Match.CurrentPlayer.DisplayName} · Get ready".Trim(); RefreshScoreboard();
        }
        private void Update() { TickMatch(Time.deltaTime); }
        public void TickMatch(float deltaSeconds)
        {
            deltaSeconds = Mathf.Max(0, deltaSeconds);
            if (paused || Match == null || IsComplete) return;
            if (Match.Complete) { Complete(); return; }
            if (Phase == BowlingMatchPhase.Resolving && resolution.Tick(deltaSeconds, bowling.ball, bowling.pinRack))
            {
                int fallen = bowling.pinRack.CollectNewlyFallen();
                var delivered = Match.CurrentFrame;
                var outcome = Match.RecordRoll(fallen);
                LastRollSummary = delivered.Strike && delivered.Rolls.Count == 1 ? "Strike!" :
                    delivered.Spare && delivered.Rolls.Count == 2 ? "Spare!" : fallen == 0 ? "Miss." : fallen + " pins.";
                if (resolution.TimedOut) Debug.Log("[Bowling] Pin-settle timeout reached; counted final poses and stopped residual motion.");
                bowling.ball.ResetBall();
                if (outcome.GameComplete) { Complete(); return; }
                BeginTransition(outcome.ResetRack);
            }
            else if (Phase == BowlingMatchPhase.TurnTransition)
            {
                transitionRemaining -= deltaSeconds;
                if (transitionRemaining > 0) return;
                if (resetRack) bowling.pinRack.ResetPins();
                bowling.PrepareTurn(Match.CurrentPlayer.ControllerId);
                Phase = BowlingMatchPhase.Ready; status = "Your turn · Aim, hold, swing, release";
                ActivePlayerChanged?.Invoke(Match.CurrentPlayer.ControllerId); RefreshScoreboard();
            }
        }
        private void Complete() {
            Phase = BowlingMatchPhase.Complete; status = "Game complete"; bowling.LockTurn();
            bowling.ball.ResetBall(); RefreshScoreboard(); ActivePlayerChanged?.Invoke(null);
        }
        public GameResult Finish() {
            if (!IsComplete) return new GameResult("Bowling in progress", "Complete all ten frames to record final scores.");
            return BowlingScorePresentation.Results(Match);
        }
    }
}
