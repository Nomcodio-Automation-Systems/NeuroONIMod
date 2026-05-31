using System;
using System.Collections.Concurrent;

namespace NeuroMod.Api
{
    /// <summary>
    /// Simple time-based throttler to prevent log spam of identical messages.
    /// </summary>
    /// <pre>Callers provide a stable key for the log category they want to suppress temporarily.</pre>
    /// <post>Repeated log attempts within the configured interval are filtered for the same key.</post>
    internal static class LogThrottler
    {
        private static readonly ConcurrentDictionary<string, global::System.DateTime> _last = new();

        /// <summary>
        /// Determines whether a message identified by <paramref name="key"/> should be logged now.
        /// </summary>
        /// <param name="key">Stable key representing the message family to throttle.</param>
        /// <param name="minInterval">Minimum interval that must elapse before the same key may log again.</param>
        /// <returns><see langword="true"/> when logging should proceed; otherwise, <see langword="false"/>.</returns>
        /// <pre><paramref name="key"/> is non-empty and maps repeated call sites to the same throttle bucket.</pre>
        /// <post>The current attempt updates the stored timestamp only when logging is allowed.</post>
        public static bool ShouldLog(string key, TimeSpan minInterval)
        {
            global::System.DateTime now = global::System.DateTime.UtcNow;
            global::System.DateTime last = _last.GetOrAdd(key, global::System.DateTime.MinValue);
            if (now - last < minInterval)
            {
                return false;
            }

            _last[key] = now;
            return true;
        }
    }
}
