using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace ABCDEFG;

internal static class AssemblyResolver
{
    static string LibPath;

    [ModuleInitializer]
    internal static void Init()
    {
        LibPath = Path.Combine(AppContext.BaseDirectory, "lib");
        AssemblyLoadContext.Default.Resolving += Resolve;
    }

    private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        var file = Path.Combine(LibPath, $"{name.Name}.dll");
        if (File.Exists(file))
            return context.LoadFromAssemblyPath(file);

        return null;
    }
}
