using ABCDEFG.Caches;
using ABCDEFG.Utils;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace ABCDEFG;

internal static partial class Program
{
    static void RerouteBundle(string filePath, ReroutingFileOptions options)
    {
        var fileName = Path.GetFileName(filePath);
        var tempFileName = Path.Combine(Paths.TempPath, Guid.NewGuid().ToString() + ".tmp");
        var manager = AssetsManagerPool.GetInstance();

        try
        {

            #region Load Bundle File
            var jobTimeScope = new BundleJobScope(fileName, "Loading Bundle File");

            var bundleInst = manager.LoadBundleFile(filePath);
            var inst = manager.LoadAssetsFileFromBundle(bundleInst, 0, false);
            AssetsManagerPool.LoadClassDatabaseOf(inst.file.Metadata.UnityVersion);

            jobTimeScope.Dispose();
            #endregion


            #region Look for reroutable assets
            jobTimeScope = new BundleJobScope(fileName, "Looking for Assets and StreamData");

            var context = new ReroutingContext();
            var dirInfos = bundleInst.file.BlockAndDirInfo.DirectoryInfos;
            var bundle = inst.file.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var container = manager.GetBaseField(inst, bundle)["m_Container"]["Array"];
            inst.file.GenerateQuickLookup();

            foreach (var asset in container)
            {
                string assetPath = asset["first"].AsString;
                AssetTypeValueField pptr = asset["second"]["asset"];

                int fileID = pptr["m_FileID"].AsInt;
                long pathID = pptr["m_PathID"].AsLong;
                if (fileID != 0)
                {
                    Log.Warning($"  [{fileName}] Found AssetPath: '{assetPath}' but It's external asset!");
                    context.AssetNamesToAssetType[assetPath] = "Unknown";
                    continue;
                }

                var assetInfo = inst.file.GetAssetInfo(pathID);
                if (assetInfo == null)
                {
                    Log.Warning($"  [{fileName}] Found AssetPath: '{assetPath}' but AssetInfo is missing!");
                    context.AssetNamesToAssetType[assetPath] = "Unknown";
                    continue;
                }

                var typeName = ((AssetClassID)assetInfo.TypeId).ToString();
                Log.WriteLine($"  [{fileName}] Found AssetPath: '{assetPath}' with type: '{typeName}'");
                context.AssetNamesToAssetType[assetPath] = ((AssetClassID)assetInfo.TypeId).ToString();
            }

            context.MainDirectoryInfo = dirInfos[0];
            context.StreamDataDirectoryInfo = dirInfos.Count > 1 ? dirInfos[1] : null;
            foreach (var assetInfo in inst.file.AssetInfos)
            {
                if (!CacheManager.Reroutables.TryGetValue((AssetClassID)assetInfo.TypeId, out var reroutableCache))
                {
                    continue;
                }

                var baseField = manager.GetBaseField(inst, assetInfo);
                var asset = reroutableCache.FromBaseField(0, assetInfo.PathId, baseField);
                if (!reroutableCache.FindRerouteTarget(asset, out var reroutable))
                {
                    Log.Warning($"  [{fileName}] Found Non-Reroutable: [{(AssetClassID)assetInfo.TypeId}] '{asset}'");
                    continue;
                }

                if (context.StreamDataDirectoryInfo != null && HasStreamDataField(baseField, out var streamData) && streamData.Size > 0)
                {
                    var pathField = baseField["m_StreamData"]["path"];
                    if (!pathField.IsNullOrDummy() && pathField.AsString.EndsWith(context.StreamDataDirectoryInfo.Name))
                    {
                        context.RemovedStreamRanges.Add(streamData.Offset, streamData);
                    }
                }

                context.PathIDToExternalPPtr[assetInfo.PathId] = new(reroutable);
                assetInfo.SetRemoved();

                Log.Verbose($"  [{fileName}] Found Reroutable: [{(AssetClassID)assetInfo.TypeId}] '{asset}'");
            }

            if (context.PathIDToExternalPPtr.Count == 0)
            {
                return;
            }

            Log.WriteLine($"  [{fileName}] Found {context.PathIDToExternalPPtr.Count} Reroutable Assets");
            jobTimeScope.Dispose();
            #endregion


            #region Inject External References
            jobTimeScope = new BundleJobScope(fileName, "Injecting External References");

            var fileIndexes = context.PathIDToExternalPPtr.Select(x => x.Value.FileIndex).Distinct();
            foreach (var fileIndex in fileIndexes)
            {
                if (!CacheManager.FileIndexes.TryGetFileName(fileIndex, out var externalFileName))
                    throw new InvalidDataException($"{fileIndex} is missing in fileIndex cache!");

                inst.file.Metadata.Externals.Add(new AssetsFileExternal()
                {
                    Type = AssetsFileExternalType.Normal,
                    PathName = externalFileName,
                    OriginalPathName = externalFileName,
                    Guid = default,
                    VirtualAssetPathName = ""
                });

                var fileID = inst.file.Metadata.Externals.Count;
                context.FileIndexToFileID[fileIndex] = fileID;
                Log.Verbose($"  [{fileName}] Injected Externals: '{externalFileName}' ({fileID})");
            }

            Log.WriteLine($"  [{fileName}] Injected {context.FileIndexToFileID.Count} Externals");
            jobTimeScope.Dispose();
            #endregion


            #region Reroute PPtrs
            jobTimeScope = new BundleJobScope(fileName, "Rerouting PPtrs and Align Offsets");

            int reroutedTotalCount = 0;
            foreach (var assetInfo in inst.file.AssetInfos)
            {
                if (assetInfo.ReplacerType == ContentReplacerType.Remove)
                    continue;

                var baseField = manager.GetBaseField(inst, assetInfo);
                var modified = false;

                if (HasStreamDataField(baseField, out var streamData) && streamData.Size > 0)
                {
                    var newOffset = context.RecalculateStreamDataOffset(streamData.Offset);
                    if (newOffset != streamData.Offset)
                    {
                        baseField["m_StreamData"]["offset"].AsULong = newOffset;
                        modified = true;
                    }
                }

                int reroutedCount = 0;
                context.SearchAndReroutePPtr(baseField, baseField.TypeName, ref reroutedCount);

                reroutedTotalCount += reroutedCount;
                if (modified || reroutedCount != 0)
                {
                    assetInfo.SetNewData(baseField);
                }
            }

            Log.WriteLine($"  [{fileName}] Rerouted {reroutedTotalCount} PPtrs");
            jobTimeScope.Dispose();
            #endregion


            #region Align StreamData
            jobTimeScope = new BundleJobScope(fileName, "Aligning StreamData");

            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(inst.file);

            if (context.RemovedStreamRanges.Count != 0)
            {
                var data = bundleInst.file.DataReader;
                data.Position = context.StreamDataDirectoryInfo.Offset;

                var length = context.StreamDataDirectoryInfo.DecompressedSize;
                var newData = new MemoryStream();
                long pos = 0;
                foreach ((ulong offset, uint size) in context.RemovedStreamRanges.Values)
                {
                    Log.Verbose($"  [{fileName}] Skip stripped data from: {offset} size: {size}");
                    LargeStreamUtil.CopyBlock(data.BaseStream, newData, pos, (long)offset - pos);
                    pos = (long)(offset + size);
                }
                LargeStreamUtil.CopyBlock(data.BaseStream, newData, pos, length - pos);

                bundleInst.file.BlockAndDirInfo.DirectoryInfos[1].SetNewData(newData.ToArray());
                Log.WriteLine($"  [{fileName}] Stripped {LargeStreamUtil.BufferLengthToString(length - newData.Length)} from StreamData");
                newData.Dispose();
            }

            jobTimeScope.Dispose();
            #endregion


            #region Save Rerouted Bundle
            jobTimeScope = new BundleJobScope(fileName, "Saving Rerouted Bundle");

            var reroutedFile = options.NoCompression ? options.GetCompressedFilePath(filePath) : tempFileName;
            Directory.CreateDirectory(Path.GetDirectoryName(reroutedFile));

            using var reroutedStream = OpenReroutedBundleStream(reroutedFile);
            using var bundleFileWriter = new AssetsFileWriter(reroutedStream);
            bundleInst.file.Write(bundleFileWriter);
            bundleFileWriter.Flush();

            if (options.NoCompression)
            {
                if (options.GenerateMetadata)
                {
                    context.GenerateMetadata(reroutedFile);
                }
                jobTimeScope.Dispose();
                return;
            }

            jobTimeScope.Dispose();
            #endregion


            #region Compress Saved Bundle
            jobTimeScope = new BundleJobScope(fileName, $"Compressing Saved Bundle (Compression: {options.Compression})");

            var packed = manager.LoadBundleFile(reroutedStream, reroutedFile, true);
            var compressedFile = options.GetCompressedFilePath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(compressedFile));

            using var compressedFileWriter = new AssetsFileWriter(compressedFile);
            packed.file.Pack(compressedFileWriter, options.Compression);
            compressedFileWriter.Dispose();

            if (options.GenerateMetadata)
            {
                context.GenerateMetadata(compressedFile);
            }

            jobTimeScope.Dispose();
            #endregion

        }
        catch (Exception e)
        {
            Log.Error($"Skipping '{fileName}' : {e}");
        }
        finally
        {
            manager.UnloadAll(unloadClassData: false);

            if (Directory.Exists(Paths.TempPath))
                File.Delete(tempFileName);

            GC.Collect();
        }
    }

    static Stream OpenReroutedBundleStream(string reroutedBundleFileName)
    {
        if (Config.Instance.WriteReroutedOnDisk)
        {
            return File.Open(reroutedBundleFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        }
        else
        {
            return new MemoryStream();
        }
    }

    static bool HasStreamDataField(AssetTypeValueField parentField, out StreamDataRange dataRange)
    {
        var streamData = parentField["m_StreamData"];
        if (streamData.IsNullOrDummy())
            goto Invalid;

        var sizeField = streamData["size"];
        if (sizeField.IsNullOrDummy())
            goto Invalid;

        var offsetField = streamData["offset"];
        if (offsetField.IsNullOrDummy())
            goto Invalid;

        dataRange = new StreamDataRange(offsetField.AsULong, sizeField.AsUInt);
        return true;

    Invalid:
        dataRange = default;
        return false;
    }
}
