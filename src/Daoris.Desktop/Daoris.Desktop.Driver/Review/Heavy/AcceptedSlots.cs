using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// How many heavy runs at once a repository declares (<see cref="HeavyGates.SlotsKey"/>), read for every caller from the declaration the
/// person accepted for it (<see cref="IntegrationAcceptance.Of"/>), never from the file of the branch, tree or checkout the run is in: a
/// branch that raises <c>heavySlots</c> in its own <c>daoris.gates.json</c> gets the line's count, as a branch's own change to a command
/// never runs (<see cref="CheckDeclaration"/>). The accepted blob is read from git's object store, which every tree of a repository shares,
/// and a blob never changes, so what is read of one is kept.
/// </summary>
public static class AcceptedSlots
{
    private static readonly ConcurrentDictionary<string, int?> Read = new(StringComparer.Ordinal);

    /// <summary>
    /// The slots the declaration at <paramref name="blob"/> declares, read in the git checkout at <paramref name="checkout"/>; null where
    /// nothing is accepted (null, or <see cref="IntegrationAcceptance.None"/>), it declares none, or git cannot give it: no more than the
    /// machine admits, which is never more than a branch could raise it to.
    /// </summary>
    public static async Task<int?> OfBlobAsync(string checkout, string? blob, CancellationToken ct)
    {
        if (blob is null || blob == IntegrationAcceptance.None || !IntegrationGatesKey.IsAcceptance(blob)) return null;
        if (Read.TryGetValue(blob, out var known)) return known;
        if (await IntegrationAcceptance.TextAsync(checkout, blob, ct).ConfigureAwait(false) is not { } text) return null;
        return Read[blob] = HeavyGates.SlotsIn(text);
    }

    /// <summary>The same for the blob this machine accepted for <paramref name="repository"/>, by the name every door keys its settings by.</summary>
    public static Task<int?> OfRepositoryAsync(string home, string repository, string checkout, CancellationToken ct) =>
        OfBlobAsync(checkout, IntegrationAcceptance.Of(DriverConfig.Load(DriverConfig.ResolvePath(home)), repository), ct);
}
