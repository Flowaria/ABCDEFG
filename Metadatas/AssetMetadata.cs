using System.Text.Json.Serialization;

namespace ABCDEFG.Metadatas;

internal class AssetMetadata
{
    public string AssetType { get; set; }
    public string[] Tags { get; set; } = [];

    [JsonIgnore]
    public bool Orphaned = true;
}
