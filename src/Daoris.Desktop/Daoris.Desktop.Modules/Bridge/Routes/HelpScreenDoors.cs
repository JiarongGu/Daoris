using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>The doors Ask Daoris's Apply goes through, each the code a screen's own route runs; <paramref name="service"/> is null before the loop's is up.</summary>
internal sealed class HelpScreenDoors(DriverContext context, ServiceClient? service) : IHelpDoors
{
    private readonly DriverLoop _loop = context.Loop;

    public void Change(Func<DriverConfig, DriverConfig> edit) => context.Change(edit);

    // The ask composer's door, `POST /api/asks`, with its review choice and words; an environment the workspace does not declare is refused first.
    public async Task<AskAnswer> AskAsync(string workspace, string sentence, string? review, string? reviewWords, CancellationToken ct, string? shortTitle = null)
    {
        var asks = service ?? throw NotReady();
        if (review is not null
            && await AskReviewCommand.UndeclaredAsync(review, workspace, new AskReviewWorld(asks, () => DriverConfig.Load(_loop.ConfigPath)), ct)
                .ConfigureAwait(false) is { } undeclared)
        {
            return new AskAnswer(false, undeclared, null, null);
        }

        return await asks.AskAsync(workspace, sentence, [], [], null, ct, review, reviewWords, shortTitle: shortTitle).ConfigureAwait(false);
    }

    // TREES_SYNC_PLAN's own list, each line fetched as the person whose press this is.
    public async Task<SyncPlan> SyncPlanAsync(string? repository, string? workspace, CancellationToken ct)
    {
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, repository, ct, workspace).ConfigureAwait(false);
        return await new SessionTrees(_loop.Home).SyncPlanAsync(repositories, inUse, fetch: true, ct, Named(repository))
            .ConfigureAwait(false);
    }

    // TREES_SYNC's own press: only the rows the look listed, fetching nothing, then the loop asked to look.
    public async Task<SyncDone> SyncAsync(string? repository, string? workspace, IReadOnlySet<string> only, CancellationToken ct)
    {
        var (repositories, inUse) = await LinesRoutes.CheckoutsAndSessionsAsync(_loop, repository, ct, workspace).ConfigureAwait(false);
        var ledger = _loop.Service ?? throw NotReady();
        var done = await new SessionTrees(_loop.Home).SyncAsync(repositories, inUse, only, fetch: false, ct,
            inUseNow: async token => await LinesRoutes.InUseAsync(_loop.Home, ledger, token).ConfigureAwait(false), scope: Named(repository),
            beside: token => BranchTidying.BesideAsync(_loop.Home, ledger, token)).ConfigureAwait(false);
        _loop.Nudge();
        return done;
    }

    /// <summary>A card naming a repository includes it, as `--repository` does; none, and the default.</summary>
    private static SyncScope? Named(string? repository) => repository is null ? null : SyncScope.Named([repository]);
}
