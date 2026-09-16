using System.Collections.Generic;
using UnityEngine;

namespace MotionControllers
{
    public sealed class ControllerManager : MonoBehaviour, IControllerButtonSource
    {
        [Range(0, 0.15f)] public float smoothingSeconds = 0.025f;
        [Range(1, 4)] public int maximumControllers = 4;
        private readonly Dictionary<string, ControllerSession> sessions = new Dictionary<string, ControllerSession>();
        private readonly Dictionary<string, int> playerNumbers = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, ControllerSession> Sessions => sessions;
        public event System.Action<ControllerButtonEvent> ButtonChanged;
        public bool TryGetController(string id, out ControllerSession session) => sessions.TryGetValue(id, out session);
        public bool Register(string id)
        {
            if (sessions.Count >= maximumControllers || sessions.ContainsKey(id)) return false;
            sessions.Add(id, new ControllerSession(id));
            int number = 1;
            while (playerNumbers.ContainsValue(number)) number++;
            playerNumbers.Add(id, number);
            return true;
        }
        public int GetPlayerNumber(string id) => playerNumbers.TryGetValue(id, out int number) ? number : 0;
        public void Remove(string id) { sessions.Remove(id); playerNumbers.Remove(id); }
        public void Clear() { sessions.Clear(); playerNumbers.Clear(); }
        public void CancelHeldInput(string id)
        {
            if (!sessions.TryGetValue(id, out var session) || !session.PrimaryHeld) return;
            session.CancelHeldInput();
            // Local cancellation is not a fabricated phone sequence or a release/throw.
            ButtonChanged?.Invoke(new ControllerButtonEvent { ControllerId = id, Button = ControllerButton.Primary,
                Phase = ButtonPhase.Canceled, Sequence = session.LastButtonSequence,
                TimestampMs = session.LastButtonTimestampMs, ReceivedAtSeconds = Time.realtimeSinceStartupAsDouble });
        }
        public bool Submit(MotionFrame frame, bool calibrate = false) =>
            sessions.TryGetValue(frame.ControllerId, out var session) && session.Accept(frame, calibrate);
        public bool SubmitButton(ControllerButtonEvent input)
        {
            if (!sessions.TryGetValue(input.ControllerId, out var session) || !session.AcceptButton(input)) return false;
            ButtonChanged?.Invoke(input);
            return true;
        }
        private void Update()
        {
            foreach (var session in sessions.Values) session.UpdateSmoothing(Time.unscaledDeltaTime, smoothingSeconds);
        }
    }
}
