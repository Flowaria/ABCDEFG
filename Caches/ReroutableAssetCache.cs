using ABCDEFG.Reroutables;
using AssetsTools.NET;

namespace ABCDEFG.Caches;

internal abstract class ReroutableAssetCache(string cacheFile) : Cache(cacheFile)
{
    public abstract string AssetTypeName { get; }
    public abstract ReroutableAsset WriteAssetCache(int fileIndex, long pathID, AssetTypeValueField baseField);
    public abstract ReroutableAsset ReadAssetCache();
    public abstract ReroutableAsset FromBaseField(int fileIndex, long pathID, AssetTypeValueField baseField);
    public abstract bool FindRerouteTarget(ReroutableAsset assetFromAssetBundle, out ReroutableAsset reroutableAsset);
}

internal class ReroutableAssetCache<T>(string cacheFile) : ReroutableAssetCache(cacheFile) where T : ReroutableAsset, new()
{
    public override string AssetTypeName => field ?? typeof(T).Name;
    public override bool IsEmpty => _Assets.Count == 0;

    private List<ReroutableAsset> _Assets = [];

    public sealed override bool FindRerouteTarget(ReroutableAsset assetFromAssetBundle, out ReroutableAsset reroutableAsset)
    {
        foreach (var asset in _Assets)
        {
            if (asset.IsSameAsset(assetFromAssetBundle))
            {
                reroutableAsset = asset;
                return true;
            }
        }

        reroutableAsset = null;
        return false;
    }

    public override ReroutableAsset WriteAssetCache(int fileIndex, long pathID, AssetTypeValueField baseField)
    {
        var asset = ReroutableAsset.FromBaseField<T>(fileIndex, pathID, baseField);
        ReroutableAsset.Write(asset, Writer);
        return asset;
    }

    public override ReroutableAsset FromBaseField(int fileIndex, long pathID, AssetTypeValueField baseField)
    {
        return ReroutableAsset.FromBaseField<T>(fileIndex, pathID, baseField);
    }

    public override ReroutableAsset ReadAssetCache()
    {
        return ReroutableAsset.Read<T>(Reader);
    }

    public override void ReadAll()
    {
        ClearCache();
        FileStream.Seek(0, SeekOrigin.Begin);
        while (FileStream.Position < FileStream.Length)
        {
            var asset = ReadAssetCache();
            _Assets.Add(asset);
        }
    }

    public override void ClearCache()
    {
        _Assets.Clear();
    }
}
