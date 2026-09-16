using UnityEngine;
using System.Collections.Generic;
namespace MotionControllers.Core
{
    public sealed class ControllerLobbyAdapter : MonoBehaviour, IControllerLobby, IControllerUiPresenter
    {
        private readonly ControllerSlot[] slots = {
            new ControllerSlot { PlayerNumber = 1 }, new ControllerSlot { PlayerNumber = 2 },
            new ControllerSlot { PlayerNumber = 3 }, new ControllerSlot { PlayerNumber = 4 } };
        private ControllerManager manager;
        private WebRtcLanControllerTransport lan;
        public IReadOnlyList<ControllerSlot> Slots => slots;
        public int ConnectedPlayers { get; private set; }
        public Texture PairingQr => lan != null ? lan.PairingQr : null;
        public string PairingUrl => lan != null ? lan.JoinUrl : null;
        public string Status => lan != null ? lan.State + " · " + lan.Status : "Waiting for controller systems…";
        private void Update()
        {
            if (manager == null && PersistentControllerRoot.Instance != null)
            { manager = PersistentControllerRoot.Instance.Manager; lan = manager.GetComponent<WebRtcLanControllerTransport>(); }
            ConnectedPlayers = 0;
            foreach (var slot in slots)
                slot.Health = slot.ControllerId == null ? PlayerConnectionHealth.Waiting : PlayerConnectionHealth.Disconnected;
            if (manager == null) return;
            foreach (var session in manager.Sessions.Values)
            {
                int number = manager.GetPlayerNumber(session.Id);
                if (number < 1 || number > 4) continue;
                var slot = slots[number - 1]; slot.ControllerId = session.Id; slot.RttMs = -1;
                slot.Health = PlayerConnectionHealth.Recovering;
                if (lan != null && lan.enabled && lan.Protocol != null)
                {
                    foreach (var c in lan.Protocol.Connections.Values)
                        if (c.ControllerId == session.Id)
                        {
                            slot.RttMs = c.RttMs;
                            slot.NetworkPath = (Application.isEditor || Debug.isDebugBuild) ? lan.GetConnectionPath(session.Id) : null;
                            if (c.Authenticated && !c.InputPaused) slot.Health = PlayerConnectionHealth.Connected;
                            break;
                        }
                }
                if (slot.Health == PlayerConnectionHealth.Connected) ConnectedPlayers++;
            }
        }
        public void PresentControllerUi(string mode, bool paused) => lan?.Protocol?.SetUiMode(mode, paused);
        public void PresentControllerStates(IGameControllerFeedback feedback)
        {
            if (lan?.Protocol == null) return;
            foreach (var pair in lan.Protocol.Connections)
                if (pair.Value.Authenticated) lan.Protocol.SetControllerUiState(pair.Key, feedback?.ControllerState(pair.Value.ControllerId) ?? "");
        }
        public bool IsConnected(string id)
        { foreach (var slot in slots) if (slot.ControllerId == id) return slot.Health == PlayerConnectionHealth.Connected; return false; }
        public void CancelHeldInput()
        { if (manager != null) foreach (var slot in slots) if (slot.ControllerId != null) manager.CancelHeldInput(slot.ControllerId); }
    }
}
