using Daoris.Contracts.History;
using static Daoris.Driver.RefusalWords;

namespace Daoris.Driver;

/// <summary>
/// The service's history doors (the service's history routes), a local host's only (HIST1b, D153; the history-clearing design §6.3): what a
/// clear of finished history would take, and the clear itself. A class over the client, reached as its <see cref="ServiceClient.History"/> and
/// by the client's own doors of the same names. The driver's <c>HistoryClearing</c> calls them, judging this machine's half before and removing
/// the home's files after.
/// </summary>
public sealed class HistoryDoors
{
    private readonly ServiceClient _service;

    internal HistoryDoors(ServiceClient service) => _service = service;

    /// <summary>The units a scope holds, each judged by the service, deleting nothing: <paramref name="plan"/> names the one scope.</summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the scope, in its words.</exception>
    public async Task<IReadOnlyList<HistoryUnitView>> PlanAsync(PlanHistory plan, CancellationToken ct = default)
    {
        var plans = Answered(await _service.SendAsync(plan, ct).ConfigureAwait(false));
        return [.. plans.Units.Select(ReadUnit)];
    }

    /// <summary>
    /// The second press: exactly <paramref name="units"/>, each judged again by the service and cleared in one transaction of its own, or
    /// kept with its word.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the press, in its words.</exception>
    public async Task<IReadOnlyList<HistoryClearedView>> ClearAsync(IReadOnlyList<HistoryUnitName> units, CancellationToken ct = default)
    {
        var cleared = Answered(await _service.SendAsync(
            new ClearHistory([.. units.Select(unit => new HistoryUnitRequest(unit.Kind, unit.Id))]), ct).ConfigureAwait(false));
        return
        [
            .. cleared.Units.Select(each => new HistoryClearedView(ReadUnit(each.Unit), each.Cleared, each.Message)
            {
                // Additive (HIST1j): absent where every removal was made, and from a host before it.
                FailedQuests = each.Failed?.Quests ?? [],
                FailedAsks = each.Failed?.Asks ?? [],
            }),
        ];
    }

    /// <summary>The door's answer, or its refusal said: in its words where it gave some, else as a host older than the door.</summary>
    private T Answered<T>(Sent<T> sent) where T : class
    {
        if (sent.Answer is { } answer) return answer;
        if (sent.Reply.Ok is false && ErrorWords(sent.Reply.Payload) is { Length: > 0 } error)
        {
            throw new DriverException($"the service refused the history door ({sent.Reply.Status}): {error}");
        }

        throw new DriverException($"the service at {_service.BaseUrl} has no history door ({sent.Reply.Status}) — is it older than this driver?");
    }

    private static HistoryUnitView ReadUnit(HistoryUnitResponse unit) => new(unit.Kind, unit.Id, unit.Workspace)
    {
        Clearable = unit.Clearable,
        Quests = unit.Quests,
        Forgotten = unit.Forgotten,
        Asks = unit.Asks,
        Sessions = unit.Sessions,
        Teammates = unit.Teammates,
        Refusal = unit.Refusal is { } refusal ? ReadRefusal(refusal) : null,
        Kept = [.. unit.Kept.Select(ReadRefusal)],
    };

    private static HistoryRefusalView ReadRefusal(HistoryRefusalResponse refusal) => new(refusal.Refusal, refusal.Error)
    {
        Quest = refusal.Quest,
        Ask = refusal.Ask,
        Session = refusal.Session,
        Origin = refusal.Origin,
        Workspace = refusal.Workspace,
        // Additive (HIST1l, HIST1m): each absent beside every other word, and from a host before it.
        Waits = refusal.Waits,
        Stands = refusal.Stands,
        By = refusal.By,
    };
}
