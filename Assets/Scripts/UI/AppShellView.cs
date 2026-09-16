using System.Collections.Generic;
using MotionControllers.Core;
using UnityEngine;
using UnityEngine.UIElements;
namespace MotionControllers.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class AppShellView : MonoBehaviour
    {
        public SceneFlowManager flow;
        public VisualTreeAsset gameCardTemplate;
        public StyleSheet theme;
        private VisualElement shell, screen, modal, fade, documentRoot;
        private Label connectionSummary, notice, status, hud;
        private GameScoreboardView scoreboard;
        private GameVitalsView vitals;
        private Image qr;
        private Button continueButton, againButton, startButton;
        private bool settingsOpen;
        private readonly List<Label> readyPlayers = new List<Label>();
        private readonly List<GameCard> cards = new List<GameCard>();
        private readonly List<Label> playerHealth = new List<Label>();
        private double nextRefresh;
        private int gamePage, selectedTile;
        private VisualElement deck, bubbleA, bubbleB;
        private Label pageLabel;
        private Button previousPage, nextPage;
        private readonly List<GameDefinition> catalog = new List<GameDefinition>();
        public void MoveGameSelection(int delta)
        {
            if (flow.Screen != AppScreen.GameSelect || cards.Count == 0 || flow.Busy) return;
            selectedTile = Mathf.Clamp(selectedTile + delta, 0, cards.Count - 1);
            SelectTile(cards[selectedTile]); cards[selectedTile].Focus();
        }
        public void ChangeGamePage(int delta)
        {
            if (flow.Busy || catalog.Count == 0) return;
            gamePage = Mathf.Clamp(gamePage + delta, 0, (catalog.Count - 1) / 4);
            PopulateGames(); cards[0].Focus();
        }
        private void SelectTile(GameCard card)
        { selectedTile = cards.IndexOf(card); foreach (var tile in cards) tile.SetSelected(tile == card); }

        private void OnEnable()
        {
            var root = documentRoot = GetComponent<UIDocument>().rootVisualElement;
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (theme != null) root.styleSheets.Add(theme);
            shell = root.Q("shell"); screen = root.Q("screen"); modal = root.Q("modal"); fade = root.Q("fade");
            connectionSummary = root.Q<Label>("connection-summary"); notice = root.Q<Label>("connection-notice");
            root.Q("environment").style.backgroundImage = new StyleBackground(AeroSurfaces.Sky);
            bubbleA = root.Q("bubble-a"); bubbleB = root.Q("bubble-b");
            root.RegisterCallback<KeyDownEvent>(OnKey);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigate);
            if (flow != null) { flow.Changed += Render; Render(); }
        }
        private void OnDisable()
        {
            if (flow != null) flow.Changed -= Render;
            documentRoot?.UnregisterCallback<NavigationMoveEvent>(OnNavigate);
            documentRoot?.UnregisterCallback<KeyDownEvent>(OnKey); documentRoot = null;
        }
        private void OnNavigate(NavigationMoveEvent e)
        {
            if (flow.Screen != AppScreen.GameSelect || flow.Busy || !(e.target is VisualElement target) || !deck.Contains(target)) return;
            int delta = e.direction == NavigationMoveEvent.Direction.Left ? -1 : e.direction == NavigationMoveEvent.Direction.Right ? 1 : e.direction == NavigationMoveEvent.Direction.Up ? -2 : 2;
            MoveGameSelection(delta); e.PreventDefault(); e.StopPropagation();
        }
        private void OnKey(KeyDownEvent e)
        {
            if (flow.Busy) return;
            if (settingsOpen) { if (e.keyCode == KeyCode.Escape) { settingsOpen = false; Render(); e.StopPropagation(); } return; }
            if (flow.Screen == AppScreen.GameSelect && (e.keyCode == KeyCode.PageDown || e.keyCode == KeyCode.PageUp))
            { ChangeGamePage(e.keyCode == KeyCode.PageDown ? 1 : -1); e.StopPropagation(); return; }
            if (flow.Screen == AppScreen.GameSelect && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space) && e.target is GameCard)
            { if (catalog.Count > gamePage * 4 + selectedTile) flow.PrepareGame(catalog[gamePage * 4 + selectedTile]); e.StopPropagation(); return; }
            if (e.keyCode != KeyCode.Escape) return;
            if (flow.Screen == AppScreen.Playing) flow.TogglePause();
            else if (flow.Screen == AppScreen.Results || flow.Screen == AppScreen.Ready) flow.ShowGameSelect();
            else flow.ShowTitle();
            e.StopPropagation();
        }
        private static VisualElement Box(VisualElement parent, string classes)
        { var box = new VisualElement(); foreach (var c in classes.Split(' ')) box.AddToClassList(c); parent.Add(box); return box; }
        private static Label Text(VisualElement parent, string value, string classes)
        { var label = new Label(value); foreach (var c in classes.Split(' ')) label.AddToClassList(c); parent.Add(label); return label; }
        private static Button Action(VisualElement parent, string title, System.Action clicked, bool primary = false)
        { var button = new Button(clicked) { text = title }; if (primary) button.AddToClassList("primary"); parent.Add(button); return button; }
        private void Render()
        {
            readyPlayers.Clear(); startButton = null;
            scoreboard = null; vitals = null; screen.Clear(); modal.Clear(); cards.Clear(); playerHealth.Clear(); qr = null; status = hud = null; continueButton = againButton = null;
            shell.EnableInClassList("title-screen", flow.Screen == AppScreen.Title);
            shell.EnableInClassList("playing", flow.Screen == AppScreen.Playing);
            shell.EnableInClassList("scoreboard-game", flow.Screen == AppScreen.Playing);
            var content = Box(screen, "screen-content");
            switch (flow.Screen)
            {
                case AppScreen.Title: Title(content); break;
                case AppScreen.Pairing: Pairing(content); break;
                case AppScreen.GameSelect: GameSelect(content); break;
                case AppScreen.Ready: Ready(content); break;
                case AppScreen.Playing: Playing(content); break;
                case AppScreen.Results: Results(content); break;
            }
            if (!string.IsNullOrEmpty(flow.Error)) Text(content, flow.Error, "connection-notice");
            modal.style.display = settingsOpen || flow.Screen == AppScreen.Playing && flow.IsPaused ? DisplayStyle.Flex : DisplayStyle.None;
            if (settingsOpen) modal.Add(new SettingsPanel(flow, () => { settingsOpen = false; Render(); }));
            else if (flow.Screen == AppScreen.Playing && flow.IsPaused) Pause();
            AeroSurfaces.Apply(shell);
            Refresh();
            var first = (settingsOpen || flow.IsPaused ? modal : screen).Q<Button>();
            first?.schedule.Execute(() => first.Focus());
        }
        private void Title(VisualElement parent)
        {
            parent.AddToClassList("title-stage");
            Text(parent, "WELCOME TO", "eyebrow");
            Text(parent, "PHONE", "logo logo-top");
            Text(parent, "SPORTS", "logo logo-bottom");
            Text(parent, "Pick up your phone. Jump into the game.", "title-tagline");
            var actions = Box(parent, "title-actions"); Action(actions, "Play", flow.ShowPairing, true); Action(actions, "Settings", OpenSettings); Action(actions, "Quit", flow.Quit);
            //Text(parent, "BOWLING  •  TENNIS  •  SWORD DUEL", "eyebrow");
        }

        private void Pairing(VisualElement parent)
        {
            Text(parent, "GET READY", "eyebrow"); Text(parent, "Connect controllers", "heading");
            var row = Box(parent, "pair-row"); var left = Box(row, "panel qr-panel");
            qr = new Image { scaleMode = ScaleMode.ScaleToFit }; qr.AddToClassList("qr"); left.Add(qr);
            Text(left, "Scan to join · no app required", "qr-caption");
            var copy = Action(left, "Copy invite link", () => { if (flow.Lobby?.PairingUrl != null) GUIUtility.systemCopyBuffer = flow.Lobby.PairingUrl; });
            copy.clicked += () => { copy.text = "Link copied!"; copy.schedule.Execute(() => copy.text = "Copy invite link").StartingIn(1800); };
            var right = Box(row, "player-list");
            for (int i = 0; i < 4; i++)
            {
                var player = Box(right, "player-row"); Text(player, "Player " + (i + 1), "player-name");
                playerHealth.Add(Text(player, "Waiting", "player-health"));
            }
            Text(right, "Scan the code, then enable motion on your phone. Connect up to four players.", "body");
            status = Text(right, "Preparing pairing…", "status");
            var actions = Box(right, "actions"); continueButton = Action(actions, "Continue", flow.ContinueFromPairing, true); Action(actions, "Back", flow.ShowTitle);
        }
        private void GameSelect(VisualElement parent)
        {
            Text(parent, "THE PLAY ROOM", "eyebrow");
            Text(parent, "Choose your sport", "heading");
            catalog.Clear(); foreach (var game in flow.games) if (game != null) catalog.Add(game);
            gamePage = Mathf.Clamp(gamePage, 0, Mathf.Max(0, (catalog.Count - 1) / 4));
            deck = Box(parent, "game-deck");
            var actions = Box(parent, "selector-actions");
            Action(actions, "Controllers", flow.ShowPairing);
            Action(actions, "Settings", OpenSettings);
            previousPage = Action(actions, "◀ Previous", () => ChangeGamePage(-1));
            pageLabel = Text(actions, "", "page-indicator");
            nextPage = Action(actions, "Next ▶", () => ChangeGamePage(1));
            Action(actions, "Back", flow.ShowTitle);
            PopulateGames();
        }
        private void PopulateGames()
        {
            deck.Clear(); cards.Clear(); selectedTile = 0;
            for (int rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                var row = Box(deck, "game-row");
                for (int column = 0; column < 2; column++)
                {
                    int index = gamePage * 4 + rowIndex * 2 + column;
                    if (index >= catalog.Count) { Box(row, "card-host empty-tile"); continue; }
                    var card = new GameCard(catalog[index], index, gameCardTemplate, flow.PrepareGame, SelectTile);
                    row.Add(card); cards.Add(card);
                }
            }
            int pages = Mathf.Max(1, (catalog.Count + 3) / 4);
            pageLabel.text = $"{gamePage + 1} / {pages}";
            previousPage.SetEnabled(gamePage > 0); nextPage.SetEnabled(gamePage + 1 < pages);
            if (cards.Count > 0) SelectTile(cards[0]);
            AeroSurfaces.Apply(deck); Refresh();
        }

        private void OpenSettings() { settingsOpen = true; Render(); }
        private void Ready(VisualElement parent)
        {
            Text(parent, "BEFORE YOU PLAY", "eyebrow");
            Text(parent, flow.CurrentGame.displayName + " · Get ready", "heading");
            var row = Box(parent, "ready-layout");
            var guide = Box(row, "panel ready-guide");
            Text(guide, "1  Calibrate flat", "ready-heading");
            Text(guide, GamePreparation.CalibrationInstructions, "body");
            Text(guide, "2  Pick up and play", "ready-heading");
            Text(guide, GamePreparation.GripInstructions(flow.CurrentGame.controllerUiMode), "body");
            Text(guide, "Do not recalibrate in the upright playing grip. The flat pose defines forward for every game.", "settings-note");
            var players = Box(row, "ready-roster");
            if (flow.Preparation != null) foreach (var id in flow.Preparation.Players)
            {
                int number = 0; foreach (var slot in flow.Lobby.Slots) if (slot.ControllerId == id) number = slot.PlayerNumber;
                Text(players, "PLAYER " + number, "ready-heading");
                readyPlayers.Add(Text(players, "Calibrate on your phone", "ready-player"));
            }
            Text(players, "Each player taps Ready on their phone. Then start on the big screen.", "body");
            var actions = Box(parent, "actions");
            startButton = Action(actions, "Start game", flow.StartPreparedGame, true);
            Action(actions, "Settings", OpenSettings); Action(actions, "Back", flow.ShowGameSelect);
        }
        private void Playing(VisualElement parent)
        {
            parent.style.justifyContent = Justify.SpaceBetween;
            var upper = Box(parent, "hud-upper");
            var top = Box(upper, "hud-top");
            Action(top, "Pause", flow.TogglePause);
            if (flow.GameSession is IGameScoreboard) { scoreboard = new GameScoreboardView(); upper.Add(scoreboard); }
            else { if(flow.GameSession is IGameVitals) { vitals = new GameVitalsView(); upper.Add(vitals); } hud = Text(upper, "Ready to play", "hud-status"); }
            // Absolute-positioned corner controls must be above the full-width score strip for picking.
            top.BringToFront();
        }
        private void Results(VisualElement parent)
        {
            var result = Box(parent, "panel result"); Text(result, "FINAL RESULTS", "eyebrow");
            Text(result, (flow.CurrentGame?.displayName ?? "Game") + " complete", "heading");
            Text(result, flow.Result.Summary, "result-stat"); Text(result, flow.Result.Detail, "body");
            Text(result, "Your controller is still paired. Ready for another round?", "body");
            var actions = Box(result, "actions"); againButton = Action(actions, "Play again", flow.Restart, true);
            againButton.SetEnabled(flow.CurrentGame != null && flow.CurrentGame.CanPlay(flow.Lobby?.ConnectedPlayers ?? 0));
            Action(actions, "Game select", flow.ShowGameSelect);
        }
        private void Pause()
        {
            var panel = Box(modal, "panel modal-panel");
            Text(panel, flow.ConnectionBlocked ? "Controller interrupted" : "Paused", "heading");
            Text(panel, flow.ConnectionBlocked ? "Attempting to reconnect. Your player and calibration stay here while recovery runs. Return to Controllers if a new pairing is needed." : "Game paused. Your phone stays connected.", "body");
            var resume = Action(panel, "Resume", flow.Resume, true); resume.SetEnabled(!flow.ConnectionBlocked);
            Text(panel, GamePreparation.CalibrationInstructions + "\n" + GamePreparation.GripInstructions(flow.CurrentGame.controllerUiMode), "body");
            var restart = Action(panel, "Restart Game", flow.Restart); restart.SetEnabled(flow.CurrentGame.CanPlay(flow.Lobby?.ConnectedPlayers ?? 0));
            Action(panel, "Settings", OpenSettings);
            Action(panel, "Return to game select", flow.ShowGameSelect);
        }
        private void Update()
        {
            if (flow == null || fade == null) return;
            float drift = Mathf.Sin(Time.unscaledTime * .35f) * 12f;
            if (bubbleA != null) bubbleA.style.translate = new Translate(0, drift);
            if (bubbleB != null) bubbleB.style.translate = new Translate(0, -drift);
            fade.style.opacity = flow.Fade; fade.pickingMode = flow.Busy ? PickingMode.Position : PickingMode.Ignore;
            screen.SetEnabled(!flow.Busy); modal.SetEnabled(!flow.Busy);
            if (Time.realtimeSinceStartupAsDouble >= nextRefresh) { nextRefresh = Time.realtimeSinceStartupAsDouble + 0.2; Refresh(); }
        }
        private void Refresh()
        {
            var lobby = flow.Lobby; int connected = lobby?.ConnectedPlayers ?? 0;
            connectionSummary.text = connected == 0 ? "Connect a phone to play" : connected + (connected == 1 ? " player connected" : " players connected");
            if (flow.Preparation != null)
            {
                for (int i = 0; i < readyPlayers.Count; i++) {
                    var id = flow.Preparation.Players[i];
                    bool ready = flow.Preparation.IsReady(id);
                    readyPlayers[i].text = !lobby.IsConnected(id) ? "Reconnecting…" : ready ? "Ready!" : flow.Preparation.IsCalibrated(id) ? "Pick up your phone and tap Ready" : "Enable motion and calibrate flat";
                    readyPlayers[i].EnableInClassList("connected", ready);
                }
                startButton?.SetEnabled(flow.Preparation.AllReady);
            }
            if (qr != null) qr.image = lobby?.PairingQr;
            if (status != null) status.text = lobby == null ? "Controller system unavailable" : connected > 0 ? "Ready when you are." : lobby.PairingQr != null ? "Waiting for your first player…" : lobby.Status;
            if (againButton != null) againButton.SetEnabled(flow.CurrentGame != null && flow.CurrentGame.CanPlay(connected));
            if (continueButton != null) continueButton.SetEnabled(connected > 0);
            if (vitals != null && flow.GameSession is IGameVitals health) vitals.Refresh(health);
            if (hud != null) hud.text = flow.GameSession?.Status ?? "Loading…";
            if (scoreboard != null && flow.GameSession is IGameScoreboard source) scoreboard.Refresh(source.Scoreboard);
            foreach (var card in cards) card.Refresh(connected);
            string issue = "";
            if (lobby != null) foreach (var slot in lobby.Slots)
            {
                if (slot.PlayerNumber <= playerHealth.Count)
                {
                    var label = playerHealth[slot.PlayerNumber - 1]; label.text = slot.Health == PlayerConnectionHealth.Connected ? "Ready" :
                        slot.Health == PlayerConnectionHealth.Waiting ? "Scan to join" :
                        slot.Health == PlayerConnectionHealth.Recovering ? "Reconnecting…" : "Disconnected";
                    label.parent.EnableInClassList("player-ready", slot.Health == PlayerConnectionHealth.Connected);
                    label.EnableInClassList("connected", slot.Health == PlayerConnectionHealth.Connected);
                    label.EnableInClassList("recovering", slot.Health == PlayerConnectionHealth.Recovering);
                }
                if (slot.Health == PlayerConnectionHealth.Recovering || slot.Health == PlayerConnectionHealth.Disconnected)
                    issue += $"Player {slot.PlayerNumber}: {slot.Health}. ";
            }
            notice.text = issue; notice.style.display = string.IsNullOrEmpty(issue) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
