using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Simple caching system for NeuroMod performance optimization
/// Caches frequently requested data with configurable expiration
/// </summary>
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
public class CacheStats
{
    public int TotalEntries { get; set; }
    public bool IsEnabled { get; set; }
    public int DefaultExpirationSeconds { get; set; }

    public override string ToString()
    {
        return $"Cache Stats: {TotalEntries} entries, Enabled: {IsEnabled}, Default expiration: {DefaultExpirationSeconds}s";
    }
}