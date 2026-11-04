using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using static Daoris.Driver.WorkflowNamed;

namespace Daoris.Driver;

/// <summary>A version's steps read from JSON: each step's shape and fields, the groups' rules and the order they stand in.</summary>
internal static class NamedStepReading
{
    /// <summary>
    /// A version's steps read (design §2.5, §3.2, §3.3, §3.9), the first problem said: the list and its bound, then each step's
    /// shape in order (a group's members inside it), then the order the runtime keeps, then whether this build runs each kind.
    /// </summary>
    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) ReadSteps(JsonElement? value, int number, IReadOnlyList<NamedVersion>? prior = null)
    {
        var (steps, problem) = ReadShape(value, number, prior);
        if (steps is null) return (null, problem);
        foreach (var each in steps.SelectMany(step => new[] { step }.Concat(step.Members)))
        {
            if (WorkflowKindTable.Of(each.Kind)!.Runtime == WorkflowKindTable.None)
            {
                return (null, $"v{number}, step `{each.Id}`: this build does not run a step of kind `{each.Kind}` yet.");
            }
        }

        return (steps, null);
    }

    /// <summary>
    /// A version's steps read as <see cref="ReadSteps"/> reads them, all but whether this build runs each kind: Ask Daoris's judge
    /// (WORKFLOW1h-b, design §8.1) says a kind this build does not run as what the card needs first, where the editor refuses it.
    /// </summary>
    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) ReadShape(JsonElement? value, int number, IReadOnlyList<NamedVersion>? prior = null)
    {
        var v = $"v{number}";
        if (value is not { ValueKind: JsonValueKind.Array } list || list.GetArrayLength() == 0) return (null, $"{v}: its `steps` is a list of at least one.");
        var count = list.GetArrayLength() + list.EnumerateArray().Sum(each =>
            each.ValueKind == JsonValueKind.Object && Json.Text(each, "kind") == WorkflowKinds.All
            && each.TryGetProperty("steps", out var members) && members.ValueKind == JsonValueKind.Array
                ? members.GetArrayLength()
                : 0);
        if (count > WorkflowKindTable.MaxSteps)
        {
            return (null, $"{v}: it has {count} steps, counting each group's; a workflow holds at most {WorkflowKindTable.MaxSteps}.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var steps = new List<NamedStep>();
        var position = 0;
        foreach (var raw in list.EnumerateArray())
        {
            var (step, problem) = ReadStep(raw, ++position, null, seen, prior ?? [], v);
            if (problem is not null) return (null, problem);
            steps.Add(step!);
        }

        return OrderProblem(steps, v) is { } order ? (null, order) : (steps, null);
    }

    /// <summary>One step's shape read (design §3.2): an object, its id once, its kind, a group's rule, its fields; a group's members inside it.</summary>
    private static (NamedStep? Step, string? Problem) ReadStep(
        JsonElement raw, int position, string? group, HashSet<string> seen, IReadOnlyList<NamedVersion> prior, string v)
    {
        var at = group is null ? $"{v}, step {position}: " : $"{v}, step {position} in `{group}`: ";
        if (raw.ValueKind != JsonValueKind.Object) return (null, $"{at}a step is a JSON object with its `id` and `kind`.");
        var id = Json.Text(raw, "id");
        if (!IsId(id)) return (null, $"{at}its `id` is {IdRule}, such as `land`.");
        if (!seen.Add(id!)) return (null, $"{at}`{id}` is an earlier step's id too; each step's id is its own.");

        var p = group is null ? $"{v}, step `{id}`: " : $"{v}, step `{id}` in `{group}`: ";
        if (Json.Text(raw, "kind") is not { } kind) return (null, $"{p}its `kind` is one of {KindList}.");
        if (WorkflowKindTable.Of(kind) is not { } row) return (null, $"{p}`{kind}` is no kind of step — one of {KindList}.");
        if (group is not null && kind == WorkflowKinds.All) return (null, $"{p}a group holds no group: one level of grouping.");
        if (group is not null && kind is not (WorkflowKinds.Check or WorkflowKinds.Stage))
        {
            return (null, $"{p}a group holds checks and stages, side by side; `{kind}` is neither.");
        }

        foreach (var earlier in prior)
        {
            var was = earlier.Steps!.Concat(earlier.Steps!.SelectMany(step => step.Members)).FirstOrDefault(step => step.Id == id);
            if (was is not null && was.Kind != kind)
            {
                return (null, $"{p}`{id}` was a step of kind `{was.Kind}` in v{earlier.Version}; an id is never used for another kind.");
            }
        }

        foreach (var property in raw.EnumerateObject())
        {
            if (property.Name is not ("id" or "kind") && row.Fields.All(each => each.Name != property.Name))
            {
                return (null, $"{p}a step of kind `{kind}` takes no `{property.Name}` — it takes {FieldList(row.Fields)}.");
            }
        }

        var fields = new List<WorkflowSetting>();
        foreach (var each in row.Fields)
        {
            var (value, problem) = ReadField(each, raw.TryGetProperty(each.Name, out var named) ? named : null);
            if (problem is not null) return (null, p + problem);
            fields.Add(new WorkflowSetting(each.Name, value));
        }

        var step = new NamedStep(id!, kind, fields);
        if (MergeProblem(step) is { } merge) return (null, p + merge);

        if (kind == WorkflowKinds.All)
        {
            var members = new List<NamedStep>();
            var index = 0;
            foreach (var member in raw.GetProperty("steps").EnumerateArray())
            {
                var (read, problem) = ReadStep(member, ++index, id, seen, prior, v);
                if (problem is not null) return (null, problem);
                members.Add(read!);
            }

            step = step with { Fields = [.. fields.Select(each => each.Name == "steps" ? each with { Value = members } : each)] };
        }

        return (step, null);
    }

    /// <summary>
    /// Why a landing that merges cannot be read, or null: a merge writes into the person's checkout (D145 point 1, D51 rule 6), so it never
    /// lands with no press, and makes no branch for a plugin (D100), so it names no pattern and no plugin. A sentence after the step's prefix.
    /// </summary>
    private static string? MergeProblem(NamedStep step)
    {
        if (step.Kind != WorkflowKinds.Landing || (string?)step.Field("form") != LandingForm.Merge) return null;
        if ((string?)step.Field("accept") == "automatic")
        {
            return "only a branch lands with no press — a merge writes into your checkout, and with no press nothing would stand between the "
                + "work and the line.";
        }

        return step.Field("pattern") is not null || step.Field("plugin") is not null
            ? "a merge makes no branch, so it names no `pattern` and no `plugin`."
            : null;
    }

    /// <summary>
    /// A graph version's gate nodes read back as the steps <see cref="WorkflowProcess.Of"/> reads (FABRIC-CUTOVER1d; D190 point 4, the cutover
    /// design §4): its <c>claimed</c> as the work, its <c>opinion</c>, its <c>look</c> and its <c>handed</c> as the landing, in the graph's order,
    /// each option read as the step of its kind reads that field, absence as its default. A landing's <c>form</c> and <c>accept</c> the node
    /// leaves out are the repository's rule's (<paramref name="landing"/>), a merge's press always yours; an option the step's kind does not take
    /// is the node's own and not read here. The first option that does not read is said with the node's id, and nothing is read in its place.
    /// </summary>
    /// <param name="landing">The landing rule a <c>handed</c> that leaves its form or its press out lands by: the declared rule, composed.</param>
    /// <param name="bound">The open options a <c>land</c> node's run resolved at its bind (<see cref="WorkflowRunBinding.Land"/>), which stand for the rule's.</param>
    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) GateSteps(
        NodeGraph graph, int number, LandingRule landing, IReadOnlyDictionary<string, string>? bound = null)
    {
        var steps = new List<NamedStep>();
        foreach (var node in graph.Nodes)
        {
            var kind = node.Kind switch
            {
                NodeKinds.Claimed => WorkflowKinds.Work,
                NodeKinds.Opinion => WorkflowKinds.Opinion,
                NodeKinds.Look => WorkflowKinds.Look,
                // WORKFLOWCLEAN1 1c: a `land` is no step here; the process carries its bound intent (LandNodes.RuleOf), not a rule read back.
                _ => null,
            };
            if (kind is null) continue;

            var p = $"v{number}, node `{node.Id}`: ";
            var fields = new List<WorkflowSetting>();
            var options = node.Options;
            foreach (var field in WorkflowKindTable.Of(kind)!.Fields)
            {
                var text = options.TryGetValue(field.Name, out var written) ? written
                    : kind != WorkflowKinds.Landing ? null
                    : field.Name switch
                    {
                        "form" => JsonSerializer.Serialize(landing.Form),
                        // The rule's switch is its branch's: a merge it leaves to the rule is pressed (D145 point 1).
                        "accept" => JsonSerializer.Serialize(
                            landing.AutoAccept && (string?)fields.FirstOrDefault(each => each.Name == "form")?.Value == LandingForm.Branch ? "automatic" : "you"),
                        _ => null,
                    };
                JsonElement? raw = null;
                if (text is not null)
                {
                    try
                    {
                        using var document = JsonDocument.Parse(text);
                        raw = document.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        return (null, $"{p}its `{field.Name}` is not a JSON value.");
                    }
                }

                var (value, problem) = ReadField(field, raw);
                if (problem is not null) return (null, p + problem);
                fields.Add(new WorkflowSetting(field.Name, value));
            }

            var step = new NamedStep(node.Id, kind, fields);
            // D145's note of 2026-11-01: a `land` merges with no press where its graph holds no review, the workflow's
            // choice, and its own options are LandNodes.Problem's; a `handed` keeps the steps' refusal.
            if (node.Kind != NodeKinds.Land && MergeProblem(step) is { } merge) return (null, p + merge);
            steps.Add(step);
        }

        return (steps, null);
    }

    /// <summary>A field's value read, or why not: a sentence after the step's prefix. Absent and JSON null are both absence.</summary>
    internal static (object? Value, string? Problem) ReadField(WorkflowKindField field, JsonElement? raw)
    {
        (object?, string?) wrong = (null, $"its `{field.Name}` is {What(field)}.");
        if (raw is not { } value || value.ValueKind == JsonValueKind.Null) return field.Required ? wrong : (field.Default, null);
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        switch (field.Type)
        {
            case WorkflowFieldTypes.Names:
                return Names(value) is { } names ? (names, null) : wrong;
            case WorkflowFieldTypes.Checks:
                if (text == "required") return (text, null);
                return Names(value) is { } checks ? (checks, null) : wrong;
            case WorkflowFieldTypes.Flag:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? (value.GetBoolean(), null) : wrong;
            case WorkflowFieldTypes.Choice:
                return text is not null && field.Choices!.Contains(text, StringComparer.Ordinal) ? (text, null) : wrong;
            case WorkflowFieldTypes.Environment:
                return text is { Length: > 0 and <= 32 } && SafeNames.IsKebabId(text) ? (text, null) : wrong;
            case WorkflowFieldTypes.Id:
                return IsId(text) ? (text, null) : wrong;
            case WorkflowFieldTypes.Text:
                return text is not null && text.Trim().Length > 0 && text.Length <= 300 ? (text, null) : wrong;
            case WorkflowFieldTypes.Plugin:
                return text is not null && PluginCatalog.IsId(text) ? (text, null) : wrong;
            case WorkflowFieldTypes.PluginOrNone:
                return text is not null && (text == "none" || PluginCatalog.IsId(text)) ? (text, null) : wrong;
            case WorkflowFieldTypes.Pattern:
                if (text is null) return wrong;
                // The landing twin's own check, asked of its one owner, so a step and a rule refuse one pattern in the same words.
                return LandingRules.Problem(text) is { } problem ? (null, $"its `pattern` will not do: {problem}") : (text, null);
            case WorkflowFieldTypes.Members:
                return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 ? (new List<NamedStep>(), null) : wrong;
            default:
                throw new InvalidOperationException($"`{field.Type}` is no field type.");
        }
    }

    /// <summary>Names as a list reads them: at least one, each text that is not blank, kept without the spaces around it, none twice in any case.</summary>
    private static List<string>? Names(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0) return null;
        var kept = new List<string>();
        foreach (var each in value.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.String || each.GetString()!.Trim().Length == 0) return null;
            var name = each.GetString()!.Trim();
            if (kept.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
            kept.Add(name);
        }

        return kept;
    }

    /// <summary>What a field's value is, as its refusal says it.</summary>
    private static string What(WorkflowKindField field) => field.Type switch
    {
        WorkflowFieldTypes.Names => "a list of names, at least one, none twice in any case",
        WorkflowFieldTypes.Flag => "true or false",
        WorkflowFieldTypes.Choice => Either(field.Choices!),
        WorkflowFieldTypes.Environment => "an environment's name: lower-case letters, digits and dashes, at most 32",
        WorkflowFieldTypes.Pattern => "a branch pattern, such as `work/{quest}-{slug}`",
        WorkflowFieldTypes.PluginOrNone => "`none` or a plugin's id: lowercase letters, digits, dots and dashes",
        WorkflowFieldTypes.Text => "your words, 1 to 300 characters",
        WorkflowFieldTypes.Plugin => "a plugin's id: lowercase letters, digits, dots and dashes",
        WorkflowFieldTypes.Checks => "`required` or a list of check names, at least one, none twice in any case",
        WorkflowFieldTypes.Id => IdRule,
        WorkflowFieldTypes.Members => "a list of the checks and stages it runs side by side, at least one",
        _ => throw new InvalidOperationException($"`{field.Type}` is no field type."),
    };

    /// <summary><c>a</c>, <c>a or b</c>, <c>a, b or c</c>: a field's choices, as its sentence names them.</summary>
    private static string Either(IReadOnlyList<string> choices)
    {
        var named = choices.Select(choice => $"`{choice}`").ToList();
        return named.Count == 1 ? named[0] : $"{string.Join(", ", named.Take(named.Count - 1))} or {named[^1]}";
    }

    /// <summary>The fields a kind takes, as its refusal names them: none, <c>a</c>, <c>a and b</c>, <c>a, b and c</c>.</summary>
    private static string FieldList(IReadOnlyList<WorkflowKindField> fields)
    {
        var named = fields.Select(each => $"`{each.Name}`").ToList();
        if (named.Count == 0) return "none";
        return named.Count == 1 ? named[0] : $"{string.Join(", ", named.Take(named.Count - 1))} and {named[^1]}";
    }

    /// <summary>
    /// The order the runtime keeps (design §2.2, §2.5, §3.3, §3.6): the work first and once; at most one opinion and one look
    /// before the landing, the opinion first, and a go-ahead before them; one landing; after it, the pull request straight after
    /// a landing on a branch whose plugin opens one, then checks, stages, groups and go-aheads, none straight before a stage the
    /// person starts.
    /// </summary>
    private static string? OrderProblem(IReadOnlyList<NamedStep> steps, string v)
    {
        NamedStep? landing = null, opinion = null, look = null, pullRequest = null;
        var landingAt = -1;
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var p = $"{v}, step `{step.Id}`: ";
            if (index == 0 && step.Kind != WorkflowKinds.Work) return $"{v}: the first step is the `work`: the agent's work comes first, once.";
            switch (step.Kind)
            {
                case WorkflowKinds.Work:
                    if (index > 0) return $"{p}there is one `work`, the first step.";
                    break;
                case WorkflowKinds.Landing:
                    if (landing is not null) return $"{p}there is one `landing` already, `{landing.Id}`.";
                    landing = step;
                    landingAt = index;
                    break;
                case WorkflowKinds.Opinion:
                    if (landing is not null) return $"{p}a step of kind `opinion` comes before the landing, in the order the runtime keeps.";
                    if (opinion is not null) return $"{p}there is one `opinion` already, `{opinion.Id}`.";
                    if (look is not null) return $"{p}the second opinion is read before your look, so the `opinion` comes first.";
                    opinion = step;
                    break;
                case WorkflowKinds.Look:
                    if (landing is not null) return $"{p}a step of kind `look` comes before the landing, in the order the runtime keeps.";
                    if (look is not null) return $"{p}there is one `look` already, `{look.Id}`.";
                    look = step;
                    break;
                case WorkflowKinds.GoAhead:
                    if (landing is null && (opinion is not null || look is not null))
                    {
                        return $"{p}a go-ahead before the landing sits with the work, before the second opinion and your look.";
                    }

                    if (landing is not null && index + 1 < steps.Count && steps[index + 1] is { Kind: WorkflowKinds.Stage } next
                        && (string?)next.Field("start") == "you")
                    {
                        return $"{p}a go-ahead straight before a stage you start is not needed: starting it is your go-ahead.";
                    }

                    break;
                case WorkflowKinds.PullRequest:
                    if (landing is null) return $"{p}a step of kind `pull-request` comes after the landing.";
                    if (pullRequest is not null) return $"{p}there is one `pull-request` already, `{pullRequest.Id}`.";
                    if (index - 1 != landingAt) return $"{p}the pull request comes straight after the landing.";
                    if ((string?)landing.Field("form") == LandingForm.Merge)
                    {
                        return $"{p}a pull request needs a landing on a branch whose plugin opens one; `{landing.Id}` merges.";
                    }

                    if ((string?)landing.Field("plugin") == "none")
                    {
                        return $"{p}a pull request needs a landing on a branch whose plugin opens one; `{landing.Id}` names no plugin.";
                    }

                    pullRequest = step;
                    break;
                default:
                    if (landing is null) return $"{p}a step of kind `{step.Kind}` comes after the landing.";
                    break;
            }
        }

        return landing is null ? $"{v}: it has no `landing`; work lands once, after the gates before it." : null;
    }
}
