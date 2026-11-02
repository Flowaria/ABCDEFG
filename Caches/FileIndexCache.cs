namespace ABCDEFG.Caches;

internal class FileIndexCache(string cacheFile) : Cache(cacheFile)
{
    public override bool IsEmpty => _FileNames.Count == 0;

    private readonly Dictionary<int, string> _FileNames = [];

    public bool TryGetFileName(int fileIndex, out string fileName)
    {
        return _FileNames.TryGetValue(fileIndex, out fileName);
    }

    public void Write(int fileIndex, string fileName)
    {
        Writer.Write7BitEncodedInt(fileIndex);
        Writer.Write(fileName);
    }

    public override void ReadAll()
    {
        ClearCache();
        FileStream.Seek(0, SeekOrigin.Begin);
        while (FileStream.Position < FileStream.Length)
        {
            var fileIndex = Reader.Read7BitEncodedInt();
            var fileName = Reader.ReadString();
            _FileNames[fileIndex] = fileName;
        }
    }

    public override void ClearCache()
    {
        _FileNames.Clear();
    }
}
