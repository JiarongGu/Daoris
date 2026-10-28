namespace Daoris.Driver;

/// <summary>
/// The agent a helper reads as (MIXAGENT2a-ii; the mixed agents design §3): the one the ask names where the person's list allows it,
/// else the person's list in its order, else another maker than the asker's, walked as a reviewer is (<see cref="ReviewerChoice"/>),
/// the account by the same walk.
/// </summary>
/// <remarks>
/// STRUCT3h, D166's note: the walk is <c>ReviewerChoice.ChooseAsync</c>, which asks the machine's accounts and which a Domain may not name, so
/// the choice left <c>Domain/HelperJudge.cs</c>.
/// </remarks>
public static class HelperChoice
{
    /// <summary>The names the walk is asked over: never a name the person's list does not allow, and never the asker's own family by default.</summary>
    public static IReadOnlyList<string> Candidates(HelperAsk ask, HelperRule rule, AdapterSet adapters, string? askerAdapter)
    {
        if (ask.Agent is { Length: > 0 } named)
        {
            // Allowed where the person's list is empty or names it, in the spelling the list gives it.
            if (rule.Agents.Count == 0) return [named];
            if (rule.Agents.FirstOrDefault(allowed => string.Equals(allowed, named, StringComparison.OrdinalIgnoreCase)) is { } listed) return [listed];
        }

        // Another maker than the asker's (D155 point 4).
        var mine = askerAdapter is { Length: > 0 } ? AgentFamily.Of(adapters, askerAdapter) : null;
        bool Other(string name) => mine is null || !AgentFamily.Of(adapters, name).Same(mine);

        // HELPERDEFAULT1: the default's agents are those set up and signed in here, of another maker than the asker's.
        if (rule.SetUp) return [.. rule.Agents.Where(Other)];
        if (rule.Agents.Count > 0) return rule.Agents;

        // Every agent of this machine that is not the asker's own family.
        return [.. adapters.Names.Where(Other)];
    }

    /// <summary>
    /// The entries the walk is asked over (D205 §4.0, §4.4, MODELROLE1e): who the rule or the ask names, each taking the actor's entries
    /// for it, else account default (<c>rule-default</c>); with no such list and the actor <i>set</i>, the actor's entries replace every
    /// agent of another maker, the asker's own family among them if the person listed it. Another maker than the asker's still comes first
    /// (the walk's passes); the actor never widens who may be asked where the person's list or the ask names the agents.
    /// </summary>
    /// <exception cref="ActorUnreadableException">The helper actor's stored entry does not read.</exception>
    public static IReadOnlyList<ActorEffective> Order(HelperAsk ask, HelperRule rule, AdapterSet adapters, string? askerAdapter, DriverConfig config)
    {
        var fence = Candidates(ask, rule, adapters, askerAdapter);
        var named = ask.Agent is { Length: > 0 } && fence.Count == 1;
        var persons = !rule.SetUp && rule.Agents.Count > 0;
        return !named && !persons && config.Actors.ContainsKey("helper")
            ? ActorChoices.Effective("helper", config)
            : fence.Count == 0 ? [] : ActorChoices.Effective("helper", config, fence: fence);
    }

    /// <summary>The walk over <see cref="Order"/>, as a reviewer's, with the asker's agent as the work's family.</summary>
    /// <param name="passed">The (entry, account) this ask's call already passed at an open's refusal (D205 §3.5(f)).</param>
    public static Task<ReviewerChoice> ChooseAsync(
        HarnessRoster roster, HelperAsk ask, HelperRule rule, string? askerAdapter, DriverConfig config, string? workspace,
        CancellationToken ct = default, IReadOnlyList<ReviewerPassed>? passed = null)
    {
        IReadOnlyList<ActorEffective> order;
        try
        {
            order = Order(ask, rule, roster.Adapters, askerAdapter, config);
        }
        catch (ActorUnreadableException error)
        {
            return Task.FromResult(new ReviewerChoice(null, null, ReviewerUnavailable.Refused, $"No second opinion: {error.Message}"));
        }

        return ReviewerChoice.ChooseAsync(
            roster, OpinionRule.None, askerAdapter is { Length: > 0 } adapter ? [adapter] : [], config, workspace, ct, "helper", passed, order);
    }
}
