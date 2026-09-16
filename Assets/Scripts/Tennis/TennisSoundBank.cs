using UnityEngine;
namespace MotionControllers.Tennis
{
    // Original synthesis, isolated so recorded tennis Foley can replace it later.
    public sealed class TennisSoundBank
    {
        public readonly AudioClip Racket,Bounce,Net,Point,Fault,Toss;
        public TennisSoundBank(){Racket=Make("Racket pop",.18f,0);Bounce=Make("Court bounce",.14f,1);Net=Make("Net rustle",.25f,2);Point=Make("Point cue",.45f,3);Fault=Make("Fault cue",.22f,4);Toss=Make("Serve toss",.16f,5);}
        private static AudioClip Make(string name,float duration,int kind)
        {
            const int rate=22050;var data=new float[Mathf.CeilToInt(rate*duration)];var random=new System.Random(93+kind);float low=0;
            for(int i=0;i<data.Length;i++){
                float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.15f);
                float envelope=Mathf.Exp(-t*(kind==2?14:28));float sample;
                if(kind==0)sample=(noise*.3f+Mathf.Sin(t*2*Mathf.PI*480)*.48f)*envelope;
                else if(kind==1)sample=(Mathf.Sin(t*2*Mathf.PI*155)*.65f+low*.3f)*envelope;
                else if(kind==2)sample=low*.75f*envelope;
                else if(kind==5)sample=low*.4f*envelope;
                else {float frequency=kind==4?330: t<.14f?659:880;sample=Mathf.Sin(t*2*Mathf.PI*frequency)*.18f*Mathf.Exp(-(t%.14f)*12);}
                data[i]=sample*Mathf.Min(1,t*700)*Mathf.Min(1,(duration-t)*30);
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Dispose(){foreach(var clip in new[]{Racket,Bounce,Net,Point,Fault,Toss})Object.Destroy(clip);}
    }
}
