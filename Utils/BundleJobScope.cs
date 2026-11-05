using System.Diagnostics;

namespace ABCDEFG.Utils;

internal readonly ref struct BundleJobScope : IDisposable
{
    private readonly string _BundleName;
    private readonly string _JobName;
    private readonly long _StartTime;

    public BundleJobScope(string bundleName, string jobName)
    {
        _BundleName = bundleName;
        _JobName = jobName;
        _StartTime = Stopwatch.GetTimestamp();
        Log.WriteLine($"[{_BundleName}] Start '{_JobName}'");
    }

    public void Dispose()
    {
        Log.WriteLine($"[{_BundleName}] Done '{_JobName}' (took {Stopwatch.GetElapsedTime(_StartTime).TotalSeconds:0.000}s)");
    }
}
