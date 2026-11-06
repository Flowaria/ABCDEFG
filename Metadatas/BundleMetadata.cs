namespace ABCDEFG.Metadatas;

internal class BundleMetadata
{
    public string Profile { get; set; } = "Unspecified";
    public string InitialMode { get; set; } = "DontLoad";
    public bool DisableJustInTimeLoading { get; set; } = false;
    public Dictionary<string, AssetMetadata> Assets { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
    public string Checksum { get; set; } = "";
    public int FormatRevision { get; set; } = 1;
}
