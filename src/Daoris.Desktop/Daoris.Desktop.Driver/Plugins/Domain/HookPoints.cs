namespace Daoris.Driver;

/// <summary>A plugin's answer at a decision point (D64 §4): allow, or hold with the sentence a person reads.</summary>
public sealed record HookDecision(bool Allowed, string? Reason = null)
{
    public static readonly HookDecision Allow = new(true);

    public static HookDecision Hold(string reason) => new(false, reason);
}


/// <summary>
/// What <see cref="HookPoints.StateChanged"/> is told (STATEHUB1g): the shell's <c>STATE_CHANGED</c>, as every window hears it
/// (D169 point 3). What moved, never what it is now: no words, no answer, no machine path.
/// </summary>
/// <param name="Scopes">The families of state that moved, in the screen's words (<c>sessions</c>, <c>landing</c>, …).</param>
/// <param name="Sessions">The sessions the change was about, or null where it was not one session's: any.</param>
public sealed record StateChangedFrame(IReadOnlyList<string> Scopes, IReadOnlyList<string>? Sessions);

/// <summary>
/// What each point is sent, built in one place (PLUG8): the loop and the landing send these, and the
/// plugin kit's sample frames are built by the same functions, so a sample cannot say what the driver
/// does not. Every name is spelled here rather than left to a serializer's policy.
/// </summary>
public static class HookFrames
{
    /// <summary><see cref="HookPoints.QuestConsider"/>: one planned start.</summary>
    public static object Consider(Consideration start) => new
    {
        quest = new { id = start.Quest.Id, title = start.Quest.Title, from = start.Quest.From, to = start.Quest.To },
        repository = start.Quest.To,
        workspace = start.Workspace,
        root = start.Root,
    };

    /// <summary><see cref="HookPoints.SessionEnded"/>: what a tick concluded.</summary>
    public static object Ended(SessionEnded ended) => new
    {
        session = ended.Session,
        quest = ended.Quest,
        repository = ended.Repository,
        state = ended.State,
        adapter = ended.Adapter,
        account = ended.Account,
        byPerson = ended.ByPerson,
        note = ended.Note,
        reason = ended.Reason,
        @class = ended.Class,
    };

    /// <summary>
    /// <see cref="HookPoints.State"/>: a landed branch, asked about (PLUGHOOK1a, design §2.2): where it is, the line, and the pull
    /// request and the commit the record holds, so a plugin finds the pull request by its address, else by its source branch; and
    /// <c>origin</c> (GITAUTH1b).
    /// </summary>
    public static object State(StateFrame frame) => new
    {
        repository = frame.Repository,
        workspace = frame.Workspace,
        root = frame.Root,
        branch = frame.Branch,
        line = frame.Line,
        pullRequest = frame.PullRequest,
        pushedTip = frame.PushedTip,
        origin = frame.Origin,
        key = frame.Key,
    };

    /// <summary>
    /// <see cref="HookPoints.StateChanged"/>: what moved (STATEHUB1g), the event's own two fields, <c>sessions</c> null where the
    /// change was about no one session.
    /// </summary>
    public static object Changed(StateChangedFrame frame) => new
    {
        scopes = frame.Scopes.ToArray(),
        sessions = frame.Sessions?.ToArray(),
    };

    /// <summary>
    /// <c>work/event</c>'s frame (EVENTHOOK1b, the event hook design §4.3), every name spelled here and each field checked against
    /// <see cref="WorkEventGrammar"/> as it is built: an optional field that fails, or whose source holds none, is left out; an event whose
    /// required field fails is an <see cref="WorkEventKinds.Missed"/> frame in its place, naming that field as unsent.
    /// </summary>
    public static WorkEventFrame Event(WorkEvent told, string run, long seq, int missed, IReadOnlySet<string> adapters)
    {
        var frame = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["run"] = run,
            ["seq"] = seq,
            ["missed"] = missed,
            ["kind"] = told.Kind,
            ["at"] = WorkEventGrammar.TimeOf(told.At),
        };
        if (!WorkEventGrammar.Fields.TryGetValue(told.Kind, out var fields)) return Missed("kind");

        (string Name, object? Value)[] carried =
        [
            ("session", told.Session), ("quest", told.Quest), ("ask", told.Ask), ("state", told.State?.ToLowerInvariant()),
            ("adapter", told.Adapter), ("strikes", told.Strikes), ("code", told.Code), ("confirmation", told.Confirmation), ("end", told.End),
        ];
        foreach (var (name, value) in carried)
        {
            var required = fields.Required.Contains(name);
            if (!required && !fields.Optional.Contains(name)) continue;
            if (value is not null && WorkEventGrammar.Fits(name, value, adapters, told.Kind)) frame[name] = value;
            else if (required) return Missed(name);
        }

        return new WorkEventFrame(frame, null);

        WorkEventFrame Missed(string field) => new(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["run"] = run, ["seq"] = seq, ["missed"] = missed, ["kind"] = WorkEventKinds.Missed, ["at"] = WorkEventGrammar.TimeOf(told.At),
            },
            field);
    }
}

/// <summary>A <c>work/event</c> frame as built, and the required field whose failure made it <c>events.missed</c>, or null.</summary>
public sealed record WorkEventFrame(IReadOnlyDictionary<string, object?> Frame, string? Unsent);
