namespace Daoris.Driver;

/// <summary>
/// A limit move's new part that a restart cut off before it ran: the line goes on from the part the limit ended, by the same carry the
/// live move planned, since the cut part holds no conversation of its own and a go-on from it would be briefed.
/// </summary>
internal static class CutLimitPart
{
    /// <summary>The part the limit ended that <paramref name="prior"/> continues, where <paramref name="prior"/> never ran; else null.</summary>
    public static async Task<PriorSession?> BehindAsync(LoopContext context, PriorSession? prior, CancellationToken ct)
    {
        if (prior is not { From: { Length: > 0 } from, FromWhy: HandOverWhy.Limit }) return null;
        if (context.Conversations.Read(prior.Session) is not null) return null;
        if (context.Events.After(prior.Session, 0).Events.Any(e => e.Kind is SessionEventKind.Turn or SessionEventKind.Message or SessionEventKind.Tool)) return null;
        return await context.Service.RecordAsync(from, ct).ConfigureAwait(false);
    }
}
