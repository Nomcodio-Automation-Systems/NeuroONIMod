using System;
using System.IO;
using System.Reflection;

// Module initializer support for older target frameworks (net472)
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}

namespace NeuroMod.Tests.AssemblySetup
{
    internal static class EarlyUnityAssemblyResolver
    {
        [System.Runtime.CompilerServices.ModuleInitializer]
        public static void Init()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromLibFolder;
        }

        private static Assembly? ResolveFromLibFolder(object? sender, ResolveEventArgs args)
        {
            try
            {
                string requested = new AssemblyName(args.Name).Name + ".dll";

                // Start search from the test assembly base directory and walk up
                string dir = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
                var cur = new DirectoryInfo(dir);
                while (cur != null)
                {
                    string candidate = Path.Combine(cur.FullName, "lib", requested);
                    if (File.Exists(candidate))
                    {
                        return Assembly.LoadFrom(candidate);
                    }
                    cur = cur.Parent;
                }
            }
            catch
            {
                // swallow and let default resolution fail
            }
            return null;
        }
    }
}
