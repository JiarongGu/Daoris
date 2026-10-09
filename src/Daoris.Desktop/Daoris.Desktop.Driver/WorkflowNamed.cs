using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The shapes a step's field takes (the workflow design §3.2), as the shared table spells them.</summary>
public static class WorkflowFieldTypes
{
    public const string Names = "names";
    public const string Flag = "flag";
    public const string Choice = "choice";
    public const string Environment = "environment";
    public const string Pattern = "pattern";
    public const string PluginOrNone = "plugin-or-none";
    public const string Text = "text";
    public const string Plugin = "plugin";
    public const string Checks = "checks";
    public const string Id = "id";
    public const string Members = "members";
}

/// <summary>One field a kind takes, in the order a version reads it: its shape, whether it must be named, and what absence reads as.</summary>
/// <param name="Default">Text, a switch, or null: what an absent field reads as.</param>
public sealed record WorkflowKindField(string Name, string Type, IReadOnlyList<string>? Choices, bool Required, object? Default);

/// <summary>One kind of step: how much of it this build runs, the limit it is said with (a <see cref="WorkflowLimits"/> code), and its fields.</summary>
/// <param name="Runtime">One of <see cref="WorkflowRuntime"/>'s, or <see cref="WorkflowKindTable.None"/>: never offered.</param>
public sealed record WorkflowKindRow(string Kind, string Runtime, string? Limit, IReadOnlyList<WorkflowKindField> Fields);

/// <summary>
/// The kinds a named workflow may hold, as main runs them (WORKFLOW1d, D157 point 8, the workflow design §3.2, §3.9) — the CLI's
/// <c>WORKFLOW_KINDS</c>, cell for cell. A kind this build runs <see cref="None"/> of is never offered, and a version naming it
/// does not read here. The opinion is <c>partial</c> since XAGENT1f's gate reads its rule, with Current's limit for it.
/// </summary>
public static class WorkflowKindTable
{
    /// <summary>A kind this build does not run: designed, never offered.</summary>
    public const string None = "none";

    /// <summary>At most this many steps in a version, a group's members counted (design §2.5).</summary>
    public const int MaxSteps = 24;

    /// <summary>A workflow keeps its newest this many versions, and every version a kept run names (design §2.6).</summary>
    public const int KeptVersions = 20;

    private static readonly WorkflowKindField Failed = new("failed", WorkflowFieldTypes.Choice, ["wait", "send-back", "stop"], false, "wait");

    public static IReadOnlyList<WorkflowKindRow> Table { get; } =
    [
        new(WorkflowKinds.Work, WorkflowRuntime.Built, null, []),
        new(WorkflowKinds.Opinion, WorkflowRuntime.Partial, WorkflowLimits.OpinionPartial,
        [
            new("reviewers", WorkflowFieldTypes.Names, null, false, null),
            new("required", WorkflowFieldTypes.Flag, null, false, false),
            new("steps", WorkflowFieldTypes.Flag, null, false, false),
            new("recheck", WorkflowFieldTypes.Flag, null, false, true),
        ]),
        new(WorkflowKinds.Look, WorkflowRuntime.Partial, WorkflowLimits.LookPartial,
            [new("environment", WorkflowFieldTypes.Environment, null, false, null)]),
        new(WorkflowKinds.Landing, WorkflowRuntime.Built, null,
        [
            new("form", WorkflowFieldTypes.Choice, [LandingForm.Merge, LandingForm.Branch], true, null),
            new("accept", WorkflowFieldTypes.Choice, ["you", "automatic"], true, null),
            new("pattern", WorkflowFieldTypes.Pattern, null, false, null),
            new("plugin", WorkflowFieldTypes.PluginOrNone, null, false, null),
        ]),
        new(WorkflowKinds.PullRequest, WorkflowRuntime.Partial, WorkflowLimits.PullRequestAtCleanUp, []),
        new(WorkflowKinds.GoAhead, WorkflowRuntime.Partial, WorkflowLimits.GoAheadPartial,
        [
            new("act", WorkflowFieldTypes.Text, null, true, null),
            new("on", WorkflowFieldTypes.Text, null, true, null),
            new("refused", WorkflowFieldTypes.Choice, ["stop", "wait"], false, "stop"),
        ]),
        new(WorkflowKinds.Check, None, null,
        [
            new("plugin", WorkflowFieldTypes.Plugin, null, true, null),
            new("checks", WorkflowFieldTypes.Checks, null, false, "required"),
            Failed,
        ]),
        new(WorkflowKinds.Stage, None, null,
        [
            new("plugin", WorkflowFieldTypes.Plugin, null, true, null),
            new("stage", WorkflowFieldTypes.Id, null, true, null),
            new("start", WorkflowFieldTypes.Choice, ["you", "automatic"], false, "you"),
            Failed,
        ]),
        new(WorkflowKinds.All, None, null, [new("steps", WorkflowFieldTypes.Members, null, true, null), Failed]),
    ];

    /// <summary>A kind's row, or null for a kind nobody has.</summary>
    public static WorkflowKindRow? Of(string kind) => Table.FirstOrDefault(row => row.Kind == kind);
}

/// <summary>
/// One step as a version reads it: its id and kind, and every field its kind takes, in order, absence read as its default. A
/// value is text, null, a switch, a list of names, or a group's members.
/// </summary>
public sealed record NamedStep(string Id, string Kind, IReadOnlyList<WorkflowSetting> Fields)
{
    /// <summary>A field's value as the step reads it.</summary>
    public object? Field(string name) => Fields.FirstOrDefault(each => each.Name == name)?.Value;

    /// <summary>A group's members, or none.</summary>
    public IReadOnlyList<NamedStep> Members => Kind == WorkflowKinds.All && Field("steps") is IReadOnlyList<NamedStep> members ? members : [];
}

/// <summary>One version as read: its number, when and through which door it was saved, and its steps and digest, or its first problem.</summary>
public sealed record NamedVersion(int Version, string? At, string? Door, IReadOnlyList<NamedStep>? Steps, string? Problem, string? Digest);

/// <summary>A workflow file as read: its id and name and every version, each readable or not; or the file's own first problem.</summary>
public sealed record NamedWorkflowRead(string? Id, string? Name, IReadOnlyList<NamedVersion> Versions, string? Problem);

/// <summary>
/// Named workflows read (WORKFLOW1d, D157 points 5, 6 and 8, the workflow design §2.5, §2.6, §3.2, §3.3, §3.9): what a workflow
/// file and each of its versions may say, the first problem in what does not, and a version's digest. Choosing one is WORKFLOW1e's,
/// and every gate reads the version a run bound (WORKFLOW1f, <see cref="WorkflowProcesses"/>).
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>namedworkflows.ts</c>: both hold one table, this suite's tests' <c>fixtures/workflow-named.json</c>,
/// cell for cell, every sentence and every digest included (<c>WorkflowNamedTests</c> and <c>namedworkflows.test.ts</c>).
/// </remarks>
public static partial class WorkflowNamed
{
    private const string IdRule = "lower-case letters, digits and dashes, at most 40";
    private const string KindList = "`work`, `opinion`, `look`, `landing`, `pull-request`, `go-ahead`, `check`, `stage` or `all`";
    private const string FileShape = "a workflow is a JSON object: its `id`, its `name` and its `versions`.";
    private const string IdSentence = $"a workflow's `id` is {IdRule}, such as `docs-to-pr`.";
    private const string Current = "`current` names Current, the workflow drawn from the rules as they stand, so no saved workflow takes it.";
    private const string NameSentence = "a workflow's `name` is your words for it, 1 to 60 characters.";
    private const string VersionsSentence = "a workflow's `versions` is a list of at least one.";
    private const string VersionShape = "each of its versions is a JSON object with its `version`, `at`, `door` and `steps`.";
    private const string VersionNumber = "each version's `version` is a whole number from 1, each greater than the one before it.";

    /// <summary>A workflow's or a step's id: lower-case letters, digits and dashes, at most 40.</summary>
    public static bool IsId(string? value) => value is { Length: > 0 and <= 40 } && IdShape().IsMatch(value);

    /// <summary>What is wrong with a workflow's id, or null. <c>current</c> is Current's (design §2.5).</summary>
    public static string? IdProblem(string? value)
    {
        if (string.IsNullOrEmpty(value)) return IdSentence;
        if (!IsId(value)) return $"`{value}` is not a workflow's id — {IdRule}, such as `docs-to-pr`.";
        return value == "current" ? Current : null;
    }

    /// <summary>What is wrong with a workflow's name, or null: the person's words, 1 to 60 characters (content, D142).</summary>
    public static string? NameProblem(string? value) =>
        value is not null && value.Trim().Length > 0 && value.Length <= 60 ? null : NameSentence;

    /// <summary>
    /// A workflow file read whole (design §2.5, §2.6): the file's own problem first (its shape, id, name, and its versions'
    /// numbers, each greater than the one before), then each version on its own, so a version a newer build or a hand edit
    /// wrote is kept and unreadable here while the others read. A step's id an earlier readable version used for another kind
    /// makes the later version unreadable.
    /// </summary>
    public static NamedWorkflowRead Read(JsonElement file)
    {
        static NamedWorkflowRead Unread(string problem) => new(null, null, [], problem);
        if (file.ValueKind != JsonValueKind.Object) return Unread(FileShape);
        var id = TextOf(file, "id");
        if (IdProblem(id) is { } idProblem) return Unread(idProblem);
        var name = TextOf(file, "name");
        if (NameProblem(name) is { } nameProblem) return Unread(nameProblem);
        if (!file.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Array || versions.GetArrayLength() == 0)
        {
            return Unread(VersionsSentence);
        }

        var last = 0;
        foreach (var version in versions.EnumerateArray())
        {
            if (version.ValueKind != JsonValueKind.Object) return Unread(VersionShape);
            if (WholeOf(version, "version") is not { } number || number <= last) return Unread(VersionNumber);
            last = number;
        }

        var read = new List<NamedVersion>();
        foreach (var version in versions.EnumerateArray())
        {
            read.Add(ReadVersion(version, WholeOf(version, "version")!.Value, [.. read.Where(each => each.Steps is not null)]));
        }

        return new NamedWorkflowRead(id, name, read, null);
    }

    /// <summary>One version read (design §2.5): its <c>at</c>, its <c>door</c>, then its steps, the first problem said with its number.</summary>
    /// <param name="prior">The versions before it that read, which a step's id is held to: an id is never used for another kind.</param>
    public static NamedVersion ReadVersion(JsonElement version, int number, IReadOnlyList<NamedVersion> prior)
    {
        var at = TextOf(version, "at");
        var door = TextOf(version, "door");
        NamedVersion Unread(string problem) => new(number, at, door, null, problem, null);
        if (!IsMoment(at)) return Unread($"v{number}: its `at` is a moment as ISO 8601 writes it, such as `2026-10-09T09:12:00Z`.");
        if (door is null || !(door is "screen" or "terminal" || DoorShape().IsMatch(door)))
        {
            return Unread($"v{number}: its `door` is `screen`, `terminal` or `ask-daoris:<proposal>`.");
        }

        var (steps, problem) = ReadSteps(version.TryGetProperty("steps", out var raw) ? raw : null, number, prior);
        return steps is null ? Unread(problem!) : new NamedVersion(number, at, door, steps, null, Digest(steps));
    }

    /// <summary>
    /// A version's steps read (design §2.5, §3.2, §3.3, §3.9), the first problem said: the list and its bound, then each step's
    /// shape in order (a group's members inside it), then the order the runtime keeps, then whether this build runs each kind.
    /// </summary>
    public static (IReadOnlyList<NamedStep>? Steps, string? Problem) ReadSteps(JsonElement? value, int number, IReadOnlyList<NamedVersion>? prior = null)
    {
        var v = $"v{number}";
        if (value is not { ValueKind: JsonValueKind.Array } list || list.GetArrayLength() == 0) return (null, $"{v}: its `steps` is a list of at least one.");
        var count = list.GetArrayLength() + list.EnumerateArray().Sum(each =>
            each.ValueKind == JsonValueKind.Object && TextOf(each, "kind") == WorkflowKinds.All
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

        if (OrderProblem(steps, v) is { } order) return (null, order);
        foreach (var each in steps.SelectMany(step => new[] { step }.Concat(step.Members)))
        {
            if (WorkflowKindTable.Of(each.Kind)!.Runtime == WorkflowKindTable.None)
            {
                return (null, $"{v}, step `{each.Id}`: this build does not run a step of kind `{each.Kind}` yet.");
            }
        }

        return (steps, null);
    }

    /// <summary>One step's shape read (design §3.2): an object, its id once, its kind, a group's rule, its fields; a group's members inside it.</summary>
    private static (NamedStep? Step, string? Problem) ReadStep(
        JsonElement raw, int position, string? group, HashSet<string> seen, IReadOnlyList<NamedVersion> prior, string v)
    {
        var at = group is null ? $"{v}, step {position}: " : $"{v}, step {position} in `{group}`: ";
        if (raw.ValueKind != JsonValueKind.Object) return (null, $"{at}a step is a JSON object with its `id` and `kind`.");
        var id = TextOf(raw, "id");
        if (!IsId(id)) return (null, $"{at}its `id` is {IdRule}, such as `land`.");
        if (!seen.Add(id!)) return (null, $"{at}`{id}` is an earlier step's id too; each step's id is its own.");

        var p = group is null ? $"{v}, step `{id}`: " : $"{v}, step `{id}` in `{group}`: ";
        if (TextOf(raw, "kind") is not { } kind) return (null, $"{p}its `kind` is one of {KindList}.");
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
        if (kind == WorkflowKinds.Landing && (string?)step.Field("form") == LandingForm.Merge)
        {
            // A merge writes into the person's checkout (D145 point 1, D51 rule 6), and makes no branch for a plugin (D100).
            if ((string?)step.Field("accept") == "automatic")
            {
                return (null, $"{p}only a branch lands with no press — a merge writes into your checkout, and with no press nothing would "
                    + "stand between the work and the line.");
            }

            if (step.Field("pattern") is not null || step.Field("plugin") is not null)
            {
                return (null, $"{p}a merge makes no branch, so it names no `pattern` and no `plugin`.");
            }
        }

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

    /// <summary>A field's value read, or why not: a sentence after the step's prefix. Absent and JSON null are both absence.</summary>
    private static (object? Value, string? Problem) ReadField(WorkflowKindField field, JsonElement? raw)
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
                return text is { Length: > 0 and <= 32 } && IdShape().IsMatch(text) ? (text, null) : wrong;
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

    /// <summary>
    /// The text a version's digest is of, built by hand so both runtimes write the same bytes — the CLI's
    /// <c>versionCanonical</c>, byte for byte: <c>workflow</c>, then each step's line, its quoted id and its kind, each followed
    /// by every field its kind takes, a line each in order, absence written as its default; a group's members follow it, a step deeper.
    /// </summary>
    public static string Canonical(IReadOnlyList<NamedStep> steps)
    {
        var lines = new List<string> { "workflow" };
        void Write(NamedStep step, string indent)
        {
            lines.Add($"{indent}step {WorkflowCurrent.Quoted(step.Id)} {step.Kind}");
            foreach (var each in step.Fields)
            {
                if (each.Value is IReadOnlyList<NamedStep> members)
                {
                    lines.Add($"{indent}  {each.Name}:");
                    foreach (var member in members) Write(member, indent + "    ");
                }
                else
                {
                    lines.Add($"{indent}  {each.Name}={CanonicalValue(each.Value)}");
                }
            }
        }

        foreach (var step in steps) Write(step, "");
        return string.Join('\n', lines);
    }

    /// <summary>A version's digest: the first 12 hex characters of the SHA-256 of <see cref="Canonical"/>'s text.</summary>
    public static string Digest(IReadOnlyList<NamedStep> steps) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(steps))))[..12];

    private static string CanonicalValue(object? value) => value switch
    {
        null => "-",
        bool flag => flag ? "true" : "false",
        string text => WorkflowCurrent.Quoted(text),
        IEnumerable<string> list => $"[{string.Join(',', list.Select(WorkflowCurrent.Quoted))}]",
        _ => throw new ArgumentException($"a field is text, a switch or a list of names, not {value.GetType().Name}."),
    };

    /// <summary>Text where the property is text, else null.</summary>
    internal static string? TextOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A whole number from 1, as JSON writes one: a fraction, text or anything past the largest int is none.</summary>
    private static int? WholeOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
        && number >= 1 && number <= int.MaxValue && Math.Floor(number) == number
            ? (int)number
            : null;

    /// <summary>
    /// A moment as ISO 8601 writes it, and only so — the CLI's <c>isoMoment</c>, as <c>AccountCooling</c> reads one: a lenient
    /// parse reads <i>Oct 9</i> as this year's, and a day that does not exist is none.
    /// </summary>
    private static bool IsMoment(string? text) =>
        text is not null && DateTimeOffset.TryParseExact(
            text, IsoMoments, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _);

    private static readonly string[] IsoMoments =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdShape();

    [GeneratedRegex(@"^ask-daoris:[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex DoorShape();
}
