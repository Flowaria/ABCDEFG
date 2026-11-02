using System.Text;

namespace ABCDEFG.Caches;

internal abstract class Cache
{
    public FileStream FileStream { get; protected set; }
    protected BinaryWriter Writer => field ?? new BinaryWriter(FileStream, Encoding.UTF8, leaveOpen: true);
    protected BinaryReader Reader => field ?? new BinaryReader(FileStream, Encoding.UTF8, leaveOpen: true);

    public bool IsFileEmpty => FileStream.Length == 0;
    public abstract bool IsEmpty { get; }

    public abstract void ReadAll();
    public abstract void ClearCache();

    public void ClearFile()
    {
        FileStream.Seek(0, SeekOrigin.Begin);
        FileStream.SetLength(0);
    }

    public Cache(string cacheFile)
    {
        FileStream = File.Open(cacheFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }
}
