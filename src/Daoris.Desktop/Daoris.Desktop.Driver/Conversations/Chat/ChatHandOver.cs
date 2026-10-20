namespace Daoris.Driver;

/// <summary>
/// A conversation goes on with another agent (MIXAGENT1a-i, D163, the mixed-agents design §2 and §5): conversations live in the
/// shell's chat runner, so the ask, the box and the take are here, over the runner's context (PART10g, D166).
/// <see cref="HandOver"/> judges and keeps the box; <see cref="HandOverTake"/> runs the sequence with the runner's own end, start
/// and say.
/// </summary>
/// <param name="runner">
/// The runner whose conversations these are: its start begins the new conversation, as every door's does, and its turns say
/// whether one is talking.
/// </param>
internal sealed class ChatHandOver(ChatContext context, ChatRunner runner)
{
    /// <summary>How long the old conversation's end is waited for before the new one starts: a stop, not a wind-up.</summary>
    private static readonly TimeSpan HandOverEnd = TimeSpan.FromSeconds(15);

    /// <summary>The states a record is live in, where nothing here may end it (it runs in another process).</summary>
    private static readonly HashSet<string> LiveStates = new(StringComparer.OrdinalIgnoreCase) { "queued", "starting", "working", "awaiting-person" };

    private readonly ChatContext _context = context;

    private readonly ChatRunner _runner = runner;

    /// <summary>
    /// A take of a conversation's box is over, however it came out, its box gone (HANDWORDS1): the person's words held while it was
    /// taken go on now, to the conversation that continues it where one opened. Told on the take's own thread, after its end.
    /// </summary>
    public event Action<string>? TakeEnded;

    /// <summary>
    /// *Hand a conversation over* (MIXAGENT1a-i): <see cref="HandOver.AskAsync"/> judges and keeps the box; a conversation that is
    /// not talking is taken at once, and one that is talking keeps the box until its turn ends (<see cref="Due"/>), or the
    /// shell comes up (<see cref="ChatRunner.TakeUpAsync"/>).
    /// </summary>
    /// <param name="why">One of <see cref="HandOverWhy"/>.</param>
    /// <param name="askedBy">One of <see cref="HandOverBy"/>.</param>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public async Task<HandedOver> HandOverAsync(
        string sessionId, string agent, string? account, string why, string askedBy, DriverConfig config,
        Func<string, string, Task>? onEnded, CancellationToken ct)
    {
        if (_context.Closing) return new HandedOver(null, false, null, "the application is closing, so no conversation is handed over now.");

        var (refused, prior) = await HandOver.JudgeAsync(_context.Service, AccountsOf, sessionId, agent, account, ct).ConfigureAwait(false);
        if (refused is not null) return new HandedOver(null, false, refused.Why, refused.Message);

        // A conversation another Daoris process runs cannot be ended from here: its own shell takes the ask, or it is stopped there.
        if (LiveStates.Contains(prior!.State) && !_context.Talking.ContainsKey(sessionId))
        {
            return new HandedOver(
                null, false, HandOver.Running,
                $"{sessionId} is {prior.State}, running in another Daoris process on this machine: hand it over there, or stop it first.");
        }

        var asked = await HandOver.AskAsync(
                _context.Service, _context.Home, AccountsOf, sessionId, agent, account, why, askedBy, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        if (!asked.Kept) return new HandedOver(null, false, asked.Why, asked.Message);

        // Talking, it goes on with its turn and is taken at that turn's end.
        if (_runner.Taking(sessionId)) return new HandedOver(null, true, null, asked.Message);
        return await TakeAsync(sessionId, config, onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The box of a conversation whose turn just ended is taken (MIXAGENT1a-i): told as the turns move, and read only where a turn
    /// is neither in flight nor waiting. A box left from before is taken as the shell comes up.
    /// </summary>
    public void Due(string sessionId, ChatQueue queue, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (_context.Closing || queue.Taking || queue.Opening || queue.Queued.Count > 0) return;
        if (new HandOverBox(_context.Home).Read(sessionId) is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await TakeAsync(sessionId, config, onEnded).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Said wherever it could be; the box is cleared by the take, and the asker asks again.
            }
        });
    }

    /// <summary>
    /// Every box left on this home is taken (MIXAGENT1a-i), as the shell comes up: an ask kept while no runner heard it, or while
    /// its conversation talked and the shell closed. Each is judged again as it is taken.
    /// </summary>
    /// <param name="onEnded">Told as each new conversation ends: the runner's own announcement, since no route started it.</param>
    public async Task<IReadOnlyList<ChatStart>> TakeAllAsync(Func<string, string, Task> onEnded, CancellationToken ct)
    {
        var taken = new List<ChatStart>();
        foreach (var id in new HandOverBox(_context.Home).Pending())
        {
            if (_context.Closing || ct.IsCancellationRequested) break;
            try
            {
                var handed = await TakeAsync(id, _context.MachineConfig(), onEnded, ct).ConfigureAwait(false);
                taken.Add(new ChatStart(handed.SessionId, handed.Message));
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                new HandOverBox(_context.Home).Clear(id);
            }
        }

        return taken;
    }

    /// <summary>Take one box: the sequence of <see cref="HandOverTake"/> through the runner, one at a time per conversation.</summary>
    public async Task<HandedOver> TakeAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        var box = new HandOverBox(_context.Home);
        if (box.Read(sessionId) is not { } ask)
        {
            return new HandedOver(null, false, null, $"nothing waits to be handed over from `{sessionId}`.");
        }

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_context.GoingOn.TryAdd(sessionId, going.Task))
        {
            return new HandedOver(null, true, null, $"`{sessionId}` is already being taken.");
        }

        ChatTurns? held = null;
        try
        {
            // Never mid-turn (SWITCH1c-ii, the switch design §4): its turns are held while it is taken, so a word said as the take
            // begins either started its turn first, and the box waits for that turn's end (Due), or takes nothing.
            if (_context.Turned.TryGetValue(sessionId, out var chat))
            {
                if (!chat.Turns.Hold()) return new HandedOver(null, true, null, $"`{sessionId}` is talking: it goes on as its turn ends.");
                held = chat.Turns;
            }

            // A move by the usage curve says which accounts it moved between: the decision kept since its ask (SWITCH1c-ii).
            var moved = ask.Why == HandOverWhy.Curve && _context.CurveMoves.TryRemove(sessionId, out var move) ? move : null;
            return await HandOverTake.RunAsync(
                _context.Service, _context.Home, _context.Events, _context.Output, sessionId, ask, AccountsOf, EndForHandOverAsync,
                (prior, request, from, token) => _runner.StartAsync(
                    prior.Repository!, request.Agent, config, onEnded, request.Account, ct: token, tree: prior.Tree, from: from),
                SayHandedOver, ct, moved).ConfigureAwait(false);
        }
        finally
        {
            // Ended, nothing listens to them now; refused before its end, the conversation takes words again.
            held?.Release();
            _context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
            Ended(sessionId);
        }
    }

    /// <summary>Tell <see cref="TakeEnded"/>: a listener's failure is its own, and the take is over either way.</summary>
    private void Ended(string sessionId)
    {
        try
        {
            TakeEnded?.Invoke(sessionId);
        }
        catch (Exception)
        {
            // The words it holds are tried again at its next look (SessionWords' poll), so nothing is lost by a listener that threw.
        }
    }

    /// <summary>
    /// The accounts the roster holds for an agent, or null where it has no such agent or one that holds no conversations: what a
    /// hand-over's agent and account are judged by, the curve's move's too.
    /// </summary>
    public IReadOnlyList<string>? AccountsOf(string agent)
    {
        try
        {
            var resolved = _context.Harnesses.Adapters.Resolve(agent);
            if (!resolved.Interactive) return null;
            return resolved.Toolchain is { } toolchain
                ? HarnessSettings.Profiles(_context.Harnesses.Home, toolchain.Owner(resolved.Name))
                : [];
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The old conversation ends by the runner's own end: its process is stopped and its record says so, returning once its watch
    /// has written it, or after <see cref="HandOverEnd"/>. One that already ended has nothing to stop.
    /// </summary>
    private async Task EndForHandOverAsync(string sessionId)
    {
        if (!_context.Watching.TryGetValue(sessionId, out var watch)) return;

        _context.Processes.Stop(sessionId, ChatRunner.EndedNote(stopped: false, notKept: false));
        await Task.WhenAny(watch, Task.Delay(HandOverEnd)).ConfigureAwait(false);
    }

    /// <summary>
    /// The new conversation's first message (MIXAGENT1a-i): Daoris's briefing, with the browser's line after it where it was handed
    /// one, and recorded as the note of where it came from (<see cref="ChatMessage.TakenOver"/>). A hand-over's carries none of the
    /// person's words; a part that goes on from a lost conversation carries those that waited there after the brief (SESSCTX1c part B,
    /// <see cref="BriefedMove"/>).
    /// </summary>
    internal async Task<bool> SayHandedOver(string sessionId, ChatMessage message)
    {
        if (!_context.Turned.ContainsKey(sessionId) && !_context.Talking.ContainsKey(sessionId)) return false;

        // The browser's line after Daoris's brief (SESSCTX1b keeps the brief a field of its own, so the line is the briefing's alone).
        if (_context.Browsers.TryRemove(sessionId, out var browser))
        {
            message = message with { Briefing = message.Briefing is { Length: > 0 } told ? $"{told}\n\n{browser}" : browser };
        }
        if (_context.Turned.TryGetValue(sessionId, out var chat)) return chat.Turns.Take(message) is not null;

        // A text door has no turns to record it as they are sent: the note is kept here, with what is sent, so no path says nothing.
        // Its process is tracked a moment after the start returns, and a send before that reaches nothing: tried again, bounded.
        var framed = _context.Talking.TryGetValue(sessionId, out var adapter) ? adapter.FrameMessage(message.Prompt) : message.Prompt;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (_context.Processes.Send(sessionId, framed))
            {
                foreach (var recorded in ChatRunner.Recorded(message)) _context.Record(sessionId, recorded);
                return true;
            }

            if (!_context.Talking.ContainsKey(sessionId)) return false;
            await Task.Delay(100).ConfigureAwait(false);
        }

        return false;
    }
}
