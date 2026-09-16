using System;
using System.Collections.Generic;
using MotionControllers.Core;
using UnityEngine.UIElements;
namespace MotionControllers.UI
{
    public sealed class SettingsPanel : VisualElement
    {
        public SettingsPanel(SceneFlowManager flow, Action close)
        {
            AddToClassList("panel"); AddToClassList("modal-panel"); AddToClassList("settings-panel");
            var heading = new Label("Settings"); heading.AddToClassList("heading"); Add(heading);
            var music = new Slider("Menu music", 0, 1) { value = PlayerPreferences.MusicVolume };
            Add(music); music.RegisterValueChangedCallback(e => { PlayerPreferences.MusicVolume = e.newValue; flow.menuMusicVolume = e.newValue; });
            var effects = new Slider("Sound effects", 0, 1) { value = PlayerPreferences.EffectsVolume };
            Add(effects); effects.RegisterValueChangedCallback(e => PlayerPreferences.EffectsVolume = e.newValue);
            var display = new DropdownField("Display", new List<string> { "Fullscreen", "Windowed" }, PlayerPreferences.Fullscreen ? 0 : 1);
            Add(display); display.RegisterValueChangedCallback(e => PlayerPreferences.Fullscreen = e.newValue == "Fullscreen");
            var note = new Label("Display mode applies to desktop builds. Changes save automatically."); note.AddToClassList("settings-note"); Add(note);
            var player = new DropdownField("Local player", new List<string> { "Player 1", "Player 2", "Player 3", "Player 4" }, 0); Add(player);
            var sensitivity = new Slider("Swing sensitivity", .5f, 2); Add(sensitivity);
            var handedness = new DropdownField("Playing hand", new List<string> { "Right", "Left" }, 0); Add(handedness);
            var value = new Label(); value.AddToClassList("settings-note"); Add(value);
            Action refresh = () => {
                int number = player.index + 1;
                sensitivity.SetValueWithoutNotify(PlayerPreferences.Sensitivity(number));
                handedness.SetValueWithoutNotify(PlayerPreferences.LeftHanded(number) ? "Left" : "Right");
                value.text = $"{sensitivity.value:F2}× swing strength · 1.00× is the default";
            };
            player.RegisterValueChangedCallback(_ => refresh());
            sensitivity.RegisterValueChangedCallback(e => {
                PlayerPreferences.SetPlayer(player.index + 1, e.newValue, handedness.value == "Left"); refresh();
            });
            handedness.RegisterValueChangedCallback(e => {
                PlayerPreferences.SetPlayer(player.index + 1, sensitivity.value, e.newValue == "Left");
            });
            var hint = new Label("Handedness sets the racket/sword hand. Physical swing directions stay natural. Preferences follow player numbers on this PC."); hint.AddToClassList("settings-note"); Add(hint);
            var done = new Button(close) { text = "Done" }; done.AddToClassList("primary"); Add(done); refresh();
        }
    }
}
