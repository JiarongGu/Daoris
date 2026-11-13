namespace Daoris.Driver;

/// <summary>
/// The agent a helper reads as: the one the ask names where the person's list allows it,
/// else the person's list in its order, else another maker than the asker's, walked as a reviewer is (<see cref="ReviewerChoice"/>),
/// the account by the same walk.
/// </summary>
/// <remarks>
/// The walk is <c>ReviewerChoice.ChooseAsync</c>, which asks the machine's accounts and which a Domain may not name, so
/// the choice left <c>Domain/HelperJudge.cs</c>.
/// </remarks>
public static class HelperChoice
{
    /// <summary>The names the walk is asked over: never a name the person's list does not allow, and never the asker's own family by default.</summary>
    public static IReadOnlyList<string> Candidates(HelperAsk ask, HelperRule rule, ProviderRegistry adapters, string? askerAdapter)
    {
        if (ask.Agent is { Length: > 0 } named)
        {
            // Allowed where the person's list is empty or names it, in the spelling the list gives it.
            if (rule.Agents.Count == 0) return [named];
            if (rule.Agents.FirstOrDefault(allowed => string.Equals(allowed, named, StringComparison.OrdinalIgnoreCase)) is { } listed) return [listed];
        }

        // Another maker than the asker's.
        var mine = askerAdapter is { Length: > 0 } ? AgentFamily.Of(adapters, askerAdapter) : null;
        bool Other(string name) => mine is null || !AgentFamily.Of(adapters, name).Same(mine);

        // The default's agents are those set up and signed in here, of another maker than the asker's.
        if (rule.SetUp) return [.. rule.Agents.Where(Other)];
        if (rule.Agents.Count > 0) return rule.Agents;

        // Every agent of this machine that is not the asker's own family.
        return [.. adapters.Names.Where(Other)];
    }

    /// <summary>
    /// The entries the walk is asked over: who the rule or the ask names, each taking the actor's entries
    /// for it, else account default (<c>rule-default</c>); with no such list and the actor <i>set</i>, the actor's entries replace every
    /// agent of another maker, the asker's own family among them if the person listed it. Another maker than the asker's still comes first
    /// (the walk's passes); the actor never widens who may be asked where the person's list or the ask names the agents.
    /// </summary>
    /// <exception cref="ActorUnreadableException">The helper actor's stored entry does not read.</exception>
    /// <param name="catalogue">
    /// The role catalogue: the role taking the helper's work at <paramref name="workspace"/> gives the entries; null, or a
    /// catalogue of the seed alone, reads the seeded <c>helper</c>.
    /// </param>
    /// <exception cref="DriverException">A role record that does not read, or two roles taking the work in one scope.</exception>
    public static IReadOnlyList<ActorEffective> Order(
        HelperAsk ask, HelperRule rule, ProviderRegistry adapters, string? askerAdapter, DriverConfig config, RoleCatalog? catalogue = null,
        string? workspace = null)
    {
        var fence = Candidates(ask, rule, adapters, askerAdapter);
        var named = ask.Agent is { Length: > 0 } && fence.Count == 1;
        var persons = !rule.SetUp && rule.Agents.Count > 0;
        // A scout's pass is orient work, the Scout role's, which falls to Exact's when removed; every other helper's is help work.
        var found = catalogue?.RoleFor(ask.Scout is null ? ParticipantWork.Help : ParticipantWork.Orient, new RolePlace(workspace));
        if (found?.Held is { } held) throw new DriverException(held);
        var role = found is null ? (ask.Scout is null ? "helper" : "scout") : found.Role?.Id;
        if (role is null) return [];
        return !named && !persons && config.Actors.ContainsKey(role)
            ? ActorChoices.Effective(role, config)
            : fence.Count == 0 ? [] : ActorChoices.Effective(role, config, fence: fence);
    }

    /// <summary>The walk over <see cref="Order"/>, as a reviewer's, with the asker's agent as the work's family.</summary>
    /// <param name="passed">The (entry, account) this ask's call already passed at an open's refusal.</param>
    public static Task<ReviewerChoice> ChooseAsync(
        HarnessRoster roster, HelperAsk ask, HelperRule rule, string? askerAdapter, DriverConfig config, string? workspace,
        CancellationToken ct = default, IReadOnlyList<ReviewerPassed>? passed = null, Func<RoleCatalog>? catalogue = null)
    {
        IReadOnlyList<ActorEffective> order;
        RoleCatalog? read;
        try
        {
            read = catalogue?.Invoke();
            order = Order(ask, rule, roster.Adapters, askerAdapter, config, read, workspace);
        }
        catch (Exception error) when (error is ActorUnreadableException or DriverException)
        {
            return Task.FromResult(new ReviewerChoice(null, null, ReviewerUnavailable.Refused, $"No second opinion: {error.Message}"));
        }

        return ReviewerChoice.ChooseAsync(
            roster, OpinionRule.None, askerAdapter is { Length: > 0 } adapter ? [adapter] : [], config, workspace, ct, "helper", passed, order,
            read is null ? null : () => read);
    }
}
