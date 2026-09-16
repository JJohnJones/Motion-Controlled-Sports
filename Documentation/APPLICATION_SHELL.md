# Application shell

## Run it

1. Exit Play Mode and let Unity finish importing/compiling.
2. Open `Assets/Scenes/Bootstrap.unity` and press Play. Bootstrap is also the first enabled scene in Build Settings; the original prototype scenes remain available for development.
3. Choose **Let's play**, scan the large QR, then enable motion and calibrate on the phone. Continue becomes available with one connected controller.
4. Choose Bowling. Aim, hold, swing and release using the existing phone controls.
5. Complete all ten Bowling frames to reach ranked results automatically, then **Game select** or **Play again**. **Exit to game select** abandons an unfinished match. No new QR is created by navigation.
6. **Pause** or Escape opens the shared pause modal. Resume, restart the current game, or return to game selection. Bowling supports 1–4 players and standard ten-frame scoring; no second game is included.

The editor command **Tools > Motion Sports > Create or Open Application Shell** opens the existing shell and registers its build scenes. On a project without generated shell assets it creates them from the saved `BowlingPrototype.unity`. It asks Unity to save modified scenes before doing that. It does not overwrite an existing shell or rebuild your original Bowling scene. **Register Shell Scenes in Build** restores the two required build entries without removing other scenes. If using a Unity Build Profile with its own scene-list override, include Bootstrap first and Games/Bowling there too.

## Ownership and flow

Bootstrap stays loaded for the lifetime of the app. It has two clearly scoped prefab instances:

- `ControllerCore`: the existing strict, controller-only `PersistentControllerRoot`, ControllerManager, WebRTC transport. The original duplicate-root guard and DontDestroyOnLoad behavior are retained. Configure the HTTPS PWA and WSS signaling URLs **here**, not in Bowling. Generated defaults were copied from the saved working prototype.
- `ApplicationShell`: scene flow, controller UI adapter, UIDocument, screen presenter and a menu background camera. It stays in Bootstrap, with no additional DontDestroyOnLoad singleton. Do not unload Bootstrap or use external Single scene loads for menu navigation.

Title, Pairing, Game Select, Results and Pause are UI views, not separate empty scenes. `SceneFlowManager` is the sole runtime scene-loading entry point. It loads games additively, unloads only the previous game scene, and uses an unscaled fade. The Bootstrap UI stays alive over games. Double navigation is guarded while a transition runs; missing scene/build configuration produces an on-screen error.

`Assets/Scenes/Games/Bowling.unity` is a copy of the existing saved prototype with its local controller root and old IMGUI debug component removed, a full-width camera, and a `BowlingGameSession` adapter. Existing physics, materials, pins, lane and release calculations are reused. The original prototype remains unchanged.

## Reskinning and future games

The shell uses UI Toolkit throughout. Edit:

- `Assets/UI/Layout/AppShell.uxml`: common frame and view/overlay slots.
- `Assets/UI/Layout/GameCard.uxml`: reusable card template.
- `Assets/UI/Styles/AppShell.uss`: colors, typography, cards, spacing, focus/disabled styling and overlays.
- `Assets/UI/Generated/ShellPanel.asset`: scale-with-screen-size panel, 1280×720 reference, Expand mode. Layout uses flex rows/columns and a two-row paged game selector. The QR has a dedicated 300-unit square with its original quiet border.
- `Assets/UI/Generated/ApplicationShell.prefab`: UI document, templates, style sheet, flow and catalog assignments.

To add a game later:

1. Create a GameDefinition via **Assets > Create > Motion Sports > Game Definition**. Set display name, description, instructions, optional sprite, accent, player range, scene path and controller UI mode.
2. Create a scene with one component implementing `IGameSession`: selected controller IDs, status, pause and basic result generation. Consume controller/input abstractions in the game; do not create a transport or controller root there.
3. Include the scene in the build/profile. Add the definition to the ApplicationShell prefab's SceneFlowManager **Games** array and enable **Available**. The same card template and navigation now handle it.

Tennis, Golf and Future Game definitions are unavailable placeholders. Bowling currently selects one connected controller; other paired phones stay available but idle. Player assignments are passed explicitly to the game adapter, so a temporarily missing phone cannot silently be replaced by a different player.

Buttons call public flow actions, not SceneManager. UI Toolkit button focus/navigation remains available for desktop keyboard input; Escape handles back/pause. A later phone/gamepad presenter can call those same actions without changing the screen layout or scene loader.

## Pause and recovery

`IControllerLobby` supplies QR, player slots and connection health to UI; `ControllerLobbyAdapter` is the only shell component translating networking state. Games do not depend on it or WebRTC. Pairing shows four slots, including Recovering and Disconnected without erasing an identity that networking is retaining.

The common connection notice is available on all screens. In a game, interruption of an assigned controller opens the shared recovery modal and pauses gameplay. A healthy unused controller does not substitute automatically. Successful recovery resumes the game unless the user had also paused manually. After final transport removal, return to Game Select > Controllers to pair again.

Physics pauses via timeScale, while WebRTC, signaling, heartbeat, controller input processing and fades remain unscaled. Bowling's pause boundary cancels held gestures, ignores button input while paused and adjusts its wall-clock roll/reset timers on resume. Starting, restarting, pausing or leaving a game cancels any held input; a stale release must never become a throw after navigation. Calibration and sensor history remain in the persistent controller session.

## Phone UI-mode extension

`SceneFlowManager.ControllerUiMode` exposes `menu`, `pairing`, or the selected definition's mode. `ControllerUiModeChanged` is the extension event for a future transport presenter. A presenter should read the current mode on subscription, then subscribe for changes and send it on each controller welcome/recovery as needed. This phase intentionally does not introduce a new wire message or change the deployed PWA; its existing motion, calibration and Hold Ball controls continue to work.

## Verification checklist

- Title > Pairing > Game Select > Bowling > Finish > Results > Game Select > Bowling: controller ID, player and calibration remain unchanged; only one ControllerManager exists.
- Pause during a hold and release on the phone: no ball launch. Resume requires a new hold. Pause while rolling and resume: no immediate reset merely because time passed while paused.
- Restart and Return to game select restore normal timeScale; networking continues while paused.
- Interrupt the assigned phone: recovery modal and player status update; restore connection and verify the same calibrated controller resumes. In menus the connection notice updates without forcibly navigating away.
- Coming-soon cards cannot load scenes. Continue/Play are unavailable without enough connected controllers.
- Check 1280×720 and 1920×1080, plus a narrower window; text/buttons should remain usable and the game selector uses explicit pages instead of scrolling.

Automated tests cover the catalog/card gate, the complete scene/navigation loop, one controller manager after loading Bowling, retained ControllerSession/calibration, live input during pause, recovery pause/resume, game restart, and cancellation of a paused Bowling release. The existing controller, motion and Bowling regressions run alongside these tests. Physical phone/WebRTC pairing through the new UI remains a manual device acceptance check.

Verification result: 44 Unity tests passed; the optional external-browser integration test was skipped. An isolated Windows development build succeeded. Full visual/resolution acceptance was stopped at the user’s request; physical phone testing through the new shell remains manual.


## Aero interface redesign

The shared USS theme now supplies aqua/green environmental colors, beveled glossy controls, bright focus rims, player badges, and consistent pause/results styling. AeroSurfaces supplies two small shared procedural textures; replace that surface provider or the USS without changing scene flow. The built-in LegacyRuntime font remains in use. Decorative background elements ignore pointer input and drift using unscaled time.

Game Select previously used a vertical ScrollView around a wrapping card grid with fixed art/description heights. This competed with headings and footer actions for the remaining viewport height. It now uses two equal-height flex rows with two tiles per row, a shrinking bounded content area, and a reserved navigation strip. Four definitions fit each page; extra definitions use Previous/Next or Page Up/Page Down. No ScrollView remains in the selection hierarchy. PanelSettings still uses the 1280×720 Expand reference, so 720p, 1080p and 1440p share the same logical 16:9 composition.

Click/focus selects a tile; its Play button launches only if available and enough controllers are connected. Arrow-key/navigation events move the highlight, and Enter/Space on a focused tile invokes the existing guarded Play action. Coming-soon tiles can be highlighted but cannot launch. Future input presenters can call MoveGameSelection and ChangeGamePage. GameDefinition icons replace the numbered placeholder artwork without layout changes.

Functional tests cover paging past four games, selection, absence of a ScrollView, and the existing full menu/game/controller-persistence flow. Visual checks were explicitly skipped for this redesign.


## Gameplay visibility and camera ownership

The menu's Aero background stays opaque only on menu/results screens. `.shell.playing` explicitly overrides theme colors to transparent, and `.playing .environment` hides sky decorations. HUD/pause/fade layers remain available; the full-screen shell must not paint over gameplay. The previous same-specificity `.playing` rule was overridden by a later blue `.shell` rule, causing the solid-blue Bowling view even with a functioning camera.

SceneFlowManager remains the single camera/scene-state owner. A game scene must provide one active GameObject with a MainCamera-tagged URP Base camera (or normal Camera), targeting Display 1 with no render texture and a nonempty culling mask. The flow validates it, enables that game camera and disables the assigned menu camera. Additional enabled Base world outputs on that display are rejected with an actionable load error; texture cameras and the game's own URP overlays are allowed. The game scene becomes the active scene. Results unload the game and restore the menu camera/scene. Core has no gameplay camera.

Bowling's saved camera now sits at (0,5,-8), looking toward (0,0,4), with a full viewport and 52-degree vertical FOV. This frames the release area, lane and pins above the bottom HUD at 16:9. The shell generator uses the same framing. Ball/pin physics and input logic are unchanged. Existing scenes need only reload/import; do not regenerate Bootstrap to apply this fix.

The flow regression test checks resolved gameplay-background alpha, hidden sky, menu/game camera switching, active scene, MainCamera lookup, display/culling settings and projected framing. These are nonvisual checks; no screenshot review is required.

Bowling match integration and setup are documented in BOWLING_MATCH.md. Optional IGameCompletion and IGameScoreboard interfaces let games report completion and scoreboard data without managing global UI or networking.
