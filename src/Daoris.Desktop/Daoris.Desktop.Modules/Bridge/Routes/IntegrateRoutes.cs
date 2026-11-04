using Daoris.Driver;
using Microsoft.Data.Sqlite;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// A repository's integration, the page's `bridge/repositories/integration.ts` (INTEGRATE1h; the integration design §5.8): the read the
/// Integration view and `daoris integrate` draw, and the owner's acts, which are Person routes (D156), a card in the window.
/// </summary>
public sealed class IntegrateRoutes(DriverContext context)
{
    private const StateScopes Moves = StateScopes.Integration | StateScopes.Landing | StateScopes.Sessions;

    private readonly DriverLoop _loop = context.Loop;

    /// <summary>
    /// A repository's integration (`repository`): the queue in its order, the standing candidates with what holds each, what went back, the
    /// superseded commits awaiting a disposition, the promoted history, and (INTEGRATE1h) the landed branches waiting for the owner's card, as
    /// the runtime last judged them. `unit` alone answers that unit's one line, its head.
    /// </summary>
    [DriverRoute("INTEGRATION", PipeClass.Open)]
    [ByEvent("INTEGRATION", Untold = "the integration record, a file the window's ask must not wait on")]
    private Task<object?> IntegrationAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var store = new IntegrationStore(_loop.Home);
        if (Optional(request, "unit") is { Length: > 0 } unit) return Task.FromResult<object?>(new { Head = IntegrationLooks.HeadOf(store, unit) });
        var repository = PayloadHelper.GetRequiredValue<string>(request.Payload, "repository");
        var waiting = _loop.CardWaits.Where(wait => string.Equals(wait.Repository, repository, StringComparison.OrdinalIgnoreCase)).Select(IntegrationLookWaiting.Of);
        return Task.FromResult<object?>(new { Look = IntegrationLooks.Of(store, repository) with { Waiting = [.. waiting] } });
    }

    /// <summary>
    /// The owner's act on a repository's integration: `request` a branch (`landed` for a recorded landing, else a records branch), `drop` a
    /// request that stands in no candidate, `recheck` to have the line looked at now; and (INTEGRATE1h) `decide` a candidate held for one
    /// (`candidate`, `decide`: split, hand-back or land, the owner's alone), `order` a queued request (`request`, `place`: first or last) and
    /// `answer` a superseded commit (`request`, `commit`, `answer`: superseded, or rebase, which tells its session). Answered
    /// `{ done, why?, message }`, a refusal as data.
    /// </summary>
    [DriverRoute("INTEGRATE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("INTEGRATE", Repository = "repository")]
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
            case "decide":
            {
                var candidate = PayloadHelper.GetRequiredValue<string>(request.Payload, "candidate");
                var decide = PayloadHelper.GetRequiredValue<string>(request.Payload, "decide");
                if (!IntegrationDecides.All.Contains(decide))
                {
                    return new Answered(false, "decide", $"`{decide}` is no decide: one of {string.Join(", ", IntegrationDecides.All)}. Nothing was changed.");
                }

                if (store.Candidate(candidate) is { } found && !string.Equals(found.Repository, repository, StringComparison.OrdinalIgnoreCase))
                {
                    return new Answered(false, IntegrationDecideRefusals.Gone, $"Candidate {candidate} is not `{repository}`'s: nothing was changed.");
                }

                var decided = store.Decide(candidate, decide, IntegrationRequester.Person);
                if (decided.Refused is null) _loop.Nudge();
                return decided.Refused switch
                {
                    null => new Answered(true, null, $"Candidate {candidate} is decided `{decide}`: the line's run acts on it at its next look."),
                    IntegrationDecideRefusals.Decided => new Answered(false, decided.Refused, $"Candidate {candidate} is decided `{decided.Decide?.Decide}` already: nothing was changed."),
                    IntegrationDecideRefusals.NotHeld => new Answered(false, decided.Refused, $"Candidate {candidate} is not held for a decide: nothing was changed."),
                    _ => new Answered(false, decided.Refused, $"No candidate {candidate} stands in `{repository}`'s integration: nothing was changed."),
                };
            }
            case "order":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "request");
                var place = PayloadHelper.GetRequiredValue<string>(request.Payload, "place");
                if (!IntegrationOrders.All.Contains(place))
                {
                    return new Answered(false, "place", $"`{place}` is no place in a queue: first or last. Nothing was changed.");
                }

                if (store.Request(id) is { } found && !string.Equals(found.Repository, repository, StringComparison.OrdinalIgnoreCase))
                {
                    return new Answered(false, IntegrationOrderRefusals.Gone, $"Request {id} is not `{repository}`'s: nothing was changed.");
                }

                return store.Order(id, place) switch
                {
                    null => new Answered(true, null, $"Request {id} is {place} in its line's queue."),
                    IntegrationOrderRefusals.Stands => new Answered(false, IntegrationOrderRefusals.Stands, $"Request {id} stands in a candidate, or is held, so it keeps its place: nothing was changed."),
                    var why => new Answered(false, why, $"Request {id} is not open: nothing was changed."),
                };
            }
            case "answer":
            {
                var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "request");
                var commit = PayloadHelper.GetRequiredValue<string>(request.Payload, "commit");
                var word = PayloadHelper.GetRequiredValue<string>(request.Payload, "answer");
                if (store.Request(id) is { } found && !string.Equals(found.Repository, repository, StringComparison.OrdinalIgnoreCase))
                {
                    return new Answered(false, "gone", $"Request {id} is not `{repository}`'s: nothing was changed.");
                }

                var service = _loop.Service;
                RebaseTelling.Say? say = service is null ? null : async (session, text, key, token) =>
                    (await WordsHead.FollowAsync(session, at => service.SayAsync(at, text, null, token, from: SaidFrom.Daoris, key: key)).ConfigureAwait(false)).Said;
                var answered = await IntegrationDispositions.AnswerAsync(store, id, commit, word, say, cancellationToken).ConfigureAwait(false);
                return new Answered(answered.Done, answered.Why, answered.Message);
            }
            default:
                return new Answered(false, "act", $"`{act}` is no act of an integration: one of request, drop, recheck, decide, order, answer.");
        }
    }

    private sealed record Answered(bool Done, string? Why, string Message);
}
