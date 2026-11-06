namespace ABCDEFG;

internal static class Paths
{
    internal static readonly string BasePath = AppContext.BaseDirectory;
    internal static readonly string LibPath = Path.Combine(BasePath, "lib");
    internal static readonly string CachePath = Path.Combine(BasePath, "cache");

    internal static readonly string ConfigFile = Path.Combine(BasePath, "config.json");
    internal static readonly string LogFile = Path.Combine(BasePath, "output.log");
    internal static readonly string ErrorLogFile = Path.Combine(BasePath, "error.log");
}
