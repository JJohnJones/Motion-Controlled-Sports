# LAN controller recovery

## Diagnosis

The previous implementation had deterministic teardown paths: PWA visibility loss called reset; either signaling socket closing closed WebRTC; the backend deleted a room when its host socket closed; a single pending heartbeat exceeding 10 seconds removed a controller; channel/PC failure disposed the peer; SDP handling accepted only the first answer. These made temporary interruptions require new player registration/calibration. `disconnected` itself was not explicitly treated as terminal, but these other paths prevented sustained recovery.

There is no historical state trace proving whether the user's original network interruptions came from Wi-Fi, OS suspension, ICE or the signaling host. New diagnostics distinguish those observations without guessing the underlying cause. WebRTC and raw motion/button packet formats remain unchanged; only signaling gains resume/revision/restart control messages. The recovery architecture now also supports temporary STUN/TURN configuration; see ../LAN_CONTROLLER_MODE.md.

## Recovery policy

| Condition | Behavior |
|---|---|
| PC/ICE disconnected | Recovering; cancel any held local input; 8-second automatic-recovery grace |
| ICE/PC failed | Request immediate host ICE restart when signaling is available |
| Channel closed/error | Rebuild channel/PC within the same logical controller |
| Heartbeats | Unity sends every 2 seconds; retries despite missing pongs; reports last packet/ping/pong and RTT |
| No successful pong/input | 12-second stale threshold starts recovery, not removal |
| Restart attempt | 15 seconds; if unsuccessful rebuild peer/channel and negotiate again |
| Total recovery budget | 60 seconds from first recovery observation; repeated events do not extend it |
| Signaling loss | Retain healthy direct channel; reconnect after 1, 2, 4, 8, then 10 seconds; authenticate resume |
| Restored channel | Same controller ID, player number, ticket, session object, history and calibration |
| Explicit leave, invalid protocol, session expiry | Terminal removal; expired room needs current QR |

Unity Inspector fields on WebRtcLanControllerTransport expose grace, stale-heartbeat, attempt and total recovery times. PWA defaults match 8/12/60 seconds; keep them aligned if changing Unity values. With a silent link, the 12-second detection period precedes the 60-second budget. Native/browser ICE may detect earlier. Timer execution can be delayed by OS suspension.

Unity pauses controller input while recovering, locally cancels any held button without generating a release, and discards backlogged input. It requires a pong for a ping issued after recovery started, received within 3 seconds with RTT under 3 seconds, before resuming input. Calibration is retained, but a canceled gesture must be started again. Sensor timestamps are never latency-corrected.

## ICE restart and identity

Unity calls official Unity WebRTC `RestartIce()` then `CreateOffer()` / `SetLocalDescription()`. The host sends the new offer with a revision. The phone serializes remote SDP and answer creation; both sides buffer early ICE until their SDP message is sent and discard stale revisions. The host accepts a new answer for each negotiation. The phone never creates an offer, avoiding offer glare.

If restart stalls or the channel is closed, the host disposes the old callbacks/channel/PC, creates replacements and sends a new offer with `reset:true`. Generation guards ignore callbacks/coroutines from disposed peers. The existing logical peer and router Connection stay alive; `hello` authenticates the replacement channel with the original ticket and `welcome` returns the original player/controller ID. Unity gameplay still consumes controller abstractions, not WebRTC.

The signaling backend preserves detached records until explicit end/leave or original four-hour expiry. Host resume uses a secret separate from the QR; phone resume requires its peer-bound ticket. Full browser reload/OS process death loses the in-memory phone ticket. Backend process restart loses all rooms. These cases still require pairing; no persistent backend or credential storage was added.

## Diagnostics and deployment

Deploy the changes in the separate **Motion Controller Signaling** repository to the existing Render service first, then publish **Motion Controller Website**, then run the updated Unity scripts. No URL, QR workflow, firewall port or architecture changes are needed. The PWA service-worker cache version changes; close old phone tabs and open the controller again once after deployment.

In Unity's controller debug panel, enable **Show connection recovery diagnostics**. View PC/ICE/gathering/SDP/DataChannel states, signaling status, last packet/ping/pong times, RTT and the last 80 events. Console/Player.log lines start `[LAN]` with UTC and monotonic timestamps. PWA **Connection diagnostics** and browser console expose the corresponding states/errors. SDP, tickets and pairing secrets are omitted. Unity's supported API does not expose the browser's icecandidateerror event; candidate rejection and negotiation errors are logged instead.

## Reproduce/test

1. Pair, enable motion and calibrate. Record player/controller ID; verify motion/button input.
2. Unity diagnostics: **Test signaling reconnect (keep channel)**. Motion should continue, QR/ID/calibration should remain, and logs show authenticated signaling resume.
3. **Test ICE restart (keep player)**. Logs should show a new revision and fresh heartbeat recovery, with the same player and calibration.
4. Turn phone Wi-Fi off for 3–5 seconds, then restore the same LAN. Depending on OS ICE timing, recovery may be automatic without a new offer. Repeat for 15–20 seconds to exercise heartbeat recovery/restart. With TURN configured, a different network path may be selected.
5. Background the phone for 5–15 seconds and restore it. A held gesture must be canceled, never thrown late. Start another hold after recovery. Repeat screen dim/lock; mobile OS suspension varies.
6. Stop only signaling temporarily: an already healthy direct channel should keep working. Restarting the service process loses rooms, unlike a socket/network interruption, so that is a separate expiration test.
7. Leave the network unavailable longer than the recovery budget: logs should show failure/removal. Re-pair after final removal. Scan an expired QR to verify the explicit expiration error.
8. Connect two phones and interrupt one: the other's state/player must remain independent. Client-isolated guest Wi-Fi or a blocking Windows Firewall can prevent direct recovery indefinitely; TURN can provide a fallback when its endpoints remain reachable.

Automated verification uses the existing isolated Unity test project and a localhost HTTPS Chrome harness. It checks actual native ICE restart, host/phone signaling resume, forced channel replacement, calibration/player identity, motion/buttons/RTT, and scene persistence. Physical iPhone/Android Wi-Fi and OS suspension still require the manual checks above.
