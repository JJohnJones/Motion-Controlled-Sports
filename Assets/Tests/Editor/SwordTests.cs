using NUnit.Framework;
using UnityEngine;
using MotionControllers.SwordDuel;
namespace MotionControllers.Tests
{
    public class SwordTests
    {
        [TestCase(SlashDirection.LeftToRight,1,0)] [TestCase(SlashDirection.RightToLeft,-1,0)]
        [TestCase(SlashDirection.Downward,0,-1)] [TestCase(SlashDirection.Upward,0,1)]
        public void SustainedArcClassifiesDirection(SlashDirection expected,int x,int y)
        {
            var m=new SwordMotion();var settings=new SwordMotionSettings();SwordAttack attack;
            Assert.That(m.Sample(0,Vector2.zero,true,settings,out attack),Is.False);
            Assert.That(m.Sample(.02,new Vector2(x,y)*8,true,settings,out attack),Is.False);
            Assert.That(m.Sample(.06,new Vector2(x,y)*8,true,settings,out attack),Is.False);
            Assert.That(m.Sample(.10,new Vector2(x,y)*8,true,settings,out attack),Is.True);
            Assert.That(attack.Direction,Is.EqualTo(expected));Assert.That(attack.Strength,Is.InRange(0,1));
            Assert.That(m.Sample(.2,new Vector2(x,y)*20,true,settings,out attack),Is.False,"Must return to a quiet stance");
        }
        [TestCase(1f)] [TestCase(1.2f)] public void OrientationCrossesHalfTurnWithoutJump(float sensitivity)
        {
            var pose=Quaternion.identity;var previous=Quaternion.identity;
            for(int i=1;i<=220;i++){
                var current=Quaternion.AngleAxis(i,Vector3.forward);
                if(i%2==1)current=new Quaternion(-current.x,-current.y,-current.z,-current.w);
                var next=SwordMotion.IntegrateOrientation(pose,previous,current,sensitivity);
                Assert.That(Quaternion.Angle(pose,next),Is.LessThan(1.5f));pose=next;previous=current;
            }
        }
        [Test] public void BlockRecoveryStartsAtActualContactPosition()
        {
            var root=new GameObject("Fighter");
            try {
                var fighter=new SwordFighter {Root=root.transform,Action=SwordAction.Active,ActionTime=.06f,Attack=new SwordAttack {Direction=SlashDirection.Downward}};
                var settings=new SwordCombatSettings();var contact=SwordPresentation.Tip(fighter,settings);
                fighter.RecoveryTip=contact;fighter.Action=SwordAction.Recovery;fighter.ActionTime=0;
                Assert.That(Vector3.Distance(contact,SwordPresentation.Tip(fighter,settings)),Is.LessThan(.001f));
            } finally {Object.DestroyImmediate(root);}
        }
        [TestCase(false)] [TestCase(true)] public void InwardGripMapsPhoneTopAndScreenCorrectly(bool left)
        {
            var basis=SwordMotion.GripBasis(left);
            Assert.That(Vector3.Distance(basis*Vector3.up,Vector3.up),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(basis*Vector3.back,left?Vector3.right:Vector3.left),Is.LessThan(.001f));
            var desired=Quaternion.AngleAxis(35,Vector3.forward);
            var phone=Quaternion.Inverse(basis)*desired*basis;
            Assert.That(Quaternion.Angle(SwordMotion.MapGripRotation(phone,left),desired),Is.LessThan(.01f));
        }
        [TestCase(false)] [TestCase(true)] public void InwardGripKeepsSwingDirectionsAndWristTwistDistinct(bool left)
        {
            var basis=SwordMotion.GripBasis(left);
            var relative=Quaternion.AngleAxis(30,Vector3.up);
            foreach(var expected in new[]{Vector3.forward*8,Vector3.back*8,Vector3.left*8,Vector3.right*8}){
                var device=Quaternion.Inverse(relative)*Quaternion.Inverse(basis)*expected;
                Assert.That(Vector3.Distance(SwordMotion.MapGripAngularVelocity(relative,device,left),expected),Is.LessThan(.001f));
            }
            var twist=SwordMotion.MapGripAngularVelocity(Quaternion.identity,Vector3.up*10,left);
            Assert.That(new Vector2(-twist.z,-twist.x).magnitude,Is.LessThan(.001f),"Twisting about the upright handle is not a slash");
        }
        [Test] public void SmallMotionAndAlternatingShakeDoNotAttack()
        {
            var m=new SwordMotion();var settings=new SwordMotionSettings();m.Sample(0,Vector2.zero,true,settings,out _);
            for(int i=1;i<30;i++)Assert.That(m.Sample(i*.02f,Vector2.right*(i%2==0?8:-8),true,settings,out _),Is.False);
            for(int i=30;i<60;i++)Assert.That(m.Sample(i*.02f,Vector2.right,true,settings,out _),Is.False);
        }
        [Test] public void DirectionAndTimingDecideParryBlockHitOnce()
        {
            var settings=new SwordCombatSettings();var attack=new SwordAttack{Direction=SlashDirection.Downward,Strength=1};
            var d=new SwordDefense{Valid=true,Blade=Vector2.right,ParryAt=.94,ParrySpeed=3,ParryMotion=Vector2.up};
            Assert.That(SwordCombat.Resolve(true,attack,d,1,settings),Is.EqualTo(SwordContact.Parry));
            Assert.That(SwordCombat.Resolve(true,attack,d,1.2,settings),Is.EqualTo(SwordContact.Block));
            d.ParryMotion=Vector2.down;Assert.That(SwordCombat.Resolve(true,attack,d,1,settings),Is.EqualTo(SwordContact.Block));
            d.Blade=Vector2.up;Assert.That(SwordCombat.Resolve(true,attack,d,1,settings),Is.EqualTo(SwordContact.Hit));
            d.Blade=Vector2.right;d.Valid=false;Assert.That(SwordCombat.Resolve(true,attack,d,1,settings),Is.EqualTo(SwordContact.Hit));
            Assert.That(SwordCombat.Resolve(false,attack,d,1,settings),Is.EqualTo(SwordContact.Miss));
        }
        [Test] public void SweptBladeCatchesFastCrossingButNotDistantMiss()
        {
            Assert.That(SwordCombat.SweptBlade(Vector3.back,Vector3.left*3,Vector3.back,Vector3.right*3,Vector3.zero,.3f),Is.True);
            Assert.That(SwordCombat.SweptBlade(Vector3.back,Vector3.left*3,Vector3.back,Vector3.right*3,Vector3.up*5,.3f),Is.False);
        }
        [Test] public void AiCanDefendParryAndInitiateVariedAttacks()
        {
            var state=Random.state;Random.InitState(44);
            try {
                var brain=new SwordAI();var self=new SwordFighter();var opponent=new SwordFighter {Action=SwordAction.Windup,Attack=new SwordAttack {Direction=SlashDirection.Downward}};
                var settings=new SwordAISettings {reactionSeconds=0,blockChance=1,parryAccuracy=1,aggression=0};var combat=new SwordCombatSettings();
                brain.Tick(.02f,1,self,opponent,settings,combat);
                Assert.That(SwordCombat.GuardMatches(self.GuardBlade,Vector2.down,combat.guardTolerance),Is.True);
                opponent.Action=SwordAction.Active;brain.Tick(.02f,1.02,self,opponent,settings,combat);
                Assert.That(SwordCombat.Resolve(true,opponent.Attack,self.Defense(true),1.02,combat),Is.EqualTo(SwordContact.Parry));
                opponent.Action=SwordAction.Guard;settings.aggression=1;settings.attackVariation=1;
                var directions=new System.Collections.Generic.HashSet<SlashDirection>();
                for(int i=0;i<20;i++){self.Action=SwordAction.Guard;brain.Tick(3,2+i*3,self,opponent,settings,combat);Assert.That(self.Action,Is.EqualTo(SwordAction.Windup));directions.Add(self.Attack.Direction);}
                Assert.That(directions.Count,Is.GreaterThan(1));
            } finally {Random.state=state;}
        }
        [Test] public void RoundsResetHealthAndFinishFirstToTwo()
        {
            var m=new SwordRounds();m.Damage(1,30);Assert.That(m.Health[1],Is.EqualTo(70));m.Damage(1,90);
            Assert.That(m.RoundComplete,Is.True);Assert.That(m.Wins[0],Is.EqualTo(1));m.Damage(1,10);Assert.That(m.Wins[0],Is.EqualTo(1));
            m.NextRound();Assert.That(m.Round,Is.EqualTo(2));Assert.That(m.Health[1],Is.EqualTo(100));
            m.Damage(0,100);m.NextRound();m.Damage(1,100);Assert.That(m.Complete,Is.True);Assert.That(m.Winner,Is.Zero);
            m.NextRound();Assert.That(m.Round,Is.EqualTo(3));
        }
    }
}
