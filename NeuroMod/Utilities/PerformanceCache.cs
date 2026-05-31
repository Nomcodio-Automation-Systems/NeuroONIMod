using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Simple caching system for NeuroMod performance optimization
/// Caches frequently requested data with configurable expiration
/// </summary>
/// <pre>
/// The configuration layer can be queried for caching settings and callers provide stable cache keys.
/// </pre>
/// <post>
/// Frequently reused values can be cached, expired, and inspected through a singleton cache instance.
/// </post>
public class PerformanceCache
{
    private static PerformanceCache? _instance;
    public static PerformanceCache Instance => _instance ??= new PerformanceCache();

    private readonly Dictionary<string, CacheEntry> _cache = [];
    private readonly object _lockObject = new();

    private PerformanceCache()
    { }

    /// <summary>
    /// Get cached value or compute it if not cached/expired
    /// </summary>
    /// <param name="key">Cache key identifying the computed value.</param>
    /// <param name="computeFunc">Factory used when no valid cached value exists.</param>
    /// <param name="customExpirationSeconds">Optional expiration override in seconds.</param>
    /// <pre>
    /// <paramref name="key"/> is stable for the value domain and <paramref name="computeFunc"/> is safe to execute under the cache lock.
    /// </pre>
    /// <post>
    /// A valid cached value is returned or a newly computed value is stored and returned.
    /// </post>
    public T GetOrCompute<T>(string key, Func<T> computeFunc, int? customExpirationSeconds = null)
    {
        if (!IsCachingEnabled())
        {
            return computeFunc();
        }

        lock (_lockObject)
        {
            // Check if cached value exists and is still valid
            if (_cache.TryGetValue(key, out CacheEntry? entry))
            {
                if (!IsExpired(entry))
                {
                    if (entry.Value is T cachedValue)
                    {
                        Debug.Log($"[PerformanceCache] Cache HIT for key: {key}");
                        return cachedValue;
                    }
                }
                else
                {
                    // Remove expired entry
                    _cache.Remove(key);
                }
            }

            // Compute new value and cache it
            Debug.Log($"[PerformanceCache] Cache MISS for key: {key}, computing...");
            T newValue = computeFunc();

            int expirationSeconds = customExpirationSeconds ?? GetDefaultCacheExpiration();
            CacheEntry newEntry = new()
            {
                Key = key,
                Value = newValue,
                CreatedAt = Time.time,
                ExpirationSeconds = expirationSeconds
            };

            _cache[key] = newEntry;
            return newValue;
        }
    }

    /// <summary>
    /// Cache a value directly
    /// </summary>
    /// <param name="key">Cache key identifying the value.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="customExpirationSeconds">Optional expiration override in seconds.</param>
    /// <pre>
    /// Caching is enabled and <paramref name="key"/> identifies the supplied value.
    /// </pre>
    /// <post>
    /// The cache entry for <paramref name="key"/> is created or replaced.
    /// </post>
    public void Set<T>(string key, T value, int? customExpirationSeconds = null)
    {
        if (!IsCachingEnabled())
        {
            return;
        }

        lock (_lockObject)
        {
            int expirationSeconds = customExpirationSeconds ?? GetDefaultCacheExpiration();
            CacheEntry entry = new()
            {
                Key = key,
                Value = value,
                CreatedAt = Time.time,
                ExpirationSeconds = expirationSeconds
            };

            _cache[key] = entry;
            Debug.Log($"[PerformanceCache] Cached value for key: {key}");
        }
    }

    /// <summary>
    /// Get cached value if it exists and is not expired
    /// </summary>
    /// <param name="key">Cache key identifying the value.</param>
    /// <pre>
    /// <paramref name="key"/> may or may not currently exist in the cache.
    /// </pre>
    /// <post>
    /// The cached reference-type value is returned when present and unexpired; otherwise <see langword="null"/> is returned.
    /// </post>
    public T? Get<T>(string key) where T : class
    {
        if (!IsCachingEnabled())
        {
            return null;
        }

        lock (_lockObject)
        {
            if (_cache.TryGetValue(key, out CacheEntry? entry))
            {
                if (!IsExpired(entry) && entry.Value is T value)
                {
                    Debug.Log($"[PerformanceCache] Retrieved cached value for key: {key}");
                    return value;
                }
                else if (IsExpired(entry))
                {
                    _cache.Remove(key);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Check if a key exists in cache and is not expired
    /// </summary>
    /// <param name="key">Cache key to inspect.</param>
    /// <pre>
    /// <paramref name="key"/> may or may not currently exist in the cache.
    /// </pre>
    /// <post>
    /// The method reports whether an unexpired entry exists for the supplied key.
    /// </post>
    public bool Contains(string key)
    {
        if (!IsCachingEnabled())
        {
            return false;
        }

        lock (_lockObject)
        {
            if (_cache.TryGetValue(key, out CacheEntry? entry))
            {
                if (!IsExpired(entry))
                {
                    return true;
                }

                _cache.Remove(key);
            }
            return false;
        }
    }

    /// <summary>
    /// Remove a specific key from cache
    /// </summary>
    /// <param name="key">Cache key to remove.</param>
    /// <pre>
    /// <paramref name="key"/> may or may not currently exist in the cache.
    /// </pre>
    /// <post>
    /// The entry for <paramref name="key"/> is removed when present.
    /// </post>
    public void Remove(string key)
    {
        lock (_lockObject)
        {
            if (_cache.Remove(key))
            {
                Debug.Log($"[PerformanceCache] Removed cached value for key: {key}");
            }
        }
    }

    /// <summary>
    /// Clear all cached values
    /// </summary>
    /// <pre>
    /// The cache may currently contain any number of entries.
    /// </pre>
    /// <post>
    /// All cached entries are removed.
    /// </post>
    public void Clear()
    {
        lock (_lockObject)
        {
            int count = _cache.Count;
            _cache.Clear();
            Debug.Log($"[PerformanceCache] Cleared {count} cached values");
        }
    }

    /// <summary>
    /// Clean up expired entries
    /// </summary>
    /// <pre>
    /// The cache may contain both live and expired entries.
    /// </pre>
    /// <post>
    /// All expired entries are removed from the cache.
    /// </post>
    public void CleanupExpired()
    {
        lock (_lockObject)
        {
            List<string> expiredKeys = [];

            foreach (KeyValuePair<string, CacheEntry> kvp in _cache)
            {
                if (IsExpired(kvp.Value))
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (string key in expiredKeys)
            {
                _cache.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                Debug.Log($"[PerformanceCache] Cleaned up {expiredKeys.Count} expired entries");
            }
        }
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    /// <pre>
    /// The cache may contain any number of current entries.
    /// </pre>
    /// <post>
    /// A snapshot of cache state and configuration is returned.
    /// </post>
    public CacheStats GetStats()
    {
        lock (_lockObject)
        {
            return new CacheStats
            {
                TotalEntries = _cache.Count,
                IsEnabled = IsCachingEnabled(),
                DefaultExpirationSeconds = GetDefaultCacheExpiration()
            };
        }
    }

    /// <summary>
    /// Check if caching is enabled in configuration
    /// </summary>
    private bool IsCachingEnabled()
    {
        return ConfigManager.Instance.Config?.Performance?.EnableCaching == true;
    }

    /// <summary>
    /// Get default cache expiration from configuration
    /// </summary>
    private int GetDefaultCacheExpiration()
    {
        return ConfigManager.Instance.Config?.Performance?.CacheExpiration ?? 60;
    }

    /// <summary>
    /// Check if a cache entry has expired
    /// </summary>
    private bool IsExpired(CacheEntry entry)
    {
        return Time.time - entry.CreatedAt > entry.ExpirationSeconds;
    }

    /// <summary>
    /// Represents a cached entry
    /// </summary>
    private class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        public float CreatedAt { get; set; }
        public int ExpirationSeconds { get; set; }
    }
}

/// <summary>
/// Cache statistics
/// </summary>
/// <pre>
/// Property values describe a point-in-time snapshot of cache state.
/// </pre>
/// <post>
/// Instances can be rendered as a diagnostic summary string.
/// </post>
public class CacheStats
{
    public int TotalEntries { get; set; }
    public bool IsEnabled { get; set; }
    public int DefaultExpirationSeconds { get; set; }

    /// <summary>
    /// Returns a human-readable summary of the cache snapshot.
    /// </summary>
    /// <returns>A formatted cache summary string.</returns>
    /// <pre>
    /// The statistics properties have already been populated.
    /// </pre>
    /// <post>
    /// A human-readable summary of the cache state is returned.
    /// </post>
    public override string ToString()
    {
        return $"Cache Stats: {TotalEntries} entries, Enabled: {IsEnabled}, Default expiration: {DefaultExpirationSeconds}s";
    }
}