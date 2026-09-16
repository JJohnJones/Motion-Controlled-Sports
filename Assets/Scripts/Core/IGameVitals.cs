namespace MotionControllers.Core
{
    public interface IGameVitals
    {
        string VitalsHeading { get; }
        string[] VitalsNames { get; }
        float[] VitalsValues { get; }
        float VitalsMaximum { get; }
    }
}
