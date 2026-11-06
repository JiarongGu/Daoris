namespace Daoris.Driver;

/// <summary>
/// Every line <c>events relay</c> writes, on either stream (EVENTHOOK1c; the event hook design §6.1 step 1, §8): a closed list of fixed English
/// sentences whose only values are a checked <c>run</c> and <c>seq</c>, an id in its minted shape, a closed code or a count, so nothing a
/// process writes to the pipe reaches the session as words. They are a machine's words to an agent, not the window's chrome, so they are
/// not translated. <see cref="Start"/> alone names a path: the copy's command, which <c>--where</c> prints for its session to run.
/// </summary>
public static class RelaySaid
{
    /// <summary>Why a relay no plugin reached ends: none came, only refused ones came, or one was refused as not contained (P5).</summary>
    public static readonly IReadOnlyList<string> NoEventsReasons = ["no-client", "refused", "not-contained"];

    /// <summary>Why the relay stopped, a closed code: its home, its lease, its copy, its pipe, or anything else.</summary>
    public static readonly IReadOnlyList<string> StopCodes = ["home", "lease", "copy", "pipe", "failed"];

    /// <summary>The diagnostics tallied as the relay ends, one line each it met (C4-2.3), and what each counts.</summary>
    public static readonly IReadOnlyDictionary<string, string> Tallies = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["dropped"] = "frames outside the grammar dropped",
        ["repeated"] = "repeated frames dropped",
        ["refused"] = "clients refused",
        ["unchecked"] = "clients taken unchecked across elevations",
    };

    /// <summary>The home was held by another act for the whole wait: <c>--where</c> laid nothing out (exit 1).</summary>
    public const string HeldByAnother = "Daoris: the home is held by another act; nothing was laid out; run --where again";

    /// <summary>The client's job could not be opened, so it is taken unchecked, its first line still checked (P1).</summary>
    public const string Unchecked = "Daoris: the client could not be checked across elevations; it is taken unchecked";

    /// <summary>A client outside the holder's agents' job, an uncontained plugin among them.</summary>
    public const string NotContained = "Daoris: refused a client that is not contained";

    /// <summary>A relay given neither <c>--home</c> nor <c>DAORIS_HOME</c> (exit 2).</summary>
    public const string NoHome = "Daoris: the relay was given no home; run events relay --where for its command";

    /// <summary>A relay started anywhere but its build's folder, or from a build being removed (exit 1).</summary>
    public const string NotFromCopy = "Daoris: the relay runs only from its laid-out copy; run events relay --where for its command";

    /// <summary>Words the verb does not take (exit 2).</summary>
    public const string Usage =
        "Daoris: events relay takes --where, or --once with --home <home>, --since <run>:<seq>, --kinds <kind,...>, --not-by <id> and --session-pid <pid>; nothing was served";

    /// <summary>A junction or a symbolic link met under the relay's folders: said, and never followed or removed through.</summary>
    public const string Linked = "Daoris: a link under the relay's folders was left, never followed";

    /// <summary>A <c>--since</c> that is not a bracket a batch closed with (exit 2).</summary>
    public const string SinceRefused = "Daoris: --since takes the bracket of the last batch's closing line as <run>:<seq>; nothing was served";

    /// <summary>
    /// <c>--where</c>'s one line naming a path: the copy's command, and where its batch is read. A background command's end names the file
    /// its output is in and does not carry its lines (the event hook measurements, M5), so its session reads that file.
    /// </summary>
    public static string Start(string copy, string home) =>
        $"Daoris: start in the background: \"{copy}\" events relay --once --home \"{home}\"; when it ends, read the batch from the output file its end names";

    /// <summary>A gap: what lay there is the records' to say.</summary>
    public static string Gap(string run, long seq) => $"Daoris: events may have been missed; read the sessions again {Bracket(run, seq)}";

    /// <summary>Events past the batch's bound, counted.</summary>
    public static string More(int count, string run, long seq) => $"Daoris: {count} more events; read the sessions again {Bracket(run, seq)}";

    /// <summary>Every batch's closing line: its watermark, which the next relay's <c>--since</c> takes.</summary>
    public static string ReadUpTo(string run, long seq) => $"Daoris: read up to {Bracket(run, seq)}";

    /// <summary>A plugin of another build connected: this copy is stale.</summary>
    public static string Updated(string run, long seq) => $"Daoris: Daoris was updated; start the relay again {Bracket(run, seq)}";

    /// <summary>A second client passed the first line's check while one was connected.</summary>
    public static string SecondClient(string run, long seq) => $"Daoris: a second client claimed the events; read the sessions again {Bracket(run, seq)}";

    /// <summary>The bound: no plugin reached the relay while an instance held the home.</summary>
    public static string NoEvents(string reason, string run, long seq) =>
        $"Daoris: no events reached this relay ({Closed(reason, NoEventsReasons)}); read the sessions again {Bracket(run, seq)}";

    /// <summary>The relay's own failure, by its closed code (exit 2).</summary>
    public static string Stopped(string code) => $"Daoris: the relay stopped ({Closed(code, StopCodes)})";

    /// <summary>One diagnostic kind's count, said as the relay ends.</summary>
    public static string Tally(string kind, int count) =>
        Tallies.TryGetValue(kind, out var counted) ? $"Daoris: {count} {counted}" : throw new ArgumentOutOfRangeException(nameof(kind));

    /// <summary>One event's line, built from a checked frame: its kind, its codes and its ids.</summary>
    public static string Event(RelayFrame frame)
    {
        var fields = frame.Fields;
        string Of(string field, string named) => fields.TryGetValue(field, out var value) ? $" ({named} {value})" : "";
        var quest = Of("quest", "quest");
        var said = frame.Kind switch
        {
            WorkEventKinds.SessionParked => $"session {fields["session"]} parked, waiting on the person{quest}{Of("ask", "ask")}",
            WorkEventKinds.QuestParked => $"quest {fields["quest"]} parked on its failed sessions{Of("strikes", "strikes")}{Of("session", "session")}",
            WorkEventKinds.SessionEnded => $"session {fields["session"]} ended: {fields["state"]}{quest}{Of("adapter", "adapter")}",
            WorkEventKinds.WorkLanded => $"session {fields["session"]}'s work {fields["code"]}{quest}",
            // EVENTHOOK1d (the event channel design §3.6).
            WorkEventKinds.QuestPublished => $"quest {fields["quest"]} published",
            WorkEventKinds.QuestClosed => $"quest {fields["quest"]} closed, {fields["state"]}",
            WorkEventKinds.QuestCloseLost => $"quest {fields["quest"]} is open again: its close lost to another machine ({fields["state"]})",
            WorkEventKinds.ConfirmationEnded => $"confirmation {fields["confirmation"]} ended: {fields["end"]}",
            WorkEventKinds.WorkEnded => $"session {fields["session"]}'s work ended: the line holds it{quest}",
            // CHANNELJOIN1d (the event channel design §5.7): an id and codes only, never the message's words.
            WorkEventKinds.ChannelPosted =>
                $"{(fields.TryGetValue("message", out var kept) ? $"message {kept}" : "a message")} posted by {fields["by"]}{Of("poster", "poster")}{Of("ask", "ask")}"
                + (fields.ContainsKey("urgent") ? ", urgent" : ""),
            _ => throw new ArgumentOutOfRangeException(nameof(frame), "a kind the relay has no sentence for is a gap, never a line"),
        };
        return $"Daoris: {said} {Bracket(frame.Run, frame.Seq)}";
    }

    /// <summary>The kinds <see cref="Event"/> says; any other the grammar names is said as a gap.</summary>
    public static readonly IReadOnlyList<string> Said =
    [
        WorkEventKinds.SessionParked, WorkEventKinds.QuestParked, WorkEventKinds.SessionEnded, WorkEventKinds.WorkLanded, WorkEventKinds.QuestPublished,
        WorkEventKinds.QuestClosed, WorkEventKinds.QuestCloseLost, WorkEventKinds.ConfirmationEnded, WorkEventKinds.WorkEnded,
        WorkEventKinds.ChannelPosted,
    ];

    private static string Bracket(string run, long seq) => $"[{run} #{seq}]";

    private static string Closed(string code, IReadOnlyList<string> codes) =>
        codes.Contains(code, StringComparer.Ordinal) ? code : throw new ArgumentOutOfRangeException(nameof(code));
}
