using System.Reflection;
using System.Runtime.InteropServices;

namespace CryPhysics.Native;

public static class NativeLibraryResolver
{
    private static bool s_registered;
    private static readonly object s_lock = new();

    public static void Register()
    {
        if (s_registered) return;
        lock (s_lock)
        {
            if (s_registered) return;
            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
            s_registered = true;
        }
    }

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != NativeMethods.Lib) return nint.Zero;

        var rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "CryPhysics.dll" : "libCryPhysics.so";

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "runtimes", rid, "native", fileName),
            Path.Combine(baseDir, fileName),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
            {
                return handle;
            }
        }

        return nint.Zero;
    }
}
