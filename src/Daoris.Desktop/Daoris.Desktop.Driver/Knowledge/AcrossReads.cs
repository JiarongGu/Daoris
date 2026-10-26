namespace Daoris.Driver;

/// <summary>What said whether a checkout is read across: set for its repository, for its workspace, or neither.</summary>
public enum AcrossSource
{
    Default,
    Workspace,
    Repository,
}

/// <summary>Whether a repository's checkout may be read by agents outside it, and what said so (D107).</summary>
public sealed record AcrossReading(bool Read, AcrossSource Source);

/// <summary>A registered checkout on this machine, by its repository's name and where it is.</summary>
public sealed record AcrossCheckout(string Repository, string Path);

/// <summary>
/// Whether a checkout may be read by agents outside it, and where a path lies (READ1, D107; FOUNDATION1 mod-viii, the design §14.3): the
/// reading of the settings for one checkout, the readable checkouts of a registry and a path's place under another. What the room says
/// of the checkouts it may read, and the checkouts' own judge, ask these, which are the agents' too: <see cref="AcrossRules"/> answers
/// through them, so every reader of the rules keeps its spelling and the rules those readers are handed are the agents' alone.
/// </summary>
public static class AcrossReads
{
    /// <summary>Whether <paramref name="repository"/>'s checkout may be read across, and what said so.</summary>
    /// <param name="workspace">Its circle; unstated is the default circle, as everywhere.</param>
    public static AcrossReading Reading(DriverConfig config, string repository, string? workspace)
    {
        if (config.ReadAcross.TryGetValue(repository.Trim(), out var own)) return new(own, AcrossSource.Repository);
        return config.WorkspaceReadAcross.TryGetValue(Circle(workspace), out var shared)
            ? new(shared, AcrossSource.Workspace)
            : new(true, AcrossSource.Default);
    }

    /// <summary>
    /// The checkouts Ask Daoris may read: each readable one of <paramref name="registry"/>, sorted by name; every workspace's for
    /// <c>default</c>'s room, one workspace's for its own (ASKSCOPE1c).
    /// </summary>
    public static IReadOnlyList<AcrossCheckout> Readable(DriverConfig config, IEnumerable<RepoView> registry) =>
        Of(Checkouts(registry).Where(known => Reading(config, known.Repository, known.Workspace).Read));

    /// <summary>Whether <paramref name="inner"/> is <paramref name="outer"/> or lies under it, on this machine's paths.</summary>
    public static bool Holds(string outer, string inner) => Holds(outer, inner, OperatingSystem.IsWindows());

    /// <summary>The same question with the platform named, so the answer is the same on any machine that tests it.</summary>
    public static bool Holds(string outer, string inner, bool windows)
    {
        static string Norm(string path) => path.Replace('\\', '/').TrimEnd('/');
        var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var (a, b) = (Norm(outer), Norm(inner));
        return string.Equals(a, b, comparison) || b.StartsWith(a + "/", comparison);
    }

    internal static IEnumerable<RepoView> Checkouts(IEnumerable<RepoView> registry) =>
        registry.Where(known => known.Root is { Length: > 0 });

    internal static IReadOnlyList<AcrossCheckout> Of(IEnumerable<RepoView> known) =>
        [.. known.OrderBy(k => k.Repository, StringComparer.Ordinal).Select(k => new AcrossCheckout(k.Repository, k.Root!))];

    internal static string Circle(string? workspace) =>
        workspace is { } named && !string.IsNullOrWhiteSpace(named) ? named.Trim() : RemoteTarget.DefaultWorkspace;
}
