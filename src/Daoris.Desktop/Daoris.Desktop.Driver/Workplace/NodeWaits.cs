using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The last reason each run's node was said to wait for, so a node that does nothing at a look says why in the machine log once per change
/// of reason, never at every pass: the landing's (<see cref="LandWaits"/>) and the check's start's. Kept in memory, as the tries are: a
/// restart says each once more.
/// </summary>
public static class NodeWaits
{
    // The last reason said for each node, by home, run unit and node.
    private static readonly ConcurrentDictionary<string, string> Last = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="reason"/> is not the last one said for the node, which it then is.</summary>
    public static bool Changed(string home, string unit, string node, string reason)
    {
        var key = Key(home, unit, node);
        if (string.Equals(Last.GetValueOrDefault(key), reason, StringComparison.Ordinal)) return false;
        Last[key] = reason;
        return true;
    }

    /// <summary>The node came to a try, which says itself: its next wait is said again, of any reason.</summary>
    public static void Tried(string home, string unit, string node) => Last.TryRemove(Key(home, unit, node), out _);

    private static string Key(string home, string unit, string node) => $"{home}\n{unit}\n{node}";
}
