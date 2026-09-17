using System;
using System.Collections.Generic;
using System.Linq;
using MotionControllers.Core;
using UnityEngine;
namespace MotionControllers.Tennis
{
    public enum TennisPhase { Setup, Toss, Rally, PointResult, Complete }
    public sealed class TennisGameSession : MonoBehaviour, IGameSession, IGameCompletion, IGameControllerFeedback
    {
        public MonoBehaviour inputSource;
        public Transform ballVisual;
        public TennisFormat format = TennisFormat.FirstToThree;
        public TennisSwingSettings swing = new TennisSwingSettings();
        public TennisShotSettings shots = new TennisShotSettings();
        public TennisMovementSettings movement = new TennisMovementSettings();
        public bool diagnostics;
        public event Action<TennisFeedback> Feedback;
        private TennisPresentation presentation;
        private void Present(TennisFeedbackKind kind, string text=null, float strength=0, int player=-1) =>
            Feedback?.Invoke(new TennisFeedback {Kind=kind,Message=text,Strength=strength,Player=player,Position=flight.Position});
        public TennisMatch Match { get; private set; }
        public TennisPhase Phase { get; private set; }
        public TennisPlayer[] Players { get; private set; }
        public bool IsComplete => Match != null && Match.Complete;
        public string Status => Match == null ? "Preparing tennis" :
            $"{TeamName(0)}  {Match.Games[0]} [{Match.PointLabel(0)}]  —  {TeamName(1)}  {Match.Games[1]} [{Match.PointLabel(1)}]\n{message}";
        private string message;
        private IControllerButtonSource source;
        private readonly TennisFlight flight = new TennisFlight();
        private readonly List<Material> materials = new List<Material>();
        private bool paused, serveFlight;
        private int lastTeam, bounces;
        private float phaseTime, flightTime, hitAge;
        public Vector3 BallPosition => flight.Position;
        public int RallyContacts { get; private set; }
        public string ControllerState(string id)
        {
            if (Players == null || !Players.Any(p => p.Id == id) || IsComplete || Phase == TennisPhase.PointResult) return "waiting";
            if (Phase == TennisPhase.Setup) return Players[Match.ServerSlot].Id == id ? "serve" : "waiting";
            return Phase == TennisPhase.Rally || Players[Match.ServerSlot].Id == id ? "rally" : "waiting";
        }
        public void SetControllers(IReadOnlyList<string> ids)
        {
            if (Match != null) return;
            Match = new TennisMatch(ids.Count,format);
            source = ControllerInput.Resolve(inputSource) as IControllerButtonSource;
            if (source != null) source.ButtonChanged += Button;
            Players = new TennisPlayer[ids.Count <= 2 ? 2 : 4];
            for (int i=0;i<Players.Length;i++) {
                var player = Players[i] = new TennisPlayer { Slot=i, Id=i<ids.Count?ids[i]:null };
                player.Name = player.AI ? "AI" : "P" + (i+1);
                if (!player.AI && source is ControllerManager manager) player.Name="P"+manager.GetPlayerNumber(player.Id);
                var color = i%2==0 ? new Color(.1f,.65f,1) : new Color(1,.45f,.12f);
                var root = new GameObject(player.Name); root.transform.SetParent(transform,false); player.Avatar=root.transform;root.transform.localScale=Vector3.one*.85f;
                Shape(PrimitiveType.Capsule,root.transform,new Vector3(0,.8f,0),new Vector3(.65f,.8f,.65f),color);
                var racketRoot = new GameObject("Racket " + player.Name); racketRoot.transform.SetParent(root.transform,false);
                racketRoot.transform.localPosition=new Vector3(.65f,1.1f,.35f*(i%2==0?1:-1)); player.Racket=racketRoot.transform; player.Racket.rotation=Quaternion.LookRotation(player.Forward);
                player.Racket.localScale=Vector3.one*1.18f;
                CreateRacket(player.Racket,color);
                var labelObject=new GameObject("Player label"); labelObject.transform.SetParent(root.transform,false); labelObject.transform.localPosition=new Vector3(0,2.4f,0);
                var label=labelObject.AddComponent<TextMesh>();label.text=player.Name;label.characterSize=.22f;label.fontSize=40;label.anchor=TextAnchor.MiddleCenter;label.color=color;
                label.transform.rotation=Quaternion.Euler(50,0,0);
            }
            if(Application.isPlaying){presentation=GetComponent<TennisPresentation>() ?? gameObject.AddComponent<TennisPresentation>();presentation.Initialize(this);}
            BeginPoint();
        }
        private Transform Shape(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var material=materials.Find(m=>m.color==color);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;materials.Add(material);}
            go.GetComponent<Renderer>().sharedMaterial=material;
            return go.transform;
        }
        private void CreateRacket(Transform pivot,Color color)
        {
            // Origin is the hand/grip, with all of the head above it.
            var dark=new Color(.07f,.09f,.12f);
            Shape(PrimitiveType.Cylinder,pivot,new Vector3(0,.035f,0),new Vector3(.065f,.115f,.065f),dark);
            for(int i=0;i<6;i++)Shape(PrimitiveType.Cylinder,pivot,new Vector3(0,-.06f+i*.037f,0),new Vector3(.069f,.004f,.069f),color);
            void Segment(Vector3 a,Vector3 b,float width,Color tint){var t=Shape(PrimitiveType.Cube,pivot,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),tint);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
            const float cx=.155f,cy=.21f,center=.46f;
            for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;Segment(new Vector3(Mathf.Cos(a)*cx,center+Mathf.Sin(a)*cy,0),new Vector3(Mathf.Cos(b)*cx,center+Mathf.Sin(b)*cy,0),.025f,i%10<3?Color.white:color);}
            Segment(new Vector3(0,.15f,0),new Vector3(-.10f,.30f,0),.022f,color);Segment(new Vector3(0,.15f,0),new Vector3(.10f,.30f,0),.022f,color);
            for(int i=-5;i<=5;i++){float x=i*cx/6,h=cy*Mathf.Sqrt(1-x*x/(cx*cx));Segment(new Vector3(x,center-h,0),new Vector3(x,center+h,0),.004f,Color.white);}
            for(int i=-7;i<=7;i++){float y=i*cy/8,w=cx*Mathf.Sqrt(1-y*y/(cy*cy));Segment(new Vector3(-w,center+y,0),new Vector3(w,center+y,0),.004f,Color.white);}
        }
        private string TeamName(int team) => string.Join(" + ",Players.Where(p=>p.Team==team).Select(p=>p.Name));
        public void SetPaused(bool value) { if(paused==value)return; paused=value; presentation?.SetPaused(value); if(value && Players!=null) foreach(var p in Players)p.Swing.Reset(); }
        private void OnDestroy() { if(source!=null)source.ButtonChanged-=Button;foreach(var m in materials)Destroy(m); }
        private void Button(ControllerButtonEvent input)
        {
            if(paused || Match==null || Phase!=TennisPhase.Setup || input.Button!=ControllerButton.Primary || input.Phase!=ButtonPhase.Pressed) return;
            if(Players[Match.ServerSlot].Id!=input.ControllerId || !source.TryGetController(input.ControllerId,out var c) || !c.IsCalibrated) return;
            Toss();
        }
        private void BeginPoint()
        {
            Phase=TennisPhase.Setup;RallyContacts=0;phaseTime=0;serveFlight=false;bounces=0;
            foreach(var p in Players) {p.Avatar.position=p.Home(Players.Length==4);p.ResetMovement();p.Swing.Reset();}
            var server=Players[Match.ServerSlot];float sign=server.Team==0?1:-1;
            server.Avatar.position=new Vector3((Match.DeuceSide?1:-1)*sign*2,0,-sign*11);
            var receiver=Players[Match.ReceiverSlot];receiver.Avatar.position=new Vector3((Match.DeuceSide?-1:1)*sign*2,0,sign*8.5f);
            ballVisual.position=server.Contact;flight.Position=ballVisual.position;
            message=$"{server.Name} serves · {(Match.Faults==0?"First":"Second")} serve · Tap phone to toss";
            Present(TennisFeedbackKind.Ready, server.Name + " · READY TO SERVE");
        }
        private void Toss()
        {
            Phase=TennisPhase.Toss;phaseTime=0;
            var server=Players[Match.ServerSlot];server.Swing.Reset();
            flight.Launch(server.Contact,new TennisShot {Velocity=Vector3.up*4.5f});
            message=server.Name+" · Swing to serve!";
            Present(TennisFeedbackKind.Toss,"SWING TO SERVE");
        }
        private void Update()
        {
            if(paused || Match==null || IsComplete) return;
            double now=Time.realtimeSinceStartupAsDouble;
            foreach(var p in Players) if(!p.AI) {
                if(source!=null && source.TryGetController(p.Id,out var session)) p.Orient(session,now,Time.deltaTime,swing);
                else p.Swing.Reset();
            }
        }
        private void FixedUpdate() { TickGame(Time.fixedDeltaTime); }
        public void TickGame(float dt)
        {
            if(paused || Match==null || IsComplete) return;
            phaseTime+=dt; hitAge+=dt;
            if(Phase==TennisPhase.PointResult) {if(phaseTime>1.5f)BeginPoint();return;}
            var server=Players[Match.ServerSlot];
            if(Phase==TennisPhase.Setup) {if(server.AI && phaseTime>1)Toss();return;}
            if(Phase==TennisPhase.Toss) {
                flight.Step(dt);ballVisual.position=flight.Position;
                bool swinging=server.AI?phaseTime>.55f:server.Swing.Active(Time.realtimeSinceStartupAsDouble,swing);
                if(swinging && flight.Position.y>1 && phaseTime>.15f) {Hit(server,true);return;}
                if(phaseTime>1.8f)Fault("Missed toss");return;
            }
            if(Phase!=TennisPhase.Rally)return;
            flightTime+=dt;flight.Step(dt);ballVisual.position=flight.Position;
            if(flight.Net) {Present(TennisFeedbackKind.Net,"NET");if(serveFlight)Fault("Serve into net");else Point(1-lastTeam,"Net");return;}
            if(flight.Ground) {
                Present(TennisFeedbackKind.Bounce);
                bool legal=TennisCourt.InCourt(flight.Position,Players.Length==4) && TennisCourt.Side(flight.Position)!=lastTeam;
                if(serveFlight) {
                    if(!legal || !TennisCourt.InServiceBox(flight.Position,Match.ServerTeam,Match.DeuceSide)) {Fault("Service fault");return;}
                    serveFlight=false;
                } else if(bounces==0 && !legal) {Point(1-lastTeam,"Out");return;}
                if(++bounces>=2) {Point(lastTeam,"Double bounce");return;}
            }
            if(flightTime>10 || Mathf.Abs(flight.Position.x)>20 || Mathf.Abs(flight.Position.z)>25) {
                if(serveFlight)Fault("Service out");else Point(bounces>0?lastTeam:1-lastTeam,"Out of reach");return;
            }
            foreach(var p in Players) {
                bool incoming=p.Team!=lastTeam && TennisCourt.Side(flight.Position)==p.Team;
                p.Move(flight.Position,p.Team!=lastTeam,Players.Length==4,dt,movement,flight.Velocity);
                if(!incoming || hitAge<.18f || flight.Position.y>3.4f || (p.AI && flight.Position.y<.75f))continue;
                if(serveFlight)continue; // A serve must bounce before returning.
                // During the service return, keep doubles receiving order fixed.
                if(bounces==1 && returnOfServe && p.Slot!=Match.ReceiverSlot)continue;
                bool active=p.AI?!p.Miss:p.Swing.Active(Time.realtimeSinceStartupAsDouble,swing);
                if(active && TennisCourt.SegmentDistance(flight.Previous,flight.Position,p.Contact)<movement.contactRadius) {Hit(p,false);return;}
            }
        }
        private bool returnOfServe;
        private void Hit(TennisPlayer p,bool serving)
        {
            RallyContacts++;
            float peak=p.AI?movement.aiSwingSpeed:p.Swing.Peak;
            var face=p.AI?Quaternion.Euler(0,UnityEngine.Random.Range(-18f,18f),0):p.Face;
            var direction=p.AI?new Vector3(.1f,UnityEngine.Random.Range(-.4f,.4f),0):p.Swing.Direction;
            float timing=p.AI?0:Mathf.Clamp((float)(Time.realtimeSinceStartupAsDouble-p.Swing.Started)/swing.windowSeconds-.5f,-.5f,.5f);
            var shot=TennisShots.Calculate(flight.Position,p.Team,peak,face,direction,timing,flight.Velocity,serving,Match.DeuceSide,shots);
            flight.Launch(flight.Position,shot);lastTeam=p.Team;bounces=0;hitAge=flightTime=0;serveFlight=serving;returnOfServe=serving;
            p.Swing.Consume();Phase=TennisPhase.Rally;message=$"Rally · {p.Name} {(direction.y<0?"backhand":"forehand")}";
            Present(TennisFeedbackKind.Racket,p.Name+" · RALLY",shot.Velocity.magnitude,p.Slot);
            foreach(var other in Players)if(other.AI)other.Miss=UnityEngine.Random.value<movement.aiMissChance;
            if(diagnostics)Debug.Log($"[Tennis] {p.Name} id={p.Id} face={face.eulerAngles} peak={peak:F2} direction={direction} timing={timing:F2} velocity={shot.Velocity} spin={shot.Spin:F2}");
        }
        private void Fault(string reason)
        {
            bool doubleFault=Match.Fault();message=doubleFault?"Double fault":reason+" · Second serve";AfterPoint();
            Present(TennisFeedbackKind.Fault,message);
        }
        private void Point(int team,string reason) {Match.AwardPoint(team);message=TeamName(team)+" wins point · "+reason;AfterPoint();Present(TennisFeedbackKind.Point,message);}
        private void AfterPoint() {if(diagnostics)Debug.Log($"[Tennis point] {message}; ball={flight.Position}; contacts={RallyContacts}; bounces={bounces}");Phase=IsComplete?TennisPhase.Complete:TennisPhase.PointResult;phaseTime=0;foreach(var p in Players)p.Swing.Reset();}
        public GameResult Finish() => new GameResult(IsComplete?TeamName(Match.Winner)+" wins!":"Tennis in progress", $"{TeamName(0)}  {Match.Games[0]} – {Match.Games[1]}  {TeamName(1)}");
    }
}
