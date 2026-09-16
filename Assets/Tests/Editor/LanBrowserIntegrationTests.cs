using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace MotionControllers.Tests
{
    public class LanBrowserIntegrationTests
    {
        // Optional real-browser integration harness. Normal EditMode runs skip this test.
        [UnityTest] public IEnumerator BrowserDataChannelFeedsExistingControllerPipeline()
        {
            string endpoint = Environment.GetEnvironmentVariable("MCS_TEST_SIGNAL");
            string directory = Environment.GetEnvironmentVariable("MCS_TEST_DIR");
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(directory)) Assert.Ignore("Set MCS_TEST_SIGNAL and MCS_TEST_DIR with the browser verification harness running.");
            yield return new EnterPlayMode();
            // Test runner domain reload does not preserve iterator locals across EnterPlayMode.
            endpoint = Environment.GetEnvironmentVariable("MCS_TEST_SIGNAL");
            directory = Environment.GetEnvironmentVariable("MCS_TEST_DIR");
            var go = new GameObject("LAN browser integration"); go.SetActive(false);
            var manager = go.AddComponent<ControllerManager>();
            var firstScene = SceneManager.CreateScene("LAN initial scene");
            SceneManager.MoveGameObjectToScene(go, firstScene);
            var lan = go.AddComponent<WebRtcLanControllerTransport>();
            go.AddComponent<PersistentControllerRoot>();
            lan.signalingUrl = endpoint; lan.pwaUrl = "https://localhost:19443/";
            go.SetActive(true);
            bool passed = false;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 60;
                while (lan.JoinUrl == null && Time.realtimeSinceStartupAsDouble < deadline && lan.State != ControllerConnectionState.Failed) yield return null;
                Assert.That(lan.JoinUrl, Is.Not.Null, lan.Status + " | " + lan.signalingUrl + " | " + lan.pwaUrl + " | editor=" + Application.isEditor);
                File.WriteAllText(Path.Combine(directory, "join.txt"), lan.JoinUrl);
                File.WriteAllBytes(Path.Combine(directory, "qr.png"), lan.PairingQr.EncodeToPNG());
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    foreach (var session in manager.Sessions.Values)
                        if (session.IsCalibrated && session.ReceivedFrames >= 3 && session.LastButtonPhase == ButtonPhase.Released)
                            foreach (var connection in lan.Protocol.Connections.Values)
                                if (connection.RttMs >= 0) passed = true;
                    if (passed) break;
                    yield return null;
                }
                Assert.That(passed, Is.True, "Native pipeline did not receive browser calibration, motion, release and heartbeat: " + lan.Status);
                var originalUrl = lan.JoinUrl;
                var originalProtocol = lan.Protocol;
                ControllerSession originalSession = null;
                foreach (var session in manager.Sessions.Values) originalSession = session;
                long sequence = originalSession.Latest.Sequence;
                var nextScene = SceneManager.CreateScene("LAN next game scene");
                SceneManager.SetActiveScene(nextScene);
                yield return SceneManager.UnloadSceneAsync(firstScene);
                double sceneDeadline = Time.realtimeSinceStartupAsDouble + 5;
                while (originalSession.Latest.Sequence <= sequence && Time.realtimeSinceStartupAsDouble < sceneDeadline) yield return null;
                Assert.That(lan.JoinUrl, Is.EqualTo(originalUrl));
                Assert.That(lan.Protocol, Is.SameAs(originalProtocol));
                Assert.That(manager.Sessions[originalSession.Id], Is.SameAs(originalSession));
                Assert.That(originalSession.Latest.Sequence, Is.GreaterThan(sequence));
                Assert.That(originalSession.IsCalibrated, Is.True);
                yield return SceneManager.UnloadSceneAsync(nextScene);
                lan.ReconnectSignalingForDiagnostics();
                yield return new WaitForSecondsRealtime(4);
                Assert.That(lan.JoinUrl, Is.EqualTo(originalUrl));
                Assert.That(manager.Sessions[originalSession.Id], Is.SameAs(originalSession));
                lan.RestartIceForControllers();
                yield return new WaitForSecondsRealtime(1);
                double recoveryDeadline = Time.realtimeSinceStartupAsDouble + 30;
                while (lan.State != ControllerConnectionState.Connected && Time.realtimeSinceStartupAsDouble < recoveryDeadline) yield return null;
                Assert.That(lan.State, Is.EqualTo(ControllerConnectionState.Connected), lan.Status);
                Assert.That(manager.Sessions[originalSession.Id], Is.SameAs(originalSession));
                Assert.That(originalSession.IsCalibrated, Is.True);
                File.WriteAllText(Path.Combine(directory, "host-recovery.txt"), "ready for browser recovery tests");
                while (!File.Exists(Path.Combine(directory, "browser-recovery.txt")) && Time.realtimeSinceStartupAsDouble < recoveryDeadline + 30) yield return null;
                Assert.That(File.Exists(Path.Combine(directory, "browser-recovery.txt")), Is.True, "Browser recovery tests did not complete");
                yield return new WaitForSecondsRealtime(3);
                Assert.That(manager.Sessions[originalSession.Id], Is.SameAs(originalSession));
                Assert.That(originalSession.IsCalibrated, Is.True);
                Assert.That(lan.JoinUrl, Is.EqualTo(originalUrl));
                Assert.That(lan.State, Is.EqualTo(ControllerConnectionState.Connected));
                File.WriteAllText(Path.Combine(directory, "unity-result.txt"), "PASS: browser WebRTC calibration, motion, release snapshot, RTT");
                // Give the browser time to collect the selected ICE pair before disposal.
                yield return new WaitForSecondsRealtime(2);
            }
            finally { UnityEngine.Object.Destroy(go); }
            yield return new ExitPlayMode();
        }
    }
}
