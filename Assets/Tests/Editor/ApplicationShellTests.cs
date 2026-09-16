using System.Collections;
using System.Collections.Generic;
using MotionControllers.Core;
using MotionControllers.UI;
using MotionControllers.Bowling;
using MotionControllers.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
namespace MotionControllers.Tests
{
    public sealed class ShellTestLobby : MonoBehaviour, IControllerLobby
    {
        public bool online;
        public int participants = 1;
        public ControllerManager manager;
        private readonly ControllerSlot[] slots = {
            new ControllerSlot { PlayerNumber=1,ControllerId="shell-phone" }, new ControllerSlot { PlayerNumber=2,ControllerId="shell-phone2" },
            new ControllerSlot { PlayerNumber=3,ControllerId="shell-phone3" }, new ControllerSlot { PlayerNumber=4,ControllerId="shell-phone4" } };
        public IReadOnlyList<ControllerSlot> Slots { get { for(int i=0;i<slots.Length;i++) slots[i].Health = i >= participants ? PlayerConnectionHealth.Waiting : online ? PlayerConnectionHealth.Connected : PlayerConnectionHealth.Recovering; return slots; } }
        public int ConnectedPlayers => online ? participants : 0;
        public Texture PairingQr => null;
        public string PairingUrl => "https://controller.example/#test";
        public string Status => online ? "Connected" : "Recovering";
        public bool IsConnected(string id) { for(int i=0;i<participants;i++) if(slots[i].ControllerId==id) return online; return false; }
        public void CancelHeldInput() { manager.CancelHeldInput("shell-phone"); }
    }
    public sealed class ApplicationShellTests
    {
        [Test] public void ShellPrefabHasExplicitPanelAndLayoutAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreateApplicationShell.Generated + "/ApplicationShell.prefab");
            Assert.That(prefab.GetComponent<UIDocument>().panelSettings, Is.Not.Null);
            Assert.That(prefab.GetComponent<UIDocument>().visualTreeAsset, Is.Not.Null);
        }
        [Test] public void GameCardUsesAvailabilityAndRequiredPlayerCount()
        {
            var definition = ScriptableObject.CreateInstance<GameDefinition>();
            try
            {
                definition.displayName = "A future game"; definition.scenePath = "Assets/Future.unity"; definition.minimumPlayers = 2;
                var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Layout/GameCard.uxml");
                var card = new GameCard(definition, 0, template, _ => Assert.Fail("Should not navigate during binding"));
                card.Refresh(4); Assert.That(card.Q<Button>("play").enabledSelf, Is.False);
                definition.available = true; card.Refresh(1); Assert.That(card.Q<Button>("play").enabledSelf, Is.False);
                card.Refresh(2); Assert.That(card.Q<Button>("play").enabledSelf, Is.True);
                Assert.That(card.Q<Label>("title").text, Is.EqualTo(definition.displayName));
            }
            finally { Object.DestroyImmediate(definition); }
        }
        [UnityTest] public IEnumerator FullShellFlowRetainsControllerAndPausesOnlyGameplay()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CreateApplicationShell.BootstrapPath) == null)
                CreateApplicationShell.Create();
            yield return new EnterPlayMode();
            // EnterPlayMode may retain the runner's default scene camera. This fixture
            // instantiates Bootstrap's shell prefab, so isolate it as a real Bootstrap launch would.
            var unrelatedCameras = new List<Camera>();
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                if (camera.enabled) { unrelatedCameras.Add(camera); camera.enabled = false; }
            var core = new GameObject("Shell test controller core"); core.SetActive(false);
            var manager = core.AddComponent<ControllerManager>(); core.AddComponent<PersistentControllerRoot>(); core.SetActive(true);
            manager.Register("shell-phone"); manager.Register("shell-phone2"); manager.Register("shell-phone3"); manager.Register("shell-phone4");
            manager.Submit(new MotionFrame { ControllerId = "shell-phone", Sequence = 1, TimestampMs = 1, Orientation = Quaternion.identity,
                ReceivedAtSeconds = Time.realtimeSinceStartupAsDouble }, true);
            var identity = manager.Sessions["shell-phone"];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreateApplicationShell.Generated + "/ApplicationShell.prefab");
            var shell = Object.Instantiate(prefab);
            var fake = shell.AddComponent<ShellTestLobby>(); fake.manager = manager; fake.participants = 4;
            var flow = shell.GetComponent<SceneFlowManager>(); flow.controllerLobby = fake; flow.fadeSeconds = 0;
            var doc = shell.GetComponent<UIDocument>();
            try
            {
                Assert.That(flow.Screen, Is.EqualTo(AppScreen.Title));
                flow.ShowPairing(); yield return Settle(flow);
                flow.ContinueFromPairing(); yield return null; Assert.That(flow.Screen, Is.EqualTo(AppScreen.Pairing));
                fake.online = true; flow.ContinueFromPairing(); yield return Settle(flow);
                Assert.That(flow.Screen, Is.EqualTo(AppScreen.GameSelect));
                Assert.That(doc.rootVisualElement.Query<GameCard>().ToList().Count, Is.EqualTo(4));
                Assert.That(doc.rootVisualElement.Query<ScrollView>().ToList().Count, Is.Zero, "Selection uses bounded pages, not vertical scrolling");
                var originalCatalog = flow.games;
                flow.games = new[] { originalCatalog[0], originalCatalog[1], originalCatalog[2], originalCatalog[3], originalCatalog[0] };
                flow.ShowGameSelect(); yield return Settle(flow);
                var view = shell.GetComponent<AppShellView>(); view.ChangeGamePage(1);
                Assert.That(doc.rootVisualElement.Query<GameCard>().ToList().Count, Is.EqualTo(1));
                Assert.That(doc.rootVisualElement.Q<GameCard>().Selected, Is.True);
                view.ChangeGamePage(-1);
                view.MoveGameSelection(1);
                Assert.That(doc.rootVisualElement.Query<GameCard>().ToList()[1].Selected, Is.True);
                flow.games = originalCatalog; flow.ShowGameSelect(); yield return Settle(flow);
                var definition = flow.games[0]; flow.Play(definition); yield return Settle(flow);
                Assert.That(flow.Screen, Is.EqualTo(AppScreen.Playing), flow.Error);
                Assert.That(flow.GameSession, Is.InstanceOf<BowlingGameSession>());
                Assert.That(((BowlingGameSession)flow.GameSession).Match.Players.Count, Is.EqualTo(4));
                Assert.That(((BowlingGameSession)flow.GameSession).resolution, Is.Not.Null);
                Assert.That(((BowlingGameSession)flow.GameSession).resolution.maximumSeconds, Is.GreaterThanOrEqualTo(2));
                yield return null; yield return null; // allow USS cascade resolution on the attached panel
                Assert.That(doc.rootVisualElement.Q("shell").resolvedStyle.backgroundColor.a, Is.Zero, "The menu background must not cover gameplay");
                Assert.That(doc.rootVisualElement.Q("environment").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(doc.rootVisualElement.Query(className: "score-player").ToList().Count, Is.EqualTo(4));
                Assert.That(doc.rootVisualElement.Q(className: "hud-bottom"), Is.Null);
                Assert.That(doc.rootVisualElement.Q(className: "hud-help"), Is.Null);
                Assert.That(doc.rootVisualElement.Q<GameScoreboardView>().layout.height, Is.LessThanOrEqualTo(105), "Compact scoreboard must leave the lane visible");
                Assert.That(flow.menuCamera.enabled, Is.False);
                Assert.That(flow.GameCamera, Is.Not.Null);
                Assert.That(flow.GameCamera.isActiveAndEnabled, Is.True);
                Assert.That(flow.GameCamera.CompareTag("MainCamera"), Is.True);
                Assert.That(flow.GameCamera.targetTexture, Is.Null); Assert.That(flow.GameCamera.targetDisplay, Is.Zero);
                Assert.That(flow.GameCamera.cullingMask, Is.EqualTo(-1));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(flow.GameCamera.gameObject.scene));
                Assert.That(Camera.main, Is.EqualTo(flow.GameCamera));
                flow.GameCamera.aspect = 16f / 9f;
                foreach (var point in new[] {new Vector3(0,.165f,.2f), new Vector3(-1.2f,0,1), new Vector3(1.2f,0,1), new Vector3(0,.8f,18.436f)}) {
                    var viewport = flow.GameCamera.WorldToViewportPoint(point);
                    Assert.That(viewport.z, Is.GreaterThan(flow.GameCamera.nearClipPlane));
                    Assert.That(viewport.x, Is.InRange(.1f,.9f));
                    Assert.That(viewport.y, Is.InRange(.3f,.8f), "Ball/lane/pins must fit between the HUD and header");
                }
                Assert.That(Object.FindObjectsByType<ControllerManager>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
                Assert.That(manager.Sessions["shell-phone"], Is.SameAs(identity));
                var pauseButton = doc.rootVisualElement.Q(className: "hud-top").Q<Button>();
                var picked = doc.rootVisualElement.panel.Pick(pauseButton.worldBound.center);
                Assert.That(picked == pauseButton || pauseButton.Contains(picked), Is.True,
                    "The scoreboard must not intercept the Pause button's pointer area");
                using (var click = NavigationSubmitEvent.GetPooled()) { click.target = pauseButton; pauseButton.SendEvent(click); }
                Assert.That(Time.timeScale, Is.Zero);
                manager.Submit(new MotionFrame { ControllerId = "shell-phone", Sequence = 2, TimestampMs = 2, Orientation = Quaternion.identity,
                    ReceivedAtSeconds = Time.realtimeSinceStartupAsDouble });
                Assert.That(identity.Latest.Sequence, Is.EqualTo(2), "Input must remain alive during pause");
                flow.Resume(); Assert.That(Time.timeScale, Is.EqualTo(1));
                fake.online = false; yield return null; yield return null;
                Assert.That(flow.ConnectionBlocked, Is.True); Assert.That(Time.timeScale, Is.Zero);
                Assert.That(manager.Sessions["shell-phone"], Is.SameAs(identity));
                fake.online = true; yield return null; yield return null; Assert.That(Time.timeScale, Is.EqualTo(1));
                flow.FinishGame(); Assert.That(flow.Screen, Is.EqualTo(AppScreen.Playing), "Incomplete matches must not produce final results");
                var matchSession = (BowlingGameSession)flow.GameSession;
                Assert.That(doc.rootVisualElement.Query<ScrollView>().ToList().Count, Is.Zero);
                Assert.That(doc.rootVisualElement.Query(className: "score-frame").ToList().Count, Is.EqualTo(10));
                while (!matchSession.Match.Complete) matchSession.Match.RecordRoll(0);
                yield return null; yield return null; yield return Settle(flow);
                Assert.That(flow.Screen, Is.EqualTo(AppScreen.Results));
                Assert.That(flow.Result.Detail, Does.Contain("0 points"));
                Assert.That(SceneManager.GetSceneByPath(definition.scenePath).isLoaded, Is.False);
                yield return null;
                Assert.That(flow.menuCamera.enabled, Is.True); Assert.That(flow.GameCamera, Is.Null);
                Assert.That(doc.rootVisualElement.Q("shell").resolvedStyle.backgroundColor.a, Is.EqualTo(1));
                Assert.That(doc.rootVisualElement.Q("environment").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(identity.IsCalibrated, Is.True);
                fake.participants = 1; flow.Restart(); yield return Settle(flow);
                Assert.That(((BowlingGameSession)flow.GameSession).Match.Players.Count, Is.EqualTo(4), "Replay must preserve its original roster");
                fake.participants = 4; yield return null;
                Assert.That(((BowlingGameSession)flow.GameSession).Match.Players[0].ControllerId, Is.EqualTo("shell-phone"));
                Assert.That(((BowlingGameSession)flow.GameSession).Match.FrameIndex, Is.Zero);
                Assert.That(((BowlingGameSession)flow.GameSession).Match.Players[0].Total, Is.Zero);
                flow.ShowGameSelect(); yield return Settle(flow); flow.Play(definition); yield return Settle(flow);
                Assert.That(flow.Screen, Is.EqualTo(AppScreen.Playing)); Assert.That(manager.Sessions["shell-phone"], Is.SameAs(identity));
                flow.Restart(); yield return Settle(flow); Assert.That(flow.Screen, Is.EqualTo(AppScreen.Playing));
                flow.TogglePause(); flow.ShowGameSelect(); yield return Settle(flow);
                Assert.That(Time.timeScale, Is.EqualTo(1)); Assert.That(identity.IsCalibrated, Is.True);
                var tennisDefinition = flow.games[1];
                Assert.That(tennisDefinition.available, Is.True);
                for(int humans=1;humans<=4;humans++) {
                    fake.participants=humans;flow.Play(tennisDefinition);yield return Settle(flow);
                    var tennis=flow.GameSession as MotionControllers.Tennis.TennisGameSession;
                    Assert.That(tennis,Is.Not.Null);Assert.That(tennis.Players.Length,Is.EqualTo(humans<=2?2:4));
                    for(int i=0;i<tennis.Players.Length;i++) {
                        Assert.That(tennis.Players[i].AI,Is.EqualTo(i>=humans));Assert.That(tennis.Players[i].Team,Is.EqualTo(i%2));
                    }
                    Assert.That(flow.ControllerUiMode,Is.EqualTo("tennis"));
                    Assert.That(tennis.ControllerState("shell-phone"),Is.EqualTo("serve"));
                    manager.SubmitButton(new ControllerButtonEvent {ControllerId="shell-phone",Button=ControllerButton.Primary,Phase=ButtonPhase.Pressed,Sequence=1000+humans,TimestampMs=1000+humans});
                    Assert.That(tennis.Phase,Is.EqualTo(MotionControllers.Tennis.TennisPhase.Toss));
                    double now=Time.realtimeSinceStartupAsDouble;
                    var gesture=tennis.Players[0].Swing;
                    gesture.Sample(1,0,now,Vector3.zero,Quaternion.identity,tennis.swing);
                    gesture.Sample(2,.01,now,Vector3.up*8,Quaternion.identity,tennis.swing);
                    gesture.Sample(3,.1,now,Vector3.up*8,Quaternion.identity,tennis.swing);
                    tennis.TickGame(.2f);Assert.That(tennis.Phase,Is.EqualTo(MotionControllers.Tennis.TennisPhase.Rally));
                    flow.TogglePause();var ballPosition=tennis.BallPosition;tennis.TickGame(1);Assert.That(tennis.BallPosition,Is.EqualTo(ballPosition));flow.Resume();
                    while(!tennis.Match.Complete)tennis.Match.AwardPoint(0);
                    yield return null;yield return Settle(flow);Assert.That(flow.Screen,Is.EqualTo(AppScreen.Results));
                    Assert.That(flow.Result.Summary,Does.Contain("wins"));
                    Assert.That(manager.Sessions["shell-phone"],Is.SameAs(identity));
                    flow.ShowGameSelect();yield return Settle(flow);
                }
                flow.Play(tennisDefinition);yield return Settle(flow);flow.Restart();yield return Settle(flow);
                Assert.That(((MotionControllers.Tennis.TennisGameSession)flow.GameSession).Match.Games[0],Is.Zero);
                var autoTennis=(MotionControllers.Tennis.TennisGameSession)flow.GameSession;
                foreach(var player in autoTennis.Players)player.Id=null; // Exercise the same AI movement/contact path in every slot.
                autoTennis.movement.aiMissChance=0;
                var randomState=Random.state;Random.InitState(123);int longest=0;
                for(int i=0;i<6000 && !autoTennis.IsComplete;i++) { autoTennis.TickGame(.02f);longest=Mathf.Max(longest,autoTennis.RallyContacts); }
                Random.state=randomState;
                Assert.That(longest,Is.GreaterThanOrEqualTo(4),"Automatic players must sustain a serve and multiple real returns");
                foreach(var player in autoTennis.Players)player.Miss=true;
                autoTennis.movement.aiMissChance=1;
                for(int i=0;i<1500 && autoTennis.Match.PointNumber==0;i++)autoTennis.TickGame(.02f);
                Assert.That(autoTennis.Match.PointNumber + autoTennis.Match.Games[0] + autoTennis.Match.Games[1],Is.GreaterThan(0));

                flow.ShowGameSelect();yield return Settle(flow);


                var swordDefinition = System.Array.Find(flow.games,g=>g.controllerUiMode=="sword");
                Assert.That(swordDefinition,Is.Not.Null);Assert.That(swordDefinition.maximumPlayers,Is.EqualTo(2));
                for(int humans=1;humans<=2;humans++) {
                    fake.participants=humans;flow.Play(swordDefinition);yield return Settle(flow);
                    var duel=flow.GameSession as MotionControllers.SwordDuel.SwordDuelSession;
                    Assert.That(duel,Is.Not.Null);Assert.That(duel.Fighters[1].AI,Is.EqualTo(humans==1));
                    duel.ai.aggression=0;duel.ai.blockChance=0;
                    for(int i=0;i<humans;i++) {
                        string id=duel.Fighters[i].Id;
                        manager.Submit(new MotionFrame {ControllerId=id,Sequence=6000+humans,TimestampMs=6000+humans,Orientation=Quaternion.identity,ReceivedAtSeconds=Time.realtimeSinceStartupAsDouble},true);
                        manager.SubmitButton(new ControllerButtonEvent {ControllerId=id,Button=ControllerButton.Primary,Phase=ButtonPhase.Pressed,Sequence=6000+humans,TimestampMs=6000+humans});
                        Assert.That(duel.Fighters[i].Ready,Is.True);
                    }
                    yield return null;yield return null;
                    duel.TickDuel(.02f);duel.TickDuel(1.1f);Assert.That(duel.Phase,Is.EqualTo(MotionControllers.SwordDuel.SwordRoundPhase.Duel));
                    duel.Fighters[0].StartAttack(new MotionControllers.SwordDuel.SwordAttack {Direction=MotionControllers.SwordDuel.SlashDirection.Downward,Strength=.6f});
                    for(int i=0;i<80;i++)duel.TickDuel(.02f);
                    Assert.That(duel.Match.Health[1],Is.EqualTo(76).Within(.01f),"A swept strike must hit exactly once");
                    yield return null;yield return null;
                    duel.Fighters[0].StartAttack(new MotionControllers.SwordDuel.SwordAttack {Direction=MotionControllers.SwordDuel.SlashDirection.LeftToRight,Strength=1});
                    for(int i=0;i<80;i++)duel.TickDuel(.02f);
                    Assert.That(duel.Match.Health[1],Is.EqualTo(76).Within(.01f),"Vertical guard blocks a horizontal strike");
                    flow.TogglePause();var hp=duel.Match.Health[1];duel.TickDuel(2);Assert.That(duel.Match.Health[1],Is.EqualTo(hp));flow.Resume();
                    duel.Match.Damage(1,200);duel.TickDuel(.02f);duel.TickDuel(2.1f);Assert.That(duel.Match.Round,Is.EqualTo(2));
                    Assert.That(duel.ControllerState("shell-phone"),Is.EqualTo("ready"));
                    duel.Match.Damage(1,200);yield return null;yield return Settle(flow);
                    Assert.That(flow.Screen,Is.EqualTo(AppScreen.Results));Assert.That(flow.Result.Summary,Does.Contain("wins Sword Duel"));
                    Assert.That(manager.Sessions["shell-phone"],Is.SameAs(identity));
                    flow.Restart();yield return Settle(flow);Assert.That(((MotionControllers.SwordDuel.SwordDuelSession)flow.GameSession).Match.Health[1],Is.EqualTo(100));
                    flow.ShowGameSelect();yield return Settle(flow);
                }
            }
            finally { Object.Destroy(shell); Object.Destroy(core); Time.timeScale = 1; foreach (var camera in unrelatedCameras) if (camera != null) camera.enabled = true; }
            yield return new ExitPlayMode();
        }
        private static IEnumerator Settle(SceneFlowManager flow)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (flow.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(flow.Busy, Is.False, "Scene transition did not complete"); yield return null;
        }
    }
}
