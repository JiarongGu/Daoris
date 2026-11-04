namespace Daoris.Driver;

/// <summary>
/// <c>harnesses.json</c> in the shape this build reads (D232; POOL1a's fold and D165 §6's names, run once): each entry's <c>early</c> and
/// <c>near</c> written as the switch and curve they read as, <c>prefer</c> and <c>parallel</c> taken out, then every agent's defaults and
/// workspace lists folded into its one set, with a receipt of what the fold changed. A file that does not read is left as it is.
/// </summary>
public sealed class HarnessesStep : IHomeMigrationStep
{
    public string Name => "harnesses";

    public void Run(HomeMigrationRun run)
    {
        var path = DaorisHome.Entry(run.Home, HomeEntry.Harnesses);
        if (!File.Exists(path)) return;
        using var held = KeepLock.Take(path);
        var settings = HarnessSettings.Load(path);
        if (settings.Problem is not null) return;

        var named = settings with
        {
            Uses = settings.Uses.ToDictionary(pair => pair.Key, pair => pair.Value.Written(), StringComparer.OrdinalIgnoreCase),
            WorkspaceUses = settings.WorkspaceUses.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyDictionary<string, UseEntry>)pair.Value.ToDictionary(each => each.Key, each => each.Value.Written(), StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
        };
        var known = HarnessSettings.AgentsKnown(run.Home);
        var (folded, _) = AccountSet.Fold(named, HarnessSettings.AccountsOf(run.Home, named, known), DateTimeOffset.UtcNow, PoolReceipts.NewId);
        var before = AtomicFile.ReadText(path);
        run.Keep(path);
        folded.SaveHolding(path);
        if (AtomicFile.ReadText(path) != before) run.Said(path);
    }
}
