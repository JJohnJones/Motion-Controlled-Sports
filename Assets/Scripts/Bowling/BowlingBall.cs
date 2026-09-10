using UnityEngine;

namespace MotionControllers.Bowling
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class BowlingBall : MonoBehaviour
    {
        public Transform releasePoint;
        public float radius = 0.16f;
        public Vector3 LastLaunchVelocity { get; private set; }
        public Rigidbody Body { get; private set; }
        private void Awake() { Body = GetComponent<Rigidbody>(); }
        public void ResetBall()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = true;
            Body.position = releasePoint.position;
            Body.rotation = releasePoint.rotation;
            transform.SetPositionAndRotation(Body.position, Body.rotation);
        }
        public void Launch(Vector3 velocity)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            LastLaunchVelocity = velocity;
            Body.isKinematic = false;
            Body.maxAngularVelocity = 100;
            Body.linearVelocity = velocity;
            // Start close to rolling contact; collisions and friction remain normal Rigidbody physics.
            Body.angularVelocity = Vector3.Cross(Vector3.up, velocity) / Mathf.Max(0.01f, radius);
            Body.WakeUp();
        }
    }
}
