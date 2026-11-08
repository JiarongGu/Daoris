namespace Daoris.Driver;

/// <summary>
/// The walk over the order of choices a quest's start takes: the order of the role taking its work, the start plan built over it with
/// its passes remembered and its gate the quest's own, the role it took, and the re-ask where its agent refused the model it asked at
/// the open.
/// </summary>
internal sealed class QuestWalk(string home, DriverConfig config, LoopContext context, PassMemory passed)
{
    /// <summary>
    /// The order the role taking a quest's work walks, with the fence, the pick or the hand-over's agent a caller names: the role its
    /// run's work step names where it names one, held by its own sentence where that role cannot take it.
    /// </summary>
    public IReadOnlyList<ActorEffective> Order(Consideration start, IReadOnlyList<string>? fence = null, string? handed = null, string? role = null) =>
        role is { Length: > 0 }
            ? RoleCatalog.ReadHome(home).Order(role, Driver.StartWork(start.Quest), Driver.PlaceOf(start), config, fence, handed: handed)
            : RoleCatalog.ReadHome(home).Order(Driver.StartWork(start.Quest), Driver.PlaceOf(start), config, fence, handed: handed);

    /// <summary>
    /// A quest's start plan: Build's order for its work, its passes remembered and said under the quest, its gate the quest's own: an
    /// agent whose harness has no mode for the repository's permission prompts, and a pipe door where the repository has not adopted
    /// Daoris, each passed before any account is asked. The role its run's work step names is walked in place of the work's.
    /// </summary>
    public StartPlan Plan(Consideration start, IReadOnlyList<RepoView> registry, string? role = null)
    {
        var quest = start.Quest;
        var adopted = registry.FirstOrDefault(repo => string.Equals(repo.Repository, quest.To, StringComparison.OrdinalIgnoreCase))?.Adopted ?? true;
        var access = AccessRules.Resolve(config, quest.To, start.Workspace);
        (string Code, string Why)? Gate(ActorEffective entry, AgentDoor adapter)
        {
            if (DrivenAccess.For(access, quest.To, adapter).Refusal is { } unposed) return (StartPasses.NoPosture, unposed);
            return Adoption.Drives(adapter.Can, adopted)
                ? null
                : (StartPasses.NoConnector, adopted
                    ? $"`{entry.Agent}` cannot take a quest here: its servers do not inherit its environment, so `{quest.To}`'s own `.mcp.json` would not reach it"
                    : $"`{entry.Agent}` cannot take a quest here: `{quest.To}` has not adopted Daoris");
        }

        return new StartPlan(
            Driver.StartWork(quest), config, context.Harnesses, context.Adapters, passed, catalogue: RoleCatalog.ReadHome(home), place: Driver.PlaceOf(start), role: role)
        {
            Workspace = start.Workspace,
            Kind = StartKind.Driven,
            Subject = $"quest:{quest.Id}",
            Named = ("quest", quest.Id),
            Log = context.Log,
            Gate = Gate,
            Staff = context.Harnesses.StaffFor(home),
            // The choices this quest's line moved off in the last 30 minutes are passed.
            LineFacts = StaffCourse.LineFacts(context.Harnesses.StaffFor(home), quest, start.Workspace, role),
        };
    }

    /// <summary>
    /// A move to another choice made inside one start (a model its agent refused, a sign-in that walked to another agent), counted
    /// against the line's bound of moves in 24 hours. It names no part, so no later carry-on is taken for a move's own.
    /// </summary>
    public void CountMove(string line, string role, string condition, ActorEffective from, string? to)
    {
        try
        {
            context.Harnesses.StaffFor(home).Moves.Record(line, role, null, condition, StaffActions.Next, StaffKey.Of(from).Text, to);
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            context.Log?.Write("warn", "actor.moves-unkept", [("line", line), ("why", error.Message)]);
        }
    }

    /// <summary>
    /// The re-ask of a handed quest's start whose agent refused the model it asked at the open: Build's later entries for the handed
    /// <paramref name="agent"/> in order, then its own default with no model asked, the first that the same account offers, set on the
    /// same session. Models already refused here (<paramref name="refused"/>) are never asked again, so the re-ask ends. Null where none
    /// can run on the account, and the opened record ends <c>choice-not-offered</c>.
    /// </summary>
    public async Task<(ActorAsked Asked, string Choice)?> ReaskHandedAsync(
        Consideration start, string agent, HarnessSelection selection, ModelRefusal refusal, HashSet<string> refused, CancellationToken ct,
        string? role = null)
    {
        refused.Add(refusal.Value);
        (ActorAsked Asked, string Choice)? answer = null;
        try
        {
            var candidates = Order(start, fence: [agent], role: role).ToList();
            if (!candidates.Any(entry => entry.Model is null)) candidates.Add(new ActorEffective(agent, null, null, "rule-default"));
            // The plan's one loop over today's candidates: a handed quest names its agent and its account, so the ask stays the handed
            // one on the account the refusal named, and a model already refused is held, neither asked nor passed.
            var walked = await StartPlan.LoopAsync(
                candidates, entry => entry,
                async entry =>
                {
                    var (picked, _) = await context.Harnesses
                        .SelectHandedAsync(agent, config, start.Workspace, selection.Profile, StartKind.Driven, ct, entry.Model, null, selection.Number, counts: false)
                        .ConfigureAwait(false);
                    return picked.Allowed && string.Equals(picked.Profile, selection.Profile, StringComparison.OrdinalIgnoreCase)
                        ? EntryTry.Runs(picked)
                        : EntryTry.Passed(StartPasses.Held, picked.Refusal, picked);
                },
                holds: (entry, _) => entry.Model is { } model && refused.Contains(model)).ConfigureAwait(false);
            if (walked.Taken is { } taken) answer = (taken.Asked, taken.From);
        }
        catch (DriverException)
        {
            return null;
        }

        context.Log?.Write("info", "actor.reasked", [("actor", "build"), ("quest", start.Quest.Id), ("refused", refusal.Value), ("answer", answer?.Choice), ("same", answer is not null)]);
        return answer;
    }

    /// <summary>The catalogue for the look's report of a start, which never faults the look: a store that will not read says the seed's, and the start itself holds.</summary>
    public RoleCatalog? CatalogueForReport()
    {
        try
        {
            return RoleCatalog.ReadHome(home);
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>
    /// The role a quest's start took and its record's revision, for its binding: the one its run's work step names where it names one,
    /// else the work's; null where none takes the work or the store will not read.
    /// </summary>
    public (string Role, long Revision)? RoleTaken(Consideration start, string? named)
    {
        try
        {
            var catalogue = RoleCatalog.ReadHome(home);
            return Found(catalogue, Driver.StartWork(start.Quest), start, named).Role is { } role ? (role.Id, catalogue.RevisionOf(role.Id)) : null;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>The name of the role a design or exact quest's start of <paramref name="work"/> took, as its opening says it; null where the catalogue will not read.</summary>
    public string? WeightedRoleName(Consideration start, string work, string? named)
    {
        try
        {
            return Found(RoleCatalog.ReadHome(home), work, start, named).Role is { } role
                ? ActorChoices.Ids.Contains(role.Id, StringComparer.Ordinal) ? ActorWords.Name(role.Id) : role.Name.In("en")
                : null;
        }
        catch (DriverException)
        {
            return null;
        }
    }

    /// <summary>The role a start of <paramref name="work"/> takes: the one its step names, else the work's at its place.</summary>
    private static RoleFound Found(RoleCatalog catalogue, string work, Consideration start, string? named) =>
        named is { Length: > 0 } ? catalogue.Named(named, work, Driver.PlaceOf(start)) : catalogue.RoleFor(work, Driver.PlaceOf(start));
}
