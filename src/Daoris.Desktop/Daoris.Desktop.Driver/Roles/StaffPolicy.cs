using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A choice's identity in a role's policy: its agent, model and effort as the staffing writes them,
/// compared ordinally, which <see cref="ActorKey.Judge"/> keeps unique in a role. No id is written into a choice.
/// </summary>
public readonly record struct StaffKey(string Agent, string? Model, string? Effort)
{
    public static StaffKey Of(ActorChoice choice) => new(choice.Agent, choice.Model, choice.Effort);

    public static StaffKey Of(ActorEffective entry) => new(entry.Agent, entry.Model, entry.Effort);

    /// <summary>The key as <c>role_starts.choice</c> keeps it: one choice as the staffing writes it, <c>{ agent, model?, effort? }</c>.</summary>
    public string Text => StaffJson.Write(WriteTuple);

    /// <summary>The terminal's words, <c>agent model|- effort|-</c>, as <see cref="ActorChoice.Said"/>.</summary>
    public string Said => $"{Agent} {Model ?? "-"} {Effort ?? "-"}";

    /// <summary>The key a <see cref="Text"/> holds, or null where it is not one.</summary>
    public static StaffKey? Read(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return Json.Text(document.RootElement, "agent") is { } agent
                ? new StaffKey(agent, Json.Text(document.RootElement, "model"), Json.Text(document.RootElement, "effort"))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal void WriteTuple(Utf8JsonWriter writer)
    {
        writer.WriteString("agent", Agent);
        if (Model is { } model) writer.WriteString("model", model);
        if (Effort is { } effort) writer.WriteString("effort", effort);
    }
}

/// <summary>A choice's daily budget: new starts or reported tokens in the machine's local day, one of the two.</summary>
public sealed record StaffBudget(long? Starts = null, long? Tokens = null);

/// <summary>
/// One choice's entry in a policy: its weight under <c>weight</c>, 0 to 10; the most of the window's starts it takes, in percent; its
/// daily budget; and the works it takes, null for every work of the role.
/// </summary>
public sealed record StaffChoice(StaffKey Key, int Weight = 1, int? Share = null, StaffBudget? Budget = null, IReadOnlyList<string>? Takes = null);

/// <summary>What a condition does: an action of <see cref="StaffActions"/>, the tries or strikes before it, and how long a wait holds.</summary>
public sealed record StaffMove(string Do, int? After = null, int? UpTo = null);

/// <summary>The rules a policy orders a role's choices by.</summary>
public static class StaffRules
{
    /// <summary>The stored order: the rule of every role without a policy.</summary>
    public const string Order = "order";

    /// <summary>The deterministic weighted pick over the window.</summary>
    public const string Weight = "weight";

    public static IReadOnlyList<string> Codes { get; } = [Order, Weight];
}

/// <summary>The conditions Daoris observes that a policy may move a line on, and the actions each allows.</summary>
public static class StaffConditions
{
    /// <summary>Every account of the choice limited or cooling.</summary>
    public const string Limit = "limit";

    /// <summary>A door's access refusal.</summary>
    public const string SignedOut = "signed-out";

    /// <summary>The choice's model refused.</summary>
    public const string ModelRefused = "model-refused";

    /// <summary>The episode: the service does not answer.</summary>
    public const string Transient = "transient";

    /// <summary>The fatal error again.</summary>
    public const string Failed = "failed";

    /// <summary>A run past the inactivity window, stopped by a press or a headless quiet end.</summary>
    public const string Quiet = "quiet";

    /// <summary>The choice's daily budget reached, at a start.</summary>
    public const string Budget = "budget";

    public static IReadOnlyList<string> Codes { get; } = [Limit, SignedOut, ModelRefused, Transient, Failed, Quiet, Budget];
}

/// <summary>What a met condition does.</summary>
public static class StaffActions
{
    /// <summary>Walk the stored order from its top, the met choice passed.</summary>
    public const string Next = "next";

    /// <summary>That walk arranged by weight.</summary>
    public const string Weighted = "weighted";

    /// <summary>Hold for the met choice's earliest moment back, at most <see cref="StaffMove.UpTo"/> minutes where named.</summary>
    public const string Wait = "wait";

    /// <summary>Hold, offering the person to go on with the next.</summary>
    public const string Ask = "ask";

    /// <summary>Hold with today's doors.</summary>
    public const string Hold = "hold";

    /// <summary>The same choice again, today's carry-on.</summary>
    public const string Retry = "retry";

    public static IReadOnlyList<string> Codes { get; } = [Next, Weighted, Wait, Ask, Hold, Retry];

    /// <summary>Whether <paramref name="action"/> moves the line to another choice, which a policy's bound counts.</summary>
    public static bool Moves(string action) => action is Next or Weighted;
}

/// <summary>The presets that fill a policy's draft and never save.</summary>
public static class StaffPresets
{
    /// <summary><i>First that can run</i>: the order, nothing else.</summary>
    public const string First = "first";

    /// <summary><i>Spread evenly</i>: every weight 1, a limit moved by weight, at most 3 moves.</summary>
    public const string Even = "even";

    /// <summary><i>Spread by weight</i>: the person's weights, a limit moved by weight, at most 3 moves.</summary>
    public const string Weighted = "weighted";

    public static IReadOnlyList<string> Names { get; } = [First, Even, Weighted];
}

/// <summary>
/// A role's staffing policy: its rule and an entry for each of its choices by tuple in the
/// staffing's order. Kept apart from the choices in <c>role_policy</c>, bound to them by <see cref="Digest"/>; no row is
/// <see cref="StaffRules.Order"/>. <see cref="Moves"/> and <see cref="Most"/> are never stored: <see cref="SupervisorRuleBook.Course"/> sets
/// them from a line's effective rules, for the course to read.
/// </summary>
public sealed record StaffPolicy(string Rule, IReadOnlyList<StaffChoice> Choices, IReadOnlyDictionary<string, StaffMove> Moves, int? Most = null)
{
    /// <summary>The code of every refusal of a policy, in the role refusals' closed set (<see cref="RoleCatalog.Codes"/>).</summary>
    public const string Code = "policy";

    /// <summary>The most moves to another choice a line makes in 24 hours: the rules' moves bound, else none while every move is as today.</summary>
    public int? Bound => Most ?? (Moves.Count > 0 ? SupervisorRuleBook.MovesByDefault : null);

    /// <summary>The entry for <paramref name="key"/>, or null where the policy names none (weight 1, no limits).</summary>
    public StaffChoice? Of(StaffKey key) => Choices.FirstOrDefault(choice => choice.Key == key);

    /// <summary>
    /// The digest that binds a policy to the staffing it is set for: the SHA-256 of the choices as the staffing writes them
    /// (<see cref="RoleStaffing.Write"/>), lowercase hex. Choices written again unchanged keep it; any change to them moves it.
    /// </summary>
    public static string Digest(IReadOnlyList<ActorChoice> choices) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RoleStaffing.Write(choices)))).ToLowerInvariant();

    /// <summary>
    /// The policy re-keyed to <paramref name="choices"/>: each entry whose tuple is still listed kept, in the staffing's order; a new
    /// choice at weight 1 with no limits; a removed choice's entry dropped. The rule stays.
    /// </summary>
    public StaffPolicy Rekeyed(IReadOnlyList<ActorChoice> choices) =>
        this with { Choices = [.. choices.Select(choice => Of(StaffKey.Of(choice)) ?? new StaffChoice(StaffKey.Of(choice)))] };

    /// <summary>
    /// The draft a preset fills for <paramref name="choices"/>: <see cref="StaffPresets.First"/> the order and nothing else; the two
    /// spreads the weight rule, every weight 1 or the draft's, the draft's limits kept; their limit move and bound are rules
    /// (<see cref="SupervisorRuleBook.PresetRules"/>), not the policy's.
    /// </summary>
    /// <exception cref="ArgumentException">No such preset.</exception>
    public static StaffPolicy Preset(string preset, IReadOnlyList<ActorChoice> choices, StaffPolicy? draft)
    {
        if (preset == StaffPresets.First)
        {
            return new StaffPolicy(StaffRules.Order, [.. choices.Select(choice => new StaffChoice(StaffKey.Of(choice)))], new Dictionary<string, StaffMove>());
        }

        if (preset is not (StaffPresets.Even or StaffPresets.Weighted)) throw new ArgumentException($"`{preset}` is not a preset — one of {string.Join(", ", StaffPresets.Names)}.", nameof(preset));
        StaffChoice Filled(ActorChoice choice)
        {
            var kept = draft?.Of(StaffKey.Of(choice)) ?? new StaffChoice(StaffKey.Of(choice));
            return preset == StaffPresets.Even ? kept with { Weight = 1 } : kept;
        }

        return new StaffPolicy(StaffRules.Weight, [.. choices.Select(Filled)], new Dictionary<string, StaffMove>());
    }

    /// <summary>
    /// Whether <paramref name="policy"/> may be the policy of <paramref name="role"/> staffed by <paramref name="choices"/>, for every
    /// door alike: the refusal, code <see cref="Code"/>, or null where it may.
    /// </summary>
    public static RoleRefusal? Judge(RoleRecord role, IReadOnlyList<ActorChoice>? choices, StaffPolicy policy)
    {
        var id = role.Id;
        var of = $"`{id}`'s policy";
        if (role.Takes.Contains(RoleWork.Brainstorm, StringComparer.Ordinal))
        {
            return Refused($"`{id}` takes brainstorm, whose choices are voices that run together, so it keeps no policy.");
        }

        if (!StaffRules.Codes.Contains(policy.Rule, StringComparer.Ordinal)) return Refused($"{of}'s rule `{policy.Rule}` is not one: `order` or `weight`.");
        if (choices is not { Count: > 0 }) return Refused($"`{id}` has no choices of its own, so it keeps no policy: give it its choices first.");

        var works = Works(role);
        var seen = new HashSet<StaffKey>();
        foreach (var entry in policy.Choices)
        {
            var said = entry.Key.Said;
            if (!choices.Any(choice => StaffKey.Of(choice) == entry.Key)) return Refused($"{of} names {said}, which the role's staffing does not list.");
            if (!seen.Add(entry.Key)) return Refused($"{of} names {said} twice.");
            if (entry.Weight is < 0 or > 10) return Refused($"{of} gives {said} a weight of {entry.Weight}: a weight is from 0 to 10.");
            if (entry.Share is { } share && share is < 1 or > 99) return Refused($"{of} gives {said} a share of {share}%: a share is from 1 to 99.");
            if (entry.Budget is { } budget && ((budget.Starts is null) == (budget.Tokens is null) || (budget.Starts ?? budget.Tokens) < 1))
            {
                return Refused($"{of} gives {said} a budget that is not one: a day's starts or its reported tokens, one of them, a whole number above 0.");
            }

            if (entry.Takes is { } takes)
            {
                if (takes.Count == 0) return Refused($"{of}: {said} takes no work; name one or more of the role's works, or none for all of them.");
                foreach (var work in takes)
                {
                    if (!RoleWork.Codes.Contains(work, StringComparer.Ordinal)) return Refused($"`{work}` is not a work — one of {string.Join(", ", RoleWork.Codes)}.");
                    if (!works.Contains(work)) return Refused($"{of} gives {said} `{work}`, and `{id}` does not take `{work}`.");
                }

                if (takes.Distinct(StringComparer.Ordinal).Count() != takes.Count) return Refused($"{of} names one of {said}'s works twice.");
            }
        }

        if (policy.Rule == StaffRules.Weight && choices.All(choice => (policy.Of(StaffKey.Of(choice))?.Weight ?? 1) == 0))
        {
            return Refused($"{of} spreads by weight and every weight is 0: one must be above 0.");
        }

        foreach (var work in role.Takes)
        {
            if (!choices.Any(choice => policy.Of(StaffKey.Of(choice))?.Takes is not { } takes || takes.Contains(work, StringComparer.Ordinal)))
            {
                return Refused($"{of} leaves `{work}` with no choice: no choice takes `{work}`, which `{id}` takes.");
            }
        }

        return null;
    }

    /// <summary>The works a choice may take: the role's, and design where it takes build, as a design step falls to build's role.</summary>
    private static HashSet<string> Works(RoleRecord role)
    {
        var works = new HashSet<string>(role.Takes, StringComparer.Ordinal);
        if (works.Contains(RoleWork.Build)) works.Add(RoleWork.Design);
        return works;
    }

    private static RoleRefusal Refused(string said) => new(Code, said);

    /// <summary>The record as <c>role_policy.policy</c> keeps it: compact, its rule and choices.</summary>
    public string Write() => StaffJson.Write(writer =>
    {
        writer.WriteString("rule", Rule);
        writer.WriteStartArray("choices");
        foreach (var choice in Choices)
        {
            writer.WriteStartObject();
            choice.Key.WriteTuple(writer);
            writer.WriteNumber("weight", choice.Weight);
            if (choice.Share is { } share) writer.WriteNumber("share", share);
            if (choice.Budget is { } budget)
            {
                writer.WriteStartObject("budget");
                if (budget.Starts is { } starts) writer.WriteNumber("starts", starts);
                if (budget.Tokens is { } tokens) writer.WriteNumber("tokens", tokens);
                writer.WriteEndObject();
            }

            if (choice.Takes is { } takes)
            {
                writer.WriteStartArray("takes");
                foreach (var work in takes) writer.WriteStringValue(work);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    /// <summary>
    /// The record <paramref name="text"/> holds, or the refusal saying why it is not one; fields it does not know are passed over, and every
    /// value is read as written for <see cref="Judge"/> to weigh.
    /// </summary>
    public static (StaffPolicy? Policy, RoleRefusal? Refused) Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, Refused("the policy is not an object."));
            if (Json.Text(root, "rule") is not { } rule) return (null, Refused("the policy names no rule: `order` or `weight`."));
            if (!root.TryGetProperty("choices", out var listed) || listed.ValueKind != JsonValueKind.Array) return (null, Refused("the policy lists no choices."));

            var choices = new List<StaffChoice>();
            foreach (var (item, n) in listed.EnumerateArray().Select((item, i) => (item, i + 1)))
            {
                if (Json.Text(item, "agent") is not { Length: > 0 } agent) return (null, Refused($"the policy's choice {n} names no agent."));
                var at = new StaffKey(agent, Json.Text(item, "model"), Json.Text(item, "effort"));
                if (!Whole(item, "weight", out var weight)) return (null, Refused($"the policy's choice {n}'s weight is not a whole number."));
                if (!Whole(item, "share", out var share)) return (null, Refused($"the policy's choice {n}'s share is not a whole number."));
                StaffBudget? budget = null;
                if (item.TryGetProperty("budget", out var day))
                {
                    if (day.ValueKind != JsonValueKind.Object || !Long(day, "starts", out var starts) || !Long(day, "tokens", out var tokens))
                    {
                        return (null, Refused($"the policy's choice {n}'s budget is not a whole number of starts or of tokens."));
                    }

                    budget = new StaffBudget(starts, tokens);
                }

                IReadOnlyList<string>? takes = null;
                if (item.TryGetProperty("takes", out var works))
                {
                    if (works.ValueKind != JsonValueKind.Array || works.EnumerateArray().Any(work => work.ValueKind != JsonValueKind.String))
                    {
                        return (null, Refused($"the policy's choice {n}'s takes is not a list of works."));
                    }

                    takes = [.. works.EnumerateArray().Select(work => work.GetString()!)];
                }

                choices.Add(new StaffChoice(at, weight ?? 1, share, budget, takes));
            }

            return (new StaffPolicy(rule, choices, new Dictionary<string, StaffMove>()), null);
        }
        catch (JsonException error)
        {
            return (null, Refused($"the policy does not read: {error.Message.TrimEnd('.', ' ')}."));
        }
    }

    /// <summary>Whether <paramref name="name"/> is absent or a whole number, read into <paramref name="value"/>.</summary>
    private static bool Whole(JsonElement element, string name, out int? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var field)) return true;
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt32(out var number)) return false;
        value = number;
        return true;
    }

    private static bool Long(JsonElement element, string name, out long? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var field)) return true;
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt64(out var number)) return false;
        value = number;
        return true;
    }
}

/// <summary>The one writer of a policy's JSON: compact, with the staffing's relaxed escaping.</summary>
internal static class StaffJson
{
    private static readonly JsonWriterOptions Compacted = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Write(Action<Utf8JsonWriter> fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compacted))
        {
            writer.WriteStartObject();
            fields(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>A moment as the policy's tables keep it: UTC, ISO 8601 round-trip, so two moments compare as text.</summary>
    public static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
