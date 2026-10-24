using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What KEEPSIZE1b's two Process classes share (<see cref="KeepSizeGitTests"/>, <see cref="KeepSizeStopsGitTests"/>): the look's tidy of
/// <c>engine</c>, the record pass over a door that keeps or refuses as the test says, and the purge with or without the registry's checkout
/// for the hour's holding check. Split in two so neither class is the git half's wall (PROCSPEED1b).
/// </summary>
public abstract class KeepSizeFixture : BranchTidyingFixture
{
    private protected readonly List<FinishedPost> _posts = [];
    private protected string? _refused;

    protected static Task<IReadOnlySet<string>> NoneRunning(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>The service's door as the record pass is handed it: kept, or refused with <see cref="_refused"/>.</summary>
    private protected Task<RecordRefusal?> PostAsync(FinishedPost post, CancellationToken ct)
    {
        if (_refused is { } why) return Task.FromResult<RecordRefusal?>(new(why));
        _posts.Add(post);
        return Task.FromResult<RecordRefusal?>(null);
    }

    /// <summary>The registry's checkout of <c>engine</c>, <paramref name="root"/>; null where the test says it names none.</summary>
    protected static Func<string, string, string?> Roots(string? root) => (_, repository) => repository == "engine" ? root : null;

    /// <summary>The look's own tidy of <c>engine</c>: the one branch it removed, its folder moved aside.</summary>
    protected static async Task<string> SetAsideAsync(SessionTrees trees, string root)
    {
        var pass = await trees.TidyEmptyAsync(root, "engine", "default", NoneRunning);
        var removed = Assert.Single(pass.Results, result => result.Removed);
        return Assert.IsType<string>(removed.MovedTo);
    }

    private protected static SlimManifest Manifest(string folder) => Assert.IsType<SlimManifest>(SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest);

    /// <summary>The record pass at <paramref name="now"/>, the registry naming <paramref name="root"/> as <c>engine</c>'s checkout.</summary>
    protected async Task RecordAsync(SessionTrees trees, string? root, DateTimeOffset now) =>
        await trees.RecordSetAsideAsync(Roots(root), PostAsync, () => now, CancellationToken.None);

    /// <summary>The Keeping pass's set-aside removal at <paramref name="now"/>; with <paramref name="root"/>, the hour's holding checks run there.</summary>
    private protected static async Task<IReadOnlyList<SetAside>> PurgeAsync(SessionTrees trees, DateTimeOffset now, string? root = null, KeepPolicy? policy = null) =>
        (await trees.KeepSetAsideAsync(policy ?? KeepPolicy.Default, now, TimeSpan.FromMinutes(5), CancellationToken.None, root is null ? null : Roots(root))).Purged;

    protected static string Going(SessionTrees trees) => Path.Combine(trees.SetAsideRoot, SetAsideEligibility.Going);
}
