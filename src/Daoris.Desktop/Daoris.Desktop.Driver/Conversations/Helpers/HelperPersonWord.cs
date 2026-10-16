namespace Daoris.Driver;

/// <summary>Why a person's ask of a helper, or their yes or no to an agent's, did nothing: the codes the page words (MIXAGENT2b, HELPERNO1).</summary>
public static class HelperAskWhy
{
    public const string Blank = "blank";
    public const string TooLong = "too-long";
    public const string Agent = "agent";
    public const string NotHeld = "not-held";
    public const string Helper = "helper";
    public const string Reviewer = "reviewer";
    public const string Intake = "intake";
    public const string Gone = "gone";
    public const string Person = "person";
    public const string Waits = "waits";
    public const string Past = "past";

    /// <summary>A no to an ask that has the person's yes already (HELPERNO1).</summary>
    public const string Approved = "approved";

    /// <summary>A no to an ask the driver's look has not read yet (HELPERNO1).</summary>
    public const string Unread = "unread";
}

/// <summary>What the person's yes or no did: whether it stands, the code of why not, a sentence, and whether it wrote the state.</summary>
public sealed record HelperWordAnswer(bool Stands, string? Why, string Message, bool Wrote);

/// <summary>
/// The person's yes and no to an agent's ask for a helper under the <c>helpers</c> rule's <c>ask</c> (MIXAGENT2b and HELPERNO1,
/// D163 point 3; HELPERTERM1): one judge for the page's <c>SESSION_HELPER_APPROVE</c> and <c>SESSION_HELPER_DECLINE</c> and the
/// terminal's <c>sessions helper --approve|--decline</c>, in the library because the terminal host cannot reference Modules.
/// </summary>
/// <remarks>
/// A yes is written where the ask waits on it: no state yet, or <c>waiting</c> with no code. A no is written only where the look
/// holds the ask <c>waiting</c> with no code, since one it has not read yet may be starting under <c>on</c>. A person's own ask needs
/// neither, one with the yes waits on the look, one past its start is past it, one waiting on a slot or an agent waits on no word,
/// and one not held is gone: each answered with its code and its state untouched.
/// </remarks>
public static class HelperPersonWord
{
    /// <param name="session">The asker the page names, or null where the terminal names the ask alone.</param>
    public static HelperWordAnswer Approve(HelperFiles files, string? session, string askId)
    {
        var ask = Find(files, session, askId);
        var state = ask is null ? null : files.ReadState(ask.Id);
        return (ask, state) switch
        {
            (null, _) => new(false, HelperAskWhy.Gone, Gone(session, askId), false),
            ({ AskedBy: HelperAsk.ByPerson }, _) => new(false, HelperAskWhy.Person, $"#{askId} is your own ask, which needs no yes.", false),
            (_, { State: HelperStates.Approved }) => new(true, null, $"#{askId} has your yes.", false),
            (_, null) or (_, { State: HelperStates.Waiting, Code: null }) => Approved(files, askId),
            (_, { State: HelperStates.Waiting }) => new(false, HelperAskWhy.Waits, $"#{askId} waits on {state!.Code}, not on your yes.", false),
            _ => new(false, HelperAskWhy.Past, $"#{askId} is past its start: {state!.State}.", false),
        };
    }

    /// <summary>
    /// A no: the ask refused <c>declined</c> at once, with the coded note the look writes on the asker's record for every refusal
    /// (<c>helper.refused</c>), so the look reads it as past judging. <paramref name="output"/> is the loop's transcript where one
    /// runs here, null for the terminal, which keeps the note on the record's events alone.
    /// </summary>
    public static HelperWordAnswer Decline(HelperFiles files, SessionEvents events, SessionOutput? output, string? session, string askId)
    {
        var ask = Find(files, session, askId);
        var state = ask is null ? null : files.ReadState(ask.Id);
        return (ask, state) switch
        {
            (null, _) => new(false, HelperAskWhy.Gone, Gone(session, askId), false),
            ({ AskedBy: HelperAsk.ByPerson }, _) => new(false, HelperAskWhy.Person, $"#{askId} is your own ask, which needs no word of yours.", false),
            (_, { State: HelperStates.Refused, Code: HelperWhy.Declined }) => new(true, null, $"#{askId} has your no.", false),
            (_, null) => new(false, HelperAskWhy.Unread, $"#{askId} is not read by the driver yet: say no once it waits on your word.", false),
            (_, { State: HelperStates.Approved }) => new(false, HelperAskWhy.Approved, $"#{askId} has your yes, and starts at the driver's next look.", false),
            ({ } waits, { State: HelperStates.Waiting, Code: null }) => Declined(files, events, output, waits),
            (_, { State: HelperStates.Waiting }) => new(false, HelperAskWhy.Waits, $"#{askId} waits on {state!.Code}, not on your word.", false),
            _ => new(false, HelperAskWhy.Past, $"#{askId} is past its start: {state!.State}.", false),
        };
    }

    private static HelperAsk? Find(HelperFiles files, string? session, string askId) =>
        files.Asks().FirstOrDefault(each => each.Id == askId && (session is null || string.Equals(each.Asker, session, StringComparison.Ordinal)));

    private static string Gone(string? session, string askId) =>
        session is null ? $"No ask #{askId} for a helper is held on this machine." : $"Session {session} holds no ask #{askId} for a helper.";

    private static HelperWordAnswer Approved(HelperFiles files, string id)
    {
        files.WriteState(new HelperState(id, HelperStates.Approved, null, null, DateTimeOffset.UtcNow));
        return new(true, null, $"#{id} has your yes: the driver starts a helper at its next look, within the limits.", true);
    }

    // The look's own refusal, as `DriverHelpers.RefuseAsync` writes it: the state, then the note on the asker's record.
    private static HelperWordAnswer Declined(HelperFiles files, SessionEvents events, SessionOutput? output, HelperAsk declined)
    {
        files.WriteState(new HelperState(declined.Id, HelperStates.Refused, HelperWhy.Declined, null, DateTimeOffset.UtcNow));
        var note = HelperNotes.Refused(HelperWhy.Declined);
        output?.Append(declined.Asker, $"— {note.Note}");
        events.Keep(declined.Asker, new SessionEvent { Kind = SessionEventKind.Note, Text = note.Note, Parts = note.Parts }, say: null);
        return new(true, null, $"#{declined.Id} has your no: no helper is started for it, and its session's timeline says so.", true);
    }
}
