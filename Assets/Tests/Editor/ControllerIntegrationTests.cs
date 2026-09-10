using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MotionControllers.Tests
{
    public sealed class ControllerIntegrationTests
    {
        [UnityTest] public IEnumerator SocketToCalibratedCubeAndTokenRejection()
        {
            var root = new GameObject("Integration test controller"); root.SetActive(false);
            var cube = new GameObject("Integration test cube"); cube.SetActive(false);
            var manager = root.AddComponent<ControllerManager>();
            var receiver = root.AddComponent<ControllerReceiver>();
            var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
            receiver.port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            receiver.allowedOrigin = "https://controller.example";
            var visualizer = cube.AddComponent<PhoneOrientationVisualizer>();
            visualizer.inputSource = manager; visualizer.useSmoothing = false;
            Call(receiver, "OnEnable"); Call(visualizer, "Start");
            using (var timeout = new CancellationTokenSource(10000))
            using (var client = new ClientWebSocket())
            using (var invalid = new ClientWebSocket())
            {
                try
                {
                    var uri = new Uri($"ws://127.0.0.1:{receiver.port}/controller");
                    client.Options.SetRequestHeader("Origin", receiver.allowedOrigin);
                    invalid.Options.SetRequestHeader("Origin", receiver.allowedOrigin);
                    yield return Pump(client.ConnectAsync(uri, timeout.Token), receiver);
                    yield return Pump(Send(client, "{\"version\":1,\"type\":\"hello\",\"token\":\"" + receiver.PairingToken + "\"}", timeout.Token), receiver);
                    var buffer = new byte[2048];
                    var welcome = client.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                    yield return Pump(welcome, receiver);
                    var reply = JsonUtility.FromJson<ControllerMessage>(Encoding.UTF8.GetString(buffer, 0, welcome.Result.Count));
                    Assert.That(reply.type, Is.EqualTo("welcome"));
                    var packet = new ControllerMessage { version = 1, type = "calibrate", controllerId = reply.controllerId,
                        sequence = 1, timestamp = 10, orientation = Quaternion.identity };
                    yield return Pump(Send(client, JsonUtility.ToJson(packet), timeout.Token), receiver);
                    var ack = client.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                    yield return Pump(ack, receiver);
                    Assert.That(Encoding.UTF8.GetString(buffer, 0, ack.Result.Count), Does.Contain("calibrated"));
                    packet.type = "motion"; packet.sequence = 2; packet.timestamp = 20;
                    packet.orientation = Quaternion.AngleAxis(45, Vector3.right);
                    yield return Pump(Send(client, JsonUtility.ToJson(packet), timeout.Token), receiver);
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (manager.Sessions[reply.controllerId].Latest.Sequence < 2)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                        Call(receiver, "Update"); yield return null;
                    }
                    Call(visualizer, "LateUpdate");
                    Assert.That(Quaternion.Angle(cube.transform.localRotation, Quaternion.AngleAxis(-45, Vector3.right)), Is.LessThan(0.01));
                    // Exercise the additive button protocol through the same real socket/codec.
                    packet.type = "button"; packet.button = "primary"; packet.phase = "pressed";
                    packet.buttonSequence = 1; packet.eventTimestamp = 35; packet.hasSnapshot = true;
                    packet.sequence = 3; packet.timestamp = 30; packet.motionTimestamp = 30;
                    packet.hasAngularVelocity = true; packet.angularVelocity = new Vector3(180, 0, 0);
                    yield return Pump(Send(client, JsonUtility.ToJson(packet), timeout.Token), receiver);
                    deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (!manager.Sessions[reply.controllerId].PrimaryHeld)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                        Call(receiver, "Update"); yield return null;
                    }
                    packet.phase = "released"; packet.buttonSequence = 2; packet.eventTimestamp = 45;
                    packet.sequence = 4; packet.timestamp = 40; packet.motionTimestamp = 40;
                    yield return Pump(Send(client, JsonUtility.ToJson(packet), timeout.Token), receiver);
                    deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (manager.Sessions[reply.controllerId].PrimaryHeld)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                        Call(receiver, "Update"); yield return null;
                    }
                    Assert.That(manager.Sessions[reply.controllerId].LastButtonPhase, Is.EqualTo(ButtonPhase.Released));
                    Assert.That(manager.Sessions[reply.controllerId].Latest.AngularVelocity.magnitude, Is.EqualTo(Mathf.PI).Within(0.001f));
                    yield return Pump(invalid.ConnectAsync(uri, timeout.Token), receiver);
                    yield return Pump(Send(invalid, "{\"version\":1,\"type\":\"hello\",\"token\":\"wrong\"}", timeout.Token), receiver);
                    deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (receiver.InvalidPackets == 0)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                        Call(receiver, "Update"); yield return null;
                    }
                    Assert.That(manager.Sessions.Count, Is.EqualTo(1));
                }
                finally
                {
                    Call(receiver, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(cube); UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
        private static Task Send(ClientWebSocket socket, string json, CancellationToken token) =>
            socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, token);
        private static IEnumerator Pump(Task task, ControllerReceiver receiver)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (!task.IsCompleted)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                Call(receiver, "Update"); yield return null;
            }
            task.GetAwaiter().GetResult(); Call(receiver, "Update");
        }
        private static void Call(object target, string method) => target.GetType().GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
    }
}
