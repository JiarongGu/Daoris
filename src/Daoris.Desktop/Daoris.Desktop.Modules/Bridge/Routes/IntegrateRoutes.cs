using Daoris.Driver;
using Microsoft.Data.Sqlite;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A repository's integration, the page's `bridge/repositories/integration.ts` (INTEGRATE1h; the integration design §5.8): the read the
/// Integration view and `daoris-driver integrate` draw, and the owner's acts, which are Person routes (D156), a card in the window.
/// </summary>
public sealed class IntegrateRoutes(DriverContext context)
{
    private const StateScopes Moves = StateScopes.Integration | StateScopes.Landing | StateScopes.Sessions;

    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// A repository's integration (`repository`): the queue, the standing candidates, what went back, the superseded commits awaiting a
    /// disposition and the promoted history. `unit` alone answers that unit's one line, its head.
    /// </summary>
    [DriverRoute("INTEGRATION")]
    [ByEvent("INTEGRATION", Untold = "the integration record, a file the window's ask must not wait on")]
    private Task<object?> IntegrationAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var store = new IntegrationStore(_loop.Home);
        if (Optional(request, "unit") is { Length: > 0 } unit) return Task.FromResult<object?>(new { Head = IntegrationLooks.HeadOf(store, unit) });
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        return Task.FromResult<object?>(new { Look = IntegrationLooks.Of(store, repository) });
    }

    /// <summary>
    /// The owner's act on a repository's integration: `request` a branch (`landed` for a recorded landing, else a records branch), `drop` a
    /// request that stands in no candidate, `recheck` to have the line looked at now. `decide` and `order` are refused until their steps are built.
    /// Answered `{ done, why?, message }`, a refusal as data.
    /// </summary>
    [DriverRoute("INTEGRATE")]
    [Publishes("INTEGRATE", Moves)]
    private async Task<object?> IntegrateAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var act = PayloadHelper.GetRequiredValue<string>(request.Payload, "act");
        var store = new IntegrationStore(_loop.Home);
        switch (act)
        {
            case "request":
            {
                var branch = PayloadHelper.GetRequiredValue<string>(request.Payload, "branch");
                var service = _loop.Service ?? throw NotReady();
                var root = LinesRoutes.Checkouts(await service.RegistryAsync(cancellationToken), repository, null)
                    .Where(row => row.Root is not null && Directory.Exists(row.Root)).FirstOrDefault();
                if (root.Root is null) return new Answered(false, "no-checkout", $"`{repository}` has no checkout here, so nothing was requested.");
                var route = Flag(request, "landed") ? IntegrationRoutes.Recorded : IntegrationRoutes.Records;
                var home = _loop.Home;
                var admitted = await new IntegrationRequests(new HomeIntegrationRecords(home), store, () => DriverConfig.Load(_loop.ConfigPath))
                    .AdmitAsync(new IntegrationAsk(repository, root.Workspace, root.Root, branch, route, IntegrationRequester.Card), cancellationToken)
                    .ConfigureAwait(false);
                if (admitted.Admitted) _loop.Nudge();
                return new Answered(admitted.Admitted, admitted.Admitted ? null : "refused", admitted.Message);
            }
            case "drop":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "request");
                try
                {
                    return store.Close(id, IntegrationRequestCloses.Dropped)
                        ? new Answered(true, null, $"Request {id} is dropped.")
                        : new Answered(false, "gone", $"Request {id} is not open.");
                }
                catch (Exception error) when (error is IntegrationRefusedException or SqliteException or IOException)
                {
                    return new Answered(false, "stands", $"Request {id} could not be dropped: {error.Message}.");
                }
            }
            case "recheck":
                _loop.Nudge();
                return new Answered(true, null, $"`{repository}`'s line is looked at now.");
            case "decide" or "order":
                return new Answered(false, "not-built", $"`{act}` is not built yet; nothing was changed.");
            default:
                return new Answered(false, "act", $"`{act}` is no act of an integration: one of request, drop, recheck, decide, order.");
        }
    }

    private sealed record Answered(bool Done, string? Why, string Message);
}
