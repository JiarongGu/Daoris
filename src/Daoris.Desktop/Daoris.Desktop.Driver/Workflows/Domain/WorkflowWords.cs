using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// What <c>daoris driver workflow</c> says (PUSH1f2b, D194 point 6; D157, the workflow design §4.7): a Current drawn in words, a named
/// step as <c>show</c> draws it, which level chose a workflow and the landed route, the door a version was saved through, a graph
/// version, and Current copied into a version's steps. The words the CLI's <c>workflowdoor.ts</c> and <c>workflows.ts</c> said, kept
/// by <c>fixtures/driver-workflows-text.json</c>; the command (<see cref="DriverWorkflowsCommand"/>) says them on both paths.
/// </summary>
public static class WorkflowWords
{
    /// <summary>Said by every verb that names a named workflow (WORKFLOW1e): a choice names one, and every gate reads the version bound.</summary>
    public const string WhatChooses = "  A choice names a workflow (`daoris driver workflow use <id> --repository <name>|--workspace <name> "
        + "[--kind <kind>]`), and work binds its newest version at its first start; every gate then reads that version.";

    /// <summary>Said by every verb that edits the choice (WORKFLOW1e): what the gate reads of it (WORKFLOW1f).</summary>
    public const string GateReads = "  Every gate reads the version a run bound at its first start: how it lands, your look and its second "
        + "opinion follow that version.";

    /// <summary>Said by <c>use … --landed</c> (FABRIC-PIPELINE1a): what reads the landed route.</summary>
    public const string LandedReads = "  A verification binds the newest version of its pipeline when it starts, and keeps it; this build keeps the "
        + "route and does not run its checks yet.";

    /// <summary>
    /// What <c>workflow show</c> says of a Current (design §4.7): whose it is and its version, the plugins that may hold a start, then
    /// each step with its source and its limit.
    /// </summary>
    /// <param name="whose"><c>`web-app`</c> or <c>the workspace `work`</c>, as the header names it.</param>
    /// <param name="where">What the header adds of where it was read, such as the workspace a repository is in.</param>
    public static IReadOnlyList<string> CurrentSaid(CurrentWorkflow workflow, string whose, string where)
    {
        var lines = new List<string>
        {
            $"daoris: {whose} follows Current: {where}, read from the rules as they stand at each gate (version {workflow.Version}).",
            workflow.StartHolds.Count == 0
                ? "  No plugin here may hold a start."
                : $"  Plugins that may hold a start: {string.Join(", ", workflow.StartHolds.Select(id => $"`{id}`"))}.",
        };
        foreach (var step in workflow.Steps) lines.AddRange(CurrentStepSaid(step));
        return lines;
    }

    /// <summary>A Current's step as the terminal draws it: its title, who takes part and who acts, the person's press, what it is set to and its source.</summary>
    private static List<string> CurrentStepSaid(WorkflowStep step)
    {
        object? Set(string name) => step.Settings.FirstOrDefault(setting => setting.Name == name)?.Value;
        bool Has(string name) => step.Settings.Any(setting => setting.Name == name);
        var participation = step.Participation switch
        {
            WorkflowParticipation.Agent => "Agent alone",
            WorkflowParticipation.AgentAndYou => "Agent + you",
            WorkflowParticipation.You => "You",
            _ => "Automatic",
        };
        var executor = step.Executor == WorkflowExecutor.Plugin ? $"`{Set("plugin")}`" : step.Executor == WorkflowExecutor.Daoris ? "Daoris" : "an agent";
        var press = step.Press switch
        {
            null => "",
            WorkflowPress.Reviewed => " · your press: Reviewed",
            WorkflowPress.Accept => " · your press: Accept",
            _ => " · your press: Merge",
        };

        string title;
        string set;
        switch (step.Kind)
        {
            case WorkflowKinds.Work:
                title = "The work";
                set = "The chain's steps in this repository; a done that departs from your words or lacks its evidence waits for your yes.";
                break;
            case WorkflowKinds.Opinion:
            {
                title = "Second opinion";
                var on = Set("on") as IEnumerable<string> ?? [];
                var when = on.Contains("landing") ? (on.Contains("steps") ? "before it lands and before each next step" : "before it lands")
                    : "before each next step";
                var reviewers = Set("reviewers") as IEnumerable<string> ?? [];
                set = $"Read by {string.Join(", else ", reviewers.Select(name => $"`{name}`"))} {when}; "
                    + $"{(Set("required") is true ? "required" : "not required")}; its answers {(Set("recheck") is true ? "" : "not ")}read again.";
                break;
            }

            case WorkflowKinds.Look:
                title = "Your look";
                set = $"In `{Set("environment")}`.";
                break;
            case WorkflowKinds.Landing:
                title = "The landing";
                set = (string?)Set("form") == LandingForm.Merge
                    ? "Merged into its line"
                    : $"On a branch named `{Set("pattern")}`{((string?)Set("accept") == "automatic" ? ", accepted automatically" : "")}"
                        + (Set("plugin") is null ? ", for you to push" : $", pushed by `{Set("plugin")}`");
                set += $"{(Set("tidy") is true ? "; its tree and branch go once it lands" : "")}.";
                break;
            default:
                title = "The pull request";
                set = "Opened by its plugin at the landing; you merge it on the platform.";
                break;
        }

        var lines = new List<string> { $"  {title} — {participation} · {executor}{press}. {set} {SourceSaid(step.Source)}" };
        if (step.Kind == WorkflowKinds.Work && Set("standing") is string standing) lines.Add($"      Your standing answer, handed to each session: \"{standing}\"");
        // FABRIC-PIPELINE1b: the checks its sessions run, where the repository sets them (`daoris-driver checks`, Setup).
        if (step.Kind == WorkflowKinds.Work && Has("checks") && (string?)Set("checks") == "light")
        {
            lines.Add("      Its sessions' checks: light: the gates it does not mark heavy and the checks a change reaches; its heavy gates are its "
                + "verification's, refused to them.");
        }

        if (step.Kind == WorkflowKinds.Work && Has("checks") && (string?)Set("checks") == "heavy-at-end")
        {
            lines.Add("      Its sessions' checks: heavy-at-end: its heavy gates once, at a session's end, through `run-heavy`.");
        }

        if (step.Limit is { } limit) lines.Add($"      {WorkflowLimits.Says(limit)}");
        return lines;
    }

    /// <summary>Where a step was read, as a door says it.</summary>
    private static string SourceSaid(WorkflowSource source)
    {
        if (source.Rule is null) return "Always.";
        var rule = source.Rule switch
        {
            WorkflowRule.Landing => "landing rule",
            WorkflowRule.Review => "review rule",
            _ => "opinion rule",
        };
        return source.Level switch
        {
            LandingSource.Repository => $"This repository's {rule}.",
            LandingSource.Workspace => $"The workspace's {rule}.",
            _ => "Daoris's default.",
        };
    }

    /// <summary>A named step as <c>show</c> draws it: who takes part and the person's press, what it is set to, and its limit.</summary>
    public static IReadOnlyList<string> NamedStepSaid(NamedStep step)
    {
        var part = step.Kind switch
        {
            WorkflowKinds.Work or WorkflowKinds.Opinion => "Agent alone",
            WorkflowKinds.Look => "Agent + you · your press: Reviewed",
            WorkflowKinds.Landing => (string?)step.Field("accept") == "you" ? "You · your press: Accept" : "Automatic · Daoris",
            WorkflowKinds.PullRequest => "You · your press: Merge",
            WorkflowKinds.GoAhead => "You · your answer",
            _ => "Automatic",
        };
        var row = WorkflowKindTable.Of(step.Kind);
        // A field written only where set (FABRIC-PIPELINE1b2) is said only where set, as its version holds it.
        var set = string.Join("; ", step.Fields
            .Where(field => field.Value is not null || row?.Fields.FirstOrDefault(each => each.Name == field.Name) is not { WhereSet: true })
            .Select(field => $"{field.Name} {WorkflowDiff.Rendered(field.Value)}"));
        var lines = new List<string> { $"  {step.Id} · {step.Kind} — {part}.{(set.Length == 0 ? "" : $" {set}.")}" };
        if (row?.Limit is { } limit) lines.Add($"      {WorkflowLimits.Says(limit)}");
        return lines;
    }

    /// <summary>
    /// Which level chose a workflow, in words (design §4.1, <i>the run says why</i>): <i>chosen by `web-app` for Documentation</i>.
    /// </summary>
    public static string WhyChosen(WorkflowSelected selected, string? repository, string workspace)
    {
        var kind = selected.Label ?? selected.Kind;
        return selected.Level switch
        {
            WorkflowLevels.Task => "chosen by its ask",
            WorkflowLevels.RepositoryKind => $"chosen by `{repository}` for {kind}",
            WorkflowLevels.Repository => $"`{repository}`'s default",
            WorkflowLevels.WorkspaceKind => $"chosen by the workspace `{workspace}` for {kind}",
            WorkflowLevels.Workspace => $"the workspace `{workspace}`'s default",
            _ => "since nothing names a workflow",
        };
    }

    /// <summary>
    /// A landed route as <c>show</c> says it (FABRIC-PIPELINE1a, the pipeline design §1.3): the verification pipeline it names, who
    /// chose it and the gates accepted, or that it names no verification, which is today's.
    /// </summary>
    public static string LandedSaid(WorkflowLandedSelected route, string? repository, string workspace)
    {
        var who = route.Level == WorkflowLevels.RepositoryLanded ? $"as `{repository}` chooses" : $"as the workspace `{workspace}` chooses";
        if (route.Workflow == WorkflowSelection.Current) return route.Level == WorkflowLevels.Current ? "no verification here" : $"no verification here, {who}";
        var gates = route.Gates is null ? "its gates not accepted yet"
            : route.Gates == WorkflowSelection.NoGates ? "its gates accepted as none" : $"its gates accepted at blob `{Short(route.Gates)}`";
        return $"`{route.Workflow}`, {who}; {gates}";
    }

    /// <summary>Which door saved a version, as <c>show</c> says it (design §2.5): never <i>the person</i>, since a terminal's edit cannot be told from an agent's.</summary>
    public static string DoorSaid(string door) => door switch
    {
        "screen" => "on the screen",
        "terminal" => "at a terminal",
        _ => $"through Ask Daoris (`{(door.StartsWith("ask-daoris:", StringComparison.Ordinal) ? door["ask-daoris:".Length..] : door)}`)",
    };

    /// <summary>A graph version, as a writer says it: its size, its end and its digest (FABRIC-CUTOVER1d: it runs as a version of steps does).</summary>
    public static string GraphSaid(NamedVersion version)
    {
        var graph = version.Graph!;
        return $"a graph (schema {WorkflowNamed.GraphSchema}) of {Count(graph.Nodes.Count, "node")} and {Count(graph.Wires.Count, "wire")}, "
            + $"ending at `{graph.End}`, digest {version.Digest}.";
    }

    /// <summary>
    /// Why a verb that reads or changes a version's steps cannot take a graph version (FABRIC-CUTOVER1d): it holds none, and is read and saved
    /// whole. A sentence that names the version.
    /// </summary>
    public static string NoSteps(string id, NamedVersion version) =>
        $"`{id}` v{version.Version} is {GraphSaid(version).TrimEnd('.')}, and holds no steps to read or change: `daoris driver workflow export {id}` "
        + "prints it whole, and a version saved after it is saved whole.";

    /// <summary>
    /// Current copied into steps (design §2.7's <i>Save as a workflow…</i>), each as Current draws it so the two read the same: the
    /// opinion with its reviewers and switches, the look in its environment, the landing with its form, its press, and on a branch its
    /// pattern and plugin, <c>none</c> where it names none. The standing answer and the landing's <c>tidy</c> stay declarations (§2.3).
    /// </summary>
    public static JsonArray StepsOfCurrent(CurrentWorkflow current)
    {
        var steps = new JsonArray();
        foreach (var step in current.Steps)
        {
            object? Set(string name) => step.Settings.FirstOrDefault(setting => setting.Name == name)?.Value;
            JsonObject saved = step.Kind switch
            {
                WorkflowKinds.Opinion => new JsonObject
                {
                    ["id"] = "opinion",
                    ["kind"] = "opinion",
                    ["reviewers"] = Names(Set("reviewers")),
                    ["required"] = Set("required") is true,
                    ["steps"] = (Set("on") as IEnumerable<string> ?? []).Contains("steps"),
                    ["recheck"] = Set("recheck") is true,
                },
                WorkflowKinds.Look => new JsonObject { ["id"] = "look", ["kind"] = "look", ["environment"] = (string?)Set("environment") },
                WorkflowKinds.Landing => (string?)Set("form") == LandingForm.Merge
                    ? new JsonObject { ["id"] = "landing", ["kind"] = "landing", ["form"] = "merge", ["accept"] = "you" }
                    : new JsonObject
                    {
                        ["id"] = "landing",
                        ["kind"] = "landing",
                        ["form"] = "branch",
                        ["accept"] = (string?)Set("accept"),
                        ["pattern"] = (string?)Set("pattern"),
                        ["plugin"] = (string?)Set("plugin") ?? "none",
                    },
                // The checks its sessions run, where the repository sets them (FABRIC-PIPELINE1b2), so a light repository's stay light.
                WorkflowKinds.Work => step.Settings.Any(setting => setting.Name == "checks")
                    ? new JsonObject { ["id"] = "work", ["kind"] = "work", ["checks"] = (string?)Set("checks") }
                    : new JsonObject { ["id"] = "work", ["kind"] = "work" },
                _ => new JsonObject { ["id"] = step.Kind, ["kind"] = step.Kind },
            };
            steps.Add(saved);
        }

        return steps;
    }

    private static JsonArray Names(object? value) => new([.. (value as IEnumerable<string> ?? []).Select(name => (JsonNode?)JsonValue.Create(name))]);

    private static string Count(int count, string what) => $"{count} {what}{(count == 1 ? "" : "s")}";

    private static string Short(string id) => id.Length > 7 ? id[..7] : id;
}
