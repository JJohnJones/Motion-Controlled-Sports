using UnityEngine;

namespace MotionControllers.Core
{
    // Owned by the application shell, so menu navigation never restarts the track.
    public sealed class MenuMusicPlayer : MonoBehaviour
    {
        private SceneFlowManager flow;
        private AudioSource source;
        private AudioListener listener;

        public void Initialize(SceneFlowManager owner, AudioClip clip, float volume)
        {
            flow = owner;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.volume = volume;
            source.clip = clip;
            listener = gameObject.AddComponent<AudioListener>();
            Synchronize();
        }

        private void LateUpdate() { Synchronize(); }
        private void Synchronize()
        {
            if (flow == null || source == null) return;
            // Game scenes own their own listener; camera.enabled alone does not disable one.
            bool menus = flow.Screen != AppScreen.Playing;
            listener.enabled = menus && (flow.menuCamera == null || flow.menuCamera.enabled);
            source.volume = flow.menuMusicVolume;
            if (menus && source.clip != null && !source.isPlaying) source.Play();
            else if (!menus && source.isPlaying) source.Stop();
        }
        private void OnDisable()
        {
            if (source != null) source.Stop();
            if (listener != null) listener.enabled = false;
        }
    }
}
