using ABCDEFG.Reroutables;
using AssetsTools.NET;

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

internal sealed class ReroutingInfo
{
    public SortedList<ulong, StreamDataRange> RemovedStreamRanges = [];
    public Dictionary<long, ExternalPPtr> PathIDToExternalPPtr = [];
    public Dictionary<int, int> FileIndexToFileID = [];
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
}
