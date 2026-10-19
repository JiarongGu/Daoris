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
/// <para><b>The room is written again</b> as every open writes it, from the machine as the shell hands it
/// (<see cref="DescribeHelp"/>): since PARALLEL1a the facts the judges keep, as last judged, so no open waits on a reading of the
/// machine; where nothing hands one, the room as last written serves and nothing outside it is read.</para>
///
/// <para>It holds the runner for the new conversation it opens and for whether a turn is taking, as <see cref="ChatHandOver"/>
/// does: they are the runner's own start and the runner's own turns.</para>
/// </remarks>
internal sealed class ChatHelp(ChatContext context, ChatRunner runner)
{
    /// <summary>How long a conversation set aside is waited for to end, so the ledger sees the room free.</summary>
    private static readonly TimeSpan SetAsideBound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a start waits for the machine's reading before the room as last written serves (HELPSTUCK1). The reading walked git
    /// across every tree and repository: 24.9 s for its session look alone on a loaded machine at HELPSTART1, and no answer for
    /// minutes at HELPSTUCK1, while the conversation sat at <c>queued</c>. Since PARALLEL1a the reading is the facts as last
    /// judged, which answer at once, so this bounds only a home whose facts were never judged and with no room written.
    /// </summary>
    public static readonly TimeSpan DefaultReadBound = TimeSpan.FromSeconds(30);

    /// <summary>How long a start waits for the machine's reading (<see cref="DefaultReadBound"/>); a test's is shorter.</summary>
    public TimeSpan ReadBound { get; set; } = DefaultReadBound;

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
    /// room as last written, reading nothing outside it, where nothing hands a reading, the reading failed, or it did not answer
    /// within <see cref="ReadBound"/> (HELPSTUCK1), which the conversation <paramref name="sessionId"/> is told.
    /// </summary>
    public async Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, CancellationToken ct)
    {
        if (DescribeHelp is { } describe)
        {
            try
            {
                var (machine, late, waited) = await ReadAsync(describe, ct).ConfigureAwait(false);
                if (machine is not null)
                {
                    var room = HelpRoom.Prepare(context.Home, machine);
                    if (machine.NotReadYet.Any) context.Note(sessionId, NotReadYet());
                    return (room, machine.Reads);
                }

                if (late) context.Note(sessionId, waited ? AsWritten(ReadBound) : NotReadYet());
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Any reading's failure, the service's or a file's: the room on disk still says what Daoris is and its doors.
            }
        }

        return (HelpRoom.PathOf(context.Home), []);
    }

    /// <summary>
    /// How long a reading left behind by its start's bound may still run, writing the room when it answers (HELPSTUCK1), before
    /// it is cancelled.
    /// </summary>
    public static readonly TimeSpan ReadBackstop = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The machine for the room (PARALLEL1a, D181 point 1): the facts as last judged, which answer at once. A reading still under
    /// way is a home whose facts were never judged at all: with a room written before, it is not waited for, and comes back late
    /// at once, the room as last written serving; with none, it is waited for within <see cref="ReadBound"/> (HELPSTUCK1's
    /// bound, now the backstop), and comes back late, having waited, where it did not answer in time. The caller starts on the
    /// room as last written, saying so, or ends saying why. The reading left behind goes on, until <see cref="ReadBackstop"/>,
    /// and writes the room when it answers, so the next conversation starts on the machine as read.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled while it waited.</exception>
    public async Task<(HelpMachine? Read, bool Late, bool Waited)> ReadAsync(
        Func<CancellationToken, Task<HelpMachine?>> machine, CancellationToken ct)
    {
        var backstop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        backstop.CancelAfter(ReadBackstop);
        Task<HelpMachine?> reading;
        try
        {
            reading = machine(backstop.Token);
        }
        catch
        {
            backstop.Dispose();
            throw;
        }

        _ = reading.ContinueWith(_ => backstop.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        if (!reading.IsCompleted && HelpRoom.Written(context.Home))
        {
            _ = WriteWhenReadAsync(reading);
            return (null, true, false);
        }

        try
        {
            return (await reading.WaitAsync(ReadBound, ct).ConfigureAwait(false), false, false);
        }
        catch (TimeoutException)
        {
            _ = WriteWhenReadAsync(reading);
            return (null, true, true);
        }
    }

    /// <summary>A reading left behind writes the room once it answers (HELPSTUCK1); one that fails late writes nothing.</summary>
    private async Task WriteWhenReadAsync(Task<HelpMachine?> reading)
    {
        try
        {
            if (await reading.ConfigureAwait(false) is { } read && !context.Closing) HelpRoom.Prepare(context.Home, read);
        }
        catch (Exception)
        {
            // Late, failed or cancelled at its backstop: the room as last written stands, and the next start reads again.
        }
    }

    /// <summary>Whole seconds of a bound as a note says it: a part of one is one.</summary>
    private static int Seconds(TimeSpan bound) => Math.Max(1, (int)Math.Ceiling(bound.TotalSeconds));

    /// <summary>
    /// The line a conversation is told first where it starts on the room as last written (HELPSTUCK1): the machine was not read
    /// within the bound, and checkouts outside the room are not read until the next conversation.
    /// </summary>
    public static Noted AsWritten(TimeSpan bound) => Noted.Of(
        NoteCodes.HelpRoomAsWritten,
        $"This machine was not read within {Seconds(bound)} seconds, so Ask Daoris starts on its room as last written, reading no "
        + "checkout outside it; the room is written again once the machine is read.",
        ("seconds", Seconds(bound)));

    /// <summary>
    /// The line a conversation is told first where it starts on facts that leave a part of the machine not read yet, or on the room
    /// as last written before any was judged (PARALLEL1a, D181 point 1): it started at once, and its room says what is not read yet.
    /// </summary>
    public static Noted NotReadYet() => Noted.Of(
        NoteCodes.HelpRoomNotReadYet,
        "Ask Daoris started at once on this machine as read so far: what is not read yet, its room says, and the room is written "
        + "again as each part is read.");

    /// <summary>
    /// What a start says where the machine was not read within the bound and no room was ever written (HELPSTUCK1): nothing can
    /// serve, so it ends failed.
    /// </summary>
    public static Noted NotRead(TimeSpan bound) => Noted.Of(
        NoteCodes.HelpRoomUnread,
        $"Ask Daoris's room could not be written: this machine was not read within {Seconds(bound)} seconds, and no room was "
        + "written before. Ask again once it is less busy.",
        ("seconds", Seconds(bound)));
}
