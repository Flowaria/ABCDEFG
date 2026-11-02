using System.Diagnostics;

namespace ABCDEFG;

internal static partial class Program
{
    static void LoadAllCache()
    {
        var start = Stopwatch.GetTimestamp();
        s_fileIndexCache.ReadAll();
        Log.WriteLine($"Loaded FileIndex cache (took {Stopwatch.GetElapsedTime(start).TotalSeconds:0.000}s)");

        foreach (var reroutableCache in s_reroutableCaches.Values)
        {
            start = Stopwatch.GetTimestamp();
            reroutableCache.ReadAll();
            Log.WriteLine($"Loaded {reroutableCache.AssetTypeName} cache (took {Stopwatch.GetElapsedTime(start).TotalSeconds:0.000}s)");
        }
    }
}
