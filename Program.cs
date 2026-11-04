using ABCDEFG.Caches;
using ABCDEFG.Reroutables;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ABCDEFG;

internal static partial class Program
{
    static string Version => "0.0.1";

    static readonly Dictionary<AssetClassID, ReroutableAssetCache> s_reroutableCaches = [];
    static FileIndexCache s_fileIndexCache;


    internal static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory;
    internal static readonly string ConfigFile = Path.Combine(BasePath, "config.json");
    internal static readonly string LogFile = Path.Combine(BasePath, "output.log");
    internal static Config CFG;

    static void Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            Log.WriteLine(ex.ToString());
        }

        Log.FlushLog();
    }

    static void Run(string[] args)
    {
        Log.Prepare(LogFile);
        Log.WriteLine($"== Asset-Bundle-Compressor-Deluxe-Edition-For-GTFO ==");
        Log.WriteLine($" - Version: {Version}");
        Log.WriteLine($" - Base Path: {BasePath}");
        if (!File.Exists(ConfigFile))
        {
            Log.WriteLine();
            Log.WriteLine($"config.json is missing!", ConsoleColor.Red);

            var configJson = JsonSerializer.Serialize(new Config(), new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(ConfigFile, configJson);
            Thread.Sleep(2000);
            return;
        }

        CFG = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFile));
        if (string.IsNullOrWhiteSpace(CFG.GTFODataPath))
        {
            Log.WriteLine();
            Log.WriteLine($"Please set GTFODataPath on config.json!", ConsoleColor.Red);
            Thread.Sleep(2000);
            return;
        }

        if (!File.Exists(Path.Combine(CFG.GTFODataPath, "app.info")))
        {
            Log.WriteLine();
            Log.WriteLine($"GTFODataPath: '{CFG.GTFODataPath}' is invalid!", ConsoleColor.Red);
            Thread.Sleep(2000);
            return;
        }

        Log.WriteLine($" - GTFO_Data Path: {CFG.GTFODataPath}");
        Log.WriteLine();

        var maxThreads = Math.Clamp(CFG.MaxThread, 1, Environment.ProcessorCount);
        if (maxThreads != CFG.MaxThread)
        {
            Log.WriteLine($"MaxThread has clamped to {maxThreads}!", ConsoleColor.Yellow);
            Log.WriteLine($" - It should be within range between 1~{Environment.ProcessorCount}!", ConsoleColor.Yellow);
            Thread.Sleep(2000);
        }

        var pathIDDirInfo = Directory.CreateDirectory(Path.Combine(BasePath, "caches"));
        var pathIDDir = pathIDDirInfo.FullName;
        s_reroutableCaches[AssetClassID.Material] = new ReroutableAssetCache<Material>(Path.Combine(pathIDDir, "material.dat"));
        s_reroutableCaches[AssetClassID.Shader] = new ReroutableAssetCache<Shader>(Path.Combine(pathIDDir, "shader.dat"));
        s_reroutableCaches[AssetClassID.Mesh] = new ReroutableAssetCache<Mesh>(Path.Combine(pathIDDir, "mesh.dat"));
        s_reroutableCaches[AssetClassID.Texture2D] = new ReroutableAssetCache<Texture2D>(Path.Combine(pathIDDir, "texture2D.dat"));
        s_reroutableCaches[AssetClassID.Texture2DArray] = new ReroutableAssetCache<Texture2DArray>(Path.Combine(pathIDDir, "texture2Darray.dat"));
        s_reroutableCaches[AssetClassID.Texture3D] = new ReroutableAssetCache<Texture3D>(Path.Combine(pathIDDir, "texture3D.dat"));
        s_fileIndexCache = new FileIndexCache(Path.Combine(pathIDDir, "fileindex.dat"));

        bool doCache = false;
        if (args.Length == 0)
        {
            WriteUsage();
            Log.WriteLine();
            Log.WriteLine($"No arguments given. Do you want to generate the cache? (Press 'Y' to confirm)");
            if (Console.ReadKey(intercept: true).Key != ConsoleKey.Y)
                return;

            doCache = true;
        }
        else if (args[0].Equals("-cache", StringComparison.OrdinalIgnoreCase))
        {
            doCache = true;
        }

        if (doCache)
        {
            Log.WriteLine();
            Log.WriteLine($"Start generating cache");

            var startTime = Stopwatch.GetTimestamp();
            BuildAllCache();

            Log.WriteLine();
            Log.WriteLine($"Cache generated! (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            return;
        }

        if (s_fileIndexCache.IsFileEmpty)
        {
            Log.WriteLine();
            Log.WriteLine($"Cache is missing! Please cache the AssetIDs first!", ConsoleColor.Red);
            Thread.Sleep(2000);
            return;
        }

        LoadAllCache();
        var fileToReroute = new Dictionary<string, ReroutingFileOptions>(FileStringComparator());
        AssetBundleCompressionType compressionType = AssetBundleCompressionType.LZMA;
        foreach (var arg in args)
        {
            if (arg.StartsWith('-'))
            {
                ParseOptions(arg, ref compressionType);
                continue;
            }

            var fullPath = Path.GetFullPath(arg);
            if (Directory.Exists(arg))
            {
                foreach (var file in Directory.GetFiles(fullPath).Where(IsUnityFSFile))
                {
                    fileToReroute.Add(file, new ReroutingFileOptions()
                    {
                        Compression = compressionType
                    });
                }
            }

            if (IsUnityFSFile(fullPath))
            {
                fileToReroute.Add(fullPath, new ReroutingFileOptions()
                {
                    Compression = compressionType
                });
            }
        }

        if (CFG.MaxThread == 1)
        {
            var startTimeTotal = Stopwatch.GetTimestamp();
            foreach (var kv in fileToReroute)
            {
                var startTime = Stopwatch.GetTimestamp();
                RerouteBundle(kv.Key, kv.Value);

                var elapsedSeconds = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
                Log.WriteLine($"Finished Compressing Bundle '{Path.GetFileName(kv.Key)}' (took {elapsedSeconds:0.000}s)");
            }
            var elapsedAllSeconds = Stopwatch.GetElapsedTime(startTimeTotal).TotalSeconds;
            Log.WriteLine($"Finished Compressing {fileToReroute.Count} Bundle(s) (took {elapsedAllSeconds:0.000}s)");
        }
        else
        {
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = CFG.MaxThread
            };

            var startTimeTotal = Stopwatch.GetTimestamp();
            Parallel.ForEach(fileToReroute, options, kv =>
            {
                var startTime = Stopwatch.GetTimestamp();
                RerouteBundle(kv.Key, kv.Value);

                var elapsedSeconds = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
                Log.WriteLine($"Finished Compressing Bundle '{Path.GetFileName(kv.Key)}' (took {elapsedSeconds:0.000}s)");
            });

            var elapsedAllSeconds = Stopwatch.GetElapsedTime(startTimeTotal).TotalSeconds;
            Log.WriteLine($"Finished Compressing {fileToReroute.Count} Bundle(s) (took {elapsedAllSeconds:0.000}s)");
        }
    }

    static byte[] s_UnityFSSignature = Encoding.ASCII.GetBytes("UnityFS");

    static bool IsUnityFSFile(string file)
    {
        bool isUnityFS = false;
        FileStream fs = null;

        try
        {
            fs = File.OpenRead(file);
            byte[] buffer = new byte[s_UnityFSSignature.Length];
            fs.ReadExactly(buffer, 0, s_UnityFSSignature.Length);
            isUnityFS = buffer.SequenceEqual(s_UnityFSSignature);
        }
        catch { }

        fs?.Dispose();
        return isUnityFS;
    }

    static StringComparer FileStringComparator()
    {
        // How .NET still doesn't have this??

        var fileName = Guid.NewGuid().ToString() + ".case_test";
        var temp = Path.Combine(Path.GetTempPath(), fileName);

        try
        {
            File.WriteAllText(temp, string.Empty);
            if (File.Exists(temp.ToUpper()))
            {
                return StringComparer.OrdinalIgnoreCase;
            }

            return StringComparer.Ordinal;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    static void ParseOptions(string arg, ref AssetBundleCompressionType compressionType)
    {
        switch (arg[1..].ToLower())
        {
            case "lzma":
                compressionType = AssetBundleCompressionType.LZMA;
                break;

            case "lz4":
                compressionType = AssetBundleCompressionType.LZ4;
                break;

            case "lz4hc":
                compressionType = AssetBundleCompressionType.LZ4Fast;
                break;

            case "nocomp":
                compressionType = AssetBundleCompressionType.None;
                break;

            default:
                Log.WriteLine($"'{arg}' is not a valid option!");
                break;
        }
    }

    static void WriteUsage()
    {
        Log.WriteLine("--------");
        Log.WriteLine("Usage:");
        Log.WriteLine(" ABCDEFG");
        Log.WriteLine(" ABCDEFG -cache");
        Log.WriteLine(" └ Generate Asset ID cache from given GTFO_Data path");
        Log.WriteLine();
        Log.WriteLine(" ABCDEFG <file/folder path> <file/folder path 2> ...");
        Log.WriteLine(" ABCDEFG [-option] <file/folder path> [-option] <file/folder path 2> ...");
        Log.WriteLine(" └ Compress given file(s) as LZMA (or option driven) compression");
        Log.WriteLine("--------");
        Log.WriteLine("Options:");
        Log.WriteLine(" -lzma\t\t; Set trailing files compression to LZMA");
        Log.WriteLine(" -lz4\t\t; Set trailing files compression to LZ4");
        Log.WriteLine(" -lz4hc\t\t; Set trailing files compression to LZ4HC");
        Log.WriteLine(" -nocomp\t; Don't compress trailing files (Rerouting still works)");
        Log.WriteLine("--------");
        Log.WriteLine("Example:");
        Log.WriteLine(" ABCDEFG -lzma \"geo_lab_01\" -lz4 \"gearpart_stock\" \"gearpart_front\"");
        Log.WriteLine(" └ This will compress 'geo_lab_01' file as LZMA. 'gearpart_stock' and 'gearpart_front' as LZ4");
        Log.WriteLine("--------");
    }
}
