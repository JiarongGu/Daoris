using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What the chat runner's classes share (PART10g, D166): what the runner was built with that more than one of them reads, the two
/// stores it keeps beside the transcripts, and the live maps that say which conversations run here and what each is doing. The
/// runner makes one and hands it to each class; a map one class alone reads stays that class's.
/// </summary>
/// <remarks>
/// <para><b>Each map has one owner, this</b>: an entry is added where a conversation starts, goes on or is asked to move, and
/// removed where it ends; every class reads the same map, so a question about a conversation has one answer.</para>
/// </remarks>
internal sealed class ChatContext(
    ServiceClient service, string home, SessionProcesses processes, SessionOutput? output, HarnessRoster harnesses,
    SessionEvents events)
{
    private volatile bool _closing;

    public ServiceClient Service { get; } = service;

    public string Home { get; } = home;

    public SessionProcesses Processes { get; } = processes;

    public SessionOutput? Output { get; } = output;

    /// <summary>Shared with the driver where a shell has both, so a probe is paid for once; the runner's own where it does not.</summary>
    public HarnessRoster Harnesses { get; } = harnesses;

    /// <summary>Where a conversation's structure is kept (D76): the shell's shared record, or the home's own.</summary>
    public SessionEvents Events { get; } = events;

    /// <summary>The harness's own conversation per session (ANSWER1a), a chat's now too, beside the transcripts under the same home.</summary>
    public HarnessConversations Conversations { get; } = new(home);

    /// <summary>The words a chat could not go on with (MSG1b's marks), so a reader knows they were tried.</summary>
    public GoOnMarks Marks { get; } = new(home);

    /// <summary>
    /// Which adapter each live conversation runs on: how a person's message is framed for its harness (CONV3). An entry lives
    /// exactly as long as the conversation's process.
    /// </summary>
    public ConcurrentDictionary<string, ISessionAdapter> Talking { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Each live conversation's watch, which ends by writing its record: what closing the driver waits on, because the host goes
    /// next and a record written after it is never written (2026-09-25).
    /// </summary>
    public ConcurrentDictionary<string, Task> Watching { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The conversations whose turns are visible, by session: the protocol door's (CONV3b) and the native door's on a structured
    /// wire (CONV4a). Every question a page asks of a turn is the same on both, so one map answers it (REV3 CLEAN1: it was two,
    /// and each question asked both in turn).
    /// </summary>
    public ConcurrentDictionary<string, ChatRunner.ITurnedChat> Turned { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, ActorRun> ActorRuns { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Each chat going on, started from another or being handed over, until its run is watched or it is said why not: one at a
    /// time, and what closing waits on.
    /// </summary>
    public ConcurrentDictionary<string, Task> GoingOn { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ask Daoris's conversations running here, by session, each with its workspace: one at a time runs in each (ASKSCOPE1a).</summary>
    public ConcurrentDictionary<string, string> Rooms { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The access mode each of Ask Daoris's running conversations got at its start (HELPMODE1a2, D183 point 3), by session: what a
    /// change of the person's access is compared with (ACCESSSWAP1), and the census of those running.
    /// </summary>
    public ConcurrentDictionary<string, string> HelpModes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The configured access each of Ask Daoris's running conversations was handed at its start, by session: its housekeeping
    /// (HELPMODE1c2a) and its chosen settings (HELPMODE1c3), what a change of a grant is compared with (ACCESSSWAP1), as its receipt
    /// keeps them.
    /// </summary>
    public ConcurrentDictionary<string, ConfiguredAccess> AccessHanded { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The Ask Daoris conversations whose access the person changed while they ran (ACCESSSWAP1, after HELPMODE1a5): <c>true</c> while
    /// one is due to end at its turn's end, <c>false</c> once it is ending. The entry goes with its run.
    /// </summary>
    public ConcurrentDictionary<string, bool> HelpEnding { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The conversations the person finished (<see cref="ChatRunner.Finish"/>), which wind up before their run ends (HELPNEWCHAT1): a
    /// start never carries one on. The entry goes with its run.
    /// </summary>
    public ConcurrentDictionary<string, byte> Finishing { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What a conversation started from an earlier one is handed with the person's first words: that one's transcript as a file.</summary>
    public ConcurrentDictionary<string, ChatRunner.Handing> Handing { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The line a new conversation's agent is told of the browser it was handed, held for its first message (BRWHINT1a), or
    /// appended to a hand-over's briefing.
    /// </summary>
    public ConcurrentDictionary<string, string> Browsers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What each curve move's decision said, by session, from its ask to its take: the take's two lines say it (SWITCH1c-i's
    /// pair). A box taken without it (a shell come up since) says the hand-over's lines, its reason <c>curve</c>.
    /// </summary>
    public ConcurrentDictionary<string, SwitchChoice.MoveTo> CurveMoves { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Each Chat's and Ask Daoris's pending start (MODELROLE1d2, D205 §3.5(b)), by its issued id and each replacement's: the owner of an
    /// attempt its agent refused at the open, and what a door asked by the issued id answers for.
    /// </summary>
    public ChatPendings Pending { get; } = new();

    /// <summary>Closing: no conversation starts, goes on or moves from here.</summary>
    public bool Closing => _closing;

    /// <summary>The runner is closing (<see cref="ChatRunner.StopAll"/>): once, and never undone.</summary>
    public void Close() => _closing = true;

    /// <summary>
    /// The machine's choices for a chat taken up by itself (MSG1c): the file every door reads where its override names this
    /// home's, as the shell's own config path is; this home's <c>driver.json</c> otherwise — the one resolution for a door that
    /// holds its home (CONFIGSEAM1).
    /// </summary>
    public DriverConfig MachineConfig() => DriverConfig.Load(DriverConfig.ResolvePath(Home));

    /// <summary>One event into a conversation's record. Sent is what the person asked for; the record's failure is its own.</summary>
    public void Record(string sessionId, SessionEvent e) => Events.Keep(sessionId, e, say: null);

    /// <summary>Whether this runner holds the conversation open, with its process or its turns.</summary>
    public bool Holds(string sessionId) => Turned.ContainsKey(sessionId) || Talking.ContainsKey(sessionId);

    /// <summary>The record read again, or null where the service did not answer: the first read's words then stand.</summary>
    public async Task<PriorSession?> ReadAgainAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            return await Service.RecordAsync(sessionId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>A driver's line in a conversation: its console and its record.</summary>
    public void Note(string sessionId, string text) => Note(sessionId, new Noted(text, []));

    /// <summary>A driver's line in a conversation with its parts, where its writer coded it (CONVNOTE2g).</summary>
    public void Note(string sessionId, Noted line)
    {
        Output?.Append(sessionId, line.Note);
        Events.Keep(
            sessionId,
            new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null },
            say: null);
    }
}
