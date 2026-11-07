using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Which run a door's driven start is held on: the transport, unless the driver carries Lyntai's agent layer,
/// whose provider folders bind their door's driven kind to the layer's run when the driver loads. Asked for a native door's driven start
/// alone; a conversation and a protocol door stay on the transport.
/// </summary>
internal static class DrivenRuns
{
    private static readonly ConcurrentDictionary<string, Func<ITransportHost, TransportStart, SpawnRequest, HeldRun?>> Bound =
        new(StringComparer.Ordinal);

    /// <summary>The bound run for <paramref name="door"/>'s driven start, or null where its driven kind is today's transport's.</summary>
    public static HeldRun? Of(string door, ITransportHost host, TransportStart start, SpawnRequest request) =>
        Bound.TryGetValue(door, out var open) ? open(host, start, request) : null;

    /// <summary>Whether <paramref name="door"/>'s driven kind is bound.</summary>
    public static bool Binds(string door) => Bound.ContainsKey(door);

    /// <summary>
    /// Binds <paramref name="door"/>'s driven kind to <paramref name="open"/>, once, before any start: null from it keeps that start on today's
    /// transport.
    /// </summary>
    internal static void Bind(string door, Func<ITransportHost, TransportStart, SpawnRequest, HeldRun?> open)
    {
        if (!Bound.TryAdd(door, open)) throw new InvalidOperationException($"the driven kind of '{door}' is bound twice.");
    }
}
