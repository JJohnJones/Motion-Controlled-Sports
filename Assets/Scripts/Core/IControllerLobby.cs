using System.Collections.Generic;
using UnityEngine;
namespace MotionControllers.Core
{
    public enum PlayerConnectionHealth { Waiting, Connected, Recovering, Disconnected }
    public sealed class ControllerSlot
    {
        public int PlayerNumber;
        public string ControllerId;
        public PlayerConnectionHealth Health;
        public double RttMs = -1;
        public string NetworkPath;
    }
    // UI-facing snapshot; no WebRTC types leak into screens or game scenes.
    public interface IControllerLobby
    {
        IReadOnlyList<ControllerSlot> Slots { get; }
        int ConnectedPlayers { get; }
        Texture PairingQr { get; }
        string PairingUrl { get; }
        string Status { get; }
        bool IsConnected(string controllerId);
        void CancelHeldInput();
    }
}
