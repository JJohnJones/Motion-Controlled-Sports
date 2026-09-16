using System;
using UnityEngine;
namespace MotionControllers.SwordDuel
{
    [Serializable] public sealed class SwordMotionSettings
    {
        public float minimumSpeed=3, maximumSpeed=13, minimumArcDegrees=22, minimumSeconds=.055f;
        [Range(0,1)] public float directionConsistency=.75f;
        public float sensitivity=1, orientationSensitivity=1, response=25, rotationLimit=100;
        public bool leftHanded;
    }
    public sealed class SwordMotion
    {
        public Quaternion Pose {get;private set;}=Quaternion.identity;
        public Vector2 MotionDirection {get;private set;}
        public float AngularSpeed {get;private set;}
        public bool Fresh {get;private set;}
        private Quaternion neutral=Quaternion.identity;
        private long sequence;
        private int revision=-1;
        private double previous=-1, started;
        private float arc, peak;
        private Vector2 direction;
        private bool armed, flush=true;
        public void SetNeutral(ControllerSession c){neutral=c.RawRotation;revision=c.CalibrationRevision;Reset();}
        public void Reset(){armed=false;arc=peak=0;previous=-1;flush=true;Fresh=false;}
        public bool Read(ControllerSession c,double now,bool canAttack,SwordMotionSettings settings,out SwordAttack attack)
        {
            attack=default;Fresh=c.IsCalibrated && c.HasFrame && now-c.Latest.ReceivedAtSeconds<.3;
            if(!Fresh){Reset();return false;}
            if(revision!=c.CalibrationRevision){revision=c.CalibrationRevision;neutral=Quaternion.identity;Reset();}
            Pose=Quaternion.RotateTowards(Quaternion.identity,Quaternion.SlerpUnclamped(Quaternion.identity,Quaternion.Inverse(neutral)*c.RawRotation,settings.orientationSensitivity),settings.rotationLimit);
            if(flush){sequence=c.Latest.Sequence;flush=false;return false;}
            bool detected=false;
            for(int i=c.HistoryCount-1;i>=0;i--){
                var f=c.GetHistoryFromNewest(i);if(f.Sequence<=sequence)continue;sequence=f.Sequence;
                if(c.Latest.TimestampMs-f.TimestampMs>220 || !f.HasAngularVelocity)continue;
                var angular=Pose*f.AngularVelocity;AngularSpeed=angular.magnitude;
                var planar=new Vector2(-angular.z,-angular.x);if(settings.leftHanded)planar.x=-planar.x;
                MotionDirection=planar.normalized;
                if(Sample(f.TimestampMs/1000,planar*c.MotionSensitivity,canAttack && !detected,settings,out var sample)){attack=sample;detected=true;}
            }
            return detected;
        }
        public bool Sample(double time,Vector2 velocity,bool canAttack,SwordMotionSettings settings,out SwordAttack attack)
        {
            attack=default;float dt=previous<0?0:(float)(time-previous);previous=time;
            float speed=velocity.magnitude*settings.sensitivity;
            if(!canAttack){armed=false;arc=peak=0;return false;}
            if(speed<settings.minimumSpeed*.45f){armed=true;arc=peak=0;return false;}
            if(!armed || speed<settings.minimumSpeed || dt<=0 || dt>.12f){arc=peak=0;return false;}
            var d=velocity.normalized;
            if(arc==0 || Vector2.Dot(d,direction)<settings.directionConsistency){arc=0;peak=0;started=time;direction=d;}
            arc+=speed*dt*Mathf.Rad2Deg;peak=Mathf.Max(peak,speed);
            if(arc<settings.minimumArcDegrees || time-started<settings.minimumSeconds)return false;
            attack=new SwordAttack {Direction=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?(direction.x>0?SlashDirection.LeftToRight:SlashDirection.RightToLeft):(direction.y>0?SlashDirection.Upward:SlashDirection.Downward),
                Peak=peak,Strength=Mathf.Clamp01((peak-settings.minimumSpeed)/Mathf.Max(.1f,settings.maximumSpeed-settings.minimumSpeed))};
            armed=false;arc=0;return true;
        }
    }
}
