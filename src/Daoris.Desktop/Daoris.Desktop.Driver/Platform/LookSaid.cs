namespace Daoris.Driver;

/// <summary>What a line the look says is (NOTIFY2h, D196 point 7; the notification design §5.1), and so where it goes.</summary>
public enum LookClass
{
    /// <summary>The look could not read or do something, and tries at the next: the machine log alone.</summary>
    Retry,

    /// <summary>The person's own act, carried out: never said back (R3), the machine log alone.</summary>
    Own,

    /// <summary>Something now waits on the person: its row of <i>What needs you</i> says it; the line is the log's.</summary>
    Wait,

    /// <summary>What moved as it was asked, which its own place says: the machine log alone.</summary>
    Progress,

    /// <summary>An end or a failure nobody pressed and nobody must answer: a coded notice in <i>Done and failed</i>, and the log.</summary>
    News,
}

/// <summary>
/// A <see cref="LookClass.News"/> line's notice (NOTIFY2h): its code, which the page words in the reader's language, and what it is
/// about. One notice per code and subject, so a line said again by a later look keeps one.
/// </summary>
/// <param name="Code">One of <see cref="LookSaid"/>'s news codes.</param>
/// <param name="Subject">What makes it one: a session's id, a quest's, a meeting's or a brainstorm's request.</param>
public sealed record LookNews(string Code, string Subject, string? Session = null, string? Quest = null, string? Workspace = null);

/// <summary>
/// A line's class as its producer judged it, carried with a line said later where it enters the look (NOTIFY2h2): a pass beside the
/// look whose line its run carries (<c>StartRun.Look</c>).
/// </summary>
/// <param name="Class">Its class; <see cref="LookClass.News"/> names its notice.</param>
/// <param name="Family">What made it, as <c>look.said</c> names it.</param>
/// <param name="News">A news line's notice.</param>
public sealed record LookClassed(LookClass Class, string Family, LookNews? News = null)
{
    /// <summary>A news line: its notice.</summary>
    public static LookClassed Noticed(string family, LookNews news) => new(LookClass.News, family, news);
}

/// <summary>A line with its class as its producer judged it, handed up to where it enters the look (NOTIFY2h2).</summary>
/// <param name="Text">The line, unchanged.</param>
/// <param name="Look">Its class.</param>
public sealed record LookLine(string Text, LookClassed Look);

/// <summary>
/// The lines one producer of the look says (NOTIFY2h2, D196's NOTIFY2h note): each classed as it is made, through
/// <see cref="LookSaid"/>, so a producer that hands the look its lines whole has no add that is not classed. <see cref="Said"/> is what
/// it hands on, the lines unchanged.
/// </summary>
/// <param name="said">The look's classing; <see cref="LookSaid.None"/> for a producer a test runs bare.</param>
/// <param name="family">What made them, as <c>look.said</c> names it.</param>
public sealed class LookLines(LookSaid said, string family)
{
    private readonly List<string> _lines = [];

    /// <summary>The lines said, in their order, unchanged.</summary>
    public IReadOnlyList<string> Said => _lines;

    /// <summary>How many lines were said.</summary>
    public int Count => _lines.Count;

    /// <inheritdoc cref="LookClass.Retry"/>
    public void Retry(string line) => _lines.Add(said.Retry(family, line));

    /// <inheritdoc cref="LookClass.Own"/>
    public void Own(string line) => _lines.Add(said.Own(family, line));

    /// <inheritdoc cref="LookClass.Wait"/>
    public void Wait(string line) => _lines.Add(said.Wait(family, line));

    /// <inheritdoc cref="LookClass.Progress"/>
    public void Progress(string line) => _lines.Add(said.Progress(family, line));

    /// <inheritdoc cref="LookSaid.Of"/>
    public void Of(LookClass @class, string line) => _lines.Add(said.Of(@class, family, line));

    /// <inheritdoc cref="LookClass.News"/>
    public void News(LookNews news, string line) => _lines.Add(said.News(family, news, line));
}

/// <summary>
/// Each line the look says, classed at its producer and written as the declared machine-log event <c>look.said</c> before it goes
/// anywhere (NOTIFY2h, D196 point 7; the notification design §5.1): a <see cref="LookClass.News"/> line is kept as a coded notice in
/// <i>Done and failed</i> through <see cref="Notices"/>, whatever the notify switch says; every other class is the log's alone.
/// </summary>
/// <remarks>
/// <para><b>The line itself is unchanged</b>: it goes on into the look's report as before, which the headless host prints and the
/// windows read to know a look moved something. No window toasts it (<c>ShellSignals</c>): it is English, and most of it is a look's
/// passing failure the next answers (D62).</para>
/// <para><b>The event carries the class and the family, never the line</b> (D94 §5): a line can quote a session's note or an agent's
/// words, which the session's own record keeps.</para>
/// <para><b>The census</b> (<c>LookSaidCensusTests</c>): every line a producer of the look adds goes through here, so a line with no
/// class fails the test.</para>
/// </remarks>
/// <param name="home">The home whose notices a news line is kept in; none keeps none.</param>
/// <param name="log">The machine log at the moment of the line, or none.</param>
/// <param name="noticed">Told after a notice is written: the desktop's notices slice, woken.</param>
/// <param name="clock">What time a notice is made at.</param>
public sealed class LookSaid(string? home, Func<MachineLog?> log, Action? noticed = null, Func<DateTimeOffset>? clock = null)
{
    /// <summary>The machine log's event.</summary>
    public const string Event = "look.said";

    /// <summary>A quest's session whose hand-over its judge refused: it was not handed over.</summary>
    public const string HandOverRefused = "hand-over-refused";

    /// <summary>A hand-over that could not be made: the session runs in another Daoris process on this machine.</summary>
    public const string HandOverElsewhere = "hand-over-elsewhere";

    /// <summary>An agent's ask to hand its session over, dropped: its session left this machine.</summary>
    public const string HandOverDropped = "hand-over-dropped";

    /// <summary>An agent's ask to hand its session over, refused by the person's rule.</summary>
    public const string HandOverAskRefused = "hand-over-ask-refused";

    /// <summary>An agent's ask to hand its session over, let go and then not taken.</summary>
    public const string HandOverFailed = "hand-over-failed";

    /// <summary>A quest handed to another agent that went back to this machine's agent: closed, taken elsewhere or lost to a reopen.</summary>
    public const string HandedBack = "handed-back";

    /// <summary>A session stopped because another machine's take on its quest reached the remote first.</summary>
    public const string LostClaim = "lost-claim";

    /// <summary>A session stopped because the person's reopen of the work its quest grew from took its quest.</summary>
    public const string LostToReopen = "lost-to-reopen";

    /// <summary>A meeting cut short: the driver that ran its round stopped.</summary>
    public const string MeetingCut = "meeting-cut";

    /// <summary>A brainstorm cut short: the driver that ran its passes stopped.</summary>
    public const string BrainstormCut = "brainstorm-cut";

    /// <summary>A session a Daoris before this one left running, ended as this one began.</summary>
    public const string Swept = "swept";

    /// <summary>A meeting stopped in a round by a failure, not by the person: ended as failed (NOTIFY2h2).</summary>
    public const string MeetingFailed = "meeting-failed";

    /// <summary>A brainstorm stopped by a failure, not by the person: ended as failed (NOTIFY2h2).</summary>
    public const string BrainstormFailed = "brainstorm-failed";

    /// <summary>An ask for a helper refused because the session that asked it has left this machine: nobody is told (NOTIFY2h2).</summary>
    public const string HelperAskerGone = "helper-asker-gone";

    /// <summary>A reopen naming a session this machine keeps no record of: nobody here is told (NOTIFY2h2).</summary>
    public const string ReopenUnrecorded = "reopen-unrecorded";

    /// <summary>A reopen, or a step's go-on, whose session's tree is gone: it is told nothing, and no work is made again (NOTIFY2h2).</summary>
    public const string ReopenTreeGone = "reopen-tree-gone";

    /// <summary>A plugin whose hook process exited three times within an hour: not started again until the person tries again (NOTIFY2h2).</summary>
    public const string PluginUnsound = "plugin-unsound";

    /// <summary>Every news code, which the page's words cover in both languages.</summary>
    public static readonly IReadOnlyList<string> NewsCodes =
        [HandOverRefused, HandOverElsewhere, HandOverDropped, HandOverAskRefused, HandOverFailed, HandedBack, LostClaim, LostToReopen,
         MeetingCut, BrainstormCut, Swept, MeetingFailed, BrainstormFailed, HelperAskerGone, ReopenUnrecorded, ReopenTreeGone,
         PluginUnsound];

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    /// <summary>Classes and writes nothing: for a look a test runs with no home and no log.</summary>
    public static LookSaid None { get; } = new(null, () => null);

    /// <inheritdoc cref="LookClass.Retry"/>
    public string Retry(string family, string line) => Said(LookClass.Retry, family, line);

    /// <inheritdoc cref="LookClass.Own"/>
    public string Own(string family, string line) => Said(LookClass.Own, family, line);

    /// <inheritdoc cref="LookClass.Wait"/>
    public string Wait(string family, string line) => Said(LookClass.Wait, family, line);

    /// <inheritdoc cref="LookClass.Progress"/>
    public string Progress(string family, string line) => Said(LookClass.Progress, family, line);

    /// <summary>A line whose class its producer judged from what it carries: a start's hold, an end's asker.</summary>
    public string Of(LookClass @class, string family, string line) =>
        @class == LookClass.News ? throw new ArgumentException("a news line names its notice: News", nameof(@class)) : Said(@class, family, line);

    /// <summary>A line its producer classed, said as it enters the look (NOTIFY2h2).</summary>
    public string Say(LookLine line) => Ran(line.Look, byPerson: false, line.Text);

    /// <summary>
    /// A run's line said as it enters the look, by the class its producer gave it (NOTIFY2h2); one its producer did not class is a
    /// session's end, which the watch announces (<c>progress</c>, the design §5.1's <c>:1246</c>), or the person's own (<c>own</c>).
    /// </summary>
    public string Ran(LookClassed? classed, bool byPerson, string line) => classed switch
    {
        { News: { } news } => News(classed.Family, news, line),
        { Class: LookClass.News } => throw new ArgumentException("a news line names its notice", nameof(classed)),
        { } judged => Said(judged.Class, judged.Family, line),
        null => Said(byPerson ? LookClass.Own : LookClass.Progress, "ended", line),
    };

    /// <inheritdoc cref="LookClass.News"/>
    public string News(string family, LookNews news, string line)
    {
        Said(LookClass.News, family, line);
        if (home is null) return line;
        var now = _clock();
        var notice = new Notice($"look-{news.Code}-{news.Subject}", Notices.Look, now, Workspace: news.Workspace, Session: news.Session,
            Quest: news.Quest, Code: news.Code);
        // A write refused is logged and dropped (§2.5); one kept is told, so the windows' slice reads it again, since a write this process
        // marked wakes nothing on its own watch; a notice kept already is told too, and the slice pushes only what moved.
        if (Notices.Add(home, notice, now) is { } why) log()?.Warn("notices.refused", ("why", why));
        else noticed?.Invoke();
        return line;
    }

    /// <summary>The class's word, as the event names it.</summary>
    public static string Word(LookClass @class) => @class switch
    {
        LookClass.Retry => "retry",
        LookClass.Own => "own",
        LookClass.Wait => "wait",
        LookClass.Progress => "progress",
        _ => "news",
    };

    private string Said(LookClass @class, string family, string line)
    {
        log()?.Info(Event, ("class", Word(@class)), ("family", family));
        return line;
    }
}
