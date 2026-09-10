using UnityEngine;

namespace MotionControllers
{
    public sealed class PhoneOrientationVisualizer : MonoBehaviour
    {
        [Tooltip("Component implementing IMotionInputSource, normally ControllerManager.")]
        public MonoBehaviour inputSource;
        [Tooltip("Empty selects the first connected phone; otherwise select its assigned ID.")]
        public string controllerId;
        public bool useSmoothing = true;
        private Quaternion neutralRotation;
        private IMotionInputSource source;
        private void Start() { neutralRotation = transform.localRotation; source = inputSource as IMotionInputSource; }
        private void LateUpdate()
        {
            if (source == null) return;
            ControllerSession session = null;
            if (!string.IsNullOrEmpty(controllerId)) source.TryGetController(controllerId, out session);
            else foreach (var candidate in source.Sessions.Values) { session = candidate; break; }
            if (session == null || !session.IsCalibrated || Time.realtimeSinceStartupAsDouble - session.Latest.ReceivedAtSeconds > 0.5) return;
            transform.localRotation = neutralRotation * (useSmoothing ? session.SmoothedRotation : session.RawRotation);
        }
    }
}
