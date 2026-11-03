using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The kinds of step (the workflow design §3.2): a version may hold every one, and <see cref="WorkflowKindTable"/> says how much of each this
/// build runs (WORKFLOW1d).
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
/// CLI's <c>WORKFLOW_LIMITS</c>, word for word. The opinion's says what its gate runs since XAGENT1f: the landing waits for it,
/// while the review's drawing of it and a task's own choice are not built.
/// </summary>
public static class WorkflowLimits
{
    public const string OpinionPartial = "opinion-partial";
    public const string LookPartial = "look-partial";
    public const string PluginUnready = "plugin-unready";
    public const string PullRequestUnread = "pull-request-unread";
    public const string PullRequestAtCleanUp = "pull-request-at-clean-up";

    /// <summary>A named workflow's go-ahead step (WORKFLOW1d): Current never draws one, and a run raising one is WORKFLOW1l's.</summary>
    public const string GoAheadPartial = "go-ahead-partial";

    /// <summary>The <c>integrate</c> step Current draws where the rule carries <c>integrate</c> (INTEGRATE1r): nothing runs an integration until row g.</summary>
    public const string NotIntegratedYet = "not-integrated-yet";

    public static IReadOnlyList<(string Code, string Says)> Table { get; } =
    [
        // XAGENT1f gates every landing door on it; the screens are XAGENT1g's and the task level's choice XAGENT1h's.
        (OpinionPartial, "Partial: work here lands only once another agent's reading of it is settled, or you go on without one by "
            + "`daoris session opinion`; the review does not draw it yet, and no task chooses its own reviewer yet."),
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
        // INTEGRATE1r: the step is drawn from the rule; the integration run (row g) arms it.
        (NotIntegratedYet, "Not integrated yet: the rule carries `integrate`, but nothing runs an integration in this build, so work stays "
            + "on its branch as it always did."),
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

/// <summary>A run's steps as its view draws them: the plugins that may hold a start, the steps in order, and the version's digest.</summary>
public sealed record CurrentWorkflow(IReadOnlyList<string> StartHolds, IReadOnlyList<WorkflowStep> Steps, string Version);

/// <summary>The step vocabulary's helpers: the plugins a door reads, a step's text quoted for a digest, and a step as the page draws it.</summary>
public static class WorkflowSteps
{
    /// <summary>The plugins switched on and sound, in the catalogue's order, with their points.</summary>
    public static IReadOnlyList<WorkflowPlugin> PluginsOf(IWorkplacePluginCatalog catalog) =>
        [.. catalog.Contributing.Select(entry => new WorkflowPlugin(entry.Id, [.. entry.Points]))];

    /// <summary>The plugins that may hold a start: those speaking on <see cref="HookPoints.QuestConsider"/>.</summary>
    public static IReadOnlyList<string> StartHolds(IReadOnlyList<WorkflowPlugin> plugins) =>
        [.. plugins.Where(plugin => plugin.Points.Contains(HookPoints.QuestConsider, StringComparer.Ordinal)).Select(plugin => plugin.Id)];

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
