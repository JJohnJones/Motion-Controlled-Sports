using System;
using System.Collections.Generic;
using MotionControllers.Core;
using UnityEngine;
namespace MotionControllers.SwordDuel
{
    public enum SwordRoundPhase { Ready, Intro, Duel, Result, Complete }
    public sealed class SwordDuelSession : MonoBehaviour, IGameSession, IGameCompletion, IGameControllerFeedback, IGameVitals
    {
        public MonoBehaviour inputSource;
        public Camera duelCamera;
        public SwordMotionSettings motion=new SwordMotionSettings();
        public SwordCombatSettings combat=new SwordCombatSettings();
        public SwordAISettings ai=new SwordAISettings();
        [Range(1,5)] public int roundsToWin=2;
        [Min(1)] public float maximumHealth=100;
        public bool diagnostics;
        public SwordRounds Match {get;private set;}
        public SwordFighter[] Fighters {get;private set;}
        public SwordRoundPhase Phase {get;private set;}
        public bool IsComplete=>Match!=null && Match.Complete;
        public string Status {get;private set;}="Preparing duel";
        public string VitalsHeading=>Match==null?"Sword Duel":"ROUND "+Match.Round;
        public string[] VitalsNames=>Fighters==null?Array.Empty<string>():new[]{Fighters[0].Name+" · "+Match.Wins[0]+" wins",Fighters[1].Name+" · "+Match.Wins[1]+" wins"};
        public float[] VitalsValues=>Match?.Health;
        public float VitalsMaximum=>maximumHealth;
        private IControllerButtonSource source;
        private readonly SwordPresentation presentation=new SwordPresentation();
        private readonly SwordAI brain=new SwordAI();
        private bool paused;
        private float phaseTime;
        private double clock;
        private Vector3 cameraHome;
        public void SetControllers(IReadOnlyList<string> ids)
        {
            if(Match!=null)return;
            if(ids.Count<1 || ids.Count>2)throw new ArgumentOutOfRangeException(nameof(ids));
            source=ControllerInput.Resolve(inputSource) as IControllerButtonSource;
            Match=new SwordRounds(roundsToWin,maximumHealth);Fighters=new SwordFighter[2];
            for(int i=0;i<2;i++){
                var f=Fighters[i]=new SwordFighter {Index=i,Id=i<ids.Count?ids[i]:null};
                f.Name=f.AI?"AI":source is ControllerManager m?"Player "+m.GetPlayerNumber(f.Id):"Player "+(i+1);
                presentation.Create(f,transform);
            }
            if(source!=null)source.ButtonChanged+=Button;
            if(duelCamera!=null)cameraHome=duelCamera.transform.position;
            ResetRound();
        }
        public string ControllerState(string id)
        {
            if(Fighters==null)return "waiting";
            foreach(var f in Fighters)if(f.Id==id){
                if(Phase==SwordRoundPhase.Ready)return f.Ready?"waiting":"ready";
                if(Phase!=SwordRoundPhase.Duel)return "waiting";
                return f.Action==SwordAction.Guard?"guard":f.Action==SwordAction.Stagger?"stagger":"attack";
            }
            return "waiting";
        }
        private void Button(ControllerButtonEvent e)
        {
            if(paused || Phase!=SwordRoundPhase.Ready || e.Button!=ControllerButton.Primary || e.Phase!=ButtonPhase.Pressed)return;
            foreach(var f in Fighters)if(f.Id==e.ControllerId && source.TryGetController(f.Id,out var c) && c.IsCalibrated && c.HasFrame && Time.realtimeSinceStartupAsDouble-c.Latest.ReceivedAtSeconds<.3){
                f.Motion.SetNeutral(c);f.Ready=true;
                if(diagnostics)Debug.Log($"[Sword] {f.Name} ready pose captured");
            }
        }
        public void SetPaused(bool value)
        {
            if(paused==value)return;paused=value;
            if(Fighters!=null)foreach(var f in Fighters){f.Motion.Reset();f.Action=SwordAction.Guard;f.ContactResolved=true;f.ParryAt=-100;}
        }
        private void OnDestroy(){if(source!=null)source.ButtonChanged-=Button;presentation.Dispose();}
        private void Update()
        {
            if(paused || Match==null || IsComplete)return;
            foreach(var f in Fighters)if(!f.AI && source!=null && source.TryGetController(f.Id,out var c)){
                bool attack=f.Motion.Read(c,Time.realtimeSinceStartupAsDouble,Phase==SwordRoundPhase.Duel && f.Action==SwordAction.Guard,motion,out var input);
                f.PreviousGuard=f.GuardBlade;
                f.GuardPose=Quaternion.Slerp(f.GuardPose,f.Motion.Pose,1-Mathf.Exp(-motion.response*Time.deltaTime));
                var blade=f.GuardPose*Vector3.up;f.GuardBlade=new Vector2(blade.x,blade.y);
                var opponent=Fighters[1-f.Index];var incoming=Incoming(opponent.Attack);
                bool threat=opponent.Action==SwordAction.Windup || opponent.Action==SwordAction.Active;
                if(threat && f.Action==SwordAction.Guard && !SwordCombat.GuardMatches(f.PreviousGuard,incoming,combat.guardTolerance) && SwordCombat.GuardMatches(f.GuardBlade,incoming,combat.guardTolerance)){
                    f.ParryAt=clock;f.ParryMotion=f.GuardBlade-f.PreviousGuard;f.ParrySpeed=f.Motion.AngularSpeed;
                }
                bool parryMove=threat && clock-f.ParryAt<combat.parryWindow && f.ParrySpeed>=combat.parryAngularSpeed && Vector2.Dot(f.ParryMotion.normalized,-incoming)>=combat.parryOpposition;
                if(attack && !parryMove){f.StartAttack(input);if(diagnostics)Debug.Log($"[Sword] {f.Name} direction={input.Direction} peak={input.Peak:F2} strength={input.Strength:F2} pose={f.GuardPose.eulerAngles}");}
            }
        }
        private static Vector2 Incoming(SwordAttack attack){var v=SwordAttack.Vector(attack.Direction);return new Vector2(-v.x,v.y);}
        private static SwordAttack Mirrored(SwordAttack a){if(a.Direction==SlashDirection.LeftToRight)a.Direction=SlashDirection.RightToLeft;else if(a.Direction==SlashDirection.RightToLeft)a.Direction=SlashDirection.LeftToRight;return a;}
        private void FixedUpdate(){TickDuel(Time.fixedDeltaTime);}
        public void TickDuel(float dt)
        {
            if(paused || Match==null || IsComplete)return;
            clock+=dt;phaseTime+=dt;
            if(Phase==SwordRoundPhase.Ready){if(Fighters[0].Ready && Fighters[1].Ready){Phase=SwordRoundPhase.Intro;phaseTime=0;Status="Ready…";}Draw();return;}
            if(Phase==SwordRoundPhase.Intro){if(phaseTime>=1){Phase=SwordRoundPhase.Duel;Status="Fight!";}Draw();return;}
            if(Phase==SwordRoundPhase.Result){if(phaseTime>=2){Match.NextRound();ResetRound();}Draw();return;}
            if(Phase!=SwordRoundPhase.Duel)return;
            for(int i=0;i<2;i++){
                var f=Fighters[i];f.PreviousHand=f.Hand;f.PreviousTip=SwordPresentation.Tip(f,combat);var before=f.Action;f.Step(dt,combat);
                if(before==SwordAction.Active && f.Action==SwordAction.Recovery && !f.ContactResolved){f.ContactResolved=true;Status=f.Name+" missed";phaseTime=0;if(diagnostics)Debug.Log("[Sword] Miss: "+f.Name);}
                if(f.AI)brain.Tick(dt,clock,f,Fighters[1-i],ai,combat);
                float lunge=f.Action==SwordAction.Active ? .22f : f.Action==SwordAction.Windup ? .10f : 0;
                var home=new Vector3(0,0,i==0?-1.55f:1.55f)+f.Forward*(lunge-f.Recoil);
                f.Root.position=Vector3.MoveTowards(f.Root.position,home,dt*3);
            }
            // Deterministic contact order; a staggered fighter cannot also land a pending blow.
            int first=Fighters[0].ActionTime>=Fighters[1].ActionTime?0:1;
            Resolve(first);if(!Match.RoundComplete)Resolve(1-first);
            Draw();
            if(Match.RoundComplete){Phase=Match.Complete?SwordRoundPhase.Complete:SwordRoundPhase.Result;phaseTime=0;Status=Fighters[Match.RoundWinner].Name+" wins round!";}
            else if(phaseTime>2)Status="Guard across the slash · Move into guard to parry";
        }
        private void Resolve(int index)
        {
            var f=Fighters[index];var d=Fighters[1-index];
            if(f.Action!=SwordAction.Active || f.ContactResolved)return;
            bool contact=Vector3.Distance(f.Root.position,d.Root.position)<=combat.reach && SwordCombat.SweptBlade(f.PreviousHand,f.PreviousTip,f.Hand,SwordPresentation.Tip(f,combat),d.Torso,.48f);
            if(!contact)return;
            f.ContactResolved=true;
            var result=SwordCombat.Resolve(true,Mirrored(f.Attack),d.Defense(d.AI || d.Motion.Fresh),clock,combat);
            if(result==SwordContact.Parry){f.Stagger(combat.stagger);d.FlashTime=.2f;d.FeedbackColor=Color.cyan;Status=d.Name+" PARRY!";}
            else if(result==SwordContact.Block){f.Action=SwordAction.Recovery;f.ActionTime=0;d.Recoil=.08f+f.Attack.Strength*.14f;d.FlashTime=.08f;d.FeedbackColor=Color.yellow;Status=d.Name+" blocked";}
            else if(result==SwordContact.Hit){Match.Damage(d.Index,combat.baseDamage+combat.strengthDamage*f.Attack.Strength);d.Stagger(.3f);d.Recoil=.15f+f.Attack.Strength*.2f;d.FlashTime=.15f;d.FeedbackColor=new Color(1,.35f,.3f);Status=f.Name+" hit!";}
            phaseTime=0;
            if(diagnostics)Debug.Log($"[Sword] {result} attacker={f.Name} direction={f.Attack.Direction} guard={d.GuardBlade} parryOffset={clock-d.ParryAt:F3} health={Match.Health[d.Index]:F0}");
        }
        private void ResetRound()
        {
            Phase=SwordRoundPhase.Ready;phaseTime=0;brain.Reset();Status="Hold your sword-ready pose; tap phone to ready";
            foreach(var f in Fighters){f.Ready=f.AI;f.Action=SwordAction.Guard;f.ActionTime=0;f.Recoil=0;f.ContactResolved=false;f.GuardPose=Quaternion.identity;f.GuardBlade=Vector2.up;f.ParryAt=-100;f.Motion.Reset();f.Root.position=new Vector3(0,0,f.Index==0?-1.55f:1.55f);}
            Draw();
        }
        private void Draw()
        {
            foreach(var f in Fighters)presentation.Draw(f,combat,duelCamera);
            if(duelCamera!=null){var center=(Fighters[0].Root.position+Fighters[1].Root.position)*.5f;duelCamera.transform.position=Vector3.Lerp(duelCamera.transform.position,cameraHome+center*.15f,.1f);duelCamera.transform.LookAt(center+Vector3.up*1.2f);}
        }
        public GameResult Finish()=>new GameResult(IsComplete?Fighters[Match.Winner].Name+" wins Sword Duel!":"Duel in progress",$"{Fighters[0].Name}  {Match.Wins[0]} – {Match.Wins[1]}  {Fighters[1].Name}");
    }
}
