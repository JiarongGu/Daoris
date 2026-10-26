namespace Daoris.Knowledge;

/// <summary>The head of a local line, read inside its caller's exclusive step.</summary>
public static class Lines
{
    /// <summary>Follows successors across kinds and a driven quest's later local runs; a cycle is unplaced.</summary>
    public static async Task<Session?> HeadOf(SessionStore sessions, Session named, CancellationToken ct = default)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var at = named;
        while (seen.Add(at.Id))
        {
            var next = await sessions.ContinuedByAsync(at.Id, ct).ConfigureAwait(false);
            if (next is null && at.Kind == SessionKind.Driven && at.Quest is { } quest)
            {
                var last = await sessions.LastOwnForQuestAsync(quest, ct).ConfigureAwait(false);
                if (last is not null && last.Id != at.Id) next = last;
            }
            if (next is null) return at;
            at = next;
        }
        return null;
    }
}
