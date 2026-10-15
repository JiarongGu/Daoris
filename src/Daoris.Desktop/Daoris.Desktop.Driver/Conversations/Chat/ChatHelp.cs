using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's conversations, kept (ASKHIST1): an earlier one goes on in its own harness conversation with the person's words,
/// as a repository's chat does (MSG1c, D137 §2.2), and a new one can start from an earlier one's words. A class over the runner's
/// context (PART10h, D166); the going-on of an ended one is <see cref="ChatGoOn"/>'s, which reads this class's set-aside and room.
/// </summary>
/// <remarks>
/// <para><b>One conversation runs in the room at a time</b> (HELP1a): the ledger holds the room as a tree, so going back to an
/// earlier conversation, or starting one from it, first sets aside the one running here, which keeps its own id and can go on
/// later. One answering a turn is never set aside: the words wait, said why, and the next word tries again.</para>
///
/// <para><b>The room is written again</b> as every open writes it, from the machine as the shell reads it now
/// (<see cref="DescribeHelp"/>); where nothing hands a reading, the room as last written serves and nothing outside it is read.</para>
///
/// <para>It holds the runner for the new conversation it opens and for whether a turn is taking, as <see cref="ChatHandOver"/>
/// does: they are the runner's own start and the runner's own turns.</para>
/// </remarks>
internal sealed class ChatHelp(ChatContext context, ChatRunner runner)
{
    /// <summary>How long a conversation set aside is waited for to end, so the ledger sees the room free.</summary>
    private static readonly TimeSpan SetAsideBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The machine as the room says it (ASKHIST1): the shell's reading, which <c>START_HELP</c> writes the room from, so a
    /// conversation going on finds the room as an open would. Null where nothing reads it, or it read nothing.
    /// </summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp { get; set; }

    /// <summary>
    /// Start a new Ask Daoris conversation from an earlier one's words (ASKHIST1): the room's running conversation set aside, a
    /// new one opened as <c>START_HELP</c> opens one, and the earlier one's transcript (<see cref="HelpTranscript"/>) kept as its
    /// file, handed with the person's first message. Never a summary: Daoris makes no model call (D24).
    /// </summary>
    /// <param name="earlier">The earlier conversation, an Ask Daoris record of this machine's; the caller has judged that.</param>
    public async Task<ChatStart> StartHelpFromAsync(
        string earlier, string adapter, DriverConfig config, HelpMachine machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        var placed = await PlaceHelpFromAsync(earlier, adapter, config, ct).ConfigureAwait(false);
        return placed.SessionId is null
            ? new(null, placed.Message)
            : await runner.RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(machine), onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The part of <see cref="StartHelpFromAsync"/> a person's press waits for (HELPSTART1b): the room's running conversation set
    /// aside, the new one placed as <see cref="ChatRunner.PlaceHelpAsync"/> places one, and the earlier one's words kept for it.
    /// The room is read and the agent spawned after, by <see cref="ChatRunner.RunHelpAsync"/>, off the request. A conversation
    /// not placed comes back with a null id and the sentence why; its message is what the finished start says.
    /// </summary>
    public async Task<HelpPlaced> PlaceHelpFromAsync(string earlier, string adapter, DriverConfig config, CancellationToken ct = default)
    {
        if (context.Closing) return new(null, "the application is closing, so no conversation starts now.");

        var kept = new HelpConversations(context.Home);
        var title = (SessionEventText.IsId(earlier) ? kept.Read(earlier).Name : null)
                    ?? context.Events.Openings([earlier]).GetValueOrDefault(earlier);
        if (HelpTranscript.Of(context.Events, earlier, title) is not { } transcript)
        {
            return new(null, $"nothing was said in conversation `{earlier}`, so there is nothing to start from.");
        }

        if (await SetAsideHelpAsync(null, ct).ConfigureAwait(false) is { } answering) return new(null, $"{HeldWords.AnsweringWhy(answering)}.");

        var placed = await runner.PlaceHelpAsync(adapter, config, ct).ConfigureAwait(false);
        if (placed.SessionId is not { } id) return placed;

        try
        {
            var files = ChatFiles.Keep(context.Home, id, [new ChatUpload(HelpTranscript.FileName(earlier), Encoding.UTF8.GetBytes(transcript))]);
            context.Handing[id] = new ChatRunner.Handing(files, HelpTranscript.Preface(earlier));
            kept.StartedFrom(id, earlier, HelpConversations.Transcript);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return placed with { Message = $"Ask Daoris `{id}` opened, but conversation `{earlier}`'s words could not be kept for it: {error.Message}" };
        }

        return placed with { Message = $"Ask Daoris `{id}` opened from conversation `{earlier}`: its words go with your first message." };
    }

    /// <summary>
    /// Set aside the room's running conversation (HELP1a holds one at a time): each other Ask Daoris conversation running here
    /// is stopped, keeping its own id to go on later, and waited for until its record ends. One answering a turn is never
    /// stopped: its id comes back instead (<see cref="HeldWords.Answering"/> says why).
    /// </summary>
    /// <param name="goingOn">The conversation going on, which is never its own obstacle; null for a new one.</param>
    public async Task<string?> SetAsideHelpAsync(string? goingOn, CancellationToken ct)
    {
        foreach (var other in context.Rooms.Keys.Where(id => !string.Equals(id, goingOn, StringComparison.OrdinalIgnoreCase)).ToList())
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
    /// The room as the machine stands now, written again as an open writes it, and what reading across lets it read (D107); the
    /// room as last written, reading nothing outside it, where nothing hands a reading or the reading failed.
    /// </summary>
    public async Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(CancellationToken ct)
    {
        if (DescribeHelp is { } describe)
        {
            try
            {
                if (await describe(ct).ConfigureAwait(false) is { } machine) return (HelpRoom.Prepare(context.Home, machine), machine.Reads);
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Any reading's failure, the service's or a file's: the room on disk still says what Daoris is and its doors.
            }
        }

        return (HelpRoom.PathOf(context.Home), []);
    }
}
