using System.Buffers;

namespace ABCDEFG;

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
}
