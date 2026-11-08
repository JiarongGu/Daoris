using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// What this machine and the service keep of a session, behind the sessions doors: what a person first said in it, the sessions beside
/// it, what it spent, the record of its finished tree and its memory rows, and the person's decision on its memory.
/// </summary>
internal sealed class SessionsRecordDesk(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;
    private readonly RelatedSessions _related = new(RelatedSessions.GitReads, () => DateTimeOffset.UtcNow);

    public async Task<object?> OpeningsAsync(SessionOpeningsRead press)
    {
        await Task.CompletedTask;
        return new { Openings = _loop.Events.Openings([.. press.Ids ?? []]) };
    }

    public async Task<object?> RelatedAsync(SessionRelatedRead press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        var service = _loop.Service ?? throw NotReady();
        if (await service.RelatedCandidatesAsync(session, cancellationToken) is not var (mine, others)) return new { Session = session, Related = Array.Empty<object>() };
        var look = await _related.AskAsync(mine, others, cancellationToken);
        return new { Session = session, Related = look.Related, Own = look.Own is { } own ? new { Read = "not-read", own.ReadAt } : null };
    }

    public object? SpentOf(SessionSpentRead press)
    {
        var spent = new Dictionary<string, SessionSpent>(StringComparer.Ordinal);
        foreach (var id in SessionsGroupsDesk.Ids(press.Ids) ?? [])
        {
            if (_loop.Events.Spent(id) is { Turns: > 0 } read) spent[id] = read;
        }

        return new { Spent = spent };
    }

    public async Task<object?> RecordAsync(SessionRecordRead press, CancellationToken cancellationToken)
    {
        var id = press.Id;
        var service = _loop.Service ?? throw NotReady();
        var json = await service.SessionRecordAsync(id, cancellationToken).ConfigureAwait(false);
        return new { Record = json is null ? (JsonElement?)null : JsonDocument.Parse(json).RootElement.Clone() };
    }

    public async Task<object?> MemoryAsync(SessionMemoryRead press, CancellationToken cancellationToken)
    {
        var session = press.Session;
        var service = _loop.Service ?? throw NotReady();
        var json = await service.SessionMemoryAsync(session, cancellationToken).ConfigureAwait(false);
        return json is null ? new { Ops = Array.Empty<object>() } : JsonDocument.Parse(json).RootElement.Clone();
    }

    public async Task<object?> MemoryDecideAsync(SessionMemoryDecide press, CancellationToken cancellationToken)
    {
        var (session, op, approve, keep) = press;
        var yes = op is { Length: > 0 } ? approve : keep;
        if (op is not { Length: > 0 } && string.IsNullOrEmpty(session))
        {
            return new { Decided = false, Why = "refused", Message = "A keep or a drop names the session whose staged changes it moves." };
        }

        var service = _loop.Service ?? throw NotReady();
        var (decided, message) = await service.DecideMemoryAsync(session ?? "", op, yes, cancellationToken).ConfigureAwait(false);
        return new { Decided = decided, Why = decided ? null : "refused", Message = message };
    }
}
