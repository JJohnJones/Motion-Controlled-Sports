using System.Collections.Generic;
using UnityEngine;
namespace MotionControllers.Tennis
{
    // All scenery is cosmetic. TennisCourt remains the authority for net and line calls.
    public sealed class TennisVenue
    {
        private readonly List<Material> materials=new List<Material>();
        public Transform Root {get;}
        public TextMesh Announcement {get;}
        public TextMesh Score {get;}
        public Material ContactMaterial {get;}
        public Material ShadowMaterial {get;}
        public TennisVenue(Transform parent)
        {
            Root=new GameObject("Tennis venue (presentation)").transform;Root.SetParent(parent,false);
            var grass=Material("Park lawn",new Color(.15f,.34f,.21f));
            var leaf=Material("Tree canopy",new Color(.13f,.4f,.24f));
            var bark=Material("Tree trunks",new Color(.3f,.21f,.13f));
            var stone=Material("Walkway",new Color(.39f,.5f,.51f));
            var navy=Material("Court furniture",new Color(.025f,.12f,.2f));
            var aqua=Material("Stadium seating",new Color(.05f,.53f,.66f),.45f);
            var white=Material("Ivory trim",new Color(.9f,.97f,.94f),.3f);
            var blue=Material("Service court tint",new Color(.095f,.39f,.52f));
            ContactMaterial=Material("Contact light",new Color(.85f,1,.3f),0,true);
            ShadowMaterial=Material("Ball shadow",new Color(.04f,.18f,.22f),0,true);
            Box("Park ground",new Vector3(0,-.42f,0),new Vector3(65,.2f,65),grass);
            for(int side=-1;side<=1;side+=2){
                Box("Courtside walkway",new Vector3(side*12,-.035f,0),new Vector3(4,.02f,36),stone);
                for(int z=-12;z<=12;z+=8){
                    Box("Bench seat",new Vector3(side*12,.48f,z),new Vector3(1.8f,.22f,3),aqua);
                    Box("Bench back",new Vector3(side*12.8f,1,z),new Vector3(.18f,1.1f,3),aqua);
                    Box("Bench support",new Vector3(side*12,.15f,z),new Vector3(1.4f,.5f,2.5f),navy);
                }
                for(int z=-18;z<=18;z+=3){
                    Box("Fence post",new Vector3(side*15.2f,2.8f,z),new Vector3(.07f,1.9f,.07f),navy);
                    Box("Fence post",new Vector3(z*.8f,2.8f,side*19.2f),new Vector3(.07f,1.9f,.07f),navy);
                }
                foreach(float y in new[]{2.15f,3,3.7f}){
                    Box("Fence rail",new Vector3(side*15.2f,y,0),new Vector3(.04f,.04f,38.4f),navy);
                    Box("Fence rail",new Vector3(0,y,side*19.2f),new Vector3(30.4f,.04f,.04f),navy);
                }
                for(int z=-20;z<=20;z+=10){
                    float x=side*(20+Mathf.Abs(z)*.1f);
                    Shape("Park tree trunk",PrimitiveType.Cylinder,new Vector3(x,1.8f,z),new Vector3(.6f,2,.6f),bark);
                    Shape("Park canopy",PrimitiveType.Sphere,new Vector3(x,4.5f,z),new Vector3(4,5,4),leaf);
                }
                Box("Wind screen stripe",new Vector3(0,1.5f,side*18.82f),new Vector3(27,.12f,.035f),aqua);
                for(int z=-10;z<=10;z+=20){
                    Box("Light mast",new Vector3(side*14,3.3f,z),new Vector3(.12f,6.6f,.12f),navy);
                    Box("Light housing",new Vector3(side*14,6.6f,z),new Vector3(1,.25f,.55f),white);
                }
            }
            for(int side=-1;side<=1;side+=2)for(int half=-1;half<=1;half+=2)
                Box("Service box tone",new Vector3(side*2.06f,-.003f,half*3.2f),new Vector3(4.08f,.001f,6.36f),blue);
            Box("Courtside score display",new Vector3(0,4.7f,18.9f),new Vector3(12.8f,2.5f,.25f),navy);
            Text("Brand","PHONE SPORTS  /  TENNIS",new Vector3(0,5.5f,18.7f),.2f,white.color);
            Score=Text("Live court score","LOVE ALL",new Vector3(0,4.75f,18.7f),.25f,ContactMaterial.color);
            Announcement=Text("Point announcement","READY TO SERVE",new Vector3(0,4.05f,18.7f),.18f,white.color);
        }
        public Transform CreateAvatarDetails(TennisPlayer player)
        {
            var visual=new GameObject("Player presentation").transform;visual.SetParent(player.Avatar,false);
            // Reparent only the existing body mesh, never the racket, contact anchor or player root.
            var body=player.Avatar.GetChild(0);if(body!=visual)body.SetParent(visual,false);
            var shirt=Material(player.Name+" trim",player.Team==0?new Color(.3f,.85f,1):new Color(1,.65f,.25f),.2f);
            var skin=Material(player.Name+" face",new Color(.72f,.48f,.3f));
            Shape("Head",PrimitiveType.Sphere,new Vector3(0,1.75f,0),Vector3.one*.36f,skin,visual);
            Shape("Headband",PrimitiveType.Cylinder,new Vector3(0,1.84f,0),new Vector3(.37f,.035f,.37f),shirt,visual);
            foreach(float x in new[]{-.19f,.19f})Shape("Court shoe",PrimitiveType.Cube,new Vector3(x,.09f,.05f),new Vector3(.22f,.16f,.35f),shirt,visual);
            visual.localScale=Vector3.one*.78f;
            return visual;
        }
        private Material Material(string name,Color color,float smooth=.1f,bool unlit=false)
        {var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,color=color};if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);materials.Add(m);return m;}
        private GameObject Box(string name,Vector3 pos,Vector3 size,Material m)=>Shape(name,PrimitiveType.Cube,pos,size,m);
        private GameObject Shape(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material m,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent!=null?parent:Root,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);go.GetComponent<Renderer>().sharedMaterial=m;return go;
        }
        private TextMesh Text(string name,string text,Vector3 pos,float size,Color color)
        {var go=new GameObject(name);go.transform.SetParent(Root,false);go.transform.localPosition=pos;var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=64;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;return label;}
        public void Dispose(){foreach(var m in materials)Object.Destroy(m);if(Root!=null)Object.Destroy(Root.gameObject);}
    }
}
