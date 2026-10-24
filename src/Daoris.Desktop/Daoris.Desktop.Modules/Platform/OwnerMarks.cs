using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a payload's field names, as an owner's door marks it (STEWARDROLE1a-ii, D209 §5.4): a workspace, a repository or a quest; a
/// session, resolved to its quest; an ask, an entry (<c>ask:&lt;id&gt;/&lt;repository&gt;</c>) or a work unit (<c>quest:&lt;id&gt;</c>,
/// <c>ask:&lt;id&gt;</c>), each resolved to its quests.
/// </summary>
public static class OwnerKinds
{
    public const string Workspace = "workspace";
    public const string Repository = "repository";
    public const string Quest = "quest";
    public const string Session = "session";
    public const string Ask = "ask";
    public const string Entry = "entry";
    public const string Unit = "unit";
}

/// <summary>
/// The closed reasons a route marks nothing by (D209 §5.4). <see cref="ActsOnNothing"/> and <see cref="OwnConversation"/> are taken
/// only by a route on the census sweep's reviewed exemptions where it names a target (<c>OwnerSetCensusTests</c>).
/// </summary>
public static class OwnerNone
{
    /// <summary>It names no repository, quest or session.</summary>
    public const string NamesNothing = "names-nothing";

    /// <summary>It begins the owner's own conversation, which is no quest.</summary>
    public const string OwnConversation = "own-conversation";

    /// <summary>A machine setting, no repository's.</summary>
    public const string MachineSetting = "machine-setting";

    /// <summary>It acts on nothing a role acts on: a folder opened, a notice read or dismissed, a channel marked.</summary>
    public const string ActsOnNothing = "acts-on-nothing";

    /// <summary>The appointment of a role, which acts on the role and never on the repository it names.</summary>
    public const string Appointment = "appointment";

    /// <summary>Every reason, the closed list.</summary>
    public static IReadOnlyList<string> All { get; } = [NamesNothing, OwnConversation, MachineSetting, ActsOnNothing, Appointment];
}

/// <summary>A payload's field that names a target, its kind, and whether it holds a list.</summary>
public sealed record OwnerField(string Field, string Kind, bool Many = false);

/// <summary>
/// A route's column of <see cref="PipeTargets"/> D209 adds (§5.4): the fields that name what it acts on, or <see cref="None"/> with its
/// closed reason, or <see cref="ByPlan"/> where it marks by the plan it applies (<c>HELP_APPLY</c>).
/// </summary>
public sealed record OwnerTarget(IReadOnlyList<OwnerField> Fields, string? None = null, bool ByPlan = false)
{
    public static OwnerTarget Of(params OwnerField[] fields) => new(fields);

    public static OwnerTarget Nothing(string why) => new([], why);
}

/// <summary>A target as a payload or a plan names it, before it is resolved.</summary>
public sealed record OwnerNamed(string Kind, string Name);

/// <summary>
/// What an owner's act resolves to (§5.4's target resolution): the repositories and quests it marks, and the gates it takes, a
/// workspace among them; <see cref="Unresolved"/> where a session, an ask or an entry could not be read, which marks every quest.
/// </summary>
public sealed record OwnerResolved(IReadOnlyList<RoleScope> Marks, IReadOnlyList<RoleScope> Gates, bool Unresolved)
{
    public static OwnerResolved Empty { get; } = new([], [], false);
}

/// <summary>
/// The reads the target resolution makes (§5.4): a session's quest, an ask's quests, a quest's repository, a repository's workspace and
/// a workspace's repositories. A read that fails throws; a session that serves no quest (a conversation) answers null.
/// </summary>
public interface IOwnerScopes
{
    Task<string?> QuestOfSessionAsync(string session, CancellationToken ct);

    Task<IReadOnlyList<string>> QuestsOfAskAsync(string ask, string? repository, CancellationToken ct);

    Task<string?> RepositoryOfQuestAsync(string quest, CancellationToken ct);

    Task<string?> WorkspaceOfAsync(string repository, CancellationToken ct);

    Task<IReadOnlyList<string>> RepositoriesOfAsync(string workspace, CancellationToken ct);

    /// <summary>Whether a person's door wrote on <paramref name="quest"/>'s records after <paramref name="since"/> (<see cref="OwnerRecords"/>).</summary>
    Task<bool> PersonWroteAsync(string quest, DateTimeOffset since, CancellationToken ct);
}

/// <summary>The target resolution's reads from the service's answers (§5.4): this machine's records, the registry and the asks.</summary>
public sealed class ServiceOwnerScopes(Func<ServiceClient?> service) : IOwnerScopes
{
    private ServiceClient Service => service() ?? throw new InvalidOperationException("the service is not reachable yet.");

    public async Task<string?> QuestOfSessionAsync(string session, CancellationToken ct)
    {
        var records = await Service.SessionRecordsAsync(ct).ConfigureAwait(false);
        var record = records.FirstOrDefault(each => string.Equals(each.Id, session, StringComparison.Ordinal))
                     ?? throw new InvalidOperationException("no record here has that id.");
        return record.Quest;
    }

    public async Task<IReadOnlyList<string>> QuestsOfAskAsync(string ask, string? repository, CancellationToken ct)
    {
        var asks = await Service.EveryAskAsync(ct).ConfigureAwait(false);
        var found = asks.FirstOrDefault(each => string.Equals(each.Id, ask, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("no ask has that id.");
        if (repository is null) return found.Quests;
        var quests = await Service.EveryQuestAsync(ct).ConfigureAwait(false);
        return
        [
            .. found.Quests.Where(id => quests.Any(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)
                                                              && string.Equals(each.To, repository, StringComparison.OrdinalIgnoreCase))),
        ];
    }

    public async Task<string?> RepositoryOfQuestAsync(string quest, CancellationToken ct) =>
        (await Service.FindQuestAsync(quest, ct).ConfigureAwait(false))?.To;

    public async Task<string?> WorkspaceOfAsync(string repository, CancellationToken ct) =>
        (await Service.RegistryAsync(ct).ConfigureAwait(false))
        .FirstOrDefault(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))?.Workspace;

    public async Task<IReadOnlyList<string>> RepositoriesOfAsync(string workspace, CancellationToken ct) =>
    [
        .. (await Service.RegistryAsync(ct).ConfigureAwait(false))
            .Where(each => string.Equals(each.Workspace, workspace, StringComparison.OrdinalIgnoreCase)).Select(each => each.Repository),
    ];

    public async Task<bool> PersonWroteAsync(string quest, DateTimeOffset since, CancellationToken ct)
    {
        var client = Service;
        var facts = OwnerRecords.Read(
            quest,
            await client.QuestsJsonAsync(ct).ConfigureAwait(false),
            await client.QuestMovesJsonAsync(quest, ct).ConfigureAwait(false),
            await client.AsksJsonAsync(ct).ConfigureAwait(false),
            await client.SessionRecordsJsonAsync(ct).ConfigureAwait(false));
        return OwnerRecords.Marked(facts, since) is not null;
    }
}

/// <summary>
/// The owner's mark (STEWARDROLE1a-ii, D209 §5.4): <see cref="Of"/> answers what an owner's door names from its census row
/// (<see cref="PipeTargets.Mark"/>), <see cref="ResolveAsync"/> resolves it to its scopes, and <see cref="EnterAsync"/> takes their
/// gates beside the owner's other presses and marks them in the judge's memory before the handler runs inside.
/// </summary>
public static class OwnerMarks
{
    /// <summary>
    /// What <paramref name="module"/>'s <paramref name="route"/> names in <paramref name="payload"/>, by its row; null where the route has
    /// no row or marks by its plan. A field another casing spells is read too, as the handler's payload reader reads it.
    /// </summary>
    public static IReadOnlyList<OwnerNamed>? Of(string module, string route, JsonElement? payload, PipeTargets? targets = null)
    {
        if ((targets ?? PipeTargets.Daoris).Mark(module, route) is not { ByPlan: false } row) return null;
        var named = new List<OwnerNamed>();
        if (payload is not { ValueKind: JsonValueKind.Object } body) return named;
        foreach (var field in row.Fields)
        {
            foreach (var property in body.EnumerateObject().Where(each => string.Equals(each.Name, field.Field, StringComparison.OrdinalIgnoreCase)))
            {
                var values = property.Value.ValueKind == JsonValueKind.Array ? property.Value.EnumerateArray() : Enumerable.Repeat(property.Value, 1);
                foreach (var value in values)
                {
                    if (value.ValueKind == JsonValueKind.String && value.GetString()?.Trim().TrimStart('#').Trim() is { Length: > 0 } name)
                    {
                        named.Add(new OwnerNamed(field.Kind, name));
                    }
                }
            }
        }

        return named;
    }

    /// <summary>
    /// <paramref name="named"/> resolved to its scopes (§5.4): a workspace to itself and every repository the registry places in it, a
    /// session to its quest (a conversation's to nothing), an ask, an entry or a unit to its quests. A read that fails marks every quest
    /// for the run (<see cref="OwnerResolved.Unresolved"/>), so the owner's press is never held up and the steward is held back.
    /// </summary>
    public static async Task<OwnerResolved> ResolveAsync(IReadOnlyList<OwnerNamed> named, IOwnerScopes? scopes, CancellationToken ct)
    {
        if (named.Count == 0) return OwnerResolved.Empty;
        var marks = new List<RoleScope>();
        var gates = new List<RoleScope>();
        var unresolved = false;
        foreach (var each in named)
        {
            try
            {
                switch (each.Kind)
                {
                    case OwnerKinds.Repository:
                        Both(new RoleScope(OwnerKinds.Repository, each.Name));
                        break;
                    case OwnerKinds.Quest:
                        Both(new RoleScope(OwnerKinds.Quest, each.Name));
                        break;
                    case OwnerKinds.Workspace:
                        gates.Add(new RoleScope(OwnerKinds.Workspace, each.Name));
                        if (scopes is null) throw new InvalidOperationException("no registry to resolve a workspace by.");
                        foreach (var repository in await scopes.RepositoriesOfAsync(each.Name, ct).ConfigureAwait(false))
                        {
                            Both(new RoleScope(OwnerKinds.Repository, repository));
                        }

                        break;
                    case OwnerKinds.Session:
                        if (scopes is null) throw new InvalidOperationException("no records to resolve a session by.");
                        if (await scopes.QuestOfSessionAsync(each.Name, ct).ConfigureAwait(false) is { Length: > 0 } quest)
                        {
                            Both(new RoleScope(OwnerKinds.Quest, quest));
                        }

                        break;
                    case OwnerKinds.Ask or OwnerKinds.Entry or OwnerKinds.Unit:
                        if (Quests(each) is { } of)
                        {
                            if (of.Quest is { } one) Both(new RoleScope(OwnerKinds.Quest, one));
                            else
                            {
                                if (scopes is null) throw new InvalidOperationException("no records to resolve an ask by.");
                                foreach (var asked in await scopes.QuestsOfAskAsync(of.Ask!, of.Repository, ct).ConfigureAwait(false))
                                {
                                    Both(new RoleScope(OwnerKinds.Quest, asked));
                                }
                            }
                        }

                        break;
                }
            }
            catch (Exception error) when (error is not (OperationCanceledException or OutOfMemoryException))
            {
                unresolved = true;
            }
        }

        return new OwnerResolved(RoleScope.Order(marks), RoleScope.Order(gates), unresolved);

        void Both(RoleScope scope)
        {
            marks.Add(scope);
            gates.Add(scope);
        }
    }

    /// <summary>An ask, an entry or a unit as the quests it reaches: one quest by its id, or an ask's, in one repository for an entry.</summary>
    private static (string? Quest, string? Ask, string? Repository)? Quests(OwnerNamed named)
    {
        var name = named.Name;
        if (named.Kind == OwnerKinds.Ask) return (null, name, null);
        var ask = name.StartsWith("ask:", StringComparison.OrdinalIgnoreCase);
        var quest = name.StartsWith("quest:", StringComparison.OrdinalIgnoreCase);
        var rest = ask ? name[4..] : quest ? name[6..] : null;
        if (rest is not { Length: > 0 }) return null;
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        if (named.Kind == OwnerKinds.Entry && ask && slash > 0) return (null, rest[..slash], rest[(slash + 1)..]);
        if (named.Kind == OwnerKinds.Entry && quest) return (slash > 0 ? rest[..slash] : rest, null, null);
        if (named.Kind == OwnerKinds.Unit) return quest ? (rest, null, null) : (null, rest, null);
        return null;
    }

    /// <summary>
    /// The owner's step before a handler (§5.4): what <paramref name="named"/> resolves to, its gates taken beside the owner's other
    /// presses in the fixed order, and its targets marked <c>owner-set</c> in <paramref name="judge"/>'s memory, which cancels every act
    /// and pending operation on them; the handler runs inside, and the gates are released as the answer is disposed. A judge whose build
    /// offers no steward marks nothing and takes no gate, so a dark build changes nothing the owner uses.
    /// </summary>
    /// <param name="standing">That the press runs under D201's standing confirmation, whose covered hold keeps its own memory.</param>
    public static async Task<IAsyncDisposable> EnterAsync(
        StandingConfirmation? judge, IOwnerScopes? scopes, IReadOnlyList<OwnerNamed> named, bool standing, CancellationToken ct)
    {
        if (judge is not { StewardOffered: true } || named.Count == 0) return Nothing.Held;
        var resolved = await ResolveAsync(named, scopes, ct).ConfigureAwait(false);
        var held = await judge.Gates.EnterAsync(resolved.Gates, alone: false, ct).ConfigureAwait(false);
        judge.MarkOwner(resolved, standing);
        return held;
    }

    private sealed class Nothing : IAsyncDisposable
    {
        public static Nothing Held { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
