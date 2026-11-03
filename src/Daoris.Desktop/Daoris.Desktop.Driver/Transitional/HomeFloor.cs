namespace Daoris.Driver;

/// <summary>
/// The build an entry kept for an older build waits out (ONEWAY1ad-ii; the oneway design §1.3): the last that writes the old way, named by its
/// release's version, or by the desktop store's writer stamp for what that store holds. Neither while no release names it, so nothing passes it.
/// </summary>
/// <param name="Said">The build in words, as the list says it.</param>
public sealed record OlderBuild(string Said)
{
    /// <summary>The release whose writes are waited out; the floor passes it once it is newer.</summary>
    public string? Version { get; init; }

    /// <summary>For what the desktop store holds: the writer stamp whose writers are waited out; passed once the store's stamp is above it.</summary>
    public int? StoreWriter { get; init; }

    /// <summary>
    /// Builds older than every build that stamps its receipt (ONEWAY1c-ii): passed by any floor that names a build, since a receipt carries its
    /// build only from the parts' door on, which every such build lacks.
    /// </summary>
    public bool BeforeStamps { get; init; }
}

/// <summary>
/// The floor: the oldest build that can still write this home (ONEWAY1ad-ii; the oneway design §1.3). While an applied conversion stands no
/// build below the running one is staged, so the build stamped in the oldest standing receipt is a floor; a receipt with no build gives none.
/// The desktop store's writer stamp is a floor for what that store holds, since an older build reads it and writes nothing there.
/// </summary>
/// <param name="Build">The oldest standing receipt's build; null where none stands or it holds no build.</param>
/// <param name="StoreWriter">The desktop store's writer stamp; null where the home has no store, or it does not read.</param>
public sealed record HomeFloor(string? Build, int? StoreWriter)
{
    /// <summary>The floor of <paramref name="home"/>, read from its receipts and its store; it writes nothing.</summary>
    public static HomeFloor Of(string home)
    {
        string? build;
        try
        {
            build = new HomeReceipts(home).All().FirstOrDefault(each => each.Stands)?.Build;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            build = null;
        }

        return new(build, StoreStamp(home));
    }

    private static int? StoreStamp(string home)
    {
        try
        {
            using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
            return store is null ? null : store.Writer;
        }
        catch (Exception error) when (error is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }

    /// <summary>Whether no build that writes the old way <paramref name="waits"/> names can write this home any more.</summary>
    public bool Passes(OlderBuild waits) =>
        waits.BeforeStamps ? Build is { Length: > 0 }
        : waits.StoreWriter is { } stamp
            ? StoreWriter > stamp
            : waits.Version is { } version && InstallRollback.Numbered(Build) is { } floor && InstallRollback.Numbered(version) is { } waited && floor > waited;
}
