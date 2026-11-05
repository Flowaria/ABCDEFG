using ABCDEFG.Utils;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace ABCDEFG;

internal static partial class Program
{
    static void RerouteBundle(string filePath, ReroutingFileOptions options)
    {
        var fileName = Path.GetFileName(filePath);
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

            var info = new ReroutingInfo();
            var dirInfos = bundleInst.file.BlockAndDirInfo.DirectoryInfos;
            info.MainDirectoryInfo = dirInfos[0];
            info.StreamDataDirectoryInfo = dirInfos.Count > 1 ? dirInfos[1] : null;
            foreach (var assetInfo in inst.file.AssetInfos)
            {
                if (!s_reroutableCaches.TryGetValue((AssetClassID)assetInfo.TypeId, out var reroutableCache))
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

                if (info.StreamDataDirectoryInfo != null && HasStreamDataField(baseField, out var streamData) && streamData.Size > 0)
                {
                    var pathField = baseField["m_StreamData"]["path"];
                    if (!pathField.IsNullOrDummy() && pathField.AsString.EndsWith(info.StreamDataDirectoryInfo.Name))
                    {
                        info.RemovedStreamRanges.Add(streamData.Offset, streamData);
                    }
                }

                info.PathIDToExternalPPtr[assetInfo.PathId] = new(reroutable);
                assetInfo.SetRemoved();

                Log.Verbose($"  [{fileName}] Found Reroutable: [{(AssetClassID)assetInfo.TypeId}] '{asset}'");
            }

            if (info.PathIDToExternalPPtr.Count == 0)
            {
                return;
            }

            Log.WriteLine($"  [{fileName}] Found {info.PathIDToExternalPPtr.Count} Reroutable Assets");
            jobTimeScope.Dispose();
            #endregion


            #region Inject External References
            jobTimeScope = new BundleJobScope(fileName, "Injecting External References");

            var fileIndexes = info.PathIDToExternalPPtr.Select(x => x.Value.FileIndex).Distinct();
            foreach (var fileIndex in fileIndexes)
            {
                if (!s_fileIndexCache.TryGetFileName(fileIndex, out var externalFileName))
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
                info.FileIndexToFileID[fileIndex] = fileID;
                Log.Verbose($"  [{fileName}] Injected Externals: '{externalFileName}' ({fileID})");
            }

            Log.WriteLine($"  [{fileName}] Injected {info.FileIndexToFileID.Count} Externals");
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
                    var newOffset = info.RecalculateStreamDataOffset(streamData.Offset);
                    if (newOffset != streamData.Offset)
                    {
                        baseField["m_StreamData"]["offset"].AsULong = newOffset;
                        modified = true;
                    }
                }

                int reroutedCount = 0;
                info.SearchAndReroutePPtr(baseField, baseField.TypeName, ref reroutedCount);

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

            if (info.RemovedStreamRanges.Count != 0)
            {
                var data = bundleInst.file.DataReader;
                data.Position = info.StreamDataDirectoryInfo.Offset;

                var length = info.StreamDataDirectoryInfo.DecompressedSize;
                var newData = new MemoryStream();
                long pos = 0;
                foreach ((ulong offset, uint size) in info.RemovedStreamRanges.Values)
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

            var reroutedFileName = options.NoCompression ? options.GetCompressedFilePath(filePath) : options.GetReroutedFilePath(filePath);
            using var reroutedStream = OpenReroutedBundleStream(reroutedFileName);
            using var bundleFileWriter = new AssetsFileWriter(reroutedStream);
            bundleInst.file.Write(bundleFileWriter);
            bundleFileWriter.Flush();

            if (options.NoCompression)
            {
                jobTimeScope.Dispose();
                return;
            }

            jobTimeScope.Dispose();
            #endregion


            #region Compress Saved Bundle
            jobTimeScope = new BundleJobScope(fileName, $"Compressing Saved Bundle (Compression: {options.Compression})");

            var packed = manager.LoadBundleFile(reroutedStream, reroutedFileName, true);
            using var compressedFileWriter = new AssetsFileWriter(options.GetCompressedFilePath(filePath));
            packed.file.Pack(compressedFileWriter, options.Compression);

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
            File.Delete(options.GetReroutedFilePath(filePath));
            GC.Collect();
        }
    }

    static Stream OpenReroutedBundleStream(string reroutedBundleFileName)
    {
        if (CFG.WriteReroutedOnDisk)
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
