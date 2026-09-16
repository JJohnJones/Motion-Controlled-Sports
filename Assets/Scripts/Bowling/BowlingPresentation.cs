using UnityEngine;
namespace MotionControllers.Bowling
{
    public sealed class BowlingPresentation : MonoBehaviour
    {
        [Range(0,1)] public float effectsVolume=.55f;
        public bool soundEnabled=true, impactFlashes=true;
        private float Gain => effectsVolume * Core.PlayerPreferences.EffectsVolume;
        private BowlingThrowController bowling;
        private BowlingGameSession session;
        private BowlingAlley alley;
        private BowlingSoundBank sounds;
        private AudioSource rolling, cues;
        private readonly AudioSource[] impacts=new AudioSource[6];
        private readonly Transform[] flashes=new Transform[6];
        private readonly float[] flashLife=new float[6];
        private TrailRenderer trail;
        private Material trailMaterial;
        private int voice;
        private double nextImpact;
        private float announceTime;
        private bool paused, transitioning;
        private float sweepTime;
        public void Initialize(BowlingThrowController controller,BowlingGameSession game)
        {
            if(alley!=null)return;
            bowling=controller;session=game;alley=new BowlingAlley(transform);sounds=new BowlingSoundBank();
            rolling=Source("Ball rolling",false);rolling.clip=sounds.Roll;rolling.loop=true;rolling.volume=0;
            cues=Source("Roll announcements",false);
            for(int i=0;i<impacts.Length;i++) {
                impacts[i]=Source("Pin impact voice",true);
                var flash=GameObject.CreatePrimitive(PrimitiveType.Sphere);flash.name="Impact flash";flash.transform.SetParent(transform,false);
                var collider=flash.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                flash.GetComponent<Renderer>().sharedMaterial=alley.Accent;flash.SetActive(false);flashes[i]=flash.transform;
            }
            foreach(var pin in bowling.pinRack.pins)if(pin!=null) {
                var feedback=pin.GetComponent<BowlingPinFeedback>() ?? pin.gameObject.AddComponent<BowlingPinFeedback>();feedback.Owner=this;
            }
            trail=bowling.ball.gameObject.AddComponent<TrailRenderer>();trail.time=.12f;trail.minVertexDistance=.06f;trail.startWidth=.025f;trail.endWidth=0;
            trailMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(.2f,.75f,1)};
            trail.sharedMaterial=trailMaterial;trail.emitting=false;
            bowling.ThrowLaunched+=OnThrow;session.RollResolved+=OnRoll;
        }
        private AudioSource Source(string name,bool spatial)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var audio=go.AddComponent<AudioSource>();
            audio.playOnAwake=false;audio.spatialBlend=spatial ? .65f : 0;audio.minDistance=8;audio.maxDistance=40;audio.rolloffMode=AudioRolloffMode.Linear;return audio;
        }
        private void OnThrow(BowlingRelease release)
        {
            if(soundEnabled && !paused)cues.PlayOneShot(sounds.Release,Gain*.65f);
            alley.Display.text="WATCH IT ROLL";announceTime=0;
        }
        private void OnRoll(string summary)
        {
            alley.Display.text=summary.ToUpperInvariant();announceTime=1.5f;
            if(soundEnabled && !paused && (summary=="Strike!" || summary=="Spare!"))
                cues.PlayOneShot(summary=="Strike!"?sounds.Strike:sounds.Spare,Gain*.6f);
        }
        public void Impact(Vector3 position,float speed)
        {
            if(paused || alley==null || bowling.State!=BowlingState.Rolling || Time.timeAsDouble<nextImpact)return;
            nextImpact=Time.timeAsDouble+.055;
            int i=voice++%impacts.Length;
            if(soundEnabled){impacts[i].transform.position=position;impacts[i].pitch=Mathf.Lerp(.85f,1.15f,(i%3)/2f);impacts[i].PlayOneShot(sounds.Impact,Gain*Mathf.Clamp01(speed/5));}
            if(impactFlashes && speed>1.5f){flashes[i].position=position+Vector3.up*.1f;flashes[i].gameObject.SetActive(true);flashLife[i]=.16f;}
        }
        public void SetPaused(bool value)
        {
            paused=value;
            if(rolling==null)return;
            foreach(var source in impacts)if(value)source.Pause();else source.UnPause();
            if(value){rolling.Pause();cues.Pause();}else{rolling.UnPause();cues.UnPause();}
        }
        private void Update()
        {
            if(alley==null || paused)return;
            bool transition=session.Phase==BowlingMatchPhase.TurnTransition;
            if(transition && !transitioning)sweepTime=0;
            transitioning=transition;
            if(transition)sweepTime+=Time.deltaTime;
            float progress=transition?Mathf.Clamp01(sweepTime/Mathf.Max(.1f,session.turnTransitionSeconds)):0;
            alley.Sweep.localPosition=new Vector3(0,2.1f-Mathf.Sin(progress*Mathf.PI)*1.5f,19.8f);
            bool moving=bowling.State==BowlingState.Rolling && !bowling.ball.OutOfPlay && !bowling.ball.Body.isKinematic;
            float speed=moving?bowling.ball.Body.linearVelocity.magnitude:0;
            bool grounded=moving && bowling.ball.transform.position.y<.3f && bowling.ball.transform.position.y>-.3f;
            float volume=soundEnabled && grounded?Mathf.Clamp01(speed/12)*Gain*.25f:0;
            rolling.volume=Mathf.MoveTowards(rolling.volume,volume,Time.deltaTime*2);
            rolling.pitch=Mathf.Lerp(.75f,1.4f,Mathf.Clamp01(speed/12));
            if(rolling.volume>.001f && !rolling.isPlaying)rolling.Play();else if(rolling.volume<=.001f && rolling.isPlaying)rolling.Stop();
            trail.emitting=moving && speed>2;if(!moving)trail.Clear();
            for(int i=0;i<flashes.Length;i++)if(flashLife[i]>0){flashLife[i]-=Time.deltaTime;flashes[i].localScale=Vector3.one*Mathf.Max(0,flashLife[i])*.7f;if(flashLife[i]<=0)flashes[i].gameObject.SetActive(false);}
            if(announceTime>0){announceTime-=Time.deltaTime;alley.Display.transform.localScale=Vector3.one*(1+Mathf.Sin((1.5f-announceTime)*8)*.045f);}
            else {alley.Display.transform.localScale=Vector3.one;if(!moving && session.Phase==BowlingMatchPhase.Ready)alley.Display.text="YOUR TURN";}
        }
        private void OnDestroy()
        {
            if(bowling!=null)bowling.ThrowLaunched-=OnThrow;if(session!=null)session.RollResolved-=OnRoll;
            if(rolling!=null)rolling.Stop();if(cues!=null)cues.Stop();foreach(var source in impacts)if(source!=null)source.Stop();
            sounds?.Dispose();alley?.Dispose();if(trailMaterial!=null)Destroy(trailMaterial);
        }
    }
}
