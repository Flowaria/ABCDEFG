using ABCDEFG.Utils;
using AssetsTools.NET.Extra;
using System.Diagnostics;

namespace ABCDEFG;

internal static partial class Program
{
    static void BuildAllCache()
    {
        foreach (var reroutableCache in s_reroutableCaches.Values)
        {
            reroutableCache.FileStream.SetLength(0);
            reroutableCache.FileStream.Seek(0, SeekOrigin.Begin);
        }

        var files = new DirectoryInfo(CFG.GTFODataPath)
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

        foreach (var reroutableCache in s_reroutableCaches.Values)
        {
            reroutableCache.FileStream.Flush();
        }
        s_fileIndexCache.FileStream.Flush();
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
                if (!s_reroutableCaches.TryGetValue((AssetClassID)assetInfo.TypeId, out var reroutableCache))
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
                s_fileIndexCache.Write(fileIndex, fileInfo.Name);
            }

            Log.WriteLine($"Found {rawReferenceNumbers} in '{fileInfo.Name}' (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
        }
        catch (Exception e)
        {
            Log.WriteLine($"Skipping '{fileInfo.Name}': {e}");
        }
        finally
        {
            manager.UnloadAll(unloadClassData: false);
            GC.Collect();
        }
    }
}
