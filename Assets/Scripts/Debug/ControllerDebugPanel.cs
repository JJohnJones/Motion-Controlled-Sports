using UnityEngine;

namespace MotionControllers
{
    public sealed class ControllerDebugPanel : MonoBehaviour
    {
        public PhoneOrientationVisualizer visualizer;
        [Tooltip("Optional game component implementing IControllerGameDebug.")]
        public MonoBehaviour gameDebugSource;
        [Tooltip("Additional size adjustment on top of automatic Game view scaling.")]
        [Range(0.75f, 2f)] public float uiScale = 1f;
        private ControllerManager manager;
        private WebRtcLanControllerTransport lan;
        private bool showNetworkDiagnostics;
        [Tooltip("Leave empty to use the persistent controller system.")]
        public ControllerManager controllerSystem;
        private GUISkin panelSkin;
        private Vector2 scrollPosition;
        private void Start()
        {
            manager = PersistentControllerRoot.Instance != null ? PersistentControllerRoot.Instance.Manager :
                controllerSystem != null ? controllerSystem : GetComponent<ControllerManager>();
            if (manager == null) { enabled = false; return; }
            lan = manager.GetComponent<WebRtcLanControllerTransport>();
        }
        private void OnGUI()
        {
            if (manager == null) return;
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
            if (lan != null && lan.enabled)
            {
                GUILayout.Label("Connect Controller · LOCAL / LAN MODE");
                GUILayout.Label(lan.State + " · " + lan.Status);
                if (lan.PairingQr != null)
                {
                    float size = Mathf.Min(280, Mathf.Max(100, Screen.width * 0.42f / (Mathf.Max(0.75f, Screen.height / 900f) * uiScale) - 64));
                    // A GUIStyle box keeps an image at its native module resolution. Reserve
                    // a square, then explicitly scale the point-filtered QR and its white border.
                    Rect qrRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(qrRect, lan.PairingQr, ScaleMode.ScaleToFit, false);
                    GUILayout.Label("Scan with your phone · same Wi-Fi/LAN");
                    if (GUILayout.Button("Copy controller link")) GUIUtility.systemCopyBuffer = lan.JoinUrl;
                }
                if (GUILayout.Button("New controller session / QR")) lan.RestartSession();
                if (lan.Protocol != null) foreach (var c in lan.Protocol.Connections.Values)
                {
                    if (c.ControllerId == null) continue;
                    GUILayout.Label($"Player {c.PlayerNumber} Paired · RTT {(c.RttMs < 0 ? "—" : c.RttMs.ToString("F0") + " ms")}");
                }
                GUILayout.Label("Rejected packets: " + (lan.Protocol?.InvalidPackets ?? 0));
                GUILayout.Label("Signaling: " + lan.SignalingStatus);
                showNetworkDiagnostics = GUILayout.Toggle(showNetworkDiagnostics, "Show connection recovery diagnostics");
                if (showNetworkDiagnostics && (Application.isEditor || UnityEngine.Debug.isDebugBuild))
                {
                    if (GUILayout.Button("Test ICE restart (keep player)")) lan.RestartIceForControllers();
                    if (GUILayout.Button("Test signaling reconnect (keep channel)")) lan.ReconnectSignalingForDiagnostics();
                    foreach (var line in lan.PeerDiagnostics) GUILayout.Label(line);
                    foreach (var line in lan.RecentLog) GUILayout.Label(line);
                }
            }
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
