using ABCDEFG.Reroutables;

namespace ABCDEFG.Caches;

internal abstract class ReroutableAssetCache(string cacheFile) : Cache(cacheFile)
{
    public abstract string AssetTypeName { get; }
    public abstract ReroutableAsset WriteAssetCache(int fileIndex, AssetContext context);
    public abstract ReroutableAsset ReadAssetCache();
    public abstract ReroutableAsset FromBaseField(int fileIndex, AssetContext context);
    public abstract bool FindRerouteTarget(ReroutableAsset assetFromAssetBundle, out ReroutableAsset reroutableAsset);
    public abstract int GetCollisionCount();
}

internal class ReroutableAssetCache<T>(string cacheFile) : ReroutableAssetCache(cacheFile) where T : ReroutableAsset, new()
{
    public override string AssetTypeName => field ?? typeof(T).Name;
    public override bool IsEmpty => _Assets.Count == 0;

    private readonly List<ReroutableAsset> _Assets = [];
    private readonly HashSet<string> _CollisionLookup = [];
    private int _CollisionCounter = 0;

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

    public override ReroutableAsset WriteAssetCache(int fileIndex, AssetContext context)
    {
        var asset = ReroutableAsset.FromBaseField<T>(fileIndex, context);
        ReroutableAsset.Write(asset, Writer);

        var lookupString = asset.ToString();
        if (_CollisionLookup.Contains(lookupString))
        {
            _CollisionCounter++;
            Log.Error($"Found colliding reroutable! [{AssetTypeName}] '{lookupString}'");
        }

        _CollisionLookup.Add(lookupString);

        return asset;
    }

    public override int GetCollisionCount()
    {
        return _CollisionCounter;
    }

    public override ReroutableAsset FromBaseField(int fileIndex, AssetContext context)
    {
        return ReroutableAsset.FromBaseField<T>(fileIndex, context);
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

        if (FileStream.Position != FileStream.Length)
            throw new InvalidDataException($"Reroutable Cache ({AssetTypeName}) is invalid! Regenerate the Cache!");
    }

    public override void ClearCache()
    {
        _Assets.Clear();
    }
}
