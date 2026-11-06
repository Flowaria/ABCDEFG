using AssetsTools.NET;

namespace ABCDEFG;

internal class ReroutingFileOptions
{
    public AssetBundleCompressionType Compression { get; set; }
    public string OutputFormat { get; set; }
    public bool GenerateMetadata { get; set; }

    public bool NoCompression => Compression == AssetBundleCompressionType.None;

    public string GetCompressedFilePath(string filePath)
    {
        var baseDir = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        var formatedName = OutputFormat.Replace("{FileName}", fileName, StringComparison.OrdinalIgnoreCase);
        return Path.Combine(baseDir, formatedName);
    }
}
