namespace Daoris.Driver;

/// <summary>
/// The codes a choice that may run is read at (COMPANYVIEW1c): beside <see cref="StartPasses"/>' codes for one that cannot, by the account it
/// would run on.
/// </summary>
public static class ChoiceStates
{
    /// <summary>An account it may use was last read signed in.</summary>
    public const string Ready = "ready";

    /// <summary>The account it would run on was last read neither signed in nor out.</summary>
    public const string Unknown = "unknown";

    /// <summary>The account it would run on was never read: a start runs on it, as on any unknown.</summary>
    public const string NotRead = "not-read";
}

/// <summary>A choice's state by the last readings alone (COMPANYVIEW1c).</summary>
/// <param name="Code">One of <see cref="ChoiceStates"/>, or of <see cref="StartPasses"/>' codes.</param>
/// <param name="At">When the reading it rests on was made: a sign-in's, a cool-off's or an offer's; none where it rests on none.</param>
/// <param name="Account">The account it rests on; none for the agent's own home and where no account is.</param>
/// <param name="Until">A cool-off's end, the soonest of the agent's accounts'.</param>
public sealed record ChoiceState(string Code, DateTimeOffset? At = null, string? Account = null, DateTimeOffset? Until = null);

/// <summary>One choice of a work's order at a place, with its state.</summary>
public sealed record StructureChoice(ActorEffective Choice, ChoiceState State);

/// <summary>One work at one place: its role and the role's revision, or why its starts hold; its order's choices; its rule; each part's failure.</summary>
/// <param name="Held">Why its starts hold, in the start's sentence: no role, two in one scope, a record or the catalogue unread.</param>
/// <param name="ChoicesFailed">Why its role's staffing does not read, the start's sentence; its choices are then none.</param>
/// <param name="PolicyFailed">Why its rule does not read; its policy is then none.</param>
public sealed record StructureWork(
    string Work, RoleRecord? Role, long Revision, string? Held, IReadOnlyList<StructureChoice> Choices, string? ChoicesFailed, WorkPolicy? Policy,
    string? PolicyFailed);

/// <summary>One place asked, with each work asked of it.</summary>
public sealed record StructurePlace(RolePlace Place, IReadOnlyList<StructureWork> Works);

/// <summary>An agent a choice names: whether this machine has it, and whether its binary is there by its last answer (null: not asked yet).</summary>
public sealed record StructureAgent(string Agent, bool Known, bool? Installed, string? Version);

/// <summary>
/// The roles' structure for the places and works asked (COMPANYVIEW1c, the company-view design §4): each place's role per work by
/// <see cref="RoleCatalog.RoleFor"/>, as a start finds it; each work's rule by <see cref="WorkPolicies.Of"/>, inherited and default answers
/// included, with where it was set; and each choice of the role's order, fenced by the rule's agents as a start's is, with its state from
/// the last readings alone.
/// </summary>
/// <remarks>
/// A projection of the configuration, never a start's selection: it walks no account, asks no account, binary or server, takes no count,
/// and reads every choice, where a start stops at the first that runs (D150 point 5, <see cref="HarnessRoster.LastRead"/>). Each part's
/// failure is said on its item; the rest reads.
/// </remarks>
public sealed record RoleStructure(RoleCatalog? Catalogue, string? CatalogueFailed, IReadOnlyList<StructurePlace> Places, IReadOnlyList<StructureAgent> Agents)
{
    /// <param name="catalogue">How the catalogue is read: a store that cannot be read holds every start, said.</param>
    public static RoleStructure Of(
        Func<RoleCatalog> catalogue, DriverConfig config, HarnessRoster roster, IReadOnlyList<RolePlace> places, IReadOnlyList<string> works)
    {
        RoleCatalog? read = null;
        string? failed = null;
        try
        {
            read = catalogue();
        }
        catch (DriverException error)
        {
            failed = error.Message;
        }

        var now = roster.Clock();
        var offers = AgentOffers.Read(roster.Home);
        var known = new Dictionary<(string, string?, StartKind), AgentLastRead?>();
        AgentLastRead? Known(string agent, string? workspace, StartKind kind)
        {
            var key = (agent.ToLowerInvariant(), workspace, kind);
            if (!known.TryGetValue(key, out var held)) known[key] = held = roster.LastRead(agent, config, workspace, kind);
            return held;
        }

        StructureWork Work(string work, RolePlace place)
        {
            WorkPolicy? policy = null;
            string? policyFailed = null;
            try
            {
                policy = WorkPolicies.Of(config, work, place.Repository, place.Workspace);
            }
            catch (DriverException error)
            {
                policyFailed = error.Message;
            }

            if (failed is not null) return new(work, null, 0, failed, [], null, policy, policyFailed);
            var found = read!.RoleFor(work, place);
            if (found.Held is { } held || found.Role is not { } role) return new(work, null, 0, found.Held, [], null, policy, policyFailed);

            IReadOnlyList<ActorEffective> order;
            string? choicesFailed = null;
            try
            {
                order = ActorChoices.Effective(role.Id, config, fence: policy?.Agents is { Count: > 0 } agents ? agents : null);
            }
            catch (ActorUnreadableException error)
            {
                (order, choicesFailed) = ([], error.Message);
            }

            var kind = work is RoleWork.Chat or RoleWork.AskDaoris ? StartKind.Conversation : StartKind.Driven;
            return new(
                work, role, read.RevisionOf(role.Id), null,
                [.. order.Select(choice => new StructureChoice(choice, StateOf(choice, place.Workspace, kind)))], choicesFailed, policy, policyFailed);
        }

        ChoiceState StateOf(ActorEffective choice, string? workspace, StartKind kind)
        {
            if (!roster.Adapters.Names.Contains(choice.Agent, StringComparer.OrdinalIgnoreCase)) return new(StartPasses.Unavailable);
            if (Known(choice.Agent, workspace, kind) is not { } agent) return new(ChoiceStates.Ready);
            return Judged(choice, agent, offers, now);
        }

        var answered = places.Select(place => new StructurePlace(place, [.. works.Select(work => Work(work, place))])).ToList();
        var agents = answered.SelectMany(place => place.Works).SelectMany(work => work.Choices).Select(choice => choice.Choice.Agent)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => roster.Adapters.Names.Contains(name, StringComparer.OrdinalIgnoreCase) && Known(name, null, StartKind.Driven) is { } agent
                ? new StructureAgent(name, true, agent.Installed, agent.Version)
                : new StructureAgent(name, roster.Adapters.Names.Contains(name, StringComparer.OrdinalIgnoreCase), null, null))
            .ToList();
        return new RoleStructure(read, failed, answered, agents);
    }

    /// <summary>
    /// A choice judged as a start's walk would pass it, from what was last read: a binary not there and no account each before any account;
    /// then the first account it may use, a signed-in one before an unknown one before one never read; else, as the walk's hold says it,
    /// a model every ready account refused, then the soonest cool-off, then a sign-out, then a refusal its provider gave.
    /// </summary>
    private static ChoiceState Judged(ActorEffective choice, AgentLastRead agent, IReadOnlyList<OfferReading> offers, DateTimeOffset now)
    {
        if (agent.Installed == false) return new(StartPasses.Unavailable);
        if (agent.Accounts.Count == 0) return new(StartPasses.SignedOut);

        CoolingEntry? soonest = null;
        AccountLastRead? signedOut = null, runs = null;
        ChoiceState? unoffered = null;
        foreach (var account in agent.Accounts)
        {
            if (account.State.Cooling is { } cooling)
            {
                if (soonest is null || cooling.Until < soonest.Until) soonest = cooling;
                continue;
            }

            if (!account.State.IsReady) continue;
            // The agent's own home: the walk asks no sign-in there.
            if (account.Account is not { } id) return new(ChoiceStates.Ready);
            if (AgentOffers.Judge(offers, agent.Agent, agent.Version, id, choice.Model, now) is not null)
            {
                var reading = offers.FirstOrDefault(each => string.Equals(each.Agent, agent.Agent, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(each.Account, id, StringComparison.OrdinalIgnoreCase));
                unoffered ??= new(StartPasses.NotOffered, reading?.RefusedAt, id);
                continue;
            }

            if (account.Read is { Login: LoginState.Out })
            {
                signedOut ??= account;
                continue;
            }

            if (runs is null || Rank(account) > Rank(runs)) runs = account;
        }

        if (runs is not null)
        {
            var code = runs.Read switch { null => ChoiceStates.NotRead, { Login: LoginState.In } => ChoiceStates.Ready, _ => ChoiceStates.Unknown };
            return new(code, runs.Read?.At, runs.Account);
        }

        if (unoffered is not null) return unoffered;
        if (soonest is not null) return new(StartPasses.Cooling, soonest.Seen, soonest.Account, soonest.Until);
        if (signedOut is not null) return new(StartPasses.SignedOut, signedOut.Read!.At, signedOut.Account);
        return new(StartPasses.Held);

        static int Rank(AccountLastRead account) => account.Read switch { null => 0, { Login: LoginState.In } => 2, _ => 1 };
    }
}
