using System.Collections.Generic;
using UnityEngine;
namespace MotionControllers.SwordDuel
{
    public sealed class SwordPresentation
    {
        private readonly List<Material> materials=new List<Material>();
        private Material Material(Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;materials.Add(m);return m;}
        private GameObject Shape(PrimitiveType type,Transform parent,Vector3 p,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;
            Object.Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        public void Create(SwordFighter f,Transform parent)
        {
            var root=new GameObject(f.Name);root.transform.SetParent(parent,false);f.Root=root.transform;
            f.Color=f.Index==0?new Color(.12f,.7f,1):new Color(1,.44f,.15f);f.BodyMaterial=Material(f.Color);
            f.BodyRenderer=Shape(PrimitiveType.Capsule,root.transform,new Vector3(0,.9f,0),new Vector3(.7f,.9f,.7f),f.BodyMaterial).GetComponent<Renderer>();
            var blade=Shape(PrimitiveType.Cube,parent,Vector3.zero,Vector3.one,Material(new Color(.8f,.94f,1)));blade.name="Sword "+f.Name;f.Blade=blade.transform;
            Shape(PrimitiveType.Cube,f.Blade,new Vector3(0,-.48f,0),new Vector3(4,.035f,3),Material(new Color(.4f,.7f,.8f)));
            Shape(PrimitiveType.Cube,f.Blade,new Vector3(0,-.57f,0),new Vector3(.8f,.15f,1.5f),Material(new Color(.12f,.16f,.22f)));
            var tip=new GameObject("Sword trail");tip.transform.SetParent(f.Blade,false);tip.transform.localPosition=Vector3.up*.5f;
            f.Trail=tip.AddComponent<TrailRenderer>();f.Trail.time=.12f;f.Trail.startWidth=.13f;f.Trail.endWidth=.01f;f.Trail.sharedMaterial=Material(f.Color);f.Trail.emitting=false;
            var label=new GameObject("Player label").AddComponent<TextMesh>();label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,2.2f,0);label.text=f.Name;
            label.fontSize=40;label.characterSize=.18f;label.anchor=TextAnchor.MiddleCenter;label.color=f.Color;
        }
        public void Draw(SwordFighter f,SwordCombatSettings settings,Camera camera)
        {
            var hand=f.Hand;var tip=Tip(f,settings);
            f.Blade.position=(hand+tip)*.5f;f.Blade.rotation=Quaternion.FromToRotation(Vector3.up,tip-hand);f.Blade.localScale=new Vector3(.11f,Vector3.Distance(hand,tip),.05f);
            f.Trail.emitting=f.Action==SwordAction.Active;f.BodyMaterial.color=f.FlashTime>0?f.FeedbackColor:f.Color;
            var label=f.Root.GetComponentInChildren<TextMesh>();if(camera!=null)label.transform.rotation=camera.transform.rotation;
        }
        public static Vector3 Tip(SwordFighter f,SwordCombatSettings settings)
        {
            Vector3 guard=f.Hand+f.Facing*(f.GuardPose*Vector3.up)*1.8f;
            var direction=SwordAttack.Vector(f.Attack.Direction);Vector3 sweep=f.Facing*new Vector3(direction.x,direction.y,0);
            var center=f.Hand+f.Forward*2.35f;
            float progress=Mathf.Clamp01(f.ActionTime/settings.active);
            Vector3 attack=center+sweep*Mathf.Lerp(-1.1f,1.1f,progress);
            if(f.Action==SwordAction.Windup)return Vector3.Lerp(guard,center-sweep*1.1f,Mathf.Clamp01(f.ActionTime/settings.windup));
            if(f.Action==SwordAction.Active)return attack;
            if(f.Action==SwordAction.Recovery)return Vector3.Lerp(f.RecoveryTip,guard,Mathf.SmoothStep(0,1,Mathf.Clamp01(f.ActionTime/settings.recovery)));
            return guard;
        }
        public void Dispose(){foreach(var m in materials)Object.Destroy(m);}
    }
}
