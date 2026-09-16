using UnityEngine;
namespace MotionControllers.Bowling
{
    // Original synthesized placeholders: replace these clips with recorded Foley later.
    public sealed class BowlingSoundBank
    {
        public readonly AudioClip Impact, Roll, Release, Strike, Spare;
        public BowlingSoundBank()
        { Impact=Make("Pin clack",.22f,0);Roll=Make("Rolling surface",1,1);Release=Make("Ball release",.16f,2);Strike=Make("Strike flourish",.8f,3);Spare=Make("Spare flourish",.55f,4); }
        private static AudioClip Make(string name,float duration,int kind)
        {
            const int rate=22050;int count=Mathf.CeilToInt(rate*duration);var samples=new float[count];var random=new System.Random(41+kind);float low=0;
            for(int i=0;i<count;i++) {
                float t=i/(float)rate;float noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.11f);
                float sample;
                if(kind==1) sample=low*.34f;
                else if(kind==0) sample=(noise*.32f+Mathf.Sin(t*2*Mathf.PI*880)*.38f+Mathf.Sin(t*2*Mathf.PI*1320)*.2f)*Mathf.Exp(-t*24);
                else if(kind==2) sample=(low*.8f+Mathf.Sin(t*2*Mathf.PI*95)*.4f)*Mathf.Exp(-t*32);
                else { int note=Mathf.Min(3,(int)(t/.13f));float frequency=note==0?523.25f:note==1?659.25f:note==2?783.99f:1046.5f;
                    sample=Mathf.Sin(t*2*Mathf.PI*frequency)*.23f*Mathf.Exp(-(t%.13f)*13)*Mathf.Min(1,(duration-t)*10); }
                samples[i]=sample*Mathf.Min(1,t*700)*Mathf.Min(1,(duration-t)*700);
            }
            var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void Dispose(){foreach(var clip in new[]{Impact,Roll,Release,Strike,Spare})Object.Destroy(clip);}
    }
}
