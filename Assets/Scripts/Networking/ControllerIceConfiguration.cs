using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.WebRTC;

namespace MotionControllers
{
    // Runtime network configuration, supplied by authenticated signaling; never serialized into assets.
    [Serializable] public sealed class ControllerIceServer
    { public string[] urls; public string username, credential; }
    [Serializable] public sealed class ControllerIceConfiguration
    {
        public ControllerIceServer[] iceServers;
        public double expiresAt;
        public string mode, error, warning;
        public bool IsFresh => !double.IsNaN(expiresAt) && !double.IsInfinity(expiresAt) && expiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000;
        public bool HasTurn {
            get { foreach (var s in iceServers ?? Array.Empty<ControllerIceServer>())
                foreach (var u in s.urls ?? Array.Empty<string>()) if (u.StartsWith("turn")) return true; return false; }
        }
        public RTCConfiguration Build()
        {
            if (!string.IsNullOrEmpty(error) || !IsFresh) throw new ArgumentException("ICE configuration unavailable or expired: " + error);
            if (mode != "all" && mode != "relay" && mode != "direct") throw new ArgumentException("Invalid ICE mode");
            if (iceServers == null || iceServers.Length > 16) throw new ArgumentException("Invalid ICE server list");
            var servers = new List<RTCIceServer>();
            foreach (var server in iceServers)
            {
                if (server == null || server.urls == null || server.urls.Length < 1 || server.urls.Length > 8) throw new ArgumentException("Invalid ICE URLs");
                bool turn = false;
                foreach (var url in server.urls)
                {
                    if (url == null || url.Length > 512 || !Regex.IsMatch(url, @"^(stuns?|turns?):[a-zA-Z0-9.\[\]:-]+(\?transport=(udp|tcp))?$")) throw new ArgumentException("Invalid ICE URL");
                    turn |= url.StartsWith("turn");
                }
                if (turn && (string.IsNullOrEmpty(server.username) || string.IsNullOrEmpty(server.credential) || server.username.Length > 256 || server.credential.Length > 256)) throw new ArgumentException("Missing/invalid temporary TURN credentials");
                servers.Add(new RTCIceServer { urls = server.urls, username = server.username, credential = server.credential });
            }
            return new RTCConfiguration { iceServers = servers.ToArray(), iceTransportPolicy =
                (UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild) && mode == "relay" ? RTCIceTransportPolicy.Relay :
                RTCIceTransportPolicy.All };
        }
    }
}
