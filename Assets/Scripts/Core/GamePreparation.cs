using System;
using System.Collections.Generic;
using UnityEngine;
namespace MotionControllers.Core
{
    // Only generic controller state/events are needed before any game scene is loaded.
    public sealed class GamePreparation : IGameControllerFeedback, IDisposable
    {
        public const string CalibrationInstructions = "Lay the phone flat, screen facing UP, with its TOP pointing FORWARD toward the TV/monitor. Keep it still and tap Calibrate on the phone.";
        public static string GripInstructions(string mode) => mode == "bowling"
            ? "Bowling: pick up the phone with its top pointing forward, like a Wii controller. Aim, hold the screen, swing your arm forward, then release."
            : mode == "tennis" ? "Tennis: lift the phone upright with its top UP, like a racket handle. Keep this grip after calibration. Tap to toss, then swing to serve."
            : "Sword Duel: hold the phone upright, TOP UP, with the SCREEN facing inward (right hand: screen LEFT; left hand: screen RIGHT). Choose your handedness in Settings. In the arena, tap in this guard pose to ready each round. Swing to attack; angle the blade to guard.";
        private readonly IControllerButtonSource source;
        private readonly IControllerLobby lobby;
        private readonly Dictionary<string, int> ready = new Dictionary<string, int>();
        public IReadOnlyList<string> Players { get; }
        public GamePreparation(IControllerButtonSource input, IControllerLobby connected, IReadOnlyList<string> players)
        { source = input; lobby = connected; Players = new List<string>(players); if (source != null) source.ButtonChanged += OnButton; }
        public bool IsCalibrated(string id) => source != null && source.TryGetController(id, out var c) && c.IsCalibrated;
        public bool IsReady(string id) => lobby.IsConnected(id) && source != null && source.TryGetController(id, out var c) &&
            c.IsCalibrated && ready.TryGetValue(id, out int revision) && revision == c.CalibrationRevision;
        public bool AllReady
        {
            get { if (Players.Count == 0) return false; foreach (var id in Players) if (!IsReady(id) || !source.TryGetController(id, out var c) || Time.realtimeSinceStartupAsDouble - c.Latest.ReceivedAtSeconds > 1) return false; return true; }
        }
        public void Tick() { foreach (var id in Players) if (!lobby.IsConnected(id) || !IsCalibrated(id)) ready.Remove(id); }
        private void OnButton(ControllerButtonEvent e)
        {
            if (e.Button != ControllerButton.Primary || e.Phase != ButtonPhase.Released || !lobby.IsConnected(e.ControllerId)) return;
            bool included = false; foreach (var id in Players) if (id == e.ControllerId) included = true;
            if (included && source.TryGetController(e.ControllerId, out var c) && c.IsCalibrated && Time.realtimeSinceStartupAsDouble - c.Latest.ReceivedAtSeconds <= 1)
                ready[e.ControllerId] = c.CalibrationRevision;
        }
        public string ControllerState(string id)
        { foreach (var player in Players) if (id == player) return IsReady(id) ? "ready" : "prepare"; return "spectator"; }
        public void Dispose() { if (source != null) source.ButtonChanged -= OnButton; }
    }
}
