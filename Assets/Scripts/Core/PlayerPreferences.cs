using UnityEngine;
namespace MotionControllers.Core
{
    public static class PlayerPreferences
    {
        private const string Prefix = "PhoneSports.";
        public static float MusicVolume { get => PlayerPrefs.GetFloat(Prefix + "Music", .45f); set { PlayerPrefs.SetFloat(Prefix + "Music", Mathf.Clamp01(value)); PlayerPrefs.Save(); } }
        public static float EffectsVolume { get => PlayerPrefs.GetFloat(Prefix + "Effects", .8f); set { PlayerPrefs.SetFloat(Prefix + "Effects", Mathf.Clamp01(value)); PlayerPrefs.Save(); } }
        public static bool Fullscreen { get => PlayerPrefs.GetInt(Prefix + "Fullscreen", 1) != 0; set { PlayerPrefs.SetInt(Prefix + "Fullscreen", value ? 1 : 0); PlayerPrefs.Save(); ApplyDisplay(); } }
        public static float Sensitivity(int player) => Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + player + ".Sensitivity", 1), .5f, 2);
        public static bool LeftHanded(int player) => PlayerPrefs.GetInt(Prefix + player + ".LeftHanded", 0) != 0;
        public static void SetPlayer(int player, float sensitivity, bool leftHanded)
        {
            PlayerPrefs.SetFloat(Prefix + player + ".Sensitivity", Mathf.Clamp(sensitivity, .5f, 2));
            PlayerPrefs.SetInt(Prefix + player + ".LeftHanded", leftHanded ? 1 : 0); PlayerPrefs.Save();
            var manager = PersistentControllerRoot.Instance?.Manager;
            if (manager != null) foreach (var session in manager.Sessions.Values) Apply(session, manager.GetPlayerNumber(session.Id));
        }
        public static void Apply(ControllerSession session, int player)
        { session.MotionSensitivity = Sensitivity(player); session.LeftHanded = LeftHanded(player); }
        public static void ApplyDisplay()
        {
            if (Application.isEditor) return;
            Screen.SetResolution(Fullscreen ? Display.main.systemWidth : 1280, Fullscreen ? Display.main.systemHeight : 720,
                Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
