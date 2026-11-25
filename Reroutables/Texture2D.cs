using ABCDEFG.Utils;
using AssetsTools.NET.Texture;
using SkiaSharp;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace ABCDEFG.Reroutables;

internal class Texture2D : ReroutableAsset
{
    public const int HashPixelWidth = 16;
    public const int HashPixelHeight = 16;
    public const int HashBitCountPerChannel = HashPixelWidth * HashPixelHeight;
    public const int HashByteCountPerChannel = HashBitCountPerChannel / 8;

    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] DHash { get; set; }
    public ulong TextureExactHash { get; set; } // not used for matching
    public TextureFormat Format { get; set; }

    public override bool IsSameAsset(ReroutableAsset other)
    {
        if (!other.Name.Equals(Name, StringComparison.Ordinal))
            return false;

        if (other is not Texture2D otherTex)
            return false;

        if (otherTex.Width != Width)
            return false;

        if (otherTex.Height != Height)
            return false;

        var sim = Similarity(otherTex.DHash, DHash);
        if (sim < 0.98)
        {
            Log.Error($"{Convert.ToBase64String(otherTex.DHash)} {Convert.ToBase64String(DHash)}");
            Log.Error(sim.ToString());
            return false;
        }


        return true;
    }

    public override void ReadExtra(AssetContext context)
    {
        var baseField = context.baseField;
        var texture = TextureFile.ReadTextureFile(baseField);

        Width = texture.m_Width;
        Height = texture.m_Height;

        if (Width <= 0 || Height <= 0)
        {
            DHash = new byte[64];
            Log.Warning($"[{nameof(Texture2D)}] '{Name}' doesn't have proper dimension! ({Width} x {Height})");
            return;
        }

        Format = (TextureFormat)texture.m_TextureFormat;

        var pictureData = texture.FillPictureData(context.assetFileInstance);
        var textureBgraRaw = texture.DecodeTextureRaw(pictureData);
        var imageInfo = new SKImageInfo(texture.m_Width, texture.m_Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var image = new SKBitmap(imageInfo);
        Marshal.Copy(textureBgraRaw, 0, image.GetPixels(), textureBgraRaw.Length);

        //var imageResizeInfo = new SKImageInfo(HashPixelWidth, HashPixelHeight, SKColorType.Bgra8888);
        //var resizeSampler = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        //using var imageResized = image.Resize(imageResizeInfo, resizeSampler);

        TextureExactHash = XxHash64.HashToUInt64(textureBgraRaw);

        DHash = ImageHash.DHashXY(image);
    }

    private static byte[] Hash(SKBitmap imageResized, SKImageInfo imageInfo, bool textureWoAlpha)
    {
        ReadOnlySpan<byte> pixels = imageResized.GetPixelSpan();

        int[] sum = new int[4];
        byte[] min = new byte[4];
        byte[] max = new byte[4];

        Array.Fill<byte>(min, 0);
        Array.Fill<byte>(max, 255);

        int pixelCount = HashBitCountPerChannel;
        for (int i = 0; i < pixelCount; i++)
        {
            var baseIndex = i * 4;
            for (int ch = 0; ch < 4; ch++)
            {
                var pixelByte = pixels[baseIndex + ch];

                if (pixelByte >= max[ch])
                    max[ch] = pixelByte;

                if (pixelByte <= min[ch])
                    min[ch] = pixelByte;

                sum[ch] += pixels[baseIndex + ch];
            }
        }

        for (int ch = 0; ch < 4; ch++)
        {
            if (max[ch] - min[ch] < 4)
            {
                if (max[ch] > 128)
                {
                    sum[ch] = int.MaxValue;
                }
                else
                {
                    sum[ch] = int.MinValue;
                }
            }
        }

        Span<byte> hash = stackalloc byte[HashByteCountPerChannel * 4];
        for (int i = 0; i < pixelCount; i++)
        {
            var baseIndex = i * 4;
            for (int ch = 0; ch < 4; ch++)
            {
                if (pixels[baseIndex] * pixelCount >= sum[ch])
                {
                    var byteIndex = Math.DivRem(i, 8, out var rem);
                    byteIndex += HashByteCountPerChannel * ch;
                    hash[byteIndex] |= (byte)(1 << rem);
                }
            }
        }
        return hash.ToArray();
    }

    private static double Similarity(byte[] hash1, byte[] hash2)
    {
        var diffCount = 0;
        var total = hash1.Length * 8;
        for (var i = 0; i < hash1.Length; i++)
        {
            diffCount += byte.PopCount((byte)(hash1[i] ^ hash2[i]));
        }
        return Math.Clamp((total - diffCount) / (double)total, 0.0, 1.0);
    }

    public override void ReadExtra(BinaryReader reader)
    {
        Width = reader.Read7BitEncodedInt();
        Height = reader.Read7BitEncodedInt();

        DHash = reader.ReadBytes(64);
    }

    public override void WriteExtra(BinaryWriter writer)
    {
        writer.Write7BitEncodedInt(Width);
        writer.Write7BitEncodedInt(Height);
        writer.Write(DHash);
    }

    public override string ToString()
    {
        return $"{Name} ({Format} {Width} x {Height} / dHash: {Convert.ToBase64String(DHash)} exact: {TextureExactHash})";
    }
}
