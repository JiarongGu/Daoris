using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One entry of an actor's effective order (D205 §4.0): who runs, with what, and <c>From</c>, what the record keeps as its <c>choice</c>:
/// <c>1</c> to <c>4</c>, <c>before</c>, <c>rule-default</c>, <c>pick</c> or <c>handed</c>.
/// </summary>
public sealed record ActorEffective(string Agent, string? Model, string? Effort, string From)
{
    /// <summary>What this entry asks a start to carry: a value the entry names is carried; one it leaves out is account default.</summary>
    public ActorAsked Asked => new(
        Agent,
        Model is { } model ? new("entry", model, true) : new("account-default", null, false),
        Effort is { } effort ? new("entry", effort, true) : new("account-default", null, false));
}

/// <summary>
/// The actors' one judge and one reading (D205 §3.1, §4.0, §6.1, MODELROLE1c): <see cref="Judge"/> refuses alike for the screen, the
/// terminal and a proposal, and <see cref="Effective"/> answers every start, the editor and the terminal the order a start walks.
/// </summary>
/// <remarks>
/// An actor's stored state is one of three (§4.0): absent, <i>as before</i>, read from today's keys at account default; <c>[]</c>,
/// <i>off</i>, for Intake and Ask Daoris alone; or one to four choices. A stored entry that does not read is kept as its error and holds
/// the start that reads it (<see cref="ActorUnreadableException"/>), never silently read as <i>as before</i> (§6.4).
/// </remarks>
public static class ActorChoices
{
    /// <summary>The eight actors' wire ids, in the screen's order (§2.2): the settings' <see cref="ActorKey.Ids"/>.</summary>
    public static IReadOnlyList<string> Ids => ActorKey.Ids;

    /// <summary>The most choices an actor holds: the most voices a brainstorm takes (<c>BrainstormDesk.MostAgents</c>).</summary>
    public const int Most = ActorKey.Most;

    /// <summary>Whether <paramref name="actor"/> may be <i>off</i>: Intake and Ask Daoris, whose absent key is off today.</summary>
    public static bool MayBeOff(string actor) => ActorKey.MayBeOff(actor);

    /// <summary>The supported harness (D23): the agent a seeded Build, Chat or Brainstorm starts on, and where Build's order names none.</summary>
    public const string DefaultAgent = "claude-code";

    /// <summary>
    /// The agent this machine's work runs on where nothing names one: Build's first entry, or <see cref="DefaultAgent"/> where Build's
    /// order is unset or does not read. Never throws: the reports and plans that name it say the unread order by their own line.
    /// </summary>
    public static string BuildAgent(DriverConfig config) => ActorKey.FirstAgent("build", config) ?? DefaultAgent;

    /// <summary>The five roles with an order of their own (the oneway design §2): each holds a written order, never <i>as before</i>.</summary>
    public static IReadOnlyList<string> Ordered { get; } = ["build", "chat", "brainstorm", "intake", "ask-daoris"];

    /// <summary>
    /// A role's seed (ONEWAY1b): the written order a fresh home starts with and the role door's <c>default</c> writes, what a home with none
    /// of the old keys ran; null for Second opinion, Partner and Helper, whose caller's rule decides who runs.
    /// </summary>
    public static IReadOnlyList<ActorChoice>? Seed(string role) => role switch
    {
        "build" or "chat" or "brainstorm" => [new ActorChoice(DefaultAgent)],
        "intake" or "ask-daoris" => [],
        _ => null,
    };

    /// <summary>
    /// Whether <paramref name="choices"/> may be <paramref name="actor"/>'s order: null (back to <i>as before</i>) always; otherwise the
    /// refusal's sentence, or null where it may (the settings' <see cref="ActorKey.Judge"/>).
    /// </summary>
    public static string? Judge(string actor, IReadOnlyList<ActorChoice>? choices) => ActorKey.Judge(actor, choices);

    /// <summary><see cref="Judge"/>, thrown as the driver's refusal.</summary>
    /// <exception cref="DriverException">The judge's refusal; nothing changed.</exception>
    public static void Judged(string actor, IReadOnlyList<ActorChoice>? choices)
    {
        if (Judge(actor, choices) is { } refused) throw new DriverException(refused);
    }

    /// <summary>
    /// <paramref name="actor"/>'s effective order (§4.0): its entries as stored, or, <i>as before</i>, read from today's keys at account
    /// default (§6.1). A <paramref name="pick"/> or a <paramref name="handed"/> agent overrides who runs, its model the actor's first
    /// entry for that agent; a <paramref name="fence"/>'s agents decide who, each taking the actor's entries for it, else
    /// <c>rule-default</c>. Second opinion and Partner have no order of their own, and Helper <i>as before</i> is
    /// <c>HelperChoice.Candidates</c>: each reads empty here, its caller's own rule deciding.
    /// </summary>
    /// <exception cref="ActorUnreadableException">The actor's stored entry does not read: the start that reads it holds.</exception>
    public static IReadOnlyList<ActorEffective> Effective(
        string actor, DriverConfig config, IReadOnlyList<string>? fence = null, string? pick = null, string? handed = null)
    {
        if (config.ActorErrors.TryGetValue(actor, out var error)) throw new ActorUnreadableException(actor, error);
        var stored = config.Actors.TryGetValue(actor, out var set) ? set : null;

        ActorEffective Named(string agent, string from)
        {
            var first = stored?.FirstOrDefault(choice => string.Equals(choice.Agent, agent, StringComparison.OrdinalIgnoreCase));
            return new(agent, first?.Model, first?.Effort, from);
        }

        if (pick is { Length: > 0 }) return [Named(pick, "pick")];
        if (handed is { Length: > 0 }) return [Named(handed, "handed")];
        if (fence is { Count: > 0 })
        {
            var fenced = new List<ActorEffective>();
            foreach (var agent in fence)
            {
                var entries = stored?
                    .Select((choice, i) => (choice, i))
                    .Where(each => string.Equals(each.choice.Agent, agent, StringComparison.OrdinalIgnoreCase))
                    .Select(each => new ActorEffective(agent, each.choice.Model, each.choice.Effort, (each.i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .ToList() ?? [];
                fenced.AddRange(entries.Count > 0 ? entries : [new ActorEffective(agent, null, null, "rule-default")]);
            }

            return fenced;
        }

        return stored is null
            ? []
            : [.. stored.Select((choice, i) => new ActorEffective(
                choice.Agent, choice.Model, choice.Effort, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))];
    }

    /// <summary>The agent a start of <paramref name="actor"/> runs where nothing walks its order: its first entry's, or null where it is off.</summary>
    /// <exception cref="ActorUnreadableException">The actor's stored entry does not read.</exception>
    public static string? FirstAgent(string actor, DriverConfig config)
    {
        if (config.ActorErrors.TryGetValue(actor, out var error)) throw new ActorUnreadableException(actor, error);
        return ActorKey.FirstAgent(actor, config);
    }

    /// <summary>
    /// The agent a new chat starts on, and the order it walks (the foundation design §2.4's third row, FOUNDATION1j part iii): the person's
    /// <paramref name="named"/> agent alone, its model Chat's first entry for it (D205 §3.5(d)); else Chat's order, whose first agent the
    /// start asks. The same answer for the page's press, a conversation started from words and a terminal's, never the machine's
    /// <c>adapter</c> key: Chat left unset answers that key itself (<see cref="FirstAgent"/>), which is read only where the order is empty,
    /// as no order of Chat's is (Chat cannot be off).
    /// </summary>
    /// <exception cref="ActorUnreadableException">Chat's stored entry does not read: the start holds, saying so.</exception>
    /// <exception cref="DriverException">Chat is off: the start holds, saying so.</exception>
    public static (string Agent, IReadOnlyList<ActorEffective> Order) NewChat(DriverConfig config, string? named = null)
    {
        var order = Effective("chat", config, pick: named);
        return (order.FirstOrDefault()?.Agent ?? FirstAgent("chat", config)
            ?? throw new DriverException("Chat is off on this machine, so it starts no conversation until it is given a choice."), order);
    }

    /// <summary>
    /// <paramref name="config"/> with <paramref name="actor"/>'s order set (or, null, back to no order of its own), judged first, and
    /// written through (§6.4).
    /// </summary>
    /// <exception cref="DriverException">The judge's refusal; nothing changed.</exception>
    public static DriverConfig With(DriverConfig config, string actor, IReadOnlyList<ActorChoice>? choices)
    {
        Judged(actor, choices);
        var actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(config.Actors, StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(config.ActorErrors, StringComparer.Ordinal);
        errors.Remove(actor);
        if (choices is null) actors.Remove(actor); else actors[actor] = [.. choices];
        return config with { Actors = actors, ActorErrors = errors };
    }

    /// <summary>The stored actors, as <c>driver.json</c>'s <c>actors</c> holds them: each readable entry, and each that does not read with why.</summary>
    public static (IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> Actors, IReadOnlyDictionary<string, string> Errors) Read(JsonElement root) =>
        ActorKey.Read(root);

    /// <summary>
    /// A press's <c>choices</c> (<c>SET_ROLE</c>'s payload): null where absent or null, as before; an empty list, off; else each entry
    /// <c>{ agent, model?, effort? }</c>, a blank model or effort read as account default.
    /// </summary>
    /// <exception cref="DriverException">A list whose entry is not one: nothing is read from it.</exception>
    public static IReadOnlyList<ActorChoice>? Payload(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } named || !named.TryGetProperty("choices", out var list)
            || list.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (list.ValueKind != JsonValueKind.Array) throw new DriverException("the press's choices are not a list.");
        var choices = new List<ActorChoice>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Text(item, "agent") is not { } agent)
            {
                throw new DriverException($"the press's choice {choices.Count + 1} names no agent.");
            }

            choices.Add(new ActorChoice(agent, Blank(Text(item, "model")), Blank(Text(item, "effort"))));
        }

        return choices;
    }

    private static string? Blank(string? word) => string.IsNullOrWhiteSpace(word) ? null : word;

    private static string? Text(JsonElement item, string name) => ActorKey.Text(item, name);

    /// <summary>The stored actors written as <c>driver.json</c> keeps them, only where any is set, in the screen's order.</summary>
    public static void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<ActorChoice>> actors) => ActorKey.Write(writer, actors);
}

/// <summary>
/// One role's staffing as the screen, the terminal and each seeded <c>ROLES</c> row say it: <c>State</c> is <c>set</c>, <c>before</c>, <c>off</c> or
/// <c>unreadable</c>, with its effective order (empty for Second opinion and Partner, whose rule or ask names the agents, and for Helper as
/// before) and, where it does not read, why.
/// </summary>
public sealed record ActorView(string Id, string State, IReadOnlyList<ActorEffective> Choices, string? Error)
{
    /// <summary>
    /// The order a start would walk now where the role's set policy arranges it otherwise than its stored order (STAFFPOLICY1d), for the page's
    /// hand-over offer; null where it is the stored order, or the role's is not asked.
    /// </summary>
    public IReadOnlyList<ActorEffective>? Arranged { get; init; }

    /// <summary>Every actor of <paramref name="config"/>, in the screen's order.</summary>
    public static IReadOnlyList<ActorView> All(DriverConfig config) => [.. ActorChoices.Ids.Select(id => Of(id, config))];

    /// <summary><paramref name="id"/> as <paramref name="config"/> holds it.</summary>
    public static ActorView Of(string id, DriverConfig config)
    {
        if (config.ActorErrors.TryGetValue(id, out var error)) return new(id, "unreadable", [], error);
        var choices = ActorChoices.Effective(id, config);
        var state = config.Actors.TryGetValue(id, out var set) ? set.Count == 0 ? "off" : "set"
            : choices.Count == 0 && ActorChoices.MayBeOff(id) ? "off" : "before";
        return new(id, state, choices, null);
    }
}

/// <summary>An actor's stored entry in <c>driver.json</c> that does not read: the start that reads it holds with this sentence (§6.4).</summary>
public sealed class ActorUnreadableException(string actor, string why)
    : DriverException($"driver.json's `actors.{actor}` {why}, so {actor} starts nothing until it is set again (`daoris role set {actor} …`).")
{
    public string Actor { get; } = actor;
}
