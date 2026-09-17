using System;
using UnityEngine;
namespace MotionControllers.SwordDuel
{
    [Serializable] public sealed class SwordMotionSettings
    {
        public float minimumSpeed=3, maximumSpeed=13, minimumArcDegrees=22, minimumSeconds=.055f;
        [Range(0,1)] public float directionConsistency=.75f;
        public float sensitivity=1, orientationSensitivity=1, response=25;
        [HideInInspector] public float rotationLimit=100; // Retained for old serialized scenes.
        [HideInInspector] public bool leftHanded; // Handedness comes from the controller/player settings.
    }
    public sealed class SwordMotion
    {
        public Quaternion Pose {get;private set;}=Quaternion.identity;
        public Vector2 MotionDirection {get;private set;}
        public float AngularSpeed {get;private set;}
        public bool Fresh {get;private set;}
        private Quaternion neutral=Quaternion.identity, neutralAbsolute=Quaternion.identity, previousPose=Quaternion.identity;
        private bool leftHanded;
        private bool poseInitialized;
        private long sequence;
        private int revision=-1;
        private double previous=-1, started;
        private float arc, peak;
        private Vector2 direction;
        private bool armed, flush=true;
        public void SetNeutral(ControllerSession c){neutral=c.RawRotation;neutralAbsolute=c.Latest.Orientation;leftHanded=c.LeftHanded;revision=c.CalibrationRevision;poseInitialized=false;Reset();}
        public void Reset(){armed=false;arc=peak=0;previous=-1;flush=true;Fresh=false;}
        public bool Read(ControllerSession c,double now,bool canAttack,SwordMotionSettings settings,out SwordAttack attack)
        {
            attack=default;Fresh=c.IsCalibrated && c.HasFrame && now-c.Latest.ReceivedAtSeconds<.3;
            if(!Fresh){Reset();return false;}
            if(revision!=c.CalibrationRevision || leftHanded!=c.LeftHanded)SetNeutral(c);
            var current=MapGripRotation(Quaternion.Inverse(neutral)*c.RawRotation,leftHanded);
            if(!poseInitialized){Pose=current;previousPose=current;poseInitialized=true;}
            else {Pose=IntegrateOrientation(Pose,previousPose,current,settings.orientationSensitivity);previousPose=current;}
            if(flush){sequence=c.Latest.Sequence;flush=false;return false;}
            bool detected=false;
            for(int i=c.HistoryCount-1;i>=0;i--){
                var f=c.GetHistoryFromNewest(i);if(f.Sequence<=sequence)continue;sequence=f.Sequence;
                if(c.Latest.TimestampMs-f.TimestampMs>220 || !f.HasAngularVelocity)continue;
                var relative=Quaternion.Inverse(neutralAbsolute)*f.Orientation;
                var angular=MapGripAngularVelocity(relative,f.AngularVelocity,leftHanded);AngularSpeed=angular.magnitude;
                var planar=new Vector2(-angular.z,-angular.x);
                MotionDirection=planar.normalized;
                if(Sample(f.TimestampMs/1000,planar*c.MotionSensitivity,canAttack && !detected,settings,out var sample)){attack=sample;detected=true;}
            }
            return detected;
        }
        // Converted device +Y is phone top; -Z faces out of the screen.
        // Right hand: screen faces left, so device +Z points right. Mirror for left hand.
        public static Quaternion GripBasis(bool leftHanded)=>Quaternion.AngleAxis(leftHanded?-90:90,Vector3.up);
        public static Quaternion MapGripRotation(Quaternion relative,bool leftHanded)
        {
            var basis=GripBasis(leftHanded);
            return (basis*relative*Quaternion.Inverse(basis)).normalized;
        }
        public static Vector3 MapGripAngularVelocity(Quaternion relative,Vector3 deviceAngularVelocity,bool leftHanded)
            => GripBasis(leftHanded)*(relative*deviceAngularVelocity);
        // Incremental rotation avoids flipping at +/-180 degrees under an absolute angle clamp.
        public static Quaternion IntegrateOrientation(Quaternion pose,Quaternion previous,Quaternion current,float sensitivity)
            => (pose*Quaternion.SlerpUnclamped(Quaternion.identity,Quaternion.Inverse(previous)*current,Mathf.Clamp(sensitivity,.1f,2))).normalized;
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
