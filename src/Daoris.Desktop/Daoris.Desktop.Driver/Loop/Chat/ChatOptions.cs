using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A conversation's model and effort as its agent offers them, the person's change of either, and the change as last kept, which the next
/// change is reconciled with. Only the protocol door carries options.
/// </summary>
internal sealed class ChatOptions(ChatContext context)
{
    private readonly ConcurrentDictionary<string, ActorOverride> _overrides = new(StringComparer.Ordinal);

    /// <summary>The composer change a conversation runs under from its start: a conversation going on re-applies its last; none forgets it.</summary>
    public void Kept(string sessionId, ActorOverride? overriding)
    {
        if (overriding is not null) _overrides[sessionId] = overriding;
        else _overrides.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// A conversation's model and effort, as its agent offered them: empty for one on a door that carries none, one whose agent offered
    /// none, one still opening, and one nothing here holds.
    /// </summary>
    public IReadOnlyList<AcpConfigOption> Of(string sessionId) =>
        context.Turned.TryGetValue(sessionId, out var turned) && turned is ProtocolChat { Session: { } session }
            ? ThePersons(session.ConfigOptions)
            : [];

    /// <summary>
    /// Change a conversation's model or effort: <c>session/set_config_option</c> on its session, as the tool's own console's <c>/model</c>
    /// would, and the record says the person did.
    /// </summary>
    /// <remarks>
    /// <b>Only the options that are the person's</b>: the model and the thought level. The mode is the posture Daoris runs a session under,
    /// and a menu that widened it would be an approval surface, so it is refused here before anything reaches the agent, whatever a page
    /// sends. A value is the agent's to judge: it refuses one it does not take, in its own words.
    /// </remarks>
    /// <returns>The conversation's options after the change, the person's only.</returns>
    /// <exception cref="DriverException">Nothing here holds it, its door carries no options, or the option is not the person's.</exception>
    public async Task<IReadOnlyList<AcpConfigOption>> SetAsync(string sessionId, string configId, string value, CancellationToken ct)
    {
        if (!context.Turned.TryGetValue(sessionId, out var turned) || turned is not ProtocolChat chat)
        {
            throw new DriverException(turned is not null || context.Talking.ContainsKey(sessionId)
                ? $"conversation `{sessionId}` runs on a door that carries no options to change — only the protocol "
                  + "door carries the model and effort an agent offers."
                : $"nothing on this machine holds conversation `{sessionId}` — it has ended, or runs elsewhere.");
        }

        var session = chat.Session
            ?? throw new DriverException($"conversation `{sessionId}` is still opening — its options are not known yet.");
        var option = session.ConfigOptions.FirstOrDefault(offered => offered.Id == configId)
            ?? throw new DriverException($"the agent offered no option `{configId}` for conversation `{sessionId}`.");
        if (!option.ThePersons)
        {
            throw new DriverException(
                $"the conversation's `{configId}` is the posture Daoris runs it under (D81), not the person's to change "
                + "here — its model and its effort are.");
        }

        var after = await session.SetConfigOptionAsync(configId, value, ct).ConfigureAwait(false);
        // Only returned configuration is acknowledged. An empty answer says nothing about what the setter asked.
        var model = session.LastAcknowledgedOptions?.FirstOrDefault(item => item.Category == "model")?.Current;
        var effort = session.LastAcknowledgedOptions?.FirstOrDefault(item => item.Category == "thought_level")?.Current;
        // Kept as values, reconciled from the returned configuration with the change before it, never merged blindly: an effort-only
        // change keeps the model, and a model change clears the effort the new model resets.
        var before = _overrides.TryGetValue(sessionId, out var kept) ? kept : null;
        var reconciled = ActorOverride.Reconciled(before, option.Category ?? "", session.LastAcknowledgedOptions);
        if (reconciled is not null && reconciled != before)
        {
            _overrides[sessionId] = reconciled;
            // The agent took the change already: a provenance write that fails is logged, never the press's failure.
            using (var keepLog = new MachineLog(context.Home, "driver"))
            {
                await context.Service.TryKeepActorAsync(sessionId, keepLog, changed: reconciled.Written, ct: ct).ConfigureAwait(false);
            }
            if (context.ActorRuns.TryGetValue(sessionId, out var actorRun)) actorRun.Report([], new(DateTimeOffset.UtcNow, model, effort));
        }
        // Said as the agent names them, so a reader later knows what ran the turns after this one.
        var chosen = option.Choices.FirstOrDefault(choice => choice.Value == value)?.Name ?? value;
        // The option and its value are the agent's own names, facts the page words the sentence around.
        var set = Noted.Of(
            NoteCodes.ChatOptionSet, $"the person set {option.Name} to {chosen} for this conversation.",
            ("option", option.Name), ("chosen", chosen));
        context.Record(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = set.Note, Parts = set.Parts });
        return ThePersons(after);
    }

    /// <summary>
    /// What a conversation's open listed, kept as its agent's offer reading on its account, as a driven open's is; a reading that cannot be
    /// kept is logged, never the conversation's failure.
    /// </summary>
    public void KeepOffers(string agent, HarnessSelection selection, string sessionId, IReadOnlyList<AcpConfigOption> options)
    {
        IReadOnlyList<string>? Values(string category) =>
            options.FirstOrDefault(option => option.Category == category) is { } option ? [.. option.Choices.Select(each => each.Value)] : null;
        if (AgentOffers.Offered(context.Home, agent, selection.Version, selection.Profile, Values("model"), Values("thought_level"), DateTimeOffset.UtcNow)
            is { } unkept)
        {
            using var log = new MachineLog(context.Home, "driver");
            log.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
        }
    }

    /// <summary>The options a person may set (<see cref="AcpConfigOption.ThePersons"/>), in the agent's order.</summary>
    public static IReadOnlyList<AcpConfigOption> ThePersons(IReadOnlyList<AcpConfigOption> options) =>
        [.. options.Where(option => option.ThePersons)];
}
