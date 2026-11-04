namespace Daoris.Driver;

/// <summary>The limit's share of a continuation: which record resumes its own conversation, and Daoris's limit word kept on it and taken off.</summary>
internal sealed class ContinuationLimits(LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>
    /// The record a start goes on in with the person's words, where its resume asks for that record's own account (MSG1g, D137
    /// §2.2): words the planner took up, on the adapter the record ran on. Null for every other start, and for a record that ran
    /// on another adapter than <paramref name="agent"/>, whose conversation cannot go on here whichever account runs (D131 §2's
    /// <c>adapter</c>), so its words are carried on by the walk's pick as before. Since FOUNDATION1j part ii the start's agent is the
    /// record's own unless a hand-over named another, so a record that ran on Build's second choice goes on in its own conversation.
    /// </summary>
    /// <remarks>
    /// 🔴 SAYHELD1: never judged by the quest's <c>awaits</c>. The planner plans words going on only where no question holds them, and
    /// a quest resumed on its answer still names that question (Planner's <c>ResumedOnAnswer</c>), so its park's answer took the walk.
    /// </remarks>
    internal PriorSession? ResumesOwn(Consideration start, string agent)
    {
        if (!start.GoesOn || start.Resumes is not { WordsWaiting: true } words) return null;
        var ranOn = words.Adapter ?? _context.Conversations.Read(words.Session)?.Adapter;
        return ranOn is not { Length: > 0 } || string.Equals(ranOn, agent, StringComparison.OrdinalIgnoreCase) ? words : null;
    }

    /// <summary>
    /// Whether a record an account's limit ended could resume its own conversation on <paramref name="agent"/> (LIMITRESUME1b, D206; the
    /// limit-resume design §3.3 step 1): the judge passing as if words waited, since with none it answers <c>ended</c> (the same agent and
    /// account, its tree, a kept id, a door that resumes), on a door whose resume of a missing conversation is known to fail closed. The
    /// protocol door refuses one with <c>resource_not_found</c> before any prompt; the native door goes by the brief until row 1f has
    /// recorded what its <c>--resume</c> of a missing id does.
    /// </summary>
    internal bool LimitResumes(PriorSession limited, string agent)
    {
        ISessionAdapter adapter;
        try { adapter = _context.Adapters.Resolve(agent); } catch (DriverException) { return false; }
        if (adapter.Wire != SessionWire.Acp) return false;

        var asIfWords = limited with { Said = [new SaidWordView(null, LimitResume.Words, DateTimeOffset.UnixEpoch, [], true) { From = DaorisWords.From }] };
        return Continuations.Judge(
            ConversationTrees.Stands, asIfWords, adapter.Name, adapter.Resumes, limited.Profile, _context.Conversations.Read(limited.Session)) is null;
    }

    /// <summary>
    /// Daoris's limit word kept on the ended record through the say door (LIMITRESUME1b, the design §3.3 step 1), as
    /// <see cref="RebaseTelling"/> keeps its own, under the key of the cool-off that held it, so a later look's start keeps one; then the
    /// record as it stands now, its words the person's first and Daoris's last, as the say door keeps them. Null where the door refused
    /// it, and the line goes on as the carry-on it was planned.
    /// </summary>
    internal async Task<(PriorSession Record, string Word)?> KeepLimitWordAsync(PriorSession limited, CancellationToken ct)
    {
        SayAnswer said;
        // AGENTERRORS1a2 (the agent-errors design §5.1): a record Try again released from its transient hold takes the same step, with the
        // episode's go-on under the key of the end released.
        var (text, key) = Planner.EndedTransient(limited) ? LimitResume.TransientWord(limited)
            : LimitResume.UpdateBuild(limited) is { } build ? (ShellClose.Words, DaorisKeys.Restart(limited.Session, build))
            : (LimitResume.Words, DaorisKeys.LimitReset(limited.Session, LimitResume.Until(limited)));
        try
        {
            said = await _context.Service.SayAsync(limited.Session, text, ct: ct, from: DaorisWords.From, key: key).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }

        if (!said.Kept || said.Word?.Id is not { } word) return null;
        var now = await ResumeLedger.ReadAgainAsync(_context.Service, limited.Session, ct).ConfigureAwait(false);
        if (now is null) return null;
        return (Continuations.HandedOn(limited, now), word);
    }

    /// <summary>
    /// Daoris's limit words taken off a record by their ids (LIMITRESUME1b): at every conclusion of the run they went with, and where its
    /// own conversation could not take them, the record's own session named as the one that took them. A refusal costs nothing more:
    /// the next attempt keeps its own under its own cool-off's key.
    /// </summary>
    internal async Task TakeLimitWordsOffAsync(string session, IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        try
        {
            await _context.Service.TakenAsync(session, ids, by: session, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            _context.Output?.Append(session, $"— Daoris's limit word could not be taken off: {error.Message}");
        }
    }
}
