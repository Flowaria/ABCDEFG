using AssetsTools.NET.Extra;

namespace ABCDEFG.Utils;

internal sealed class AssetsManagerHolder
{
    public AssetsManager Manager;
    public bool IsDBLoaded = false;
}

internal static class AssetsManagerPool
{
    static readonly ThreadLocal<AssetsManagerHolder> s_manager = new(() =>
    {
        var manager = new AssetsManager();
        manager.LoadClassPackage(Path.Combine(Paths.BasePath, "lib", "classdata.tpk"));
        return new()
        {
            Manager = manager
        };
    });

    public static AssetsManager GetInstance()
    {
        return s_manager.Value.Manager;
    }

    public static void LoadClassDatabaseOf(string unityVersion)
    {
        var managerHolder = s_manager.Value;
        if (!managerHolder.IsDBLoaded)
        {
            managerHolder.Manager.LoadClassDatabaseFromPackage(unityVersion);
            managerHolder.IsDBLoaded = true;
        }
    }
}
