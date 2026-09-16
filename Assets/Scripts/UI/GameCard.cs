using System;
using MotionControllers.Core;
using UnityEngine.UIElements;
namespace MotionControllers.UI
{
    public sealed class GameCard : VisualElement
    {
        private readonly GameDefinition definition;
        private readonly Button play;
        public bool Selected { get; private set; }
        public void SetSelected(bool selected) { Selected = selected; EnableInClassList("selected", selected); }

        public GameCard(GameDefinition game, int index, VisualTreeAsset template, Action<GameDefinition> selected, Action<GameCard> highlighted = null)
        {
            definition = game; focusable = true; tabIndex = 0; AddToClassList("card-host"); template.CloneTree(this);
            this.Q<Label>("title").text = game.displayName;
            this.Q<Label>("description").text = game.description;
            this.Q<Label>("number").text = (index + 1).ToString("00");
            this.Q<Label>("number").style.color = game.accent;
            var icon = this.Q<Image>("icon"); icon.sprite = game.icon;
            if (game.icon == null) icon.image = SportEmblems.For(game.controllerUiMode);
            this.Q<Label>("number").style.display = DisplayStyle.None;
            icon.style.display = DisplayStyle.Flex;
            this.Q<Label>("players").text = game.minimumPlayers == game.maximumPlayers ?
                game.minimumPlayers + (game.minimumPlayers == 1 ? " PLAYER" : " PLAYERS") : $"{game.minimumPlayers}–{game.maximumPlayers} PLAYERS";
            this.Q<Label>("availability").text = game.available ? "PLAY NOW" : "COMING SOON";
            EnableInClassList("unavailable", !game.available);
            this.Q("art").style.backgroundImage = new StyleBackground(AeroSurfaces.Gloss);
            this.Q("art").style.backgroundColor = UnityEngine.Color.Lerp(game.accent, new UnityEngine.Color(.02f, .18f, .32f), .52f);
            RegisterCallback<PointerDownEvent>(_ => { highlighted?.Invoke(this); Focus(); });
            RegisterCallback<FocusInEvent>(_ => highlighted?.Invoke(this));
            play = this.Q<Button>("play"); play.clicked += () => selected(definition);
        }
        public void Refresh(int count)
        {
            play.text = !definition.available ? "Coming soon" : count < definition.minimumPlayers ? "Connect controller" : "Play";
            play.SetEnabled(definition.CanPlay(count));
        }
    }
}
