using UnityEngine;

namespace MotionControllers
{
    // Bootstrap ownership only. Game objects, cameras and scene UI must remain outside this root.
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ControllerManager))]
    public sealed class PersistentControllerRoot : MonoBehaviour
    {
        public static PersistentControllerRoot Instance { get; private set; }
        public ControllerManager Manager { get; private set; }
        public IControllerButtonSource Input => Manager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Disable synchronously: duplicate transports must never start a session/listener.
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            bool systemsOnly = true;
            foreach (var component in GetComponents<Component>())
                if (!(component is Transform || component is PersistentControllerRoot || component is ControllerManager ||
                     component is WebRtcLanControllerTransport)) systemsOnly = false;
            if (transform.parent != null || transform.childCount != 0 || !systemsOnly)
            {
                Debug.LogError("Persistent controller root must contain only controller systems. Run Tools > Motion Controllers > Make Selected Controller System Persistent before Play.", this);
                gameObject.SetActive(false);
                return;
            }
            Instance = this;
            Manager = GetComponent<ControllerManager>();
            DontDestroyOnLoad(gameObject);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }

    public static class ControllerInput
    {
        // Preserve explicit alternate sources (test/replay/gamepad adapters). Redirect scene-local
        // manager references to the surviving manager when a duplicate bootstrap is discarded.
        public static IMotionInputSource Resolve(MonoBehaviour source)
        {
            if ((source == null || source is ControllerManager) && PersistentControllerRoot.Instance != null)
                return PersistentControllerRoot.Instance.Input;
            return source as IMotionInputSource;
        }
    }
}
