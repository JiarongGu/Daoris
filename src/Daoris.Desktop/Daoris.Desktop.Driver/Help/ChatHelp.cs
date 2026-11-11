namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's conversations, kept: an earlier one goes on in its own harness conversation with the person's words,
/// as a repository's chat does, and a new one can start from an earlier one's words. A registered executor; the
/// going-on of an ended one is <see cref="ChatGoOn"/>'s, which reads this class's set-aside and room.
/// </summary>
/// <remarks>
/// <para><b>Each conversation is its own</b>: going back to an earlier conversation, or starting one from it, sets nothing
/// aside and waits on no other's turn. What they share is the room's files, which one writer at a time writes whole
/// (<see cref="HelpRoomWrites"/>).</para>
///
/// <para><b>The room is written again</b> as every open writes it, from the machine as the shell hands it
/// (<see cref="DescribeHelp"/>): the facts the judges keep, as last judged, so no open waits on a reading of the
/// machine; where nothing hands one, the room as last written serves and nothing outside it is read. It is the room of the
/// conversation's workspace (<see cref="HelpRoom.GoesOnIn"/>), the machine projected to it.</para>
///
/// <para>It opens through the runner's own start and asks the runner's own turns whether one is taking, as <see cref="ChatHandOver"/>
/// does.</para>
/// </remarks>
[Executor("help-rooms", ExecutorPhase.Start, "help")]
internal sealed class ChatHelp(IExecutorWiring wiring) : IChatHelping
{
    private ChatContext context => wiring.Context;

    private IChatRunning runner => wiring;

    /// <summary>The machine as the shell reads it, which every open and go-on writes the room from; null where nothing reads it.</summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp { get; set; }

    /// <summary>
    /// Start a new Ask Daoris conversation from an earlier one, opened as <c>START_HELP</c> opens one, Daoris's brief of the earlier
    /// one's line (<see cref="ContextBriefs"/>) handed with the person's first message. Never a summary: Daoris makes no model call.
    /// </summary>
    /// <param name="earlier">The earlier conversation, an Ask Daoris record of this machine's; the caller has judged that.</param>
    public async Task<ChatStart> StartHelpFromAsync(
        string earlier, string adapter, DriverConfig config, HelpMachine machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        var placed = await PlaceHelpFromAsync(earlier, adapter, config, ct: ct).ConfigureAwait(false);
        return placed.SessionId is null
            ? new(null, placed.Message)
            : await runner.RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(machine), onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The part of <see cref="StartHelpFromAsync"/> a person's press waits for: the new one placed as
    /// <see cref="ChatRunner.PlaceHelpAsync"/> places one, and the earlier line's brief kept for it.
    /// The room is read and the agent spawned after, by <see cref="ChatRunner.RunHelpAsync"/>, off the request. A conversation
    /// not placed comes back with a null id and the sentence why; its message is what the finished start says.
    /// </summary>
    /// <param name="workspace">The workspace the person's window showed at the press: the new one's.</param>
    /// <param name="order">Ask Daoris's effective order, a new conversation's, as <see cref="ChatRunner.PlaceHelpAsync"/> takes it.</param>
    public async Task<HelpPlaced> PlaceHelpFromAsync(
        string earlier, string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default,
        IReadOnlyList<ActorEffective>? order = null)
    {
        if (context.Closing) return new(null, "the application is closing, so no conversation starts now.");

        var (said, _) = context.Events.Spoken(earlier);
        if (!said.Any(passage => passage.Text.Trim().Length > 0))
        {
            return new(null, $"nothing was said in conversation `{earlier}`, so there is nothing to start from.");
        }

        // Composed before the new one opens, so the line it hands ends at the earlier conversation.
        var brief = await ContextBriefs.ComposeAsync(context.Service, context.Events, context.Home, earlier, HandOverWhy.Press, conversation: true, ct: ct)
            .ConfigureAwait(false);
        var placed = await runner.PlaceHelpAsync(adapter, config, workspace, ct, order: order).ConfigureAwait(false);
        if (placed.SessionId is not { } id) return placed;

        context.Handing[id] = new Handing(brief);
        try
        {
            new HelpConversations(context.Home).StartedFrom(id, earlier, HelpConversations.Brief);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its brief still says which it continues.
        }

        return placed with { Message = $"Ask Daoris `{id}` opened from conversation `{earlier}`: Daoris's brief of it goes with your first message." };
    }

    /// <summary>
    /// The room of <paramref name="workspace"/> as the machine stands now (its facts projected to it, in its own
    /// folder), written again as an open writes it, and what reading across lets it read; the room as last written,
    /// reading nothing outside it, where nothing hands a reading, the reading failed, or it did not answer within
    /// <see cref="HelpStart.ReadBound"/>, which the conversation <paramref name="sessionId"/> is told.
    /// </summary>
    /// <param name="workspace">The workspace whose room it goes on in (<see cref="HelpRoom.GoesOnIn"/>); null is <c>default</c>'s.</param>
    public async Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, string? workspace, CancellationToken ct)
    {
        if (DescribeHelp is { } describe)
        {
            try
            {
                // The help start's own read and bound, so a conversation going on waits for the machine exactly as one opening does.
                var start = wiring.Of<HelpStart>();
                var (read, late, waited) = await start.ReadAsync(describe, workspace, ct).ConfigureAwait(false);
                if (read?.Within(workspace, FocusOf(sessionId, workspace)) is { } machine)
                {
                    var room = HelpRoom.Prepare(context.Home, machine);
                    if (machine.NotReadYet.Any) context.Note(sessionId, HelpStart.NotReadYet());
                    return (room, machine.Reads);
                }

                if (late) context.Note(sessionId, waited ? HelpStart.AsWritten(start.ReadBound) : HelpStart.NotReadYet());
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Any reading's failure, the service's or a file's: the room on disk still says what Daoris is and its doors.
            }
        }

        return (HelpRoom.PathOf(context.Home, workspace), []);
    }

    public HelpFocus FocusOf(string sessionId, string? workspace) => new HelpFocusing(context.Home).Applied(sessionId, workspace);

    public IReadOnlyList<SaidWordView> TakeFocus(
        string sessionId, string? workspace, IReadOnlyList<SaidWordView> words, IReadOnlyList<RepoView>? registry) =>
        HelpFocusTurn.Apply(context.Home, sessionId, workspace, registry, words.FirstOrDefault()) is { } change
            ? HelpFocusTurn.Ahead(words, change.Line)
            : words;

    public void CarryFocus(string from, string to, string? workspace)
    {
        var focusing = new HelpFocusing(context.Home);
        focusing.Begin(to, focusing.Applied(from, workspace));
    }
}
