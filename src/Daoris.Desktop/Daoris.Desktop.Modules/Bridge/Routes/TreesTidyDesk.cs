using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;
using static Daoris.Desktop.SweepWires;

namespace Daoris.Desktop;

/// <summary>The clean-up and bringing up to date behind the lines domain's doors: each one's list over the checkouts here, and its press.</summary>
internal sealed class TreesTidyDesk(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    public async Task<object?> SweepPlanAsync(SweepPlanRead read, CancellationToken cancellationToken)
    {
        // `trees clean --workspace` takes one workspace's checkouts alone, as that workspace's Branches tab does; none takes every one.
        var workspace = read.Workspace;
        // the pull-request answers kept before this look, so what its asks wrote reaches the pushed list at once (below).
        var kept = new LandedBranches(_loop.Home).Entries();
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, null, cancellationToken, workspace);
        // a terminal's list says a plugin's own lines as they come, under its name, as its alone path does; the page's says none.
        var pluginLines = new List<string>();
        var trees = read.Said ? Asking(pluginLines) : new SessionTrees(_loop.Home);

        // Which quests a landing the line does not hold names are closed, the service's, read once for the list.
        var closed = await LandedStanding.ClosedAsync(kept, (_loop.Service ?? throw NotReady()).EveryQuestAsync, cancellationToken);
        var plan = await trees.CleanPlanAsync(repositories, inUse, cancellationToken, closed);
        // The person's look asks the plugins and keeps their answers in the landing record, which no watch hears and
        // which moves no ref: each repository whose kept answers it wrote has its part of the pushed list judged again now,
        //rather than at its next ref move or deadline. Asked of no plugin again: the part reads what is kept.
        SweepStateSlice.Asked(_loop.Facts, kept, new LandedBranches(_loop.Home).Entries());
        // the session each kept branch's tree is, so its row says how to land it as that session's page does. The
        // records are read only where a row could offer one, and records that do not read offer none: the list stands.
        var records = plan.Sessions.Any(OffersLanding)
            ? await SessionBranchDoors.RecordsOrNoneAsync(_loop.Service ?? throw NotReady(), cancellationToken)
            : [];
        // `trees clean` asks for the verb's words beside the list's fields (`said`); the page does not.
        return read.Said
            ? SweepAnswer(plan, records, TreesPlanWords.CleanPlan(plan, records, new TreesAsk(TreesLook.Clean) { Workspace = workspace }).After(pluginLines))
            : SweepAnswer(plan, records);
    }

    public async Task<object?> SweepAsync(SweepPress press, CancellationToken cancellationToken)
    {
        var workspace = press.Workspace;
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, null, cancellationToken, workspace);
        var trees = new SessionTrees(_loop.Home);
        var only = press.Only?.ToHashSet(StringComparer.Ordinal);

        // A workspace the registry does not name is refused, naming the ones it does, rather than clean nothing and say all is well.
        if (workspace is not null && TreesCommand.Take(
                await (_loop.Service ?? throw NotReady()).RegistryAsync(cancellationToken), new TreesAsk(TreesLook.Clean) { Workspace = workspace }).Refusal is { } refusal)
        {
            return new
            {
                Results = Array.Empty<object>(), Landed = Array.Empty<object>(), Folders = Array.Empty<string>(), Removed = 0,
                Said = TreesWords.Refused($"trees: {refusal}").ToWire(),
            };
        }

        var done = await trees.CleanAsync(repositories, inUse, only, cancellationToken);
        _loop.Nudge();
        return new
        {
            Results = done.Sessions.Select(result => new { Branch = SweepRow(result.Item), result.Removed, result.Message }).ToArray(),
            Landed = done.Landed.Select(result => new { Branch = LandedRow(result.Item), result.Removed, result.Message }).ToArray(),
            // The empty folders trees left where something held them open, each removed or still held.
            Folders = done.Folders ?? [],
            Removed = done.Sessions.Count(result => result.Removed) + done.Landed.Count(result => result.Removed),
            // what `trees clean --yes` prints, each branch removed or kept with why, and the count.
            Said = TreesWords.Clean(done).ToWire(),
        };
    }

    public async Task<object?> TreesSyncPlanAsync(TreesSyncPlanRead read, CancellationToken cancellationToken)
    {
        var named = read.Repository;
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, named, cancellationToken, read.Workspace);
        // a terminal's list says a plugin's own lines as they come, under its name, as its alone path does; the page's says none.
        var pluginLines = new List<string>();
        var trees = read.Said ? Asking(pluginLines) : new SessionTrees(_loop.Home);
        var plan = await trees.SyncPlanAsync(repositories, inUse, fetch: true, cancellationToken, SyncScopeOf(named, read.Also, read.All));
        // `trees sync` asks for the verb's words beside the list's fields (`said`); the page does not.
        if (read.Said)
        {
            var asked = new TreesAsk(TreesLook.Sync) { Repository = named, Workspace = read.Workspace, All = read.All };
            return new
            {
                Lines = plan.Lines.Select(PullRow).ToArray(),
                Rebases = plan.Rebases.Select(RebaseRow).ToArray(),
                Deletes = plan.Deletes.Select(LandedRow).ToArray(),
                Looked = plan.Looked.Select(SyncRepositoryRow).ToArray(),
                Apart = plan.Apart.Select(SyncRepositoryRow).ToArray(),
                Said = TreesPlanWords.SyncPlan(plan, asked, DateTimeOffset.UtcNow).After(pluginLines).ToWire(),
            };
        }

        return new
        {
            Lines = plan.Lines.Select(PullRow).ToArray(),
            Rebases = plan.Rebases.Select(RebaseRow).ToArray(),
            Deletes = plan.Deletes.Select(LandedRow).ToArray(),
            // What the look took, and every other repository with a checkout, listed apart for the person to include.
            Looked = plan.Looked.Select(SyncRepositoryRow).ToArray(),
            Apart = plan.Apart.Select(SyncRepositoryRow).ToArray(),
        };
    }

    public async Task<object?> TreesSyncAsync(TreesSyncPress press, CancellationToken cancellationToken)
    {
        var named = press.Repository;
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, named, cancellationToken, press.Workspace);
        var trees = new SessionTrees(_loop.Home);
        var scope = SyncScopeOf(named, press.Also, press.All);
        var only = press.Only?.ToHashSet(StringComparer.Ordinal);

        // The sessions in use, asked again while each repository's trees are held for its replays: the
        // driver may have opened one in a listed tree between the list and this press.
        var service = _loop.Service ?? throw NotReady();

        // `trees sync --yes` takes the checkouts its words name, as the verb takes them, and says so where it takes none: a
        // workspace the registry does not name, or no checkout of the repository named.
        var ask = new TreesAsk(TreesLook.Sync) { Repository = named, Workspace = press.Workspace, All = press.All };
        if (repositories.Count == 0)
        {
            var taken = TreesCommand.Take(await service.RegistryAsync(cancellationToken), ask);
            return new
            {
                Lines = Array.Empty<object>(), Rebases = Array.Empty<object>(), Deletes = Array.Empty<object>(), Changed = 0,
                Apart = Array.Empty<object>(), Followed = Array.Empty<string>(),
                Said = TreesWords.Refused(taken.Refusal is { } refusal ? $"trees: {refusal}" : TreesCommand.NothingToSync(ask)).ToWire(),
            };
        }

        // The verb fetches as it presses (`fetch`); the page's press acts on what its list fetched, and sends none. The lines the press
        // moved are followed by the registry at once where the verb asks (`follow`), as the page's next look would.
        var done = await trees.SyncAsync(repositories, inUse, only, fetch: press.Fetch, cancellationToken,
            inUseNow: async token => await LinesRoutes.InUseAsync(_loop.Home, service, token), scope: scope,
            beside: token => BranchTidying.BesideAsync(_loop.Home, service, token));
        _loop.Nudge();
        var followed = press.Follow
            ? await TreesWords.FollowMovedAsync(service, _loop.Home, () => DriverConfig.Load(_loop.ConfigPath), _loop.Log, cancellationToken)
            : null;
        return new
        {
            Lines = done.Lines.Select(result => new { Line = PullRow(result.Pull), result.Moved, result.Message }).ToArray(),
            Rebases = done.Rebases.Select(result => new { Branch = RebaseRow(result.Item), result.Replayed, result.Message }).ToArray(),
            Deletes = done.Deletes.Select(result => new { Branch = LandedRow(result.Item), result.Removed, result.Message }).ToArray(),
            Changed = done.Lines.Count(result => result.Moved) + done.Rebases.Count(result => result.Replayed) + done.Deletes.Count(result => result.Removed),
            // What the press left apart and the lines the registry followed, which the verb says; the page ignores both.
            Apart = done.Apart.Select(SyncRepositoryRow).ToArray(),
            Followed = followed?.Out ?? [],
            Said = TreesWords.Sync(done, DateTimeOffset.UtcNow, followed).ToWire(),
        };
    }

    /// <summary>
    /// The trees' look a terminal asks for: its landing plugins say their own lines into <paramref name="lines"/> as the alone
    /// path says them on its console, under the plugin's name, and the answer's words carry them first.
    /// </summary>
    private SessionTrees Asking(List<string> lines) => new(
        _loop.Home, new LandingPlugins(_loop.Home, say: (plugin, line) => lines.Add(TreesWords.PluginLine(plugin, line)), log: _loop.Log, health: _loop.Health));

    /// <summary>
    /// Which repositories a sync takes: every one (<c>all</c>), the ones ticked (<c>also</c>) and the one named; the press takes each a
    /// listed row names besides.
    /// </summary>
    private static SyncScope SyncScopeOf(string? named, IReadOnlyList<string>? also, bool all)
    {
        if (all) return SyncScope.Everything;
        var taken = (also ?? []).ToHashSet(StringComparer.Ordinal);
        if (named is not null) taken.Add(named);
        return SyncScope.Named(taken);
    }

    private static object SyncRepositoryRow(SyncRepository each) => new { each.Repository, each.Workspace, each.Holds };

    private static string? Short(string? commit) => commit is null ? null : Daoris.Driver.TextBounds.ShortCommit(commit, 8);

    private static object PullRow(LinePull pull) => new
    {
        pull.Repository, pull.Workspace, pull.Line, pull.Kind, From = Short(pull.From), To = Short(pull.To), pull.Commits,
        pull.Fetch, pull.Detail, pull.Moves,
        // Where the fetch failed: when this checkout last heard from origin, and how origin is reached.
        pull.LastFetch, pull.Reach,
    };
}
