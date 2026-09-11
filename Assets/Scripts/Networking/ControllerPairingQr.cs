using QRCoder;
using UnityEngine;

namespace MotionControllers
{
    public static class ControllerPairingQr
    {
        public static Texture2D Create(string url)
        {
            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M, forceUtf8: true))
            {
                int size = data.ModuleMatrix.Count;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        pixels[(size - y - 1) * size + x] = data.ModuleMatrix[y][x] ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels32(pixels); texture.Apply(); return texture;
            }
        }
    }
}
