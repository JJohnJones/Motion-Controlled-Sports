using UnityEngine;
namespace MotionControllers.SwordDuel
{
    public sealed class SwordFighter
    {
        public string Id,Name;
        public bool AI=>Id==null;
        public Transform Root,Blade;
        public TrailRenderer Trail;
        public Renderer BodyRenderer;
        public Material BodyMaterial;
        public Color Color, FeedbackColor=UnityEngine.Color.white;
        public int Index;
        public bool Ready, LeftHanded;
        public readonly SwordMotion Motion=new SwordMotion();
        public SwordAction Action= SwordAction.Guard;
        public SwordAttack Attack;
        public float ActionTime, FlashTime, Recoil;
        public bool ContactResolved;
        public Quaternion GuardPose=Quaternion.identity;
        public Vector2 GuardBlade=Vector2.up, PreviousGuard=Vector2.up;
        public double ParryAt=-100;
        public float ParrySpeed;
        public Vector2 ParryMotion;
        public Vector3 PreviousHand,PreviousTip,RecoveryTip;
        public bool PreviousGuardMatch;
        public Vector3 Forward=>Index==0?Vector3.forward:Vector3.back;
        public Vector3 Hand=>Root.position+Vector3.up*1.25f+Forward*.3f+(LeftHanded ? Facing*Vector3.left*.2f : Vector3.zero);
        public Vector3 Torso=>Root.position+Vector3.up*1.15f;
        public Quaternion Facing=>Quaternion.LookRotation(Forward);
        public void StartAttack(SwordAttack attack){if(Action!=SwordAction.Guard)return;Attack=attack;Action=SwordAction.Windup;ActionTime=0;ContactResolved=false;}
        public void Stagger(float duration){Action=SwordAction.Stagger;ActionTime=-duration;ContactResolved=true;Motion.Reset();}
        public void Step(float dt,SwordCombatSettings settings)
        {
            FlashTime=Mathf.Max(0,FlashTime-dt);Recoil=Mathf.MoveTowards(Recoil,0,dt*2);
            ActionTime+=dt;
            if(Action==SwordAction.Windup && ActionTime>=settings.windup){Action=SwordAction.Active;ActionTime=0;}
            else if(Action==SwordAction.Active && ActionTime>=settings.active){RecoveryTip=SwordPresentation.Tip(this,settings);Action=SwordAction.Recovery;ActionTime=0;}
            else if(Action==SwordAction.Recovery && ActionTime>=settings.recovery || Action==SwordAction.Stagger && ActionTime>=0){Action=SwordAction.Guard;ActionTime=0;Motion.Reset();}
        }
        public SwordDefense Defense(bool fresh) => new SwordDefense {Valid=Action==SwordAction.Guard && fresh,Blade=GuardBlade,ParryAt=ParryAt,ParryMotion=ParryMotion,ParrySpeed=ParrySpeed};
    }
    [System.Serializable] public sealed class SwordAISettings
    {
        public float reactionSeconds=.18f, attackInterval=1.8f, swingStrength=.5f;
        [Range(0,1)] public float aggression=.7f, blockChance=.55f, parryAccuracy=.22f, attackVariation=.85f;
    }
    public sealed class SwordAI
    {
        private float attackIn=.8f, defendIn;
        private bool noticed, defending, parry;
        public void Reset(){attackIn=.8f;noticed=defending=false;}
        public void Tick(float dt,double now,SwordFighter self,SwordFighter opponent,SwordAISettings settings,SwordCombatSettings combat)
        {
            bool threat=opponent.Action==SwordAction.Windup || opponent.Action==SwordAction.Active;
            if(!threat){noticed=defending=false;self.GuardBlade=Vector2.Lerp(self.GuardBlade,Vector2.up,dt*4);self.GuardPose=Quaternion.FromToRotation(Vector3.up,new Vector3(self.GuardBlade.x,self.GuardBlade.y,0));}
            if(threat && !noticed){noticed=true;defendIn=settings.reactionSeconds*Random.Range(.75f,1.3f);defending=Random.value<settings.blockChance;parry=Random.value<settings.parryAccuracy;}
            defendIn-=dt;
            if(threat && defending && defendIn<=0 && self.Action==SwordAction.Guard){
                Vector2 incoming=-SwordAttack.Vector(opponent.Attack.Direction);incoming.y=-incoming.y; // opposing horizontal axes
                self.GuardBlade=Mathf.Abs(incoming.x)>.5f?Vector2.up:Vector2.right;
                if(parry && opponent.Action==SwordAction.Active){self.ParryAt=now;self.ParryMotion=-incoming;self.ParrySpeed=combat.parryAngularSpeed+1;parry=false;}
                self.GuardPose=Quaternion.FromToRotation(Vector3.up,new Vector3(self.GuardBlade.x,self.GuardBlade.y,0));
            }
            attackIn-=dt;
            if(self.Action!=SwordAction.Guard || threat || attackIn>0)return;
            attackIn=Mathf.Max(.4f,settings.attackInterval)*Random.Range(.7f,1.4f);
            if(Random.value>settings.aggression)return;
            var direction=Random.value<settings.attackVariation?(SlashDirection)Random.Range(0,4):SlashDirection.Downward;
            self.StartAttack(new SwordAttack {Direction=direction,Strength=Mathf.Clamp01(settings.swingStrength*Random.Range(.7f,1.2f)),Peak=7});
        }
    }
}
