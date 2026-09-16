using UnityEngine;
namespace MotionControllers.Tennis
{
    // One shared third-person view follows the near-side player, never the remote opponent.
    [RequireComponent(typeof(Camera))]
    public sealed class TennisFollowCamera : MonoBehaviour
    {
        public TennisGameSession game;
        public Vector3 offset = new Vector3(0, 3.8f, -6.5f);
        [Min(1)] public float response = 7;
        private bool positioned;
        private void LateUpdate()
        {
            if (game == null || game.Players == null || game.Players.Length == 0) return;
            var player = game.Players[0].Avatar.position;
            var desired = player + offset;
            // A little ball tracking keeps lobs readable without swinging the view around.
            var focus = player + new Vector3(0, 1.5f, 8);
            focus.y = Mathf.Max(focus.y, Mathf.Min(5, game.BallPosition.y * .45f));
            float blend = positioned ? 1 - Mathf.Exp(-response * Time.deltaTime) : 1;
            transform.position = Vector3.Lerp(transform.position, desired, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(focus - transform.position), blend);
            positioned = true;
        }
    }
}
