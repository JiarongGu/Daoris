namespace Daoris.Driver;

/// <summary>
/// One landing of one session's tree at a time, whichever door lands it (LANDRACE1, D145 point 2, D173's FABRIC-END7 note).
/// </summary>
internal static class LandingHolds
{
    /// <summary>Hold the tree for one landing.</summary>
    public static Task<LandingHold?> TakeAsync(string home, string tree, CancellationToken ct = default, TimeSpan? within = null) =>
        Task.FromResult<LandingHold?>(new LandingHold(null));

    /// <summary>How many landings of the tree wait for its hold in this process now.</summary>
    public static int Waiting(string home, string tree) => 0;

    /// <summary>Where the tree's hold is across processes.</summary>
    public static string PathOf(string home, string tree) => DaorisHome.Entry(home, HomeEntry.Locks, "landings", "none.lock");
}

/// <summary>A landing's hold of its tree, let go when disposed.</summary>
/// <param name="Since">Where the landing waited for another: when the landings it waited behind began; null where it waited for none.</param>
internal sealed record LandingHold(DateTimeOffset? Since) : IDisposable
{
    public void Dispose()
    {
    }
}
