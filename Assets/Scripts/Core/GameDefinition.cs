using UnityEngine;
namespace MotionControllers.Core
{
    [CreateAssetMenu(menuName = "Motion Sports/Game Definition")]
    public sealed class GameDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        [TextArea] public string instructions = "Follow the instructions on your phone controller.";
        [Tooltip("Full scene asset path, included in Build Settings.")]
        public string scenePath;
        public Sprite icon;
        public Color accent = new Color(0.28f, 0.89f, 0.72f);
        [Range(1, 4)] public int minimumPlayers = 1;
        [Range(1, 4)] public int maximumPlayers = 1;
        public bool available;
        public string controllerUiMode = "menu";
        public bool CanPlay(int connectedPlayers) => available && !string.IsNullOrWhiteSpace(scenePath) && connectedPlayers >= minimumPlayers;
        private void OnValidate() { maximumPlayers = Mathf.Max(minimumPlayers, maximumPlayers); }
    }
}
