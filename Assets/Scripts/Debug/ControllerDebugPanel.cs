using UnityEngine;

namespace MotionControllers
{
    [RequireComponent(typeof(ControllerReceiver), typeof(ControllerManager))]
    public sealed class ControllerDebugPanel : MonoBehaviour
    {
        public PhoneOrientationVisualizer visualizer;
        [Tooltip("Optional game component implementing IControllerGameDebug.")]
        public MonoBehaviour gameDebugSource;
        public string pwaUrl = "https://jjohnj.github.io/Motion-Controller-Website/";
        public string publicWssUrl = "wss://YOUR-TUNNEL.trycloudflare.com/controller";
        [Tooltip("Additional size adjustment on top of automatic Game view scaling.")]
        [Range(0.75f, 2f)] public float uiScale = 1f;
        private ControllerReceiver receiver;
        private ControllerManager manager;
        private GUISkin panelSkin;
        private Vector2 scrollPosition;
        private void Awake() { receiver = GetComponent<ControllerReceiver>(); manager = GetComponent<ControllerManager>(); }
        private void OnGUI()
        {
            if (panelSkin == null)
            {
                panelSkin = Instantiate(GUI.skin);
                panelSkin.label.fontSize = 18;
                panelSkin.label.wordWrap = true;
                panelSkin.label.margin = new RectOffset(4, 4, 6, 6);
                panelSkin.textField.fontSize = 18;
                panelSkin.textField.padding = new RectOffset(8, 8, 8, 8);
                panelSkin.button.fontSize = 18;
                panelSkin.button.wordWrap = true;
                panelSkin.button.padding = new RectOffset(10, 10, 10, 10);
                panelSkin.toggle.fontSize = 18;
                panelSkin.toggle.wordWrap = true;
                panelSkin.toggle.padding = new RectOffset(24, 4, 4, 4);
                panelSkin.box.padding = new RectOffset(12, 12, 12, 12);
            }

            // Reference a 900px-high view so rendering at 1440p/4K stays readable,
            // even when the Editor scales that render down to fit its Game tab.
            float scale = Mathf.Max(0.75f, Screen.height / 900f) * Mathf.Clamp(uiScale, 0.75f, 2f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUISkin previousSkin = GUI.skin;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                GUI.skin = panelSkin;
                // Match the scene camera's 42% left margin and keep overflow scrollable.
                float width = Mathf.Max(100, Screen.width * 0.42f / scale - 24);
                float height = Mathf.Max(100, Screen.height / scale - 24);
                GUILayout.BeginArea(new Rect(12, 12, width, height), GUI.skin.box);
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                DrawContents();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.skin = previousSkin;
            }
        }

        private void OnDestroy()
        {
            if (panelSkin != null) Destroy(panelSkin);
        }

        private void DrawContents()
        {
            GUILayout.Label("PHONE CONTROLLER · technical prototype");
            GUILayout.Label(receiver.Status + " | Invalid/rejected: " + receiver.InvalidPackets);
            GUILayout.Label("Pairing token (new each Play)");
            GUILayout.TextField(receiver.PairingToken ?? "");
            GUILayout.Label("Public WSS endpoint from your tunnel");
            publicWssUrl = GUILayout.TextField(publicWssUrl);
            if (GUILayout.Button("Copy pairing link"))
                GUIUtility.systemCopyBuffer = pwaUrl + "#server=" + System.Uri.EscapeDataString(publicWssUrl) + "&token=" + receiver.PairingToken;
            if (visualizer != null) visualizer.useSmoothing = GUILayout.Toggle(visualizer.useSmoothing, "Use light smoothing (uncheck for raw)");
            GUILayout.Label("Smoothing: " + manager.smoothingSeconds.ToString("F3") + " s (Inspector configurable)");
            GUILayout.Label("Controllers: " + manager.Sessions.Count + "/" + manager.maximumControllers);
            (gameDebugSource as IControllerGameDebug)?.DrawControllerDebug();
            foreach (var s in manager.Sessions.Values)
            {
                GUILayout.Label("ID: " + s.Id);
                if (!s.HasFrame) { GUILayout.Label("Waiting for motion permission/data"); continue; }
                double age = Time.realtimeSinceStartupAsDouble - s.Latest.ReceivedAtSeconds;
                GUILayout.Label($"Frames {s.ReceivedFrames} | sequence {s.Latest.Sequence} | gaps {s.SequenceGaps} | arrival age {age * 1000:F0} ms");
                GUILayout.Label(age > 0.5 ? "STALE — cube held" : s.NeedsCalibration ? "CALIBRATION REQUIRED" : "Receiving / calibrated");
                GUILayout.Label("Relative degrees: " + s.RawRotation.eulerAngles.ToString("F1"));
                if (s.Latest.HasDeviceAngles) GUILayout.Label("Device α / β / γ: " + s.Latest.DeviceAnglesDegrees.ToString("F1"));
                GUILayout.Label("Angular rad/s: " + (s.Latest.HasAngularVelocity ? s.Latest.AngularVelocity.ToString("F2") : "unavailable"));
                GUILayout.Label("Acceleration m/s²: " + (s.Latest.HasAcceleration ? s.Latest.Acceleration.ToString("F2") : "unavailable"));
                if (GUILayout.Button("Calibrate " + s.Id.Substring(0, 8)) && age <= 0.5) s.Calibrate();
            }
        }
    }
}
