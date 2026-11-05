using AssetsTools.NET;

namespace ABCDEFG;

internal class ReroutingFileOptions
{
    public AssetBundleCompressionType Compression { get; set; }
    public string OutputSuffix { get; set; }
    public string OutputSubdir { get; set; }

    public bool NoCompression => Compression == AssetBundleCompressionType.None;

    public string GetReroutedFilePath(string filePath)
    {
        var baseDir = GetOutputBaseDir(filePath);
        var fileName = Path.GetFileName(filePath);
        return Path.Combine(baseDir, fileName + "-rerouted.tmp");
    }

    public string GetCompressedFilePath(string filePath)
    {
        var baseDir = GetOutputBaseDir(filePath);
        var fileName = Path.GetFileName(filePath);
        return Path.Combine(baseDir, fileName + OutputSuffix);
    }

    public string GetOutputBaseDir(string filePath)
    {
        var baseDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(OutputSubdir))
        {
            baseDir = Path.Combine(baseDir, OutputSubdir);
            Directory.CreateDirectory(baseDir);
        }

        return baseDir;
    }
}
