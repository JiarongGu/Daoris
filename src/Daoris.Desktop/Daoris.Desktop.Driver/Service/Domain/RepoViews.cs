namespace Daoris.Driver;

/// <summary>A registration as the service answered it. The root is present only from a local service.</summary>
/// <param name="Workspace">
/// The circle this repository is wired into on THIS machine — the registry row, which is the
/// only honest source for it. The toolchain reads it to pick a workspace's credential profile:
/// a work account for the work circle, a personal one at home.
/// </param>
public sealed record RepoView(
    string Repository, bool Adopted, string? Root, string Workspace = RemoteTarget.DefaultWorkspace)
{
    /// <summary>
    /// The ids of the lanes the repository declares, as the registry answers them; none from a host without lanes.
    /// Heard in <see cref="Repositories.Signature"/>, so a lanes-only registration moves the tick.
    /// </summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];

    /// <summary>The person's own globs a folder's manifest passes by, beside its defaults, as its registration keeps them.</summary>
    public IReadOnlyList<string> Excludes { get; init; } = [];

    /// <summary>Its kind (<see cref="PlaceKind"/>), a repository's where the host does not say.</summary>
    public string Kind { get; init; } = PlaceKind.Git;

    /// <summary>A system department's adapter as its registration names it; null for any other kind.</summary>
    public SystemPlace? System { get; init; }
}

/// <summary>One lane of a repository, as the registry answers it: what a quest addresses as `repository:id`.</summary>
/// <param name="Steward">The one lane that keeps the repository's records.</param>
public sealed record LaneView(string Id, string Title, string Summary, bool Steward);
