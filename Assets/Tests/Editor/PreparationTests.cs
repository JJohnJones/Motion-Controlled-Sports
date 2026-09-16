using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotionControllers.Core;
using MotionControllers.Tennis;
namespace MotionControllers.Tests
{
    public class PreparationTests
    {
        private sealed class Lobby : IControllerLobby
        {
            public bool online = true;
            public IReadOnlyList<ControllerSlot> Slots => new ControllerSlot[0];
            public int ConnectedPlayers => online ? 2 : 0;
            public Texture PairingQr => null; public string PairingUrl => ""; public string Status => "";
            public bool IsConnected(string id) => online; public void CancelHeldInput() {}
        }
        [Test] public void ReadyRequiresReleaseCalibrationAndEveryParticipantAndExpiresOnDisconnect()
        {
            var go = new GameObject("Ready test"); var manager = go.AddComponent<ControllerManager>(); var lobby = new Lobby();
            manager.Register("one"); manager.Register("two"); manager.Register("extra");
            try {
                using (var preparation = new GamePreparation(manager, lobby, new[] {"one", "two"})) {
                    Assert.That(preparation.AllReady, Is.False);
                    long sequence = 0;
                    foreach (var id in new[] {"one", "two"}) {
                        manager.Submit(new MotionFrame {ControllerId=id,Sequence=1,TimestampMs=1,Orientation=Quaternion.identity,ReceivedAtSeconds=Time.realtimeSinceStartupAsDouble}, true);
                        manager.SubmitButton(new ControllerButtonEvent {ControllerId=id,Button=ControllerButton.Primary,Phase=ButtonPhase.Pressed,Sequence=++sequence,TimestampMs=sequence});
                        Assert.That(preparation.IsReady(id), Is.False);
                        manager.SubmitButton(new ControllerButtonEvent {ControllerId=id,Button=ControllerButton.Primary,Phase=ButtonPhase.Released,Sequence=++sequence,TimestampMs=sequence});
                        Assert.That(preparation.IsReady(id), Is.True);
                    }
                    Assert.That(preparation.AllReady, Is.True);
                    Assert.That(preparation.ControllerState("extra"), Is.EqualTo("spectator"));
                    manager.Sessions["one"].Calibrate(); Assert.That(preparation.AllReady, Is.False);
                    lobby.online=false; preparation.Tick(); lobby.online=true;
                    Assert.That(preparation.IsReady("two"), Is.False, "Reconnect requires a deliberate Ready again");
                }
            } finally { Object.DestroyImmediate(go); }
        }
        [Test] public void SavedPlayerPreferencesAreClampedAndAppliedToSessions()
        {
            const int player=4;
            float previous=PlayerPreferences.Sensitivity(player); bool left=PlayerPreferences.LeftHanded(player);
            try {
                PlayerPreferences.SetPlayer(player, 7, true);
                var session=new ControllerSession("settings"); PlayerPreferences.Apply(session,player);
                Assert.That(session.MotionSensitivity, Is.EqualTo(2)); Assert.That(session.LeftHanded, Is.True);
                PlayerPreferences.SetPlayer(player, -.1f, false); PlayerPreferences.Apply(session,player);
                Assert.That(session.MotionSensitivity, Is.EqualTo(.5f)); Assert.That(session.LeftHanded, Is.False);
            } finally { PlayerPreferences.SetPlayer(player,previous,left); }
        }
        [TestCase(1f)] [TestCase(1.2f)]
        public void RacketCrossesHalfTurnAndQuaternionSignWithoutJump(float sensitivity)
        {
            var go=new GameObject("racket"); var session=new ControllerSession("racket");
            session.Accept(new MotionFrame {Sequence=1,TimestampMs=1,Orientation=Quaternion.identity},true);
            var player=new TennisPlayer {Racket=go.transform};
            var settings=new TennisSwingSettings {orientationSensitivity=sensitivity};
            try {
                Quaternion last=Quaternion.identity;
                for(int i=0;i<=190;i++) {
                    var q=Quaternion.AngleAxis(i,Vector3.up);
                    if(i%2==1)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                    session.Accept(new MotionFrame {Sequence=i+2,TimestampMs=i+2,Orientation=q,ReceivedAtSeconds=1});
                    player.Orient(session,1,1f/60,settings);
                    Assert.That(Quaternion.Angle(last,player.Face), Is.LessThan(2), "No shortest-angle clamp flip near 180 degrees");
                    last=player.Face;
                }
                var stopped=go.transform.rotation; player.Orient(session,4,1f/60,settings);
                Assert.That(Quaternion.Angle(stopped,go.transform.rotation),Is.LessThan(.01f),"Stale data must not move the racket");
            } finally { Object.DestroyImmediate(go); }
        }
    }
}
