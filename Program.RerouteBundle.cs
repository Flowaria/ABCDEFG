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
            Log.WriteLine($"[{fileName}] Loaded (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            #endregion



            #region Look for reroutable assets
            Log.WriteLine($"[{fileName}] Start Look for reroutables and streamed");
            var removedStreamRanges = new List<(ulong offset, uint size)>();
            var internalPathIDToExternalPathID = new Dictionary<long, (long toPathID, int fileIndex)>();
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

                var streamData = baseField["m_StreamData"];
                if (streamData != AssetTypeValueField.DUMMY_FIELD && streamData["size"].AsUInt > 0)
                {
                    removedStreamRanges.Add((streamData["offset"].AsULong, streamData["size"].AsUInt));
                }
                assetInfo.SetRemoved();

                internalPathIDToExternalPathID[assetInfo.PathId] = (reroutable.PathID, reroutable.FileIndex);
                Log.WriteLine($" - [{fileName}] Reroutable: [{(AssetClassID)assetInfo.TypeId}] '{asset}'");
            }

            removedStreamRanges.Sort((a, b) => a.offset.CompareTo(b.offset));

            Log.WriteLine($"[{fileName}] Found {internalPathIDToExternalPathID.Count} Reroutables (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            if (internalPathIDToExternalPathID.Count == 0)
                return;
            #endregion



            #region Inject External References
            Log.WriteLine($"[{fileName}] Start Injecting Externals");
            startTime = Stopwatch.GetTimestamp();
            var fileIndexes = internalPathIDToExternalPathID.Select(x => x.Value.fileIndex).Distinct();
            var fileIndexToFileID = new Dictionary<int, int>();
            foreach (var fileIndex in fileIndexes)
            {
                if (!s_fileIndexCache.TryGetFileName(fileIndex, out var externalFileName))
                    throw new DataMisalignedException($"{fileIndex} is missing in fileIndex cache!");

                inst.file.Metadata.Externals.Add(new AssetsFileExternal()
                {
                    Type = AssetsFileExternalType.Normal,
                    PathName = externalFileName,
                    OriginalPathName = externalFileName,
                    Guid = default,
                    VirtualAssetPathName = ""
                });
                var fileID = inst.file.Metadata.Externals.Count;
                fileIndexToFileID[fileIndex] = fileID;
                Log.WriteLine($" - [{fileName}] Injected Externals: '{externalFileName}' ({fileID})");
            }
            Log.WriteLine($"[{fileName}] Injected {fileIndexToFileID.Count} External References (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            #endregion



            #region Reroute PPtrs
            Log.WriteLine($"[{fileName}] Start Rerouting PPtrs and Realign");
            startTime = Stopwatch.GetTimestamp();
            foreach (var assetInfo in inst.file.AssetInfos)
            {
                if (assetInfo.ReplacerType == ContentReplacerType.Remove)
                    continue;

                var baseField = manager.GetBaseField(inst, assetInfo);
                var modified = false;

                var streamData = baseField["m_StreamData"];
                if (streamData != AssetTypeValueField.DUMMY_FIELD && streamData["size"].AsUInt > 0)
                {
                    var origOffset = streamData["offset"].AsULong;

                    ulong shift = 0;
                    foreach ((ulong offset, uint size) in removedStreamRanges)
                        if (offset < origOffset) shift += size;
                    var newOffset = origOffset - shift;

                    if (newOffset != origOffset)
                    {
                        streamData["offset"].AsULong = newOffset;
                        modified = true;
                    }
                }

                SearchPPtr(baseField, baseField.TypeName);

                void SearchPPtr(AssetTypeValueField field, string parentName)
                {
                    foreach (var inField in field.Children)
                    {
                        var fileIDField = inField["m_FileID"];
                        if (fileIDField == AssetTypeValueField.DUMMY_FIELD)
                        {
                            SearchPPtr(inField, $"{parentName}.{inField.FieldName}");
                            continue;
                        }

                        var fileID = fileIDField.AsInt;
                        if (fileID != 0)
                            continue;

                        var pathIDField = inField["m_PathID"];
                        if (pathIDField == AssetTypeValueField.DUMMY_FIELD)
                            continue;

                        var pathID = pathIDField.AsLong;
                        if (!internalPathIDToExternalPathID.TryGetValue(pathID, out var pair))
                            continue;

                        var newFileID = fileIndexToFileID[pair.fileIndex];
                        var newPathID = pair.toPathID;
                        fileIDField.AsInt = newFileID;
                        pathIDField.AsLong = newPathID;
                        modified = true;
                        //Log.WriteLine($" - [{fileName}] Rerouted PPtr: {parentName}.{field.FieldName} :: {fileID} {pathID} -> {newFileID} {newPathID}");
                    }
                }

                if (modified)
                {
                    assetInfo.SetNewData(baseField);
                }
            }
            Log.WriteLine($"[{fileName}] Rerouted {fileIndexToFileID.Count} PPtrs (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            #endregion



            #region Save AssetBundle
            Log.WriteLine($"[{fileName}] Save Uncompressed Buffer");
            startTime = Stopwatch.GetTimestamp();
            bundleInst.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(inst.file);

            if (removedStreamRanges.Count != 0)
            {
                var streamInfo = bundleInst.file.BlockAndDirInfo.DirectoryInfos[1];
                var data = bundleInst.file.DataReader;
                data.Position = streamInfo.Offset;

                var bytes = data.ReadBytes((int)streamInfo.DecompressedSize);
                var ms = new MemoryStream();
                ulong pos = 0;
                foreach ((ulong offset, uint size) in removedStreamRanges)
                {
                    ms.Write(bytes, (int)pos, (int)(offset - pos));
                    pos = offset + size;
                }
                ms.Write(bytes, (int)pos, (int)((ulong)bytes.Length - pos));

                bundleInst.file.BlockAndDirInfo.DirectoryInfos[1].SetNewData(ms.ToArray());
                ms.Dispose();
            }

            using (var w = new AssetsFileWriter(fileName + "-uncompressed"))
                bundleInst.file.Write(w);
            Log.WriteLine($"[{fileName}] Saved! (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            #endregion



            #region Save AssetBundle
            Log.WriteLine($"[{fileName}] Compressing Bundle");
            startTime = Stopwatch.GetTimestamp();
            var packed = manager.LoadBundleFile(fileName + "-uncompressed", true);
            using (var w = new AssetsFileWriter(fileName + "-compressed"))
                packed.file.Pack(w, options.Compression);

            Log.WriteLine($"[{fileName}] Compressed! (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            #endregion
        }
        catch (Exception e)
        {
            Log.WriteLine($"Skipping '{fileName}' : {e}");
        }
        finally
        {
            manager.UnloadAll(unloadClassData: false);
            File.Delete(fileName + "-uncompressed");
            GC.Collect();
        }
    }
}
