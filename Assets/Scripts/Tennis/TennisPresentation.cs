using UnityEngine;
namespace MotionControllers.Tennis
{
    public enum TennisFeedbackKind { Ready,Toss,Racket,Bounce,Net,Point,Fault }
    public struct TennisFeedback
    {
        public TennisFeedbackKind Kind;
        public Vector3 Position;
        public float Strength;
        public int Player;
        public string Message;
    }
    public sealed class TennisPresentation : MonoBehaviour
    {
        [Range(0,1)] public float effectsVolume=.65f;
        public bool soundEnabled=true, contactRings=true;
        private TennisGameSession game;
        private TennisVenue venue;
        private TennisSoundBank sounds;
        private readonly AudioSource[] voices=new AudioSource[6];
        private readonly LineRenderer[] rings=new LineRenderer[4];
        private readonly Vector3[] ringCenters=new Vector3[4];
        private readonly bool[] groundRings=new bool[4];
        private readonly float[] ringLife=new float[4];
        private Transform[] avatars;
        private Vector3[] previousPositions;
        private float[] hitReaction;
        private Transform shadow;
        private TrailRenderer trail;
        private Vector3 ballScale;
        private float ballPulse,clock;
        private bool paused;
        private int voice,ring;
        public void Initialize(TennisGameSession owner)
        {
            if(venue!=null)return;game=owner;venue=new TennisVenue(transform);sounds=new TennisSoundBank();
            avatars=new Transform[game.Players.Length];previousPositions=new Vector3[avatars.Length];hitReaction=new float[avatars.Length];
            for(int i=0;i<avatars.Length;i++){avatars[i]=venue.CreateAvatarDetails(game.Players[i]);previousPositions[i]=game.Players[i].Avatar.position;}
            for(int i=0;i<voices.Length;i++){
                var go=new GameObject("Tennis sound voice");go.transform.SetParent(transform,false);var a=voices[i]=go.AddComponent<AudioSource>();a.playOnAwake=false;a.spatialBlend=.25f;a.minDistance=12;a.maxDistance=60;a.rolloffMode=AudioRolloffMode.Linear;
            }
            for(int i=0;i<rings.Length;i++){
                var go=new GameObject("Contact ring");go.transform.SetParent(transform,false);var line=rings[i]=go.AddComponent<LineRenderer>();
                line.sharedMaterial=venue.ContactMaterial;line.useWorldSpace=true;line.loop=true;line.positionCount=32;line.enabled=false;line.startWidth=line.endWidth=.025f;
            }
            var disc=GameObject.CreatePrimitive(PrimitiveType.Cylinder);disc.name="Ball grounding shadow";disc.transform.SetParent(transform,false);
            var collider=disc.GetComponent<Collider>();collider.enabled=false;Destroy(collider);disc.GetComponent<Renderer>().sharedMaterial=venue.ShadowMaterial;shadow=disc.transform;
            ballScale=game.ballVisual.localScale;
            trail=game.ballVisual.GetComponent<TrailRenderer>() ?? game.ballVisual.gameObject.AddComponent<TrailRenderer>();
            trail.time=.12f;trail.startWidth=.065f;trail.endWidth=.008f;trail.minVertexDistance=.04f;trail.sharedMaterial=venue.ContactMaterial;trail.emitting=false;trail.Clear();
            game.Feedback+=OnFeedback;
        }
        private void OnFeedback(TennisFeedback e)
        {
            if(venue==null)return;
            venue.Score.text=$"{game.Match.PointLabel(0)}   :   {game.Match.PointLabel(1)}     |     GAMES  {game.Match.Games[0]} : {game.Match.Games[1]}";
            if(e.Kind==TennisFeedbackKind.Ready){trail.emitting=false;trail.Clear();ballPulse=0;game.ballVisual.localScale=ballScale;venue.Announcement.text=e.Message;for(int i=0;i<avatars.Length;i++)previousPositions[i]=game.Players[i].Avatar.position;return;}
            if(!string.IsNullOrEmpty(e.Message))venue.Announcement.text=e.Message;
            AudioClip clip=e.Kind==TennisFeedbackKind.Racket?sounds.Racket:e.Kind==TennisFeedbackKind.Bounce?sounds.Bounce:e.Kind==TennisFeedbackKind.Net?sounds.Net:e.Kind==TennisFeedbackKind.Point?sounds.Point:e.Kind==TennisFeedbackKind.Fault?sounds.Fault:sounds.Toss;
            if(soundEnabled && !paused){var source=voices[voice++%voices.Length];source.Stop();source.transform.position=e.Position;source.pitch=e.Kind==TennisFeedbackKind.Racket?Mathf.Lerp(.9f,1.15f,Mathf.Clamp01(e.Strength/25)):1;source.PlayOneShot(clip,effectsVolume*Core.PlayerPreferences.EffectsVolume*(e.Kind==TennisFeedbackKind.Racket ? .85f : .65f));}
            if(e.Kind==TennisFeedbackKind.Racket || e.Kind==TennisFeedbackKind.Bounce || e.Kind==TennisFeedbackKind.Net){
                ballPulse=.12f;
                if(contactRings){int i=ring++%rings.Length;ringCenters[i]=e.Position;groundRings[i]=e.Kind==TennisFeedbackKind.Bounce;ringLife[i]=.28f;}
            }
            if(e.Kind==TennisFeedbackKind.Racket && e.Player>=0 && e.Player<hitReaction.Length)hitReaction[e.Player]=.25f;
            if(e.Kind==TennisFeedbackKind.Point || e.Kind==TennisFeedbackKind.Fault){trail.emitting=false;trail.Clear();}
        }
        public void SetPaused(bool value){paused=value;foreach(var source in voices)if(source!=null){if(value)source.Pause();else source.UnPause();}}
        private void LateUpdate()
        {
            if(venue==null || paused)return;float dt=Time.deltaTime;clock+=dt;
            bool flying=game.Phase==TennisPhase.Toss || game.Phase==TennisPhase.Rally;
            trail.emitting=flying;if(!flying)trail.Clear();
            ballPulse=Mathf.Max(0,ballPulse-dt);game.ballVisual.localScale=ballScale*(1+ballPulse*.65f);
            var ball=game.ballVisual.position;shadow.gameObject.SetActive(Mathf.Abs(ball.x)<15 && Mathf.Abs(ball.z)<19);
            shadow.position=new Vector3(ball.x,.018f,ball.z);float size=Mathf.Lerp(.3f,.17f,Mathf.Clamp01(ball.y/6));shadow.localScale=new Vector3(size,.002f,size);
            var camera=Camera.main;
            for(int i=0;i<rings.Length;i++){
                ringLife[i]=Mathf.Max(0,ringLife[i]-dt);rings[i].enabled=ringLife[i]>0;if(ringLife[i]<=0)continue;
                float t=1-ringLife[i]/.28f;float radius=Mathf.Lerp(.12f,.42f,t);rings[i].startWidth=rings[i].endWidth=.028f*(1-t);
                Vector3 right=groundRings[i]?Vector3.right:camera!=null?camera.transform.right:Vector3.right;
                Vector3 up=groundRings[i]?Vector3.forward:camera!=null?camera.transform.up:Vector3.up;
                var center=ringCenters[i];if(groundRings[i])center.y=.025f;
                for(int j=0;j<32;j++){float angle=j*Mathf.PI*2/32;rings[i].SetPosition(j,center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius);}
            }
            for(int i=0;i<avatars.Length;i++){
                var player=game.Players[i];var delta=player.Avatar.position-previousPositions[i];previousPositions[i]=player.Avatar.position;
                float speed=dt>0?Mathf.Min(8,delta.magnitude/dt):0;
                hitReaction[i]=Mathf.Max(0,hitReaction[i]-dt);
                var lean=Quaternion.Euler(Mathf.Clamp(delta.z*150,-4,4),0,Mathf.Clamp(-delta.x*150,-4,4));
                avatars[i].localRotation=Quaternion.Slerp(avatars[i].localRotation,lean,1-Mathf.Exp(-12*dt));
                avatars[i].localPosition=Vector3.up*(Mathf.Abs(Mathf.Sin(clock*12))*speed*.002f+Mathf.Sin(hitReaction[i]*Mathf.PI/.25f)*.025f);
                if(player.AI)player.Racket.rotation=Quaternion.LookRotation(player.Forward)*Quaternion.Euler(-Mathf.Sin(hitReaction[i]*Mathf.PI/.25f)*24,0,0);
            }
        }
        private void OnDestroy()
        {if(game!=null)game.Feedback-=OnFeedback;foreach(var source in voices)if(source!=null)source.Stop();sounds?.Dispose();venue?.Dispose();}
    }
}
