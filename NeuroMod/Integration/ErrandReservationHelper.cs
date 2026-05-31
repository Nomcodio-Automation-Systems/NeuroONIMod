using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NeuroMod.Integration;

/// <summary>
/// Helper class to manage chore reservations for the Neuro duplicate.
/// Prevents other duplicates from taking assigned errands.
/// </summary>
/// <pre>
/// Callers coordinate reservations using the same live <see cref="Chore"/> instances.
/// </pre>
/// <post>
/// The helper maintains a process-local set of reserved chores for Neuro-specific errand coordination.
/// </post>
public static class ErrandReservationHelper
{
    // Track reserved chore references directly to avoid unstable hash codes
    private static readonly HashSet<Chore> _reservedChores = new();

    /// <summary>
    /// Reserve a chore for Neuro (prevent other duplicates from taking it).
    /// </summary>
    /// <param name="chore">The chore to reserve</param>
    /// <returns>True if successfully reserved, false if already reserved or null</returns>
    /// <pre>chore may be null</pre>
    /// <post>If successful, the chore reference is added to the reservation set.</post>
    public static bool ReserveChore(Chore chore)
    {
        if (chore == null) return false;

        // Use GetInstanceID which is stable for UnityEngine.Object instances
        if (_reservedChores.Contains(chore)) return false;

        _reservedChores.Add(chore);
        LogReserved(chore, chore.GetHashCode());
        return true;
    }

    /// <summary>
    /// Logs a chore reservation. Isolated to prevent JIT from resolving
    /// ChoreType when compiling ReserveChore (avoids UnityEngine.CoreModule load).
    /// </summary>
    /// <param name="chore">The reserved chore.</param>
    /// <param name="choreId">The local identifier used in reservation logging.</param>
    /// <pre><paramref name="chore"/> has just been added to the reservation set.</pre>
    /// <post>A reservation log entry has been emitted for the supplied chore.</post>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LogReserved(Chore chore, int choreId)
    {
        NeuroLogger.Log($"Reserved chore {chore.choreType.Name} (ID: {choreId})", "Reservation");
    }

    /// <summary>
    /// Release a chore reservation.
    /// </summary>
    /// <param name="chore">The chore to release</param>
    /// <pre>chore may be null</pre>
    /// <post>If the chore was reserved, its reference is removed from the reservation set.</post>
    public static void ReleaseChore(Chore chore)
    {
        if (chore == null) return;

        if (_reservedChores.Remove(chore))
        {
            LogReleased(chore, chore.GetHashCode());
        }
    }

    /// <summary>
    /// Logs a chore release. Isolated to prevent JIT from resolving
    /// ChoreType when compiling ReleaseChore (avoids UnityEngine.CoreModule load).
    /// </summary>
    /// <param name="chore">The released chore.</param>
    /// <param name="choreId">The local identifier used in release logging.</param>
    /// <pre><paramref name="chore"/> has just been removed from the reservation set.</pre>
    /// <post>A release log entry has been emitted for the supplied chore.</post>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LogReleased(Chore chore, int choreId)
    {
        NeuroLogger.Log($"Released chore {chore.choreType.Name} (ID: {choreId})", "Reservation");
    }

    /// <summary>
    /// Check if a chore is reserved.
    /// </summary>
    /// <param name="chore">The chore to check</param>
    /// <returns>True if the chore is reserved, false otherwise</returns>
    /// <pre>chore may be null</pre>
    /// <post>The method reports whether the current reservation set contains the supplied chore.</post>
    public static bool IsReserved(Chore chore)
    {
        return chore != null && _reservedChores.Contains(chore);
    }

    /// <summary>
    /// Clear all reservations.
    /// </summary>
    /// <pre>
    /// Callers no longer need any outstanding reservation entries.
    /// </pre>
    /// <post>
    /// The reservation set is empty.
    /// </post>
    public static void ClearAll()
    {
        _reservedChores.Clear();
        NeuroLogger.Log("Cleared all chore reservations", "Reservation");
    }
}
