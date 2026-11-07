namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's conversation: a chat about Daoris itself, placed in its workspace's room on the helper's agent, then its room written from
/// the machine and its agent spawned as any conversation's. The record is the service's; the process is this machine's.
/// </summary>
/// <remarks>
/// <para><b>It reads, and it advises</b>: handed the knowledge connector on either door, the room's allow-list and the person's denies,
/// and none of the plugins' servers, since a browser brought up for a tool it is not allowed would be a window for nothing.</para>
///
/// <para><b>Its tools load with the first request</b>, as every session's do, so the first answer is not a search for the tools it is
/// handed.</para>
///
/// <para><b>One at a time in each workspace</b>: the ledger refuses a second while one runs, naming it, and the caller hands the person the
/// running one rather than asking for another.</para>
/// </remarks>
[Executor("help", ExecutorPhase.Start)]
internal sealed class HelpStart(IExecutorWiring wiring) : IExecutor
{
    /// <summary>
    /// How long a start waits for the machine's reading before the room as last written serves. The room reads the facts as last judged, which answer at once,
    /// so this bounds only a home whose facts were never judged and with no room written.
    /// </summary>
    public static readonly TimeSpan DefaultReadBound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a reading left behind by its start's bound may still run, writing the room when it answers, before it is cancelled.
    /// </summary>
    public static readonly TimeSpan ReadBackstop = TimeSpan.FromMinutes(10);

    /// <summary>How long a start waits for the machine's reading (<see cref="DefaultReadBound"/>); a test's is shorter.</summary>
    public TimeSpan ReadBound { get; set; } = DefaultReadBound;

    /// <summary>Place the conversation (<see cref="PlaceHelpAsync"/>), then run it on <paramref name="machine"/>.</summary>
    public async Task<ChatStart> StartHelpAsync(
        string adapter, DriverConfig config, HelpMachine machine, Func<string, string, Task>? onEnded, CancellationToken ct,
        IReadOnlyList<ActorEffective>? order)
    {
        var placed = await PlaceHelpAsync(adapter, config, ct: ct, order: order).ConfigureAwait(false);
        return placed.SessionId is null
            ? new(null, placed.Message)
            : await RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(machine), onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Place the conversation: its agent resolved, its account chosen and its record opened in its workspace's room folder
    /// (<see cref="HelpRoom.PathOf(string, string?)"/>), whose path is known before anything is read or written. What the person's press
    /// waits for; <see cref="RunHelpAsync"/> reads the machine, writes the room and spawns the agent after.
    /// </summary>
    /// <param name="workspace">
    /// The workspace the person's window showed at the press, captured there, so a window that moves meanwhile moves nothing here: its record
    /// keeps it and its account is that workspace's. Null is a press that named none, which opens in <c>default</c>.
    /// </param>
    /// <param name="carries">
    /// The conversation whose account's limit refused its turn, which this one goes on from on the next account: its opening line and
    /// <c>account.rotated</c> name it, as a driven carry-on's do. Null for every other start.
    /// </param>
    /// <param name="order">
    /// Ask Daoris's effective order for a new conversation: one entry runs on its agent, asking its model; more are walked choice by choice,
    /// each entry judged first by <see cref="HelpAccess.For"/> under the mode standing now. Null for one that goes on from another, which
    /// runs on <paramref name="adapter"/>.
    /// </param>
    /// <exception cref="DriverException">The agent names no harness this machine knows.</exception>
    public async Task<HelpPlaced> PlaceHelpAsync(
        string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default, string? carries = null,
        IReadOnlyList<ActorEffective>? order = null)
    {
        if (order is { Count: > 1 })
        {
            var walk = await wiring.WalkAsync("ask-daoris", order, config, workspace, null, ct).ConfigureAwait(false);
            if (walk is not { Adapter: { } walked, Selection: { } taken, Chosen: { } entry }) return new(null, walk.Refusal!);
            return await OpenHelpAsync(walked, taken, entry, walk.Passes, config, workspace, carries, ct, order: order).ConfigureAwait(false);
        }

        var chosen = order is [var only] ? only : null;
        if (chosen is not null) adapter = chosen.Agent;
        var harnesses = wiring.Context.Harnesses;
        var resolved = harnesses.Adapters.Door(adapter);
        if (!resolved.Can.Interactive)
        {
            return new(
                null,
                $"the `{resolved.Id.Value}` adapter does not hold conversations — it spawns an agent that "
                + "takes its target once and runs to completion. Name another under Agents → Roles, Ask Daoris, "
                + "or `daoris role set ask-daoris \"<agent>\"`.");
        }

        // Full access on an agent with no mode that asks nothing is refused before anything is opened.
        var access = HelpAccess.For(config.HelpMode, new ChatAgent(wiring.Context.Harnesses.Adapters, resolved));
        if (access.Refusal is { } refused) return new(null, refused);

        // The account its workspace names, then the machine's, as a start in that workspace takes one.
        var selection = await harnesses
            .SelectAsync(resolved.Id.Value, config, workspace, null, StartKind.Conversation, ct, chosen?.Model).ConfigureAwait(false);
        if (!selection.Allowed) return new(null, selection.Refusal!);
        return await OpenHelpAsync(resolved, selection, chosen, [], config, workspace, carries, ct, order: order).ConfigureAwait(false);
    }

    /// <summary>
    /// Run the conversation <see cref="PlaceHelpAsync"/> placed: the room written from the machine as the facts the judges keep say it, at
    /// once, then the agent spawned as any conversation's. A reading or a room that fails ends the record <c>failed</c> saying why, so a
    /// conversation handed to the person says what became of it rather than waiting at <c>queued</c>. Where no fact has ever been judged, the
    /// room as last written serves, said first in its conversation; with none written before, the first judgement is waited for within
    /// <see cref="ReadBound"/>, and past it the conversation ends <c>failed</c> saying so.
    /// </summary>
    /// <param name="machine">
    /// The whole machine from the facts as last judged; null where it could not be read. The room written is the placed workspace's: the
    /// machine projected to it (<see cref="HelpMachine.Within"/>) in its own folder.
    /// </param>
    public async Task<ChatStart> RunHelpAsync(
        HelpPlaced placed, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (placed is not { SessionId: { } sessionId, Adapter: { } resolved, Selection: { } selection }) return new(null, placed.Message);

        var context = wiring.Context;
        // A start being made, as a start from a conversation's words is: no go-on takes it up meanwhile, and a closing waits for it.
        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.GoingOn.TryAdd(sessionId, going.Task);
        try
        {
            string room;
            IReadOnlyList<HelpRead> reads;
            try
            {
                // 🔴 The room is the facts the judges keep, as last judged, which answer at once; a reading of its own would walk git across
                // every tree and repository and, on a loaded machine, not answer for minutes while this conversation sat at `queued`.
                // Only a home whose facts were never judged waits, and only where no room has been written before, within the bound; else it
                // starts on the room as last written, said first in its conversation; with none written and no answer in the bound, it
                // ends below saying why.
                var (read, late, waited) = await ReadAsync(machine, placed.Workspace, ct).ConfigureAwait(false);
                if (late && !HelpRoom.Written(context.Home, placed.Workspace))
                {
                    var unread = NotRead(ReadBound);
                    await wiring.ConcludeAsync(sessionId, "failed", unread, onEnded).ConfigureAwait(false);
                    return new(null, unread.Note);
                }

                if (late)
                {
                    (room, reads) = (HelpRoom.PathOf(context.Home, placed.Workspace), []);
                    context.Note(sessionId, waited ? AsWritten(ReadBound) : NotReadYet());
                }
                else
                {
                    // Its workspace's room, the facts projected to it, so what is not read yet is its own.
                    var machineRead = (read ?? throw new DriverException("this machine could not be read for it: the service is not answering yet"))
                        .Within(placed.Workspace);
                    (room, reads) = (HelpRoom.Prepare(context.Home, machineRead), machineRead.Reads);
                    // What the facts had not read yet is said first, so the person knows the room may name less than there is.
                    if (machineRead.NotReadYet.Any) context.Note(sessionId, NotReadYet());
                }
            }
            catch (Exception error)
            {
                var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
                var why = $"Ask Daoris's room could not be written — {error.Message}";
                await wiring.ConcludeAsync(
                    sessionId, "failed",
                    cancelled ? Noted.Of(ChatNoteCodes.ChatCancelled, "the request that started it was cancelled before its process started.") : Observation.Failure(why),
                    onEnded).ConfigureAwait(false);
                if (cancelled) throw;
                return new(null, why);
            }

            if (context.Closing)
            {
                await wiring.ConcludeAsync(sessionId, "failed", Observation.Failure("the application closed before its agent started."), onEnded)
                    .ConfigureAwait(false);
                return new(null, "the application is closing, so no conversation starts now.");
            }

            // A replacement of this start runs as this run does, on the machine's reading, its end told to the same listener.
            if (placed.Pending is { } pending) pending.Open = (owner, token) => RunReplacementAsync(owner, config, machine, onEnded, token);
            return await wiring.RunAsync(
                    "ask-daoris", sessionId, placed.Message, new ChatAgent(wiring.Context.Harnesses.Adapters, resolved), selection, config, wiring.HelpPlace(room, reads, placed.Workspace), onEnded, ct,
                    chosen: placed.Chosen, pending: placed.Pending)
                .ConfigureAwait(false);
        }
        finally
        {
            context.GoingOn.TryRemove(new KeyValuePair<string, Task>(sessionId, going.Task));
            going.TrySetResult();
        }
    }

    /// <summary>
    /// The replacement placed: its pending start's order walked in its workspace, the refused account passed, and its record opened in that
    /// workspace's room under the same pending start.
    /// </summary>
    private async Task<HelpPlaced> PlaceReplacementAsync(ChatPending pending, DriverConfig config, CancellationToken ct)
    {
        var walk = await wiring.WalkAsync("ask-daoris", pending.Order, config, pending.Workspace, null, ct, pending.Refusing).ConfigureAwait(false);
        if (walk is not { Adapter: { } walked, Selection: { } taken, Chosen: { } entry }) return new(null, walk.Refusal!);
        return await OpenHelpAsync(walked, taken, entry, [.. pending.Passes, .. walk.Passes], config, pending.Workspace, null, ct, pending)
            .ConfigureAwait(false);
    }

    /// <summary>The record opened on the agent and account its start chose, and its opening said.</summary>
    /// <param name="pending">The pending start a replacement opens for; null makes one where the start asks its entry on the protocol wire.</param>
    private async Task<HelpPlaced> OpenHelpAsync(
        AgentDoor resolved, HarnessSelection selection, ActorEffective? chosen, IReadOnlyList<BuildPass> passes, DriverConfig config,
        string? workspace, string? carries, CancellationToken ct, ChatPending? pending = null, IReadOnlyList<ActorEffective>? order = null)
    {
        var context = wiring.Context;
        var access = HelpAccess.For(config.HelpMode, new ChatAgent(wiring.Context.Harnesses.Adapters, resolved));

        // Its workspace's own room, which its record keeps as its tree: the folder its harness keeps it by. With the mode its start gets,
        // which its run reads from the same file.
        var (sessionId, message) = await context.Service
            .OpenHelpAsync(resolved.Id.Value, HelpRoom.PathOf(context.Home, workspace), selection.Version, selection.Profile, workspace, access.Mode, ct)
            .ConfigureAwait(false);
        if (sessionId is null) return new(null, message);
        // Marked as this process's start until its spawn's marker replaces it: placed, it waits for its run.
        context.Processes.MarkOpening(sessionId);

        // Its opening rotates like a start, on its scope's order, and says so first; one that goes on from a limit names the conversation it
        // carries and the turn the limit refused.
        RotatedOpening.Say(context.Service, context.Events, sessionId, resolved.Id.Value, selection, wiring.LimitCarried(carries));
        // Where a later choice of its order ran, its actor line says which and why.
        if (chosen is not null) RotatedOpening.SayActor(context.Events, sessionId, "ask-daoris", chosen, passes);
        await RotatedOpening.SayStaffAsync(context.Events, context.Harnesses.Staff(), context.Service, null, sessionId, selection, "ask-daoris")
            .ConfigureAwait(false);

        // Owned from its placement until dispatched or exhausted; its run says how a replacement runs.
        if (pending is not null)
        {
            pending.Entry = chosen!;
            context.Pending.Link(pending, sessionId);
        }
        else if (chosen is not null && resolved.Can.SetAtOpen && carries is null)
        {
            pending = context.Pending.Add(new ChatPending(
                sessionId, "ask-daoris", order is { Count: > 0 } ? order : [chosen], chosen, null, workspace, (_, _) => Task.FromResult(new ChatStart(null, "Ask Daoris's replacement opens only from its run."))));
        }

        return new(sessionId, message)
        {
            Workspace = RemoteTarget.Workspace(workspace), Adapter = resolved, Selection = selection, Chosen = chosen, Pending = pending,
        };
    }

    /// <summary>
    /// The replacement opened and run: placed in its workspace's room, its link kept in the room's history, then run on the machine as the
    /// press's run reads it.
    /// </summary>
    private async Task<ChatStart> RunReplacementAsync(
        ChatPending pending, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine, Func<string, string, Task>? onEnded,
        CancellationToken ct)
    {
        var placed = await PlaceReplacementAsync(pending, config, ct).ConfigureAwait(false);
        if (placed.SessionId is not { } id) return new(null, placed.Message);
        try
        {
            new HelpConversations(wiring.Context.Home).StartedFrom(id, pending.Current, HelpConversations.NotOffered);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // Its history names no earlier conversation; its opening's actor line still says why it opened.
        }

        return await RunHelpAsync(placed, config, machine, onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The machine for the room: the facts as last judged, which answer at once. A reading still under way is a home whose facts were never
    /// judged at all: with a room written before, it is not waited for, and comes back late at once, the room as last written serving; with
    /// none, it is waited for within <see cref="ReadBound"/>, and comes back late, having waited, where it did not answer in time. The
    /// reading left behind goes on, until <see cref="ReadBackstop"/>, and writes the room when it answers, so the next conversation starts
    /// on the machine as read.
    /// </summary>
    /// <param name="workspace">
    /// The workspace whose room it is for: its room's being written decides the wait, and the reading left behind writes that room, from the
    /// machine projected to it. The reading answered is the whole machine's.
    /// </param>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> is cancelled while it waits.</exception>
    public async Task<(HelpMachine? Read, bool Late, bool Waited)> ReadAsync(
        Func<CancellationToken, Task<HelpMachine?>> machine, string? workspace, CancellationToken ct)
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
        if (!reading.IsCompleted && HelpRoom.Written(wiring.Context.Home, workspace))
        {
            _ = WriteWhenReadAsync(reading, workspace);
            return (null, true, false);
        }

        try
        {
            return (await reading.WaitAsync(ReadBound, ct).ConfigureAwait(false), false, false);
        }
        catch (TimeoutException)
        {
            _ = WriteWhenReadAsync(reading, workspace);
            return (null, true, true);
        }
    }

    /// <summary>A reading left behind writes the room of <paramref name="workspace"/> once it answers; one that fails late writes nothing.</summary>
    private async Task WriteWhenReadAsync(Task<HelpMachine?> reading, string? workspace)
    {
        try
        {
            var context = wiring.Context;
            if (await reading.ConfigureAwait(false) is { } read && !context.Closing) HelpRoom.Prepare(context.Home, read.Within(workspace));
        }
        catch (Exception)
        {
            // Late, failed or cancelled at its backstop: the room as last written stands, and the next start reads again.
        }
    }

    /// <summary>Whole seconds of a bound as a note says it: a part of one is one.</summary>
    private static int Seconds(TimeSpan bound) => Math.Max(1, (int)Math.Ceiling(bound.TotalSeconds));

    /// <summary>
    /// The line a conversation is told first where it starts on the room as last written: the machine is not read within the bound, and
    /// checkouts outside the room are not read until the next conversation.
    /// </summary>
    public static Noted AsWritten(TimeSpan bound) => Noted.Of(
        PeerNoteCodes.HelpRoomAsWritten,
        $"This machine was not read within {Seconds(bound)} seconds, so Ask Daoris starts on its room as last written, reading no "
        + "checkout outside it; the room is written again once the machine is read.",
        ("seconds", Seconds(bound)));

    /// <summary>
    /// The line a conversation is told first where it starts on facts that leave a part of the machine not read yet, or on the room as last
    /// written before any is judged: it starts at once, and its room says what is not read yet.
    /// </summary>
    public static Noted NotReadYet() => Noted.Of(
        PeerNoteCodes.HelpRoomNotReadYet,
        "Ask Daoris started at once on this machine as read so far: what is not read yet, its room says, and the room is written "
        + "again as each part is read.");

    /// <summary>
    /// What a start says where the machine is not read within the bound and no room has ever been written: nothing can serve, so it ends failed.
    /// </summary>
    public static Noted NotRead(TimeSpan bound) => Noted.Of(
        PeerNoteCodes.HelpRoomUnread,
        $"Ask Daoris's room could not be written: this machine was not read within {Seconds(bound)} seconds, and no room was "
        + "written before. Ask again once it is less busy.",
        ("seconds", Seconds(bound)));
}
