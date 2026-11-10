using ABCDEFG.Reroutables;
using ABCDEFG.Utils;
using AssetsTools.NET.Extra;
using System.Diagnostics;

namespace ABCDEFG.Caches;

internal static class CacheManager
{
    public static readonly Dictionary<AssetClassID, ReroutableAssetCache> Reroutables = [];
    public static FileIndexCache FileIndexes;

    public static void Prepare(string cacheDir)
    {
        Reroutables[AssetClassID.Material] = new ReroutableAssetCache<Material>(Path.Combine(cacheDir, "material.dat"));
        Reroutables[AssetClassID.Shader] = new ReroutableAssetCache<Shader>(Path.Combine(cacheDir, "shader.dat"));
        Reroutables[AssetClassID.Mesh] = new ReroutableAssetCache<Mesh>(Path.Combine(cacheDir, "mesh.dat"));
        Reroutables[AssetClassID.Texture2D] = new ReroutableAssetCache<Texture2D>(Path.Combine(cacheDir, "texture2D.dat"));
        Reroutables[AssetClassID.Texture2DArray] = new ReroutableAssetCache<Texture2DArray>(Path.Combine(cacheDir, "texture2Darray.dat"));
        Reroutables[AssetClassID.Texture3D] = new ReroutableAssetCache<Texture3D>(Path.Combine(cacheDir, "texture3D.dat"));
        FileIndexes = new FileIndexCache(Path.Combine(cacheDir, "fileindex.dat"));
    }

    public static void LoadAllCache()
    {
        var start = Stopwatch.GetTimestamp();
        FileIndexes.ReadAll();
        Log.WriteLine($"Loaded FileIndex cache (took {Stopwatch.GetElapsedTime(start).TotalSeconds:0.000}s)");

        foreach (var reroutableCache in Reroutables.Values)
        {
            start = Stopwatch.GetTimestamp();
            reroutableCache.ReadAll();
            Log.WriteLine($"Loaded {reroutableCache.AssetTypeName} cache (took {Stopwatch.GetElapsedTime(start).TotalSeconds:0.000}s)");
        }
    }

    public static void BuildAllCache()
    {
        foreach (var reroutableCache in Reroutables.Values)
        {
            reroutableCache.FileStream.SetLength(0);
            reroutableCache.FileStream.Seek(0, SeekOrigin.Begin);
        }

        var files = new DirectoryInfo(Config.Instance.GTFODataPath)
            .EnumerateFiles()
            .Where(file => !file.Extension.Equals(".resS", StringComparison.Ordinal))
            .Where(file => !file.Extension.Equals(".config", StringComparison.Ordinal))
            .Where(file => !file.Extension.Equals(".info", StringComparison.Ordinal))
            .OrderBy(file => file.FullName);

        int fileIndex = 0;
        foreach (var file in files)
        {
            BuildCache(file, fileIndex++);
        }

        foreach (var reroutableCache in Reroutables.Values)
        {
            var collisions = reroutableCache.GetCollisionCount();
            if (collisions > 0)
                Log.Error($"Found {collisions} colliding {reroutableCache.AssetTypeName}!");

            reroutableCache.FileStream.Flush();
        }
        FileIndexes.FileStream.Flush();
    }

    static void BuildCache(FileInfo fileInfo, int fileIndex)
    {
        var manager = AssetsManagerPool.GetInstance();
        try
        {
            Log.WriteLine($"Reading '{fileInfo.Name}'");
            var startTime = Stopwatch.GetTimestamp();

            int rawReferenceNumbers = 0;
            var inst = manager.LoadAssetsFile(fileInfo.FullName, loadDeps: true);
            AssetsManagerPool.LoadClassDatabaseOf(inst.file.Metadata.UnityVersion);

            foreach (var assetInfo in inst.file.AssetInfos)
            {
                if (!Reroutables.TryGetValue((AssetClassID)assetInfo.TypeId, out var reroutableCache))
                {
                    continue;
                }

                var baseField = manager.GetBaseField(inst, assetInfo);
                var asset = reroutableCache.WriteAssetCache(fileIndex, assetInfo.PathId, baseField);
                rawReferenceNumbers++;
                Log.WriteLine($" - [{(AssetClassID)assetInfo.TypeId}] '{asset}'");
            }

            if (rawReferenceNumbers != 0)
            {
                FileIndexes.Write(fileIndex, fileInfo.Name);
            }

            Log.WriteLine($"Found {rawReferenceNumbers} in '{fileInfo.Name}' (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
        }
        catch (Exception e)
        {
            Log.Error($"Skipping '{fileInfo.Name}': {e}");
        }
        finally
        {
            manager.UnloadAll(unloadClassData: false);
            GC.Collect();
        }
    }
}
