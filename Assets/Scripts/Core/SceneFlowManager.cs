using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
namespace MotionControllers.Core
{
    public enum AppScreen { Title, Pairing, GameSelect, Playing, Results }
    public sealed class SceneFlowManager : MonoBehaviour
    {
        public MonoBehaviour controllerLobby;
        public Camera menuCamera;
        public GameDefinition[] games = Array.Empty<GameDefinition>();
        [Range(0, 1)] public float fadeSeconds = 0.2f;
        public IControllerLobby Lobby => controllerLobby as IControllerLobby;
        public AppScreen Screen { get; private set; } = AppScreen.Title;
        public GameDefinition CurrentGame { get; private set; }
        public GameResult Result { get; private set; }
        public IGameSession GameSession { get; private set; }
        public Camera GameCamera { get; private set; }
        public string Error { get; private set; }
        public bool Busy { get; private set; }
        public bool IsPaused => userPaused || ConnectionBlocked;
        public bool ConnectionBlocked { get; private set; }
        public float Fade { get; private set; }
        public string ControllerUiMode { get; private set; } = "menu";
        public event Action Changed;
        // Presentation state only; the lobby adapter delivers it over the controller abstraction.
        public event Action<string> ControllerUiModeChanged;
        private readonly List<string> requiredControllers = new List<string>();
        private readonly List<string> lastParticipants = new List<string>();
        private Scene gameScene;
        private bool userPaused, appliedPause;
        private float runningTimeScale = 1;
        public void ShowPairing() { Navigate(AppScreen.Pairing); }
        public void ShowTitle() { Navigate(AppScreen.Title); }
        public void ShowGameSelect() { Navigate(AppScreen.GameSelect); }
        public void ContinueFromPairing() { if (Lobby?.ConnectedPlayers > 0) Navigate(AppScreen.GameSelect); }
        public void Play(GameDefinition game)
        {
            if (Busy || game == null || !game.CanPlay(Lobby?.ConnectedPlayers ?? 0)) return;
            if (!Application.CanStreamedLevelBeLoaded(game.scenePath))
            { Error = "Game scene is not in the build: " + game.scenePath; Changed?.Invoke(); return; }
            var roster = new List<string>();
            if (Lobby != null) foreach (var slot in Lobby.Slots)
                if (slot.Health == PlayerConnectionHealth.Connected && roster.Count < game.maximumPlayers) roster.Add(slot.ControllerId);
            if (roster.Count < game.minimumPlayers) return;
            StartCoroutine(Transition(AppScreen.Playing, game, roster));
        }
        public void Restart() {
            if (!Busy && CurrentGame != null && lastParticipants.Count > 0)
                StartCoroutine(Transition(AppScreen.Playing, CurrentGame, new List<string>(lastParticipants)));
        }
        public void FinishGame()
        {
            if (Busy || Screen != AppScreen.Playing || GameSession is IGameCompletion completion && !completion.IsComplete) return;
            Result = GameSession?.Finish() ?? new GameResult("Session complete", "Thanks for playing.");
            Navigate(AppScreen.Results);
        }
        public void TogglePause() { if (!Busy && Screen == AppScreen.Playing) { userPaused = !userPaused; ApplyPause(); Changed?.Invoke(); } }
        public void Resume() { if (!Busy && !ConnectionBlocked) { userPaused = false; ApplyPause(); Changed?.Invoke(); } }
        public void Quit() { Application.Quit(); }
        private void Navigate(AppScreen target)
        { if (!Busy) StartCoroutine(Transition(target, null)); }
        private IEnumerator FadeTo(float target)
        {
            float start = Fade, elapsed = 0;
            while (elapsed < fadeSeconds)
            { elapsed += Time.unscaledDeltaTime; Fade = Mathf.Lerp(start, target, fadeSeconds <= 0 ? 1 : elapsed / fadeSeconds); yield return null; }
            Fade = target;
        }
        private IEnumerator Transition(AppScreen target, GameDefinition game, IReadOnlyList<string> preservedPlayers = null)
        {
            Busy = true; Error = null; Lobby?.CancelHeldInput();
            GameSession?.SetPaused(true); Time.timeScale = 0;
            Changed?.Invoke(); yield return FadeTo(1);
            if (gameScene.IsValid() && gameScene.isLoaded)
            { GameSession = null; yield return SceneManager.UnloadSceneAsync(gameScene); }
            gameScene = default; GameCamera = null;
            requiredControllers.Clear(); userPaused = ConnectionBlocked = appliedPause = false;
            if (target == AppScreen.Playing)
            {
                CurrentGame = game;
                AsyncOperation load = null;
                try { load = SceneManager.LoadSceneAsync(game.scenePath, LoadSceneMode.Additive); }
                catch (Exception e) { Error = "Could not load game: " + e.Message; }
                if (load != null)
                {
                    yield return load;
                    gameScene = SceneManager.GetSceneByPath(game.scenePath);
                    if (gameScene.IsValid())
                    {
                        SceneManager.SetActiveScene(gameScene);
                        foreach (var root in gameScene.GetRootGameObjects())
                            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                                if (component is IGameSession session) GameSession = session;
                    }
                }
                if (GameSession == null || !PrepareGameplayCamera())
                {
                    Error = Error ?? "The game scene needs a component implementing IGameSession.";
                    if (gameScene.IsValid() && gameScene.isLoaded) yield return SceneManager.UnloadSceneAsync(gameScene);
                    gameScene = default; GameSession = null; GameCamera = null; target = AppScreen.GameSelect;
                }
                else if (preservedPlayers != null) requiredControllers.AddRange(preservedPlayers);
                else if (Lobby != null)
                {
                    foreach (var slot in Lobby.Slots)
                        if (slot.Health == PlayerConnectionHealth.Connected && requiredControllers.Count < game.maximumPlayers)
                            requiredControllers.Add(slot.ControllerId);
                }
            }
            if (target == AppScreen.Playing) { lastParticipants.Clear(); lastParticipants.AddRange(requiredControllers); }
            GameSession?.SetControllers(requiredControllers);
            GameSession?.SetPaused(true);
            Screen = target;
            if (menuCamera != null) menuCamera.enabled = target != AppScreen.Playing;
            if (target != AppScreen.Playing) SceneManager.SetActiveScene(gameObject.scene);
            SetMode(target == AppScreen.Playing ? game.controllerUiMode : target == AppScreen.Pairing ? "pairing" : "menu");
            Lobby?.CancelHeldInput(); Changed?.Invoke();
            yield return FadeTo(0);
            if (target == AppScreen.Playing)
            {
                ConnectionBlocked = requiredControllers.Count < game.minimumPlayers;
                foreach (var id in requiredControllers) if (Lobby == null || !Lobby.IsConnected(id)) ConnectionBlocked = true;
            }
            appliedPause = IsPaused; GameSession?.SetPaused(IsPaused);
            Time.timeScale = IsPaused ? 0 : runningTimeScale;
            Lobby?.CancelHeldInput(); Busy = false; Changed?.Invoke();
        }
        // One scene-owned MainCamera is the world output. Other base cameras must be
        // disabled or render to a texture; URP overlays belong to the game's own stack.
        private bool PrepareGameplayCamera()
        {
            var outputs = new List<Camera>();
            foreach (var root in gameScene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>())
                {
                    var data = camera.GetComponent<UniversalAdditionalCameraData>();
                    if (camera.targetTexture == null && camera.targetDisplay == 0 &&
                        (data == null || data.renderType == CameraRenderType.Base)) outputs.Add(camera);
                }
            foreach (var camera in outputs) if (camera.CompareTag("MainCamera"))
            {
                if (GameCamera != null) { Error = "Game scene has multiple MainCamera world outputs."; return false; }
                GameCamera = camera;
            }
            if (GameCamera == null || GameCamera.cullingMask == 0)
            { Error = "Game scene needs an active MainCamera Base camera on Display 1, with world layers and no target texture."; return false; }
            foreach (var camera in outputs) if (camera != GameCamera && camera.enabled)
            { Error = "Game scene has another enabled Base camera: " + camera.name; return false; }
            GameCamera.enabled = true;
            if (Application.isEditor || Debug.isDebugBuild)
                Debug.Log($"[Scene flow] World camera='{GameCamera.name}', scene='{gameScene.path}', activeScene='{SceneManager.GetActiveScene().path}', " +
                    $"position={GameCamera.transform.position}, rotation={GameCamera.transform.eulerAngles}, mask={GameCamera.cullingMask}, display={GameCamera.targetDisplay}, depth={GameCamera.depth}");
            return true;
        }
        private void SetMode(string mode)
        { if (ControllerUiMode == mode) return; ControllerUiMode = mode; ControllerUiModeChanged?.Invoke(mode); }
        private void Update()
        {
            if (controllerLobby is IControllerUiPresenter presenter)
            {
                presenter.PresentControllerUi(ControllerUiMode, Busy || IsPaused);
                presenter.PresentControllerStates(GameSession as IGameControllerFeedback);
            }
            if (Screen != AppScreen.Playing || Busy) return;
            if (GameSession is IGameCompletion complete && complete.IsComplete) { FinishGame(); return; }
            bool blocked = requiredControllers.Count < (CurrentGame != null ? CurrentGame.minimumPlayers : 1);
            foreach (var id in requiredControllers) if (Lobby == null || !Lobby.IsConnected(id)) blocked = true;
            if (ConnectionBlocked != blocked) { ConnectionBlocked = blocked; ApplyPause(); Changed?.Invoke(); }
        }
        private void ApplyPause()
        {
            if (appliedPause == IsPaused) return;
            appliedPause = IsPaused; Lobby?.CancelHeldInput(); GameSession?.SetPaused(IsPaused);
            Time.timeScale = IsPaused ? 0 : runningTimeScale;
        }
        private void OnEnable() { runningTimeScale = Time.timeScale > 0 ? Time.timeScale : 1; }
        private void OnDisable() { StopAllCoroutines(); Time.timeScale = runningTimeScale; }
    }
}
