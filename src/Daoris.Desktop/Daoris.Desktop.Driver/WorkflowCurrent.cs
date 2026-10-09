using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The kinds of step (the workflow design §3.2). Current draws the first five, each at most once, so its step's id is its kind;
/// a named workflow may hold every one, and <see cref="WorkflowKindTable"/> says how much of each this build runs (WORKFLOW1d).
/// </summary>
public static class WorkflowKinds
{
    public const string Work = "work";
    public const string Opinion = "opinion";
    public const string Look = "look";
    public const string Landing = "landing";
    public const string PullRequest = "pull-request";
    public const string GoAhead = "go-ahead";
    public const string Check = "check";
    public const string Stage = "stage";
    public const string All = "all";
}

/// <summary>The person's part in a step (design §3.1), said apart from who acts in it.</summary>
public static class WorkflowParticipation
{
    /// <summary><i>Agent alone</i>: an agent's session does it, and the person is told.</summary>
    public const string Agent = "agent";

    /// <summary><i>Agent + you</i>: an agent does the work, and one named press of the person's lets it go.</summary>
    public const string AgentAndYou = "agent-and-you";

    /// <summary><i>You</i>: the person's press is the step.</summary>
    public const string You = "you";

    /// <summary><i>Automatic</i>: Daoris, or a plugin speaking for a service, acts with no press.</summary>
    public const string Automatic = "automatic";
}

/// <summary>Who acts in a step (design §3.1): an agent, Daoris itself, or a plugin speaking for a service.</summary>
public static class WorkflowExecutor
{
    public const string Agent = "agent";
    public const string Daoris = "daoris";
    public const string Plugin = "plugin";
}

/// <summary>The person's one named press on a step, where it has one.</summary>
public static class WorkflowPress
{
    public const string Reviewed = "reviewed";
    public const string Accept = "accept";
    public const string Merge = "merge";
}

/// <summary>How much of a kind this build runs (design §3.2, §3.9): whole, in part, or declared with nothing acting on it.</summary>
public static class WorkflowRuntime
{
    public const string Built = "built";
    public const string Partial = "partial";
    public const string Declared = "declared";
}

/// <summary>The rule a step was read from, for its source.</summary>
public static class WorkflowRule
{
    public const string Landing = "landing";
    public const string Review = "review";
    public const string Opinion = "opinion";
}

/// <summary>
/// The limits a step may carry (design §3.9), each a code and the sentence it is said in, in the shared table's order — the
/// CLI's <c>WORKFLOW_LIMITS</c>, word for word. The opinion's is its rule's own <see cref="OpinionRules.DeclaredOnly"/>, until
/// XAGENT1f's gate reads the rule.
/// </summary>
public static class WorkflowLimits
{
    public const string OpinionDeclared = "opinion-declared";
    public const string LookPartial = "look-partial";
    public const string PluginUnready = "plugin-unready";
    public const string PullRequestUnread = "pull-request-unread";
    public const string PullRequestAtCleanUp = "pull-request-at-clean-up";

    /// <summary>A named workflow's go-ahead step (WORKFLOW1d): Current never draws one, and a run raising one is WORKFLOW1l's.</summary>
    public const string GoAheadPartial = "go-ahead-partial";

    public static IReadOnlyList<(string Code, string Says)> Table { get; } =
    [
        (OpinionDeclared, OpinionRules.DeclaredOnly),
        // REVIEWENV1d shows a posted set-up's build from its session's end; the intake's step (1f) and a run of its own (1e) are not built.
        (LookPartial, "Partial: work here lands only once you say it is reviewed or skip the review, and a set-up step's build is "
            + "shown in Daoris's browser from its session's end until then; no set-up step is composed for you yet, and nothing runs a "
            + "process of its own for one."),
        (PluginUnready, "Its plugin cannot land work on this machine now: it is not installed, is switched off, contributes "
            + "nothing, or speaks on no `work/land` point. `daoris plugin list` says which."),
        (PullRequestUnread, "Its plugin answers no `work/state`, so Daoris never reads whether the pull request was merged or "
            + "abandoned."),
        (PullRequestAtCleanUp, "Its state is asked of its plugin only when branches are cleaned up, never while someone waits on it."),
        (GoAheadPartial, "Partial: a session asks you for a go-ahead as it needs one; a step that raises one at its place and waits "
            + "on your answer is not built yet."),
    ];

    /// <summary>The sentence a limit is said in.</summary>
    public static string Says(string code) => Table.First(limit => limit.Code == code).Says;
}

/// <summary>One plugin switched on and sound, as the derivation reads it: its id and the points it speaks on.</summary>
public sealed record WorkflowPlugin(string Id, IReadOnlyList<string> Points);

/// <summary>Where a step was read from: the rule (<see cref="WorkflowRule"/>), or none for the work, and the level it stood at.</summary>
/// <param name="Level">One of <see cref="LandingSource"/>'s: <c>repository</c>, <c>workspace</c> or <c>default</c>.</param>
public sealed record WorkflowSource(string? Rule, string Level);

/// <summary>One of a step's settings: a name and its value, text or none, a switch, or a list of names.</summary>
public sealed record WorkflowSetting(string Name, object? Value);

/// <summary>
/// One step of a workflow (design §3.1, §3.2): what it is, the person's part and who acts, the person's press, where it was read,
/// what it is set to, how much of it this build runs, and its limit, a code of <see cref="WorkflowLimits"/> or null.
/// </summary>
public sealed record WorkflowStep(
    string Id, string Kind, string Participation, string Executor, string? Press, WorkflowSource Source,
    IReadOnlyList<WorkflowSetting> Settings, string Runtime, string? Limit);

/// <summary>A repository's, or a workspace's, Current: the plugins that may hold a start, the steps in order, and their digest.</summary>
public sealed record CurrentWorkflow(IReadOnlyList<string> StartHolds, IReadOnlyList<WorkflowStep> Steps, string Version);

/// <summary>
/// A repository's Current workflow (WORKFLOW1a, D157 point 7, the workflow design §2.7, §3.2): how its work moves, drawn from the
/// rules as they stand, each step with the rule it was read from and the limit the runtime has. A view over the keys, never a
/// migration: nothing here writes, and every gate goes on reading the rules as it did.
/// </summary>
/// <remarks>
/// <para>A TWIN with the CLI's <c>workflows.ts</c>: both hold one table, this suite's tests' <c>fixtures/workflow-current.json</c>,
/// cell for cell, the version included (<c>WorkflowCurrentTests</c> and <c>workflows.test.ts</c>).</para>
///
/// <para>It reads through the resolvers every gate reads: <see cref="LandingRules.Choose"/> (the repository's name as given,
/// untrimmed), <see cref="ReviewRules.Resolve"/>, <see cref="OpinionRules.Resolve"/> and <see cref="DriverConfig.StandingFor"/>
/// (trimmed). Pure: the plugins are handed in.</para>
/// </remarks>
public static class WorkflowCurrent
{
    /// <summary>
    /// A repository's Current (design §2.7): the plugins that may hold a start; the work, with its standing answer; the second
    /// opinion where a rule stands that is not <c>false</c>, with its own occasions; the look where the review rule is required,
    /// in its default environment; the landing as its rule says; and the pull request where a branch rule names a plugin. A null
    /// <paramref name="repository"/> draws the workspace's Current, which no repository's rule and no standing answer reach.
    /// </summary>
    /// <param name="plugins">The plugins switched on and sound, in catalogue order (<see cref="PluginsOf"/>).</param>
    public static CurrentWorkflow Derive(DriverConfig config, string? repository, string? workspace, IReadOnlyList<WorkflowPlugin> plugins)
    {
        // The workspace's Current reads the same resolvers with nothing set for any repository.
        var read = repository is null
            ? config with
            {
                Landings = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase),
                Reviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase),
                Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase),
                Standing = new Dictionary<string, StandingAnswer>(StringComparer.OrdinalIgnoreCase),
            }
            : config;
        var named = repository ?? "";

        var startHolds = plugins
            .Where(plugin => plugin.Points.Contains(HookPoints.QuestConsider, StringComparer.Ordinal))
            .Select(plugin => plugin.Id)
            .ToList();
        var steps = new List<WorkflowStep>
        {
            new(WorkflowKinds.Work, WorkflowKinds.Work, WorkflowParticipation.Agent, WorkflowExecutor.Agent, null,
                new WorkflowSource(null, LandingSource.Default), [new("standing", read.StandingFor(named)?.Says)],
                WorkflowRuntime.Built, null),
        };

        if (OpinionRules.Resolve(read, named, workspace) is { Rule.IsNone: false } opinion)
        {
            steps.Add(new(WorkflowKinds.Opinion, WorkflowKinds.Opinion, WorkflowParticipation.Agent, WorkflowExecutor.Agent, null,
                new WorkflowSource(WorkflowRule.Opinion, opinion.Source),
                [
                    new("reviewers", opinion.Rule.Reviewers.ToList()),
                    new("on", opinion.Rule.On.ToList()),
                    new("required", opinion.Rule.Required),
                    new("recheck", opinion.Rule.Recheck),
                ],
                WorkflowRuntime.Declared, WorkflowLimits.OpinionDeclared));
        }

        if (ReviewRules.Resolve(read, named, workspace) is { Rule: { IsNone: false, Required: true } } review)
        {
            steps.Add(new(WorkflowKinds.Look, WorkflowKinds.Look, WorkflowParticipation.AgentAndYou, WorkflowExecutor.Agent,
                WorkflowPress.Reviewed, new WorkflowSource(WorkflowRule.Review, review.Source),
                [new("environment", review.Rule.Environments[0].Name)],
                WorkflowRuntime.Partial, WorkflowLimits.LookPartial));
        }

        var landing = LandingRules.Choose(read, named, workspace);
        var rule = landing.Rule;
        var plugin = rule.Form == LandingForm.Branch ? rule.Plugin : null;
        // The plugin a rule names, as PluginProblem finds it: the first of that id in any case.
        var speaker = plugin is null ? null : plugins.FirstOrDefault(each => string.Equals(each.Id, plugin, StringComparison.OrdinalIgnoreCase));
        var lands = speaker is not null && speaker.Points.Contains(HookPoints.Land, StringComparer.Ordinal);
        var automatic = rule.Form == LandingForm.Branch && rule.AutoAccept;
        steps.Add(new(WorkflowKinds.Landing, WorkflowKinds.Landing,
            automatic ? WorkflowParticipation.Automatic : WorkflowParticipation.You, WorkflowExecutor.Daoris,
            automatic ? null : WorkflowPress.Accept, new WorkflowSource(WorkflowRule.Landing, landing.Source),
            [
                new("form", rule.Form),
                new("accept", automatic ? "automatic" : "you"),
                new("pattern", rule.Form == LandingForm.Branch ? rule.Pattern : null),
                new("plugin", plugin),
                new("tidy", rule.Tidy),
            ],
            WorkflowRuntime.Built, plugin is not null && !lands ? WorkflowLimits.PluginUnready : null));

        if (plugin is not null)
        {
            steps.Add(new(WorkflowKinds.PullRequest, WorkflowKinds.PullRequest, WorkflowParticipation.You, WorkflowExecutor.Plugin,
                WorkflowPress.Merge, new WorkflowSource(WorkflowRule.Landing, landing.Source), [new("plugin", plugin)],
                WorkflowRuntime.Partial,
                !lands ? WorkflowLimits.PluginUnready
                    : speaker!.Points.Contains(HookPoints.State, StringComparer.Ordinal) ? WorkflowLimits.PullRequestAtCleanUp
                    : WorkflowLimits.PullRequestUnread));
        }

        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(startHolds, steps))))[..12];
        return new CurrentWorkflow(startHolds, steps, version);
    }

    /// <summary>The plugins a door hands <see cref="Derive"/>: those switched on and sound, in the catalogue's order, with their points.</summary>
    public static IReadOnlyList<WorkflowPlugin> PluginsOf(PluginCatalog catalog) =>
        [.. catalog.Contributing.Select(entry => new WorkflowPlugin(entry.Manifest.Id, [.. entry.Manifest.Hooks?.Points ?? []]))];

    /// <summary>The text a workflow's version is the digest of — the CLI's <c>workflowCanonical</c>, byte for byte.</summary>
    public static string Canonical(CurrentWorkflow workflow) => Canonical(workflow.StartHolds, workflow.Steps);

    /// <summary>
    /// The text built by hand so both runtimes write the same bytes: <c>current</c>; <c>holds</c> and the plugins that may hold a
    /// start; then each step's line, its id, kind, participation, executor, press, rule, level, runtime and limit, each followed
    /// by its settings, a line each in order. Text is quoted, with <c>\</c>, <c>"</c> and each control character escaped; none
    /// is <c>-</c>; a list is its quoted items between brackets.
    /// </summary>
    private static string Canonical(IReadOnlyList<string> startHolds, IReadOnlyList<WorkflowStep> steps)
    {
        var lines = new List<string> { "current", $"holds {Value(startHolds)}" };
        foreach (var step in steps)
        {
            lines.Add(string.Join(' ', new object?[]
            {
                step.Kind, step.Participation, step.Executor, step.Press, step.Source.Rule, step.Source.Level, step.Runtime, step.Limit,
            }.Select(Value).Prepend("step " + Value(step.Id))));
            lines.AddRange(step.Settings.Select(setting => $"  {setting.Name}={Value(setting.Value)}"));
        }

        return string.Join('\n', lines);
    }

    private static string Value(object? value) => value switch
    {
        null => "-",
        bool flag => flag ? "true" : "false",
        string text => Quoted(text),
        IEnumerable<string> list => $"[{string.Join(',', list.Select(Quoted))}]",
        _ => throw new ArgumentException($"a setting is text, a switch or a list of names, not {value.GetType().Name}."),
    };

    /// <summary>Text quoted for a digest's text, by hand: <c>\</c>, <c>"</c> and each control character escaped (WORKFLOW1d's digest too).</summary>
    internal static string Quoted(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
        {
            quoted.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                < ' ' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }

    /// <summary>A step as compact JSON, its settings in order: for the shared table's cells, and for the page that draws it.</summary>
    public static string ToJson(WorkflowStep step)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("id", step.Id);
            writer.WriteString("kind", step.Kind);
            writer.WriteString("participation", step.Participation);
            writer.WriteString("executor", step.Executor);
            Text(writer, "press", step.Press);
            writer.WriteStartObject("source");
            Text(writer, "rule", step.Source.Rule);
            writer.WriteString("level", step.Source.Level);
            writer.WriteEndObject();
            writer.WriteStartObject("settings");
            foreach (var setting in step.Settings)
            {
                writer.WritePropertyName(setting.Name);
                switch (setting.Value)
                {
                    case null: writer.WriteNullValue(); break;
                    case bool flag: writer.WriteBooleanValue(flag); break;
                    case string text: writer.WriteStringValue(text); break;
                    case IEnumerable<string> list:
                        writer.WriteStartArray();
                        foreach (var each in list) writer.WriteStringValue(each);
                        writer.WriteEndArray();
                        break;
                }
            }

            writer.WriteEndObject();
            writer.WriteString("runtime", step.Runtime);
            Text(writer, "limit", step.Limit);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Text(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }
}
