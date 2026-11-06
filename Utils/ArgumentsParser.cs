using AssetsTools.NET;
using System.Text;
using System.Text.RegularExpressions;

namespace ABCDEFG.Utils;

internal static class ArgumentsParser
{
    public const string OPT_NOCOMP = "nocomp";
    public const string OPT_LZMA = "lzma";
    public const string OPT_LZ4 = "lz4";
    public const string OPT_LZ4HC = "lz4hc";

    public const string OPT_OUTPUT_FORMAT = "output-format";
    public const string OPT_EXCLUDE = "exclude";
    public const string OPT_WRITE_METADATA = "write-metadata";

    public static Dictionary<string, ReroutingFileOptions> Parse(string[] args)
    {
        var parsed = new Dictionary<string, ReroutingFileOptions>(FileStringComparator());
        var shouldParseData = false;
        var parsingOptionName = "";
        var compression = AssetBundleCompressionType.LZMA;
        var optionsData = new Dictionary<string, string>
        {
            [OPT_OUTPUT_FORMAT] = "{filename}-compressed",
            [OPT_EXCLUDE] = "",
            [OPT_WRITE_METADATA] = "true",
        };

        Regex excludeRegex = null;
        foreach (var arg in args)
        {
            if (shouldParseData)
            {
                optionsData[parsingOptionName] = arg;
                if (parsingOptionName == OPT_EXCLUDE)
                {
                    if (string.IsNullOrEmpty(arg))
                        excludeRegex = null;
                    else
                        excludeRegex = new Regex(arg);
                }

                shouldParseData = false;
                continue;
            }

            if (arg.StartsWith("--"))
            {
                var optionName = arg[2..].ToLower();
                ParseOptions(optionName, optionsData, ref compression, ref shouldParseData);
                if (shouldParseData)
                {
                    parsingOptionName = optionName;
                }
                continue;
            }

            var fullPath = Path.GetFullPath(arg);
            if (Directory.Exists(arg))
            {
                foreach (var file in Directory.GetFiles(fullPath).Where(IsUnityFSFile))
                {
                    AddFileToReroute(fullPath);
                }
            }

            if (IsUnityFSFile(fullPath))
            {
                AddFileToReroute(fullPath);
            }
        }

        void AddFileToReroute(string fullPath)
        {
            if (excludeRegex != null)
            {
                if (excludeRegex.IsMatch(Path.GetFileName(fullPath)))
                {
                    return;
                }
            }

            parsed.Add(fullPath, new ReroutingFileOptions()
            {
                Compression = compression,
                OutputFormat = optionsData[OPT_OUTPUT_FORMAT],
                GenerateMetadata = bool.Parse(optionsData[OPT_WRITE_METADATA])
            });
        }

        return parsed;
    }

    static void ParseOptions(string optionName, Dictionary<string, string> optionValues, ref AssetBundleCompressionType compressionType, ref bool shouldParseData)
    {
        switch (optionName)
        {
            case OPT_LZMA:
                compressionType = AssetBundleCompressionType.LZMA;
                return;

            case OPT_LZ4:
                compressionType = AssetBundleCompressionType.LZ4;
                return;

            case OPT_LZ4HC:
                compressionType = AssetBundleCompressionType.LZ4Fast;
                return;

            case OPT_NOCOMP:
                compressionType = AssetBundleCompressionType.None;
                return;
        }

        if (optionValues.ContainsKey(optionName))
        {
            shouldParseData = true;
            return;
        }
        else
        {
            throw new ArgumentException("Argument is invalid!", $"--{optionName}");
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
}