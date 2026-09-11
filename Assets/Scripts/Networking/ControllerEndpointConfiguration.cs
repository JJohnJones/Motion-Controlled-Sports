using System;

namespace MotionControllers
{
    public static class ControllerEndpointConfiguration
    {
        public static bool TryValidate(string pwaUrl, string signalingUrl, bool allowEditorLoopback,
            out Uri signaling, out string error)
        {
            signaling = null;
            error = null;
            if (!Uri.TryCreate(signalingUrl, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                error = "Signaling URL must be an absolute WSS URL.";
            else if (uri.Scheme != "wss" && !(allowEditorLoopback && uri.Scheme == "ws" && uri.IsLoopback))
                error = "Signaling URL must use WSS (only Editor loopback tests may use WS).";
            else if (uri.AbsolutePath != "/signal" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
                error = "Signaling URL must end in /signal with no query, fragment, or credentials.";
            // Reject only the shipped placeholder, not real hosts containing the word 'signaling'.
            else if (string.Equals(uri.Host, "SIGNALING-HOST", StringComparison.OrdinalIgnoreCase))
                error = "Replace the SIGNALING-HOST placeholder with your deployed signaling host.";
            else if (!Uri.TryCreate(pwaUrl, UriKind.Absolute, out var page) || string.IsNullOrEmpty(page.Host) || page.Scheme != "https")
                error = "PWA URL must be an absolute HTTPS URL.";
            else if (page.Fragment.Length != 0 || page.UserInfo.Length != 0)
                error = "PWA URL must not contain a fragment or credentials.";
            else signaling = uri;
            return error == null;
        }
    }
}
