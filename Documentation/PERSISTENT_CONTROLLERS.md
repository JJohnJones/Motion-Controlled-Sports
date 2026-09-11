# Controller lifetime across scenes

Previously, `ControllerManager` and the LAN transport belonged to the game scene. Unloading it ran transport `OnDisable`, closed PeerConnections/signaling and discarded controller sessions. The original bowling generator also attached gameplay to that same object. Persisting that mixed object would incorrectly keep old gameplay alive.

The controller subsystem now supports a dedicated `PersistentControllerRoot` using `DontDestroyOnLoad`. There is no separate class named `ControllerSessionManager`: the existing manager owns controller states/history/calibration, while `WebRtcLanControllerTransport` and `ControllerProtocolRouter` own network/session lifecycle. All remain on the same persistent root and keep the same instances across scene changes.

## Required one-time scene migration

1. Exit Play Mode. In the existing scene select **Controller System** (the object with `ControllerManager`).
2. Run **Tools → Motion Controllers → Make Selected Controller System Persistent**.
3. The tool retains manager/transports and their current Inspector settings on that object, adds `PersistentControllerRoot`, and moves bowling logic and the debug panel onto separate scene-local objects. It preserves their serialized settings and remaps scene component references. The operation supports Undo.
4. Save the scene. If the root has children or other custom components, the tool stops without migrating: move those game/visual objects outside the controller root first. The persistent root must be top-level and controller-only.
5. Your initial scene's LAN/signaling/PWA settings remain authoritative. **Enable LAN WebRTC On Selected Controller System** now includes this migration, and both prototype generators produce the separated layout.

Existing saved scenes are **not silently rewritten**, particularly while the Editor may hold unsaved changes. Run the migration on the current scene before relying on persistence. Run it on other older prototype scenes that contain their own mixed controller system, too.

## Future application layout

```text
Startup / Bootstrap (first scene in build)
  Controller System → moved to DontDestroyOnLoad
    PersistentControllerRoot
    ControllerManager
    WebRtcLanControllerTransport
    ControllerReceiver (optional disabled fallback)

Current menu or game scene → unload normally
  pairing/debug UI (optional)
  gameplay controller → IControllerButtonSource / IMotionInputSource
  cameras, ball/pins, effects, scene UI
```

No bootstrap scene, menus, or scene navigation were added. When you build startup later, place the configured controller-only root there and then load menu/game scenes normally, including `LoadSceneMode.Single`. Do not parent game objects beneath the root or move it into a game scene. Its runtime guard rejects mixed hierarchies to avoid silently persisting a game.

A scene may contain its own configured root for direct Editor testing. During normal navigation the first root wins: a duplicate root is deactivated in early `Awake`, before its transport starts, then destroyed. Its Inspector values do not replace the active session. Gameplay must live outside that duplicate root.

`ControllerInput.Resolve(inputSource)` returns the persistent source for an empty input field or a scene-local `ControllerManager` reference. Explicit alternative input adapters are preserved. Bowling and the cube visualizer use this resolver, then consume their existing input interfaces; neither knows about WebRTC or signaling. Future scenes should leave the input field empty to use the persistent source. Load startup before such scenes in builds. A directly opened game scene without a controller root or explicit input adapter will not create/configure a hidden networking service.

The debug panel is scene-local and resolves the active persistent manager in `Start`. Each scene can display its own pairing UI without recreating the QR/session. A scene without this panel still receives input. Bowling subscribes only while enabled and unsubscribes on disable, so its old event handler does not survive scene unload. An in-progress game hold is canceled on exit; a new game does not reinterpret an already-held button's eventual release as a new throw. Phone pairing/calibration/history are retained.

## What should and should not reset

Scene changes retain signaling, session capability/join URL, QR texture, PeerConnections/DataChannels, controller IDs/player slots, calibration and motion histories. New game objects receive current controller input through the abstraction.

Quitting/exiting Play, explicitly destroying/disabling the root, disabling its transport, clicking **New controller session / QR**, network/service failure, or the signaling service's four-hour expiry can still disconnect/rotate sessions. Persistence does not override those existing network policies. Normal scene transitions must not call those reset operations. Online game networking remains a separate future responsibility.

## Verify manually

1. Pair and calibrate a phone in the migrated scene; note its controller ID and QR.
2. Load another migrated game/test scene using `SceneManager.LoadScene` or `LoadSceneAsync`, including Single mode. Do not stop Play.
3. In the Hierarchy, verify exactly one Controller System under `DontDestroyOnLoad`. Old gameplay/UI objects should be gone.
4. Confirm the same phone ID and calibration, unchanged QR, increasing packet count and live RTT. No rescan should occur.
5. Load the original scene again. Its duplicate root should disappear before opening a second session; its fresh scene UI/game should bind to the existing input.
6. Repeat while holding the phone button: the old game cancels its hold, and the next game requires a fresh press. Repeat with two phones to check independent retained states.

Automated tests cover scene unload, retained session/calibration/history, duplicate-root rejection, and a new scene-local input consumer. The optional real-browser/native integration also checks unchanged pairing URL/protocol/session and continuing motion after scene unload. Physical phone navigation remains a manual acceptance check.

Validation on Unity 6000.6.0f1: **22 regression tests passed**, including the scene migration/reference-remapping test. The optional integration was skipped in that ordinary run and **passed separately with real Chrome ↔ native Unity WebRTC**, retaining the same URL, protocol, controller session and calibration while motion continued after scene unload.
