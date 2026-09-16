using UnityEngine;
namespace MotionControllers.Bowling
{
    public sealed class BowlingPinFeedback : MonoBehaviour
    {
        public BowlingPresentation Owner {get;set;}
        private double lastHit=-10;
        private void OnCollisionEnter(Collision collision)
        {
            if(Owner==null || collision.relativeVelocity.magnitude<.65f || Time.timeAsDouble-lastHit<.12)return;
            lastHit=Time.timeAsDouble;
            Owner.Impact(collision.contactCount>0?collision.GetContact(0).point:transform.position,collision.relativeVelocity.magnitude);
        }
    }
}
