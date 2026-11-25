using SkiaSharp;

namespace ABCDEFG.Utils;

internal static class ImageHash
{
    public static byte[] DHashXY(SKBitmap image)
    {
        const int Width = 9;
        const int Height = 9;

        var imageResizeInfo = new SKImageInfo(Width, Height, SKColorType.Bgra8888);
        var resizeSampler = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        using var imageResized = image.Resize(imageResizeInfo, resizeSampler);
        ReadOnlySpan<byte> pixels = imageResized.GetPixelSpan();

        Span<byte> hash = stackalloc byte[64];
        for (int y = 0; y < Height - 1; y++)
        {
            for (int x = 0; x < Width - 1; x++)
            {
                var pixelXY = (y * Width) + x;
                var rightPixelXY = (y * Width) + (x + 1);
                var downPixelXY = ((y + 1) * Width) + x;

                var bitIndex = (y * (Width - 1)) + x;
                var byteInChannel = Math.DivRem(bitIndex, 8, out var rem);
                var mask = (byte)(1 << rem);

                for (int c = 0; c < 4; c++)
                {
                    var data = pixels[(pixelXY * 4) + c];
                    var rightData = pixels[(rightPixelXY * 4) + c];
                    var downData = pixels[(downPixelXY * 4) + c];

                    if (data > rightData)
                        hash[(16 * c) + byteInChannel] |= mask;

                    if (data > downData)
                        hash[(16 * c) + 8 + byteInChannel] |= mask;
                }
            }
        }
        return hash.ToArray();
    }
}
