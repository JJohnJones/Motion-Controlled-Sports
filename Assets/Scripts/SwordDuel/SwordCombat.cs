using System;
using UnityEngine;
namespace MotionControllers.SwordDuel
{
    public enum SlashDirection { LeftToRight, RightToLeft, Downward, Upward }
    public enum SwordAction { Guard, Windup, Active, Recovery, Stagger }
    public enum SwordContact { Miss, Hit, Block, Parry }
    [Serializable] public sealed class SwordCombatSettings
    {
        [Min(.05f)] public float windup = .28f, active = .24f, recovery = .55f, stagger = .85f;
        [Range(.04f,.25f)] public float parryWindow = .12f;
        [Range(5,60)] public float guardTolerance = 28;
        [Min(.1f)] public float parryAngularSpeed = 1.6f;
        [Range(0,1)] public float parryOpposition = .3f;
        public float baseDamage = 18, strengthDamage = 10, reach = 3.6f;
    }
    public struct SwordAttack
    {
        public SlashDirection Direction;
        public float Strength, Peak;
        public static Vector2 Vector(SlashDirection d) => d == SlashDirection.LeftToRight ? Vector2.right : d == SlashDirection.RightToLeft ? Vector2.left : d == SlashDirection.Downward ? Vector2.down : Vector2.up;
    }
    public struct SwordDefense
    {
        public bool Valid;
        public Vector2 Blade;
        public Vector2 ParryMotion;
        public double ParryAt;
        public float ParrySpeed;
    }
    // Pure resolution: one outcome per contact; faster attacks never bypass a valid defense.
    public static class SwordCombat
    {
        public static bool GuardMatches(Vector2 blade, Vector2 incoming, float tolerance) => blade.sqrMagnitude > .35f &&
            Mathf.Abs(Vector2.Dot(blade.normalized,incoming.normalized)) <= Mathf.Sin(tolerance*Mathf.Deg2Rad);
        public static SwordContact Resolve(bool bodyContact, SwordAttack attack, SwordDefense defense, double now, SwordCombatSettings settings)
        {
            if(!bodyContact)return SwordContact.Miss;
            var incoming=SwordAttack.Vector(attack.Direction);
            if(!defense.Valid || !GuardMatches(defense.Blade,incoming,settings.guardTolerance))return SwordContact.Hit;
            double age=now-defense.ParryAt;
            if(age>=0 && age<=settings.parryWindow && defense.ParrySpeed>=settings.parryAngularSpeed &&
                Vector2.Dot(defense.ParryMotion.normalized,-incoming)>=settings.parryOpposition)return SwordContact.Parry;
            return SwordContact.Block;
        }
        public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {var d=b-a;return Vector3.Distance(p,a+d*(d.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude)));}
        // The tip segment is swept analytically, plus the blade at intermediate poses.
        public static bool SweptBlade(Vector3 oldHand,Vector3 oldTip,Vector3 hand,Vector3 tip,Vector3 torso,float radius)
        {
            if(SegmentDistance(oldTip,tip,torso)<=radius)return true;
            for(int i=0;i<=8;i++)if(SegmentDistance(Vector3.Lerp(oldHand,hand,i/8f),Vector3.Lerp(oldTip,tip,i/8f),torso)<=radius)return true;
            return false;
        }
    }
    // Plain match state, independent of GameObjects and motion input.
    public sealed class SwordRounds
    {
        public float[] Health {get;} = new float[2];
        public int[] Wins {get;} = new int[2];
        public int Round {get;private set;} = 1;
        public int Winner {get;private set;} = -1;
        public int RoundWinner {get;private set;} = -1;
        public bool Complete => Winner >= 0;
        public bool RoundComplete {get;private set;}
        private readonly int target;
        private readonly float maximumHealth;
        public SwordRounds(int wins=2,float health=100) {target=Math.Max(1,wins);maximumHealth=Math.Max(1,health);ResetHealth();}
        public void Damage(int player,float amount)
        {
            if(RoundComplete || Complete)return;
            if(player<0 || player>1 || float.IsNaN(amount) || float.IsInfinity(amount))throw new ArgumentOutOfRangeException();
            Health[player]=Math.Max(0,Health[player]-Math.Max(0,amount));
            if(Health[player]>0)return;
            RoundWinner=1-player;RoundComplete=true;Wins[RoundWinner]++;
            if(Wins[RoundWinner]>=target)Winner=RoundWinner;
        }
        public void NextRound(){if(!RoundComplete || Complete)return;Round++;ResetHealth();}
        private void ResetHealth(){Health[0]=Health[1]=maximumHealth;RoundComplete=false;RoundWinner=-1;}
    }
}
