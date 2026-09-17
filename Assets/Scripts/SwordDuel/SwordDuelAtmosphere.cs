using System.Collections.Generic;
using MotionControllers.Core;
using UnityEngine;
namespace MotionControllers.SwordDuel
{
    // Scene-owned presentation; no controller or combat ownership, no gameplay colliders.
    public sealed class SwordDuelAtmosphere : MonoBehaviour
    {
        [Min(2)] public float cameraDistance=4.4f;
        public float cameraHeight=2.65f, shoulderOffset=.95f;
        [Range(35,75)] public float fieldOfView=52;
        [Range(0,.1f)] public float impactShake=.035f;
        private SwordDuelSession session;
        private readonly List<Material> materials=new List<Material>();
        private readonly List<AudioClip> clips=new List<AudioClip>();
        private readonly AudioSource[] voices=new AudioSource[6];
        private readonly Transform[] bursts=new Transform[4];
        private readonly float[] burstLife=new float[4];
        private readonly SwordAction[] previousAction=new SwordAction[2];
        private readonly Transform[] helmets=new Transform[2];
        private int voice,burst;
        private float shake,elapsed;
        private bool paused;
        private SwordRoundPhase previousPhase;
        public void Initialize(SwordDuelSession value)
        {
            session=value;session.ContactFeedback+=Contact;
            BuildArena();
            for(int i=0;i<6;i++){var go=new GameObject("Duel audio");go.transform.SetParent(transform,false);voices[i]=go.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=.45f;voices[i].minDistance=3;voices[i].maxDistance=22;}
            for(int i=0;i<6;i++)clips.Add(Sound(i));
            for(int i=0;i<4;i++){bursts[i]=Shape("Contact burst",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one,Mat(new Color(1,.85f,.35f)));bursts[i].gameObject.SetActive(false);}
            for(int i=0;i<2;i++){
                var f=session.Fighters[i];var dark=Mat(new Color(.09f,.16f,.23f));
                helmets[i]=Shape("Helmet",PrimitiveType.Sphere,f.Root,new Vector3(0,1.83f,0),new Vector3(.52f,.52f,.52f),Mat(new Color(.68f,.82f,.88f)));
                Shape("Visor",PrimitiveType.Cube,helmets[i],new Vector3(0,.02f,i==0?.46f:-.46f),new Vector3(.7f,.2f,.14f),dark);
                for(int side=-1;side<=1;side+=2){Shape("Boot",PrimitiveType.Cube,f.Root,new Vector3(side*.21f,.16f,.02f),new Vector3(.25f,.3f,.46f),dark);Shape("Shoulder armor",PrimitiveType.Sphere,f.Root,new Vector3(side*.38f,1.4f,0),new Vector3(.3f,.26f,.32f),f.BodyMaterial);}
            }
            UpdateCamera(1,true);
        }
        private Material Mat(Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;materials.Add(m);return m;}
        private Transform Shape(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        private void BuildArena()
        {
            var root=new GameObject("Duel courtyard scenery").transform;root.SetParent(transform,false);
            var stone=Mat(new Color(.28f,.36f,.42f));var trim=Mat(new Color(.69f,.78f,.79f));var grass=Mat(new Color(.12f,.29f,.23f));var blue=Mat(new Color(.07f,.52f,.73f));var orange=Mat(new Color(.88f,.39f,.13f));
            Shape("Garden grounds",PrimitiveType.Cube,root,new Vector3(0,-.65f,0),new Vector3(70,.4f,70),grass);
            for(int side=-1;side<=1;side+=2){
                Shape("Courtyard wall",PrimitiveType.Cube,root,new Vector3(side*9,1.3f,0),new Vector3(.7f,3,22),stone);
                Shape("Courtyard wall",PrimitiveType.Cube,root,new Vector3(0,1.3f,side*11),new Vector3(18,3,.7f),stone);
                for(int j=-2;j<=2;j++){
                    Shape("Wall pilaster",PrimitiveType.Cube,root,new Vector3(side*8.6f,1.65f,j*4),new Vector3(.8f,3.7f,.9f),trim);
                    Shape("Spectator step",PrimitiveType.Cube,root,new Vector3(side*7, .2f+j*.04f,j*3),new Vector3(1.7f,.55f,2.5f),stone);
                }
                for(int x=-1;x<=1;x+=2){
                    Shape("Banner pole",PrimitiveType.Cylinder,root,new Vector3(x*4.3f,2.8f,side*7),new Vector3(.09f,2.8f,.09f),trim);
                    Shape("Team banner",PrimitiveType.Cube,root,new Vector3(x*4.3f,4,side*7),new Vector3(1.2f,2,.06f),side<0?blue:orange);
                }
            }
            for(int i=0;i<4;i++){
                float x=i%2==0?-3.9f:3.9f,z=i<2?-3.9f:3.9f;
                Shape("Arena corner cap",PrimitiveType.Cube,root,new Vector3(x,.06f,z),new Vector3(.45f,.08f,.45f),trim);
            }
        }
        public void SetPaused(bool value){paused=value;foreach(var source in voices)if(source!=null){if(value)source.Pause();else source.UnPause();}}
        private void Contact(SwordContact kind,Vector3 position,float strength)
        {
            int sound=kind==SwordContact.Parry?3:kind==SwordContact.Block?2:1;Play(sound,position,.65f+strength*.25f);
            int slot=burst++%bursts.Length;bursts[slot].position=position;bursts[slot].gameObject.SetActive(true);bursts[slot].GetComponent<Renderer>().sharedMaterial.color=kind==SwordContact.Parry?Color.cyan:kind==SwordContact.Block?new Color(1,.85f,.3f):new Color(1,.4f,.22f);burstLife[slot]=.2f;
            shake=kind==SwordContact.Parry?.2f:.11f;
        }
        private void Play(int clip,Vector3 position,float volume)
        {
            var source=voices[voice++%voices.Length];source.transform.position=position;source.pitch=1;source.clip=clips[clip];source.volume=volume*PlayerPreferences.EffectsVolume;source.Play();
        }
        private void LateUpdate()
        {
            if(session==null || paused)return;float dt=Time.deltaTime;elapsed+=dt;
            for(int i=0;i<2;i++){
                var f=session.Fighters[i];
                if(f.Action==SwordAction.Active && previousAction[i]!=SwordAction.Active)Play(0,f.Hand,.5f+f.Attack.Strength*.25f);
                previousAction[i]=f.Action;
                float lean=f.Action==SwordAction.Windup?-7:f.Action==SwordAction.Active?12:f.Action==SwordAction.Stagger?-14:0;
                f.BodyRenderer.transform.localRotation=Quaternion.Slerp(f.BodyRenderer.transform.localRotation,Quaternion.Euler(lean*(i==0?1:-1),0,0),1-Mathf.Exp(-dt*15));
                helmets[i].localPosition=new Vector3(0,1.83f+Mathf.Sin(elapsed*2+i)*.012f,Mathf.Sin(lean*Mathf.Deg2Rad)*.12f*(i==0?1:-1));
            }
            if(session.Phase!=previousPhase){if(session.Phase==SwordRoundPhase.Duel)Play(4,Vector3.up,.6f);if(session.Phase==SwordRoundPhase.Result || session.Phase==SwordRoundPhase.Complete)Play(5,Vector3.up,.65f);previousPhase=session.Phase;}
            for(int i=0;i<bursts.Length;i++)if(burstLife[i]>0){burstLife[i]-=dt;float t=1-burstLife[i]/.2f;bursts[i].localScale=Vector3.one*(.08f+t*.38f);if(burstLife[i]<=0)bursts[i].gameObject.SetActive(false);}
            shake=Mathf.Max(0,shake-dt);UpdateCamera(dt,false);
        }
        private void UpdateCamera(float dt,bool snap)
        {
            var camera=session.duelCamera;if(camera==null)return;
            var player=session.Fighters[0].Root.position;var enemy=session.Fighters[1].Root.position;
            var forward=(enemy-player).normalized;var right=Vector3.Cross(Vector3.up,forward);
            var desired=player-forward*cameraDistance+Vector3.up*cameraHeight+right*shoulderOffset;
            camera.transform.position=snap?desired:Vector3.Lerp(camera.transform.position,desired,1-Mathf.Exp(-dt*7));
            var target=Vector3.Lerp(player,enemy,.62f)+Vector3.up*1.2f;
            camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position);
            if(shake>0)camera.transform.Rotate(Mathf.Sin(elapsed*93)*impactShake*shake*50,Mathf.Sin(elapsed*79)*impactShake*shake*30,0);
            camera.fieldOfView=fieldOfView;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.backgroundColor=new Color(.42f,.67f,.78f);
        }
        private static AudioClip Sound(int kind)
        {
            const int rate=22050;float duration=kind>=4?.55f:kind==3?.35f:.22f;var data=new float[(int)(duration*rate)];var random=new System.Random(140+kind);float low=0;
            for(int i=0;i<data.Length;i++){float t=i/(float)rate,n=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,n,.2f);float value;
                if(kind==0)value=low*.8f*Mathf.Sin(t/duration*Mathf.PI);
                else if(kind==1)value=(low*.5f+Mathf.Sin(t*950)*.5f)*Mathf.Exp(-t*25);
                else if(kind<4)value=(Mathf.Sin(t*2*Mathf.PI*(kind==3?1450:870))*.35f+Mathf.Sin(t*2*Mathf.PI*2137)*.15f+n*.12f)*Mathf.Exp(-t*15);
                else value=Mathf.Sin(t*2*Mathf.PI*(t<.18f?523:t<.36f?659:784))*.2f*Mathf.Exp(-(t%.18f)*8);
                data[i]=value*Mathf.Min(1,t*600)*Mathf.Min(1,(duration-t)*30);
            }
            var clip=AudioClip.Create("Sword cue "+kind,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private void OnDestroy(){if(session!=null)session.ContactFeedback-=Contact;foreach(var clip in clips)Destroy(clip);foreach(var material in materials)Destroy(material);}
    }
}
