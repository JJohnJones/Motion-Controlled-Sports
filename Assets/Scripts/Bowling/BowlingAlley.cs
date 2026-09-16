using System.Collections.Generic;
using UnityEngine;
namespace MotionControllers.Bowling
{
    // Cosmetic geometry only. The existing lane and cleanup bounds own all gameplay collision.
    public sealed class BowlingAlley
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly Transform root;
        public TextMesh Display { get; private set; }
        public Transform Sweep { get; private set; }
        public Material Accent { get; private set; }
        public BowlingAlley(Transform parent)
        {
            root = new GameObject("Alley environment (presentation)").transform; root.SetParent(parent, false);
            var wood = Material("Warm maple", new Color(.65f,.41f,.19f), .6f);
            var lightWood = Material("Maple boards", new Color(.77f,.54f,.28f), .55f);
            var carpet = Material("Midnight carpet", new Color(.025f,.09f,.15f));
            var wall = Material("Alley walls", new Color(.1f,.23f,.3f));
            var dark = Material("Pinsetter", new Color(.018f,.035f,.055f));
            var seat = Material("Aqua seating", new Color(.025f,.45f,.56f), .45f);
            var white = Material("Porcelain", new Color(.9f,.95f,.97f), .6f);
            Accent = Material("Cyan light strips", new Color(.1f,.75f,.86f), .3f, true);
            Box("Building floor", new Vector3(0,-.63f,7), new Vector3(24,.3f,35),carpet);
            Box("Approach",new Vector3(0,-.17f,-4),new Vector3(15,.3f,6),wood);
            Box("Back wall",new Vector3(0,3.5f,23),new Vector3(24,8,.4f),wall);
            Box("Ceiling",new Vector3(0,9.5f,7),new Vector3(24,.2f,35),wall).GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            for(int i=0;i<2;i++){var lamp=new GameObject("Room fill");lamp.transform.SetParent(root,false);lamp.transform.localPosition=new Vector3(0,5,3+i*13);var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=22;light.intensity=2;light.color=new Color(1,.9f,.77f);light.shadows=LightShadows.None;}
            for(int side=-1;side<=1;side+=2) {
                Box("Side wall",new Vector3(side*12,3.5f,7),new Vector3(.3f,8,35),wall);
                Box("Wall light band",new Vector3(side*11.8f,2.8f,7),new Vector3(.05f,.12f,34),Accent);
                float laneX=side*4.4f;
                Box("Neighbor lane",new Vector3(laneX,-.15f,10),new Vector3(2.4f,.3f,22),wood);
                for(int edge=-1;edge<=1;edge+=2) Box("Neighbor gutter",new Vector3(laneX+edge*1.5f,-.24f,10),new Vector3(.55f,.2f,22),dark);
                for(int row=0;row<4;row++)for(int col=0;col<=row;col++) {
                    Vector3 pin=new Vector3(laneX+(col-row*.5f)*.36f,.22f,17.5f+row*.312f);
                    Shape("Display pin",PrimitiveType.Capsule,pin,new Vector3(.16f,.22f,.16f),white);
                }
                Box("Bench cushion",new Vector3(side*8,.4f,-3),new Vector3(2,.3f,3.6f),seat);
                Box("Bench back",new Vector3(side*8.9f,.9f,-3),new Vector3(.25f,1.2f,3.6f),seat);
                Box("Bench base",new Vector3(side*8,.05f,-3),new Vector3(1.5f,.5f,3.2f),dark);
                Shape("Table",PrimitiveType.Cylinder,new Vector3(side*5.8f,.65f,-4.8f),new Vector3(1.5f,.07f,1.5f),white);
                Box("Table pedestal",new Vector3(side*5.8f,.2f,-4.8f),new Vector3(.2f,.85f,.2f),dark);
            }
            for(int lane=-1;lane<=1;lane++) {
                float x=lane*4.4f;
                // Thin overlay boards sit above the original surface without extra colliders.
                for(int board=0;board<16;board++) {
                    float bx=x-1.2f+(board+.5f)*.15f;
                    Box("Lane board",new Vector3(bx,.001f,10),new Vector3(.147f,.001f,21.95f),board%3==0?lightWood:wood);
                }
                Box("Foul line",new Vector3(x,.007f,1),new Vector3(2.4f,.004f,.05f),dark);
                for(int arrow=-3;arrow<=3;arrow++) {
                    var marker=Box("Aim diamond",new Vector3(x+arrow*.24f,.006f,4+Mathf.Abs(arrow)*.18f),new Vector3(.055f,.002f,.055f),dark);
                    marker.transform.rotation=Quaternion.Euler(0,45,0);
                }
                Box("Pinsetter opening",new Vector3(x,1.25f,21.7f),new Vector3(3.8f,2.5f,.3f),dark);
                Box("Pinsetter fascia",new Vector3(x,2.8f,21.5f),new Vector3(4.1f,.7f,.6f),seat);
                Box("Fascia light",new Vector3(x,2.4f,21.15f),new Vector3(4,.05f,.05f),Accent);
                Text("Lane number",(lane+2).ToString("00"),new Vector3(x,2.85f,21.15f),.22f);
            }
            for(int z=1;z<22;z+=6) {
                Box("Ceiling beam",new Vector3(0,7.8f,z),new Vector3(22,.16f,.25f),dark);
                Box("Ceiling diffuser",new Vector3(0,7.65f,z),new Vector3(13,.04f,.35f),white);
            }
            Sweep=Box("Visual pinsetter sweep",new Vector3(0,2.1f,19.8f),new Vector3(3.2f,.12f,.12f),Accent).transform;
            Box("Feature sign",new Vector3(0,4.35f,22.3f),new Vector3(12,1.7f,.2f),dark);
            Text("Brand","PHONE SPORTS  /  BOWLING",new Vector3(0,4.65f,22.1f),.22f);
            Display=Text("Roll announcement","MAKE YOUR MOVE",new Vector3(0,3.98f,22.08f),.20f);
        }
        private Material Material(string name,Color color,float smooth=.15f,bool emission=false)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};
            m.SetFloat("_Smoothness",smooth);
            if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.4f);}
            materials.Add(m);return m;
        }
        private GameObject Box(string name,Vector3 position,Vector3 scale,Material material)=>Shape(name,PrimitiveType.Cube,position,scale,material);
        private GameObject Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private TextMesh Text(string name,string value,Vector3 position,float size)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;
            var label=go.AddComponent<TextMesh>();label.text=value;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=64;label.characterSize=size;label.color=new Color(.75f,.98f,1);return label;
        }
        public void Dispose(){foreach(var m in materials)Object.Destroy(m);if(root!=null)Object.Destroy(root.gameObject);}
    }
}
