namespace Daoris.Driver;

/// <summary>The limit's share of a continuation: which record resumes its own conversation, and Daoris's limit word kept on it and taken off.</summary>
internal sealed class ContinuationLimits(LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>
    /// The record a start goes on in with the person's words, where its resume asks for that record's own account, by the
    /// one rule the sessions' groups report it by (<see cref="OwnResume"/>), on the agent the start runs on.
    /// </summary>
    internal PriorSession? ResumesOwn(Consideration start, string agent) =>
        OwnResume.Of(start, agent, session => _context.Conversations.Read(session)?.Adapter);

    /// <summary>
    /// Whether a record an account's limit ended could resume its own conversation on <paramref name="agent"/>: the judge passing as if words waited, since with none it answers <c>ended</c> (the same agent and
    /// account, its tree, a kept id, a door that resumes), on a door whose resume of a missing conversation is known to fail closed. The
    /// protocol door refuses one with <c>resource_not_found</c> before any prompt; the native door goes by the brief, since what
    /// its <c>--resume</c> of a missing id does is not recorded.
    /// </summary>
    internal bool LimitResumes(PriorSession limited, string agent)
    {
        AgentDoor adapter;
        try { adapter = _context.Adapters.Door(agent); } catch (DriverException) { return false; }
        if (!adapter.Can.HeldOnWire) return false;

        var asIfWords = limited with { Said = [new SaidWordView(null, LimitResume.Words, DateTimeOffset.UnixEpoch, [], true) { From = DaorisWords.From }] };
        return Continuations.Judge(
            tree => ConversationTrees.Stands(_context.Home, tree), asIfWords, adapter.Id.Value, adapter.Can.Resumes, limited.Profile, _context.Conversations.Read(limited.Session)) is null;
    }

    /// <summary>
    /// Daoris's limit word kept on the ended record through the say door, as
    /// <see cref="RebaseTelling"/> keeps its own, under the key of the cool-off that held it, so a later look's start keeps one; then the
    /// record as it stands now, its words the person's first and Daoris's last, as the say door keeps them. Null where the door refused
    /// it, and the line goes on as the carry-on it is planned.
    /// </summary>
    internal async Task<(PriorSession Record, string Word)?> KeepLimitWordAsync(PriorSession limited, CancellationToken ct)
    {
        SayAnswer said;
        // A record Try again released from its transient hold takes the same step, with the
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
    /// Daoris's limit words taken off a record by their ids: at every conclusion of the run they went with, and where its
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
