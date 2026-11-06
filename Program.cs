using ABCDEFG.Caches;
using ABCDEFG.Utils;
using System.Diagnostics;
using System.Text.Json;

namespace ABCDEFG;

internal static partial class Program
{
    static string Version => "0.1.1";

    static void Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }

        Log.FlushLog();
    }

    static void Run(string[] args)
    {
        Log.Prepare(Paths.LogFile, Paths.ErrorLogFile);
        Log.WriteLine($"== Asset-Bundle-Compressor-Deluxe-Edition-For-GTFO ==");
        Log.WriteLine($" - Version: {Version}");
        Log.WriteLine($" - Base Path: {Paths.BasePath}");
        if (!File.Exists(Paths.ConfigFile))
        {
            Log.WriteLine();
            Log.Error($"config.json is missing!");

            var configJson = JsonSerializer.Serialize(new Config(), new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(Paths.ConfigFile, configJson);
            Thread.Sleep(2000);
            return;
        }

        Config.Instance = JsonSerializer.Deserialize<Config>(File.ReadAllText(Paths.ConfigFile));
        if (string.IsNullOrWhiteSpace(Config.Instance.GTFODataPath))
        {
            Log.WriteLine();
            Log.Error($"Please set GTFODataPath on config.json!");
            Thread.Sleep(2000);
            return;
        }

        if (!File.Exists(Path.Combine(Config.Instance.GTFODataPath, "app.info")))
        {
            Log.WriteLine();
            Log.Error($"GTFODataPath: '{Config.Instance.GTFODataPath}' is invalid!");
            Thread.Sleep(2000);
            return;
        }

        Log.WriteLine($" - GTFO_Data Path: {Config.Instance.GTFODataPath}");
        Log.WriteLine();

        var maxThreads = Math.Clamp(Config.Instance.MaxThread, 1, Environment.ProcessorCount);
        if (maxThreads != Config.Instance.MaxThread)
        {
            Log.Warning($"MaxThread has clamped to {maxThreads}!");
            Log.Warning($"It should be within range between 1~{Environment.ProcessorCount}!");
            Thread.Sleep(2000);
        }

        var cacheDirInfo = Directory.CreateDirectory(Paths.CachePath);
        CacheManager.Prepare(cacheDirInfo.FullName);

        var argsRaw = string.Join(" ", args.Select(arg => (arg.Contains(' ') ? $"\"{arg}\"" : arg)));
        if (!string.IsNullOrWhiteSpace(argsRaw))
        {
            Log.WriteLine($"Used Arguments: ABCDEFG {argsRaw}");
        }

        bool doCache = false;
        if (args.Length == 0)
        {
            WriteUsage();
            Log.WriteLine();
            Log.WriteLine($"No arguments given. Do you want to generate the cache? (Press 'Y' to confirm)", ConsoleColor.Yellow);
            if (Console.ReadKey(intercept: true).Key != ConsoleKey.Y)
                return;

            doCache = true;
        }
        else if (args[0].Equals("--cache", StringComparison.OrdinalIgnoreCase))
        {
            doCache = true;
        }

        if (doCache)
        {
            Log.WriteLine();
            Log.WriteLine($"Start generating cache");

            var startTime = Stopwatch.GetTimestamp();
            CacheManager.BuildAllCache();

            Log.WriteLine();
            Log.WriteLine($"Cache generated! (took {Stopwatch.GetElapsedTime(startTime).TotalSeconds:0.000}s)");
            return;
        }

        if (CacheManager.FileIndexes.IsFileEmpty)
        {
            Log.WriteLine();
            Log.Error($"Cache is missing! Please cache the AssetIDs first!");
            Thread.Sleep(2000);
            return;
        }

        Log.WriteLine();
        CacheManager.LoadAllCache();
        var fileToReroute = ArgumentsParser.Parse(args);
        if (fileToReroute.Count == 0)
        {
            Log.Error($"No file arguments specified!");
            Thread.Sleep(2000);
        }

        var startTimeTotal = Stopwatch.GetTimestamp();
        if (Config.Instance.MaxThread == 1 || fileToReroute.Count == 1)
        {
            foreach (var kv in fileToReroute)
            {
                var startTime = Stopwatch.GetTimestamp();
                RerouteBundle(kv.Key, kv.Value);

                var elapsedSeconds = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
                Log.WriteLine($"Finished Compressing Bundle '{Path.GetFileName(kv.Key)}' (took {elapsedSeconds:0.000}s)");
            }
        }
        else
        {
            var options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Config.Instance.MaxThread
            };

            Parallel.ForEach(fileToReroute, options, kv =>
            {
                var startTime = Stopwatch.GetTimestamp();
                RerouteBundle(kv.Key, kv.Value);

                var elapsedSeconds = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
                Log.WriteLine($"Finished Compressing Bundle '{Path.GetFileName(kv.Key)}' (took {elapsedSeconds:0.000}s)");
            });
        }

        var elapsedAllSeconds = Stopwatch.GetElapsedTime(startTimeTotal).TotalSeconds;
        Log.WriteLine($"Finished Compressing {fileToReroute.Count} Bundle(s) (took {elapsedAllSeconds:0.000}s)");
    }

    static void WriteUsage()
    {
        Log.WriteLine("--------");
        Log.WriteLine("Usage:");
        Log.WriteLine(" ABCDEFG");
        Log.WriteLine(" ABCDEFG --cache");
        Log.WriteLine(" └ Generate Asset ID cache from given GTFO_Data path");
        Log.WriteLine();
        Log.WriteLine(" ABCDEFG <file/folder path> <file/folder path 2> ...");
        Log.WriteLine(" ABCDEFG [--option] <file/folder path> [--option] <file/folder path 2> ...");
        Log.WriteLine(" └ Compress given file(s) as LZMA (or option driven) compression");
        Log.WriteLine("--------");
        Log.WriteLine("Options:");
        Log.WriteLine(" --lzma\t\t; Set trailing files compression to LZMA");
        Log.WriteLine(" --lz4\t\t; Set trailing files compression to LZ4");
        Log.WriteLine(" --lz4hc\t\t; Set trailing files compression to LZ4HC");
        Log.WriteLine(" --nocomp\t; Don't compress trailing files (Rerouting still works)");
        Log.WriteLine($" --{ArgumentsParser.OPT_EXCLUDE} <regex> ; Exclude trailing files that match RegEx pattern (Default: None)");
        Log.WriteLine($" --{ArgumentsParser.OPT_OUTPUT_FORMAT} <format> ; Set trailing files output format (Default: {{FileName}}-compressed)");
        Log.WriteLine(" └ Allowed replacement texts: {FileName}");
        Log.WriteLine("--------");
        Log.WriteLine("Example:");
        Log.WriteLine(" ABCDEFG -lzma \"geo_lab_01\" -lz4 \"gearpart_stock\" \"gearpart_front\"");
        Log.WriteLine(" └ This will compress 'geo_lab_01' file as LZMA. 'gearpart_stock' and 'gearpart_front' as LZ4");
        Log.WriteLine("--------");
    }
}
