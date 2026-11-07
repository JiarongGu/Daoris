namespace Daoris.Driver;

/// <summary>
/// The change of Ask Daoris's access under its running conversations and the receipt of what a start
/// handed: each conversation running under another access than the one chosen now ends at its turn's end, and
/// its next words resume it under the access chosen then.
/// </summary>
[Executor("help-access", ExecutorPhase.Move, "help")]
internal sealed class HelpAccessSwap(IExecutorWiring wiring) : IExecutor
{
    /// <summary>
    /// An Ask Daoris start's receipt is on disk: raised with the session's id once it is kept, so the
    /// modules publish the help scope after it, never before, and an open <i>What it has</i> reads the new receipt, not the old.
    /// </summary>
    public event Action<string>? WiredKept;

    /// <summary>
    /// The person changed Ask Daoris's access: its mode, now <paramref name="mode"/>, or its configured access, read from the home's record
    /// now. Each of its conversations running here under another access than that ends at its turn's end, never mid-turn, and at once
    /// where it is idle, the way a person finishing ends it (the turns asked for run first); its console says what changed and when.
    /// </summary>
    /// <remarks>
    /// <para>Nothing handed to a running conversation changes while it runs: a live switch of its harness's posture alone would
    /// leave the rules, servers, guard and room its start handed for the old access. Every access change
    /// applies at the next turn, a grant taken back too.</para>
    ///
    /// <para>A change back to what it runs under before its turn ends withdraws the end; one already ending goes on, and its next
    /// words resume it under the same access. A further change while one is due is said again.</para>
    /// </remarks>
    public void Chosen(string mode)
    {
        var context = wiring.Context;
        var access = ConfiguredAccess.Read(context.Home);
        foreach (var (sessionId, running) in context.HelpModes)
        {
            if (context.HelpEnding.TryGetValue(sessionId, out var due) && !due) continue;
            if (HelpSwap.Changed(running, Handed(sessionId), mode, access) is not { } changed)
            {
                // Chosen back to what it runs under: an end due at its turn's end is withdrawn.
                context.HelpEnding.TryRemove(new KeyValuePair<string, bool>(sessionId, true));
                continue;
            }

            // Due first and judged idle after, so a turn ending between the two is ended by TurnChanged, and once.
            context.HelpEnding[sessionId] = true;
            var idle = !wiring.Taking(sessionId);
            context.Output?.Append(sessionId, HelpSwap.Said(changed, idle));
            if (idle && context.HelpEnding.TryUpdate(sessionId, false, true)) wiring.Finish(sessionId);
        }
    }

    /// <summary>
    /// The Ask Daoris conversations running here under another access than the one chosen now, by session, in order: each ends at its
    /// turn's end, and its next words resume it under the access chosen. What the page's <i>pending</i> marks read.
    /// </summary>
    /// <param name="mode">The mode chosen now; the configured access is read from the home's record.</param>
    public IReadOnlyList<string> Swapping(string mode)
    {
        var context = wiring.Context;
        var access = ConfiguredAccess.Read(context.Home);
        return [.. context.HelpModes
            .Where(each => HelpSwap.Changed(each.Value, Handed(each.Key), mode, access) is not null)
            .Select(each => each.Key)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Whether an Ask Daoris conversation running here started under another choice of <paramref name="grant"/> than
    /// <paramref name="chosen"/> holds: the grant's <i>pending</i> mark, from the change until that
    /// conversation's agent has ended.
    /// </summary>
    public bool GrantPending(string grant, ConfiguredAccess chosen) =>
        wiring.Context.HelpModes.Keys.Any(sessionId => !Handed(sessionId).Of(grant).SequenceEqual(chosen.Of(grant), StringComparer.Ordinal));

    /// <summary>
    /// Where a conversation's turns stand: one whose access changed is ended once nothing runs or waits, as its turn has ended. Called by
    /// each start's turns, under their lock, so it ends off it.
    /// </summary>
    public void TurnChanged(string sessionId, ChatQueue queue)
    {
        if (queue.Taking || queue.Opening || queue.Queued.Count > 0) return;
        // Due at its turn's end, and marked ending in the same step, so it is finished once.
        if (!wiring.Context.HelpEnding.TryUpdate(sessionId, false, true)) return;
        _ = Task.Run(() => wiring.Finish(sessionId));
    }

    /// <summary>
    /// A conversation's run is over: what its start handed is not compared with the person's access, so nothing is pending.
    /// Said before its end is told, so the state judged at that word reads it gone.
    /// </summary>
    public void Forget(string sessionId)
    {
        var context = wiring.Context;
        context.HelpModes.TryRemove(sessionId, out _);
        context.AccessHanded.TryRemove(sessionId, out _);
        context.HelpEnding.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Keep what an Ask Daoris start handed as its receipt, each server's approval and what restricted mode lacks read from the plugins
    /// and the person's grants as they stand at this start. A receipt the disk will not take does not hold the start: its console says
    /// so, and that start reads as not recorded.
    /// </summary>
    public void KeepWired(string sessionId, HelpHanded handed)
    {
        var context = wiring.Context;
        try
        {
            var catalog = PluginCatalog.Load(context.Home, ProviderRegistry.Built().Names);
            new HelpWired(context.Home).Keep(sessionId, HelpWired.Compose(handed, catalog, HelpGrants.Read(context.Home, catalog), DateTimeOffset.UtcNow));
            WiredKept?.Invoke(sessionId);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            context.Output?.Append(sessionId, $"— what this start handed could not be recorded: {error.Message}");
        }
    }

    /// <summary>The configured access a running Ask Daoris conversation's start handed; none where it kept none.</summary>
    private ConfiguredAccess Handed(string sessionId) =>
        wiring.Context.AccessHanded.TryGetValue(sessionId, out var handed) ? handed : ConfiguredAccess.None;
}
