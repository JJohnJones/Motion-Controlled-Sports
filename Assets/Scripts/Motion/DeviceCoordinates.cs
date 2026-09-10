using UnityEngine;

namespace MotionControllers
{
    public static class DeviceCoordinates
    {
        // Browser RH device-to-reference q, intrinsic Z(alpha) X(beta) Y(gamma).
        // Physical screen angle is counter-clockwise; the upright UI basis compensates -angle.
        // Reflect Z on BOTH bases to Unity: R_u = S R_b S, S=diag(1,1,-1).
        public static Quaternion Orientation(Quaternion browser, float screenAngle)
        {
            var screen = browser * Quaternion.AngleAxis(-screenAngle, Vector3.forward);
            return new Quaternion(-screen.x, -screen.y, screen.z, screen.w).normalized;
        }

        public static Vector3 Acceleration(Vector3 device, float screenAngle)
        {
            var screen = Quaternion.AngleAxis(screenAngle, Vector3.forward) * device;
            return new Vector3(screen.x, screen.y, -screen.z);
        }

        public static Vector3 AngularVelocity(Vector3 degreesPerSecond, float screenAngle)
        {
            var screen = Quaternion.AngleAxis(screenAngle, Vector3.forward) * degreesPerSecond;
            // Angular velocity is axial: det(S)*S, unlike acceleration.
            return new Vector3(-screen.x, -screen.y, screen.z) * Mathf.Deg2Rad;
        }
    }
}
