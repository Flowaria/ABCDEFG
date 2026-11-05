using System.Buffers;

namespace ABCDEFG.Utils;

internal static class LargeStreamUtil
{
    private const int BufferSize = 1024 * 1024 * 4;

    public static void CopyBlock(Stream stream, Stream target, long position, long count)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            stream.Position = position;
            while (count > 0)
            {
                var toWrite = (int)Math.Min(count, BufferSize);
                stream.ReadExactly(buffer, 0, toWrite);
                target.Write(buffer, 0, toWrite);
                count -= toWrite;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static readonly string[] s_SizeSuffix = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
    public static string BufferLengthToString(long byteCount)
    {
        if (byteCount == 0)
            return "0" + s_SizeSuffix[0];

        long bytes = Math.Abs(byteCount);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return (Math.Sign(byteCount) * num).ToString() + s_SizeSuffix[place];
    }
}
