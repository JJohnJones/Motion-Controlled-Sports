# Controller verification

The historical controller-WebSocket test harness has been removed with that transport. Current coverage is the Unity EditMode suite (motion, protocol, persistence, gameplay regression, ICE configuration), PWA Node tests (input, permissions, ICE configuration and recovery), and backend Node tests (pairing, session resume, credential issuance/security).

See ../LAN_CONTROLLER_MODE.md and the separate signaling repository's TURN_SETUP.md for deployment and physical-device acceptance. Automated local checks do not establish live Cloudflare relay or campus Wi-Fi success.
