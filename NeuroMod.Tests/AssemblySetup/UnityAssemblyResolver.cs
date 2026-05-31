using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace NeuroMod.Tests.AssemblySetup;

[SetUpFixture]
public class UnityAssemblyResolver
{
    [OneTimeSetUp]
    public void RegisterResolver()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFromLibFolder;
    }

    private Assembly? ResolveFromLibFolder(object? sender, ResolveEventArgs args)
    {
        try
        {
            string requested = new AssemblyName(args.Name).Name + ".dll";

            // Walk up from the test assembly base directory to find a lib folder
            string dir = AppDomain.CurrentDomain.BaseDirectory;
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
