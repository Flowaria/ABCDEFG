using ABCDEFG.Utils;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Diagnostics;

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
            Log.WriteLine($"[{fileName}] Start Loading");
            var startTime = Stopwatch.GetTimestamp();

            var bundleInst = manager.LoadBundleFile(filePath);
            var inst = manager.LoadAssetsFileFromBundle(bundleInst, 0, false);
            AssetsManagerPool.LoadClassDatabaseOf(inst.file.Metadata.UnityVersion);
            Log.WriteLine($"[{fileName}] Loaded (took {GetElapsedSeconds(startTime)}s)");
            #endregion



            #region Look for reroutable assets
            var info = new ReroutingInfo();
            Log.WriteLine($"[{fileName}] Start Look for reroutables and streamed");
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
                    Log.WriteLine($" - [{fileName}] Not Reroutable: [{(AssetClassID)assetInfo.TypeId}] '{asset}'");
                    continue;
                }

                if (HasStreamDataField(baseField, out var streamData) && streamData.Size > 0)
                {
                    info.RemovedStreamRanges.Add(streamData.Offset, streamData);
                }

                info.PathIDToExternalPPtr[assetInfo.PathId] = new(reroutable);
                assetInfo.SetRemoved();

                Log.WriteLine($" - [{fileName}] Reroutable: [{(AssetClassID)assetInfo.TypeId}] '{asset}'");
            }

            Log.WriteLine($"[{fileName}] Found {info.PathIDToExternalPPtr.Count} Reroutables (took {GetElapsedSeconds(startTime)}s)");
            if (info.PathIDToExternalPPtr.Count == 0)
                return;
            #endregion



            #region Inject External References
            Log.WriteLine($"[{fileName}] Start Injecting Externals");
            startTime = Stopwatch.GetTimestamp();
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
                Log.WriteLine($" - [{fileName}] Injected Externals: '{externalFileName}' ({fileID})");
            }
            Log.WriteLine($"[{fileName}] Injected {info.FileIndexToFileID.Count} External References (took {GetElapsedSeconds(startTime)}s)");
            #endregion



            #region Reroute PPtrs
            Log.WriteLine($"[{fileName}] Start Rerouting PPtrs and Realign Offsets");
            startTime = Stopwatch.GetTimestamp();
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

                info.SearchPPtr(baseField, baseField.TypeName, ref modified);

                if (modified)
                {
                    assetInfo.SetNewData(baseField);
                }
            }
            Log.WriteLine($"[{fileName}] Rerouted {info.FileIndexToFileID.Count} PPtrs (took {GetElapsedSeconds(startTime)}s)");
            #endregion



            #region Save AssetBundle
            Log.WriteLine($"[{fileName}] Realigning StreamData");
            startTime = Stopwatch.GetTimestamp();
            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(inst.file);

            if (info.RemovedStreamRanges.Count != 0)
            {
                var streamDatas = bundleInst.file.BlockAndDirInfo.DirectoryInfos[1];
                var data = bundleInst.file.DataReader;
                data.Position = streamDatas.Offset;

                var length = streamDatas.DecompressedSize;
                var newData = new MemoryStream();
                long pos = 0;
                foreach ((ulong offset, uint size) in info.RemovedStreamRanges.Values)
                {
                    LargeStreamUtil.CopyBlock(data.BaseStream, newData, pos, (long)offset - pos);
                    pos = (long)(offset + size);
                }
                LargeStreamUtil.CopyBlock(data.BaseStream, newData, pos, length - pos);

                bundleInst.file.BlockAndDirInfo.DirectoryInfos[1].SetNewData(newData.ToArray());
                newData.Dispose();
            }
            #endregion



            #region Save AssetBundle
            Log.WriteLine($"[{fileName}] Saving Bundle (Compression: {options.Compression})");

            var reroutedFileName = options.NoCompression ? options.GetCompressedFilePath(filePath) : options.GetReroutedFilePath(filePath);
            using var reroutedStream = OpenReroutedBundleStream(reroutedFileName);
            using var bundleFileWriter = new AssetsFileWriter(reroutedStream);
            bundleInst.file.Write(bundleFileWriter);
            bundleFileWriter.Flush();

            if (options.Compression == AssetBundleCompressionType.None)
            {
                Log.WriteLine($"[{fileName}] Saved! (took {GetElapsedSeconds(startTime)}s)");
                return;
            }

            Log.WriteLine($"[{fileName}] Saved Uncompressed Buffer! (took {GetElapsedSeconds(startTime)}s)");
            startTime = Stopwatch.GetTimestamp();
            var packed = manager.LoadBundleFile(reroutedStream, reroutedFileName, true);
            using var compressedFileWriter = new AssetsFileWriter(options.GetCompressedFilePath(filePath));
            packed.file.Pack(compressedFileWriter, options.Compression);

            Log.WriteLine($"[{fileName}] Saved Compressed Bundle! (took {GetElapsedSeconds(startTime)}s)");
            #endregion
        }
        catch (Exception e)
        {
            Log.WriteLine($"Skipping '{fileName}' : {e}");
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

    static string GetElapsedSeconds(long startTime)
    {
        return Stopwatch.GetElapsedTime(startTime).TotalSeconds.ToString("0.000");
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
