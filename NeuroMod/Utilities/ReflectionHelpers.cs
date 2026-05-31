#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NeuroSdk.Utilities;

/// <summary>
/// Provides reflection-based helpers for discovering and instantiating runtime types.
/// </summary>
/// <pre>The current AppDomain may contain assemblies that are only partially loadable.</pre>
/// <post>Discovery skips unloadable or non-instantiable types instead of failing the entire scan.</post>
internal static class ReflectionHelpers
{
    /// <summary>
    /// Enumerates all concrete implementations of <typeparamref name="T"/> that can be created from the current AppDomain.
    /// </summary>
    /// <typeparam name="T">The service or component type to discover.</typeparam>
    /// <param name="parent">The transform that becomes the parent for dynamically created component instances.</param>
    /// <returns>A lazy sequence of instantiated implementations of <typeparamref name="T"/>.</returns>
    /// <pre><paramref name="parent"/> is a valid transform that can own newly created GameObjects for discovered components.</pre>
    /// <post>Only successfully instantiated, non-abstract implementations of <typeparamref name="T"/> are returned.</post>
    public static IEnumerable<T> GetAllInDomain<T>(Transform parent)
    {
        IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(GetLoadableTypes)
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(T).IsAssignableFrom(type));

        foreach (Type type in types)
        {
            if (TryCreateInstance<T>(type, parent, out T? instance) && instance is not null)
            {
                yield return instance;
            }
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    private static bool TryCreateInstance<T>(Type type, Transform parent, out T? instance)
    {
        try
        {
            if (type.GetMethod("CreateInstance", BindingFlags.Static | BindingFlags.Public) is { } method)
            {
                object? createdByFactory = method.Invoke(null, null);
                if (createdByFactory is T factoryInstance)
                {
                    instance = factoryInstance;
                    return true;
                }
            }

            if (typeof(Component).IsAssignableFrom(type))
            {
                GameObject obj = new(type.FullName ?? type.Name);
                obj.transform.SetParent(parent);
                instance = (T)(object)obj.AddComponent(type);
                return true;
            }

            object? created = Activator.CreateInstance(type);
            if (created is T typedInstance)
            {
                instance = typedInstance;
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ReflectionHelpers] Failed to instantiate {type.FullName ?? type.Name}: {ex.Message}");
        }

        instance = default;
        return false;
    }
}