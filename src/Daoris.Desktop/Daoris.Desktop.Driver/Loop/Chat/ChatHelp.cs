namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's conversations, kept (ASKHIST1): an earlier one goes on in its own harness conversation with the person's words,
/// as a repository's chat does (MSG1c, D137 §2.2), and a new one can start from an earlier one's words. A class over the runner's
/// context (PART10h, D166); the going-on of an ended one is <see cref="ChatGoOn"/>'s, which reads this class's set-aside and room.
/// </summary>
/// <remarks>
/// <para><b>One conversation runs in each workspace at a time</b> (HELP1a, ASKSCOPE1a), so going back to an earlier conversation,
/// or starting one from it, first sets aside its workspace's running one, which keeps its own id and can go on later. One
/// answering a turn is never set aside: the words wait, said why, and the next word tries again.</para>
///
/// <para><b>The room is written again</b> as every open writes it, from the machine as the shell hands it
/// (<see cref="DescribeHelp"/>): since PARALLEL1a the facts the judges keep, as last judged, so no open waits on a reading of the
/// machine; where nothing hands one, the room as last written serves and nothing outside it is read. It is the room of the
/// conversation's workspace (ASKSCOPE1b, <see cref="HelpRoom.GoesOnIn"/>), the machine projected to it.</para>
///
/// <para>It holds the runner for the new conversation it opens and for whether a turn is taking, as <see cref="ChatHandOver"/>
/// does: they are the runner's own start and the runner's own turns.</para>
/// </remarks>
internal sealed class ChatHelp(ChatContext context, ChatRunner runner) : IChatHelping
{
    /// <summary>How long a conversation set aside is waited for to end, so the ledger sees the room free.</summary>
    private static readonly TimeSpan SetAsideBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The machine as the room says it (ASKHIST1): the shell's reading, which <c>START_HELP</c> writes the room from, so a
    /// conversation going on finds the room as an open would. Null where nothing reads it, or it read nothing.
    /// </summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp { get; set; }

    /// <summary>
    /// Start a new Ask Daoris conversation from an earlier one (ASKHIST1, D233): its workspace's running conversation set aside, a new
    /// one opened as <c>START_HELP</c> opens one, and Daoris's brief of the earlier one's line (<see cref="ContextBriefs"/>) handed with
    /// the person's first message, as every new part of a line is handed one. Never a summary: Daoris makes no model call (D24).
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
    /// The part of <see cref="StartHelpFromAsync"/> a person's press waits for (HELPSTART1b): its workspace's running conversation
    /// set aside, the new one placed as <see cref="ChatRunner.PlaceHelpAsync"/> places one, and the earlier line's brief kept for it.
    /// The room is read and the agent spawned after, by <see cref="ChatRunner.RunHelpAsync"/>, off the request. A conversation
    /// not placed comes back with a null id and the sentence why; its message is what the finished start says.
    /// </summary>
    /// <param name="workspace">The workspace the person's window was on at the press (ASKSCOPE1a): the new one's, whose running one is set aside.</param>
    /// <param name="order">Ask Daoris's effective order, a new conversation's (MODELROLE1d), as <see cref="ChatRunner.PlaceHelpAsync"/> takes it.</param>
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

        if (await SetAsideHelpAsync(null, workspace, ct).ConfigureAwait(false) is { } answering) return new(null, $"{HeldWords.AnsweringWhy(answering)}.");

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
    /// Set aside its workspace's running conversation (HELP1a holds one at a time, ASKSCOPE1a one in each workspace): each other
    /// Ask Daoris conversation of that workspace running here is stopped, keeping its own id to go on later, and waited for until
    /// its record ends; another workspace's runs on. One answering a turn is never stopped: its id comes back instead
    /// (<see cref="HeldWords.Answering"/> says why).
    /// </summary>
    /// <param name="goingOn">The conversation going on, which is never its own obstacle; null for a new one.</param>
    /// <param name="workspace">The workspace of the one going on or starting, as its record keeps it; null is <c>default</c>.</param>
    public async Task<string?> SetAsideHelpAsync(string? goingOn, string? workspace, CancellationToken ct)
    {
        foreach (var other in Aside(context.Rooms, goingOn, workspace))
        {
            if (runner.Taking(other))
            {
                return other;
            }

            context.Processes.Stop(other);
            if (context.Watching.TryGetValue(other, out var watch)) await Task.WhenAny(watch, Task.Delay(SetAsideBound, ct)).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Which of the conversations running here (<paramref name="rooms"/>, each by its workspace) <see cref="SetAsideHelpAsync"/>
    /// sets aside: those of <paramref name="workspace"/>, never <paramref name="goingOn"/> itself.
    /// </summary>
    internal static IReadOnlyList<string> Aside(IReadOnlyDictionary<string, string> rooms, string? goingOn, string? workspace)
    {
        var circle = RemoteTarget.Workspace(workspace);
        return [.. rooms
            .Where(room => !string.Equals(room.Key, goingOn, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(RemoteTarget.Workspace(room.Value), circle, StringComparison.OrdinalIgnoreCase))
            .Select(room => room.Key)];
    }

    /// <summary>
    /// The room of <paramref name="workspace"/> as the machine stands now (ASKSCOPE1b: its facts projected to it, in its own
    /// folder), written again as an open writes it, and what reading across lets it read (D107); the room as last written,
    /// reading nothing outside it, where nothing hands a reading, the reading failed, or it did not answer within
    /// <see cref="HelpStart.ReadBound"/> (HELPSTUCK1), which the conversation <paramref name="sessionId"/> is told.
    /// </summary>
    /// <param name="workspace">The workspace whose room it goes on in (<see cref="HelpRoom.GoesOnIn"/>); null is <c>default</c>'s.</param>
    public async Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, string? workspace, CancellationToken ct)
    {
        if (DescribeHelp is { } describe)
        {
            try
            {
                // The help start's own read and bound, so a conversation going on waits for the machine exactly as one opening does.
                var start = runner.Executors.Of<HelpStart>();
                var (read, late, waited) = await start.ReadAsync(describe, workspace, ct).ConfigureAwait(false);
                if (read?.Within(workspace) is { } machine)
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
}
