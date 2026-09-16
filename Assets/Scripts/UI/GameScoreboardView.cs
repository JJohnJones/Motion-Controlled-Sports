using System.Collections.Generic;
using MotionControllers.Core;
using UnityEngine.UIElements;
namespace MotionControllers.UI
{
    // Reusable fixed-height game HUD: compact player tabs plus one detailed score strip.
    public sealed class GameScoreboardView : VisualElement
    {
        private readonly Label turn = new Label(), instruction = new Label();
        private readonly VisualElement players = new VisualElement(), frames = new VisualElement();
        private GameScoreboard data;
        private int selected, lastActive = -1;
        public GameScoreboardView()
        {
            AddToClassList("scoreboard"); turn.AddToClassList("turn-banner"); instruction.AddToClassList("turn-instruction");
            players.AddToClassList("score-players"); frames.AddToClassList("score-frames");
            var header = new VisualElement(); header.AddToClassList("score-heading");
            header.Add(turn); header.Add(instruction); Add(header); Add(players); Add(frames);
        }
        public void Refresh(GameScoreboard value)
        {
            if (value == null || ReferenceEquals(data, value)) return;
            data = value;
            if (lastActive != value.ActivePlayer) selected = value.ActivePlayer;
            lastActive = value.ActivePlayer; turn.text = value.Turn; instruction.text = value.Instruction;
            RenderRows();
        }
        private void RenderRows()
        {
            players.Clear(); frames.Clear();
            for (int i = 0; i < data.Players.Length; i++) {
                int index = i; var player = data.Players[i];
                var button = new Button(() => { selected = index; RenderRows(); }) { text = player.Name + "  ·  " + player.Total };
                button.AddToClassList("score-player"); button.EnableInClassList("active-player", i == data.ActivePlayer);
                button.EnableInClassList("selected-player", i == selected); players.Add(button);
            }
            foreach (var cell in data.Players[selected].Frames) {
                var frame = new VisualElement(); frame.AddToClassList("score-frame"); frame.style.backgroundImage = new StyleBackground(AeroSurfaces.Gloss); frame.EnableInClassList("current-frame", cell.Current);
                var number = new Label(cell.Heading); number.AddToClassList("frame-number"); frame.Add(number);
                var rolls = new Label(cell.Rolls); rolls.AddToClassList("frame-rolls"); frame.Add(rolls);
                var score = new Label(cell.Score); score.AddToClassList("frame-score"); frame.Add(score); frames.Add(frame);
            }
            AeroSurfaces.Apply(players);
        }
    }
}
