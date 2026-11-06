using ABCDEFG.Metadatas;
using ABCDEFG.Reroutables;
using AssetsTools.NET;
using System.Security.Cryptography;
using System.Text.Json;

namespace ABCDEFG;

internal readonly record struct StreamDataRange(ulong Offset, uint Size) : IEquatable<StreamDataRange>
{
    public StreamDataRange(AssetTypeValueField streamData) :
        this(streamData["offset"].AsULong, streamData["size"].AsUInt)
    {

    }
}

internal readonly record struct ExternalPPtr(int FileIndex, long PathID)
{
    public ExternalPPtr(ReroutableAsset asset) :
        this(asset.FileIndex, asset.PathID)
    {

    }
}

internal sealed class ReroutingContext
{
    public SortedList<ulong, StreamDataRange> RemovedStreamRanges = [];
    public Dictionary<long, ExternalPPtr> PathIDToExternalPPtr = [];
    public Dictionary<int, int> FileIndexToFileID = [];
    public Dictionary<string, string> AssetNamesToAssetType = [];
    public AssetBundleDirectoryInfo MainDirectoryInfo;
    public AssetBundleDirectoryInfo StreamDataDirectoryInfo;

    public ulong RecalculateStreamDataOffset(ulong originalOffset)
    {
        ulong shift = 0;

        foreach ((ulong offset, uint size) in RemovedStreamRanges.Values)
        {
            if (offset < originalOffset)
                shift += size;
        }

        return originalOffset - shift;
    }

    public void SearchAndReroutePPtr(AssetTypeValueField field, string parentName, ref int reroutedCount)
    {
        foreach (var inField in field.Children)
        {
            var fileIDField = inField["m_FileID"];
            if (fileIDField == AssetTypeValueField.DUMMY_FIELD)
            {
                SearchAndReroutePPtr(inField, $"{parentName}.{inField.FieldName}", ref reroutedCount);
                continue;
            }

            var fileID = fileIDField.AsInt;
            if (fileID != 0)
                continue;

            var pathIDField = inField["m_PathID"];
            if (pathIDField == AssetTypeValueField.DUMMY_FIELD)
                continue;

            var pathID = pathIDField.AsLong;
            if (!PathIDToExternalPPtr.TryGetValue(pathID, out var pair))
                continue;

            var newFileID = FileIndexToFileID[pair.FileIndex];
            var newPathID = pair.PathID;
            fileIDField.AsInt = newFileID;
            pathIDField.AsLong = newPathID;
            reroutedCount++;
            //Log.WriteLine($" - [{fileName}] Rerouted PPtr: {parentName}.{field.FieldName} :: {fileID} {pathID} -> {newFileID} {newPathID}");
        }
    }

    public void GenerateMetadata(string outputFile)
    {
        using var fs = File.OpenRead(outputFile);
        using var md5 = MD5.Create();
        var checksum = Convert.ToHexString(md5.ComputeHash(fs));
        var metaFile = outputFile + ".meta.json";
        var metadata = new BundleMetadata();

        using var metaFS = File.Open(metaFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        metaFS.Seek(0, SeekOrigin.Begin);

        if (metaFS.Length != 0)
        {
            try
            {
                metadata = JsonSerializer.Deserialize<BundleMetadata>(metaFS);
            }
            finally
            {
                metaFS.Seek(0, SeekOrigin.Begin);
            }
        }

        metadata.Checksum = checksum;

        var assets = new Dictionary<string, AssetMetadata>();
        foreach ((string path, string type) in AssetNamesToAssetType)
        {
            if (!metadata.Assets.TryGetValue(path, out var asset))
            {
                asset = new();
                metadata.Assets[path] = asset;
            }
            asset.AssetType = type;
            asset.Orphaned = false;
        }

        foreach (var toRemove in metadata.Assets.Where(asset => asset.Value.Orphaned).ToArray())
        {
            metadata.Assets.Remove(toRemove.Key);
        }

        metaFS.SetLength(0);
        JsonSerializer.Serialize(metaFS, metadata, options: new() { WriteIndented = true });
    }
}
