using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace ABCDEFG;

internal static class AssemblyResolver
{
    [ModuleInitializer]
    internal static void Init()
    {
        AssemblyLoadContext.Default.Resolving += Resolve;
    }

    private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        var file = Path.Combine(Paths.LibPath, $"{name.Name}.dll");
        if (File.Exists(file))
            return context.LoadFromAssemblyPath(file);

        return null;
    }
}
