namespace Daoris.Driver;

/// <summary>
/// One pass's reads of the gates for the evaluation runtime (link 8's <see cref="NodeRuntimeLook.Gates"/>): a unit's work
/// read as a landing door reads it (<see cref="SessionTreeGates.GateAsync"/>), at its newest session's tree here: a branch unit's newest executor,
/// a quest unit's newest session serving it. The sessions are read once a pass, at the first unit asked. A unit with no such tree here (a
/// teammate's work, a tree tidied), or a read that failed, reads nothing, and its nodes wait. At the
/// commit given, a run's branch at the tip its pass read, as a landing door judges at a commit it pins; else at that tree's <c>HEAD</c>.
/// </summary>
internal sealed class NodeGates(string home, ServiceClient service)
{
    private Task<IReadOnlyList<SessionRecord>>? _sessions;

    public async Task<(OpinionGateState? Opinion, ReviewGateState? Review)> ReadAsync(WorkUnit unit, string? tip, CancellationToken ct)
    {
        try
        {
            var sessions = await (_sessions ??= service.SessionRecordsAsync(ct)).ConfigureAwait(false);
            var serving = unit.Kind == WorkUnitKind.Quest
                ? sessions.Where(session => session.Quest is { Length: > 0 } quest && string.Equals(QuestUnits.IdOf(quest.TrimStart('#')), unit.Id, StringComparison.Ordinal))
                : sessions.Where(session => unit.Executors.Contains(session.Id, StringComparer.Ordinal));
            if (serving.Where(session => session.Tree is { Length: > 0 } tree && Directory.Exists(tree)).MaxBy(session => session.Updated) is not { } newest)
            {
                return (null, null);
            }

            var gate = await new SessionTrees(home).GateAsync(newest.Tree!, newest.Quest, new ServiceReviewWorld(service), newest.Id, ct, tip: tip).ConfigureAwait(false);
            return (gate.Opinion, gate.Review);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// What the line holds of a run's branch at <paramref name="tip"/>, read in its newest session's tree as a landing door reads it:
    /// the head's sentence where the line holds the work by content, and the proof. Null where that tree is not here or not at the tip.
    /// </summary>
    public async Task<HoldContent?> ContentAsync(WorkUnit unit, string tip, CancellationToken ct)
    {
        try
        {
            var sessions = await (_sessions ??= service.SessionRecordsAsync(ct)).ConfigureAwait(false);
            var tree = sessions.Where(session => unit.Executors.Contains(session.Id, StringComparer.Ordinal) && session.Tree is { Length: > 0 } each && Directory.Exists(each))
                .MaxBy(session => session.Updated)?.Tree;
            return tree is null ? null : await HoldReads.ContentAsync(new SessionTrees(home), tree, tip, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
