namespace Daoris.Driver;

/// <summary>
/// A conversation goes on with another agent (MIXAGENT1a-i, D163, the mixed-agents design §2 and §5): conversations live in the
/// shell's chat runner, so the ask, the box and the take are here. <see cref="HandOver"/> judges and keeps the box;
/// <see cref="HandOverTake"/> runs the sequence with this runner's own end, start and say.
/// </summary>
public sealed partial class ChatRunner
{
    /// <summary>How long the old conversation's end is waited for before the new one starts: a stop, not a wind-up.</summary>
    private static readonly TimeSpan HandOverEnd = TimeSpan.FromSeconds(15);

    /// <summary>The states a record is live in, where nothing here may end it (it runs in another process).</summary>
    private static readonly HashSet<string> LiveStates = new(StringComparer.OrdinalIgnoreCase) { "queued", "starting", "working", "awaiting-person" };

    /// <summary>
    /// *Hand a conversation over* (MIXAGENT1a-i): <see cref="HandOver.AskAsync"/> judges and keeps the box; a conversation that is
    /// not talking is taken at once, and one that is talking keeps the box until its turn ends (<see cref="HandOverDue"/>), or the
    /// shell comes up (<see cref="TakeUpAsync"/>).
    /// </summary>
    /// <param name="why">One of <see cref="HandOverWhy"/>.</param>
    /// <param name="askedBy">One of <see cref="HandOverBy"/>.</param>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public async Task<HandedOver> HandOverAsync(
        string sessionId, string agent, string? account, string why, string askedBy, DriverConfig config,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (_closing) return new HandedOver(null, false, null, "the application is closing, so no conversation is handed over now.");

        var (refused, prior) = await HandOver.JudgeAsync(_service, AccountsOf, sessionId, agent, account, ct).ConfigureAwait(false);
        if (refused is not null) return new HandedOver(null, false, refused.Why, refused.Message);

        // A conversation another Daoris process runs cannot be ended from here: its own shell takes the ask, or it is stopped there.
        if (LiveStates.Contains(prior!.State) && !_talking.ContainsKey(sessionId))
        {
            return new HandedOver(
                null, false, HandOver.Running,
                $"{sessionId} is {prior.State}, running in another Daoris process on this machine: hand it over there, or stop it first.");
        }

        var asked = await HandOver.AskAsync(_service, _home, AccountsOf, sessionId, agent, account, why, askedBy, DateTimeOffset.UtcNow, ct)
            .ConfigureAwait(false);
        if (!asked.Kept) return new HandedOver(null, false, asked.Why, asked.Message);

        // Talking, it goes on with its turn and is taken at that turn's end.
        if (Taking(sessionId)) return new HandedOver(null, true, null, asked.Message);
        return await TakeHandOverAsync(sessionId, config, onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The box of a conversation whose turn just ended is taken (MIXAGENT1a-i): told as the turns move, and read only where a turn
    /// is neither in flight nor waiting. A box left from before is taken as the shell comes up.
    /// </summary>
    private void HandOverDue(string sessionId, ChatQueue queue, DriverConfig config, Func<string, string, Task>? onEnded)
    {
        if (_closing || queue.Taking || queue.Opening || queue.Queued.Count > 0) return;
        if (new HandOverBox(_home).Read(sessionId) is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await TakeHandOverAsync(sessionId, config, onEnded).ConfigureAwait(false);
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
    private async Task<IReadOnlyList<ChatStart>> TakeHandOversAsync(CancellationToken ct)
    {
        var taken = new List<ChatStart>();
        foreach (var id in new HandOverBox(_home).Pending())
        {
            if (_closing || ct.IsCancellationRequested) break;
            try
            {
                var handed = await TakeHandOverAsync(id, MachineConfig(), Announce, ct).ConfigureAwait(false);
                taken.Add(new ChatStart(handed.SessionId, handed.Message));
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                new HandOverBox(_home).Clear(id);
            }
        }

        return taken;
    }

    /// <summary>Take one box: the sequence of <see cref="HandOverTake"/> through this runner, one at a time per conversation.</summary>
    private async Task<HandedOver> TakeHandOverAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default)
    {
        var box = new HandOverBox(_home);
        if (box.Read(sessionId) is not { } ask)
        {
            return new HandedOver(null, false, null, $"nothing waits to be handed over from `{sessionId}`.");
        }

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_goingOn.TryAdd(sessionId, going.Task))
        {
            return new HandedOver(null, true, null, $"`{sessionId}` is already being taken.");
        }

        try
        {
            return await HandOverTake.RunAsync(
                _service, _home, _events, _output, sessionId, ask, AccountsOf, EndForHandOverAsync,
                (prior, request, token) => StartAsync(prior.Repository!, request.Agent, config, onEnded, request.Account, ct: token, tree: prior.Tree),
                SayHandedOver, ct).ConfigureAwait(false);
        }
        finally
        {
            _goingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// The accounts the roster holds for an agent, or null where it has no such agent or one that holds no conversations: what a
    /// hand-over's agent and account are judged by.
    /// </summary>
    private IReadOnlyList<string>? AccountsOf(string agent)
    {
        try
        {
            var resolved = _harnesses.Adapters.Resolve(agent);
            if (!resolved.Interactive) return null;
            return resolved.Toolchain is { } toolchain ? HarnessSettings.Profiles(_harnesses.Home, toolchain.Owner(resolved.Name)) : [];
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The old conversation ends by this runner's own end: its process is stopped and its record says so, returning once its watch
    /// has written it, or after <see cref="HandOverEnd"/>. One that already ended has nothing to stop.
    /// </summary>
    private async Task EndForHandOverAsync(string sessionId)
    {
        if (!_watching.TryGetValue(sessionId, out var watch)) return;

        _processes.Stop(sessionId, EndedNote(stopped: false, notKept: false));
        await Task.WhenAny(watch, Task.Delay(HandOverEnd)).ConfigureAwait(false);
    }

    /// <summary>
    /// The new conversation's first message (MIXAGENT1a-i): Daoris's briefing, with the browser's line after it where it was handed
    /// one, never the person's words, and recorded as the note of where it came from (<see cref="ChatMessage.TakenOver"/>).
    /// </summary>
    private bool SayHandedOver(string sessionId, ChatMessage message)
    {
        if (!_turned.ContainsKey(sessionId) && !_talking.ContainsKey(sessionId)) return false;

        if (_browsers.TryRemove(sessionId, out var browser)) message = message with { Briefing = $"{message.Briefing}\n\n{browser}" };
        if (_turned.TryGetValue(sessionId, out var chat)) return chat.Turns.Take(message) is not null;

        var framed = _talking.TryGetValue(sessionId, out var adapter) ? adapter.FrameMessage(message.Prompt) : message.Prompt;
        return _processes.Send(sessionId, framed);
    }
}
