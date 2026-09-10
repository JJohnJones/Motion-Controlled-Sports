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
        private float spin, hookAcceleration;
        private void Awake() { Body = GetComponent<Rigidbody>(); }
        public void ResetBall()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = true;
            spin = hookAcceleration = 0;
            Body.position = releasePoint.position;
            Body.rotation = releasePoint.rotation;
            transform.SetPositionAndRotation(Body.position, Body.rotation);
        }
        public void Launch(Vector3 velocity, float wristSpin = 0, float hook = 0)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            LastLaunchVelocity = velocity;
            spin = Mathf.Clamp(wristSpin, -1, 1); hookAcceleration = Mathf.Max(0, hook);
            Body.isKinematic = false;
            Body.maxAngularVelocity = 100;
            Body.linearVelocity = velocity;
            // Start close to rolling contact; collisions and friction remain normal Rigidbody physics.
            Body.angularVelocity = Vector3.Cross(Vector3.up, velocity) / Mathf.Max(0.01f, radius) + Vector3.up * spin * 12;
            Body.WakeUp();
        }
        private void FixedUpdate() { ApplyHook(); }
        public void ApplyHook()
        {
            if (Body == null || Body.isKinematic || hookAcceleration <= 0 || Mathf.Abs(spin) < 0.001f) return;
            Vector3 p = Body.position, velocity = Body.linearVelocity;
            // Simple arcade hook while on the lane; never steer an airborne or gutter ball.
            if (Mathf.Abs(p.x) > 1.18f || p.y < 0.10f || p.y > 0.24f || p.z < 1 || p.z > 19 || velocity.z <= 0.5f) return;
            float speedFactor = Mathf.Clamp01(velocity.magnitude / 6f);
            Body.AddForce(Vector3.right * (spin * hookAcceleration * speedFactor), ForceMode.Acceleration);
        }
    }
}
