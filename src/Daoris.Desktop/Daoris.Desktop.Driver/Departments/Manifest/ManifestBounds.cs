namespace Daoris.Driver;

/// <summary>
/// How large a folder's manifest may be before the place is too large to follow: the files it lists and the time its walk and hashes
/// take. Past either, the manifest records nothing but that, and the place is still worked in place. <see cref="Copy"/> bounds the
/// files a session set apart copies, which a folder past it is refused.
/// </summary>
/// <param name="Files">The most files a manifest lists.</param>
/// <param name="Time">The longest a manifest's read takes.</param>
public sealed record ManifestBounds(int Files, TimeSpan Time)
{
    /// <summary>A copy's bound, 2 GB, as the place-work design proposes it.</summary>
    public const long CopyDefault = 2L << 30;

    /// <summary>250,000 files and five minutes, as the place-work design proposes them.</summary>
    public static ManifestBounds Default { get; } = new(250_000, TimeSpan.FromMinutes(5));

    /// <summary>The most bytes a folder's followed files may hold together and still be copied for a session working apart.</summary>
    public long Copy { get; init; } = CopyDefault;

    /// <summary>The most files a folder may hold and still be copied, 20,000 as the design proposes: each costs the copy its own creation.</summary>
    public int CopyFiles { get; init; } = 20_000;

    /// <summary>The bounds this machine's <c>driver.json</c> sets under <paramref name="home"/>, each the default where it sets none.</summary>
    public static ManifestBounds Of(string home) => DriverConfig.Load(DriverConfig.ResolvePath(home)).Manifest.Bounds;
}
