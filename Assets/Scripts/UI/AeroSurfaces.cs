using UnityEngine;
using UnityEngine.UIElements;
namespace MotionControllers.UI
{
    // Small, shared procedural surfaces; no external art or font dependency.
    public static class AeroSurfaces
    {
        private static Texture2D gloss, sky;
        public static Texture2D Gloss => gloss != null ? gloss : gloss = Make(false);
        public static Texture2D Sky => sky != null ? sky : sky = Make(true);
        public static void Apply(VisualElement root)
        {
            root.Query<Button>().ForEach(b => b.style.backgroundImage = new StyleBackground(Gloss));
            root.Query(className: "panel").ForEach(p => p.style.backgroundImage = new StyleBackground(Gloss));
            root.Query(className: "player-row").ForEach(p => p.style.backgroundImage = new StyleBackground(Gloss));
        }
        private static Texture2D Make(bool background)
        {
            const int size = 128;
            var texture = new Texture2D(4, size, TextureFormat.RGBA32, false) { name = background ? "Aero sky" : "Aero gloss", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                Color color = background ? Color.Lerp(new Color(.32f,.82f,.69f), new Color(.025f,.28f,.68f), t) :
                    new Color(1, 1, 1, t > .52f ? Mathf.Lerp(.08f,.52f,(t-.52f)/.48f) : .03f + .12f*(1-t));
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
            }
            texture.Apply(false, true); return texture;
        }
    }
}
