using System.Collections.Generic;
using UnityEngine;

namespace MotionControllers.UI
{
    // Replace with GameDefinition.icon whenever final artwork is available.
    public static class SportEmblems
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        public static Texture2D For(string mode)
        {
            mode = mode ?? "";
            if (cache.TryGetValue(mode, out var existing)) return existing;
            const int size = 192;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Sport emblem " + mode, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 v = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1);
                float d;
                switch (mode)
                {
                    case "bowling":
                        d = (v - new Vector2(0, -.05f)).magnitude - .66f;
                        d = Mathf.Max(d, .095f - (v - new Vector2(-.18f, .20f)).magnitude);
                        d = Mathf.Max(d, .095f - (v - new Vector2(.13f, .24f)).magnitude);
                        d = Mathf.Max(d, .11f - (v - new Vector2(.02f, -.04f)).magnitude); break;
                    case "tennis":
                        d = Mathf.Abs(new Vector2(v.x / .48f, (v.y - .25f) / .56f).magnitude - 1) * .48f - .045f;
                        if (new Vector2(v.x / .45f, (v.y - .25f) / .53f).sqrMagnitude < 1)
                            d = Mathf.Min(d, Mathf.Min(Mathf.Abs(Mathf.Repeat(v.x + .06f, .16f) - .08f), Mathf.Abs(Mathf.Repeat(v.y, .16f) - .08f)) - .012f);
                        d = Mathf.Min(d, Segment(v, new Vector2(0, -.29f), new Vector2(0, -.78f)) - .075f); break;
                    case "sword":
                        d = Mathf.Min(Sword(v), Sword(new Vector2(-v.x, v.y))); break;
                    case "golf":
                        d = Segment(v, new Vector2(-.22f, -.6f), new Vector2(-.22f, .7f)) - .035f;
                        if (v.x > -.22f && v.x < .5f && v.y < .68f && v.y > .68f - (.5f - v.x) * .6f) d = -.03f;
                        d = Mathf.Min(d, (v - new Vector2(.25f, -.57f)).magnitude - .15f); break;
                    default:
                        d = Mathf.Min(Segment(v, new Vector2(-.4f, 0), new Vector2(.4f, 0)), Segment(v, new Vector2(0, -.4f), new Vector2(0, .4f))) - .075f; break;
                }
                float alpha = Mathf.Clamp01(.5f - d * size * .5f);
                pixels[y * size + x] = Color.Lerp(new Color(.68f, .9f, 1, alpha), new Color(1, 1, 1, alpha), Mathf.Clamp01(v.y * .5f + .5f));
            }
            texture.SetPixels(pixels); texture.Apply(false, true); cache[mode] = texture; return texture;
        }
        private static float Sword(Vector2 p)
        {
            float blade = Segment(p, new Vector2(-.19f, -.19f), new Vector2(.58f, .65f)) - .055f;
            float guard = Segment(p, new Vector2(-.46f, -.08f), new Vector2(-.03f, -.47f)) - .055f;
            float grip = Segment(p, new Vector2(-.25f, -.28f), new Vector2(-.56f, -.63f)) - .075f;
            return Mathf.Min(blade, Mathf.Min(guard, grip));
        }
        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        { Vector2 ab = b - a; return (p - a - ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude)).magnitude; }
    }
}
