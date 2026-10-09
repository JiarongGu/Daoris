using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Where a run's own part of the landing gate stands (WORKFLOW1f, D157 points 4, 6 and 10; the workflow design §2.3, §4.4–§4.6,
/// §5): the workflow its run was bound to, read before the second opinion and the person's look. Three let the work go.
/// </summary>
public static class WorkflowGateStates
{
    /// <summary>Its run follows Current, or has no binding here: every gate reads the rules live, as today.</summary>
    public const string None = "none";

    /// <summary>A named version governs it, and nothing of the run's own holds it.</summary>
    public const string Follows = "follows";

    /// <summary>The person kept the kind's workflow for this work, though it changed paths outside the kind's (§4.4).</summary>
    public const string Kept = "kept";

    /// <summary>The workflow its run was bound to cannot be read here, so none of its steps can start (§3.7).</summary>
    public const string CannotStart = "cannot-start";

    /// <summary>The kind's workflow asks less of the person, and the chain changed paths outside the kind's (§4.4).</summary>
    public const string KindPaths = "kind-paths";

    /// <summary>Whether it holds could not be read: the service named no chain, or git no paths.</summary>
    public const string Unread = "unread";
}

/// <summary>
/// D154's rows 5 and 6 as a named version says them (WORKFLOW1f, the workflow design §4.6): the environment its look step reads the
/// work in, or none where it has no look; and why its look cannot start, where it names what the repository does not declare.
/// </summary>
/// <param name="Environment">The environment the look is in; null where the version has no look, or its look cannot start for want of one.</param>
/// <param name="Cannot">Why the look cannot start (§3.7), a sentence naming the Setup row that declares what it needs; else null.</param>
public sealed record WorkflowLook(string? Environment, string? Cannot);

/// <summary>
/// The named workflow a landing went by (WORKFLOW1f, the workflow design §5.5): its id, the version its run kept, the level of §4.1
/// that chose it, and the task's kind. Kept on the landing record beside the review and the opinion; absent under Current.
/// </summary>
public sealed record LandingWorkflow(string Workflow, int Version, string Level)
{
    public string? Kind { get; init; }
}

/// <summary>
/// The process a chain's work in a repository follows at its gate (WORKFLOW1f, D157 points 4 and 6; the workflow design §2.3,
/// §4.6): how it lands, the review rule its look reads and what rows 5 and 6 say, and the second opinion it waits for. Under
/// Current, or with no binding here, the rules as they stand, read live as every gate reads them today; under a named workflow,
/// its bound version's steps over what the repository declares, each step choosing among what is declared and never beyond it.
/// </summary>
/// <param name="Landing">The landing rule the work lands by: the declared rule, or the version's landing step over it.</param>
/// <param name="Review">The review rule as declared: its environments, which the chain's and the ask's <c>on</c> reads (§2.3).</param>
/// <param name="Opinion">The second-opinion rule the gate reads; null where none is set, or the version has no opinion step.</param>
public sealed record WorkflowProcess(string Repository, Landing Landing, ResolvedReview? Review, ResolvedOpinion? Opinion)
{
    /// <summary>The run it was read for: the chain's first quest in the repository; null where none was named.</summary>
    public string? Run { get; init; }

    /// <summary>The run's binding here, Current's or a named workflow's; null where it has none.</summary>
    public WorkflowRunBinding? Binding { get; init; }

    /// <summary>The bound version's steps; empty under Current, or where the version could not be read.</summary>
    public IReadOnlyList<NamedStep> Steps { get; init; } = [];

    /// <summary>Rows 5 and 6 of D154's table as the version says them; null under Current, where the rule says them, as today.</summary>
    public WorkflowLook? Look { get; init; }

    /// <summary>Why the version's opinion step cannot start (§3.7): it names reviewers the repository does not declare.</summary>
    public string? OpinionCannot { get; init; }

    /// <summary>Why the version's landing step cannot start (§3.7): it lands on a branch no pattern names.</summary>
    public string? LandingCannot { get; init; }

    /// <summary>Why the named workflow its run was bound to cannot be read here: none of its steps can start, and nothing is swapped in.</summary>
    public string? Problem { get; init; }

    /// <summary>Why it could not be read whether a named workflow governs this work: the service named no chain.</summary>
    public string? Unread { get; init; }

    /// <summary>Whether a named workflow governs this work: its run was bound to one.</summary>
    public bool Named => Binding is { IsCurrent: false };

    /// <summary>How the work is said to follow it: <c>`docs-to-pr` v2</c>, or Current.</summary>
    public string Name => Binding is { IsCurrent: false } named
        ? named.Version is { } version ? $"`{named.Workflow}` v{version}" : $"`{named.Workflow}`"
        : "Current";

    /// <summary>What the landing record keeps of it (design §5.5): the named version that decided; null under Current.</summary>
    public LandingWorkflow? Record => Binding is { IsCurrent: false, Version: { } version } named && Problem is null && Unread is null
        ? new LandingWorkflow(named.Workflow, version, named.Level) { Kind = named.Kind }
        : null;
}

/// <summary>
/// The process each gate reads for a chain's work in a repository (WORKFLOW1f, D157 points 4, 6 and 11; the workflow design §2.3,
/// §2.6, §4.5–§4.6): the run's bound version where it names one, the rules live where it does not.
/// </summary>
/// <remarks>
/// <para><b>One read per decision.</b> The landing gate reads it once (<see cref="SessionTrees.GateAsync"/>) and hands it to every
/// part, the landing among them; the look's automatic landing and the second opinions owed read it once per entry, and hand it on.
/// Never a second copy per door.</para>
///
/// <para><b>A step chooses among what is declared, never beyond it</b> (§2.3). A look names one of the environments the review rule
/// declares, or takes its first; an opinion names reviewers from the rule's list, or takes it, with the rule's <c>verify</c> and
/// <c>minutes</c>; a landing names its form and its press, and a pattern and a plugin where it wants its own, the rule's otherwise,
/// with the rule's <c>tidy</c>. A step that needs what is not declared cannot start, and sits saying so (§3.7).</para>
///
/// <para><b>Never swapped</b> (§4.5): a named workflow that cannot be read here holds its run, saying why, and nothing else is read
/// in its place.</para>
/// </remarks>
public static class WorkflowProcesses
{
    /// <summary>The rules as they stand, read live as every gate reads them today: Current, and work with no binding here.</summary>
    public static WorkflowProcess Current(DriverConfig config, string repository, string? workspace) =>
        new(repository, LandingRules.Choose(config, repository, workspace), ReviewRules.Resolve(config, repository, workspace),
            OpinionRules.Resolve(config, repository, workspace));

    /// <summary>
    /// The process of a run (design §2.6, §4.5): its binding read from the home, and a named workflow's version from the store by the
    /// number the run kept, which must still be the version it bound (a version is never edited). Current, or no binding, is the
    /// rules live; a named workflow that cannot be read here holds the run with why.
    /// </summary>
    /// <param name="run">The run: the chain's first quest in the repository (<see cref="WorkflowRunBindings.RunOf"/>); null for none.</param>
    public static WorkflowProcess Read(string home, DriverConfig config, string repository, string? workspace, string? run)
    {
        var current = Current(config, repository, workspace) with { Run = run };
        if (run is null || WorkflowRunBindings.Read(home, run) is not { } binding) return current;
        if (binding.IsCurrent) return current with { Binding = binding };

        // Held before any step: no opinion is asked for a run whose workflow cannot be read.
        var held = current with { Binding = binding, Opinion = null };
        if (binding.Problem is { } problem) return held with { Problem = problem };
        if (binding.Version is not { } number) return held with { Problem = $"its run names no version of `{binding.Workflow}`." };
        var found = WorkflowNamed.IsId(binding.Workflow) ? WorkflowStore.Load(home, binding.Workflow) : null;
        if (found is null) return held with { Problem = $"no workflow `{binding.Workflow}` is saved here any more." };
        if (found.Read.Problem is { } unread) return held with { Problem = $"`{binding.Workflow}` cannot be read: {unread}" };
        var version = found.Read.Versions.FirstOrDefault(each => each.Version == number);
        if (version is null) return held with { Problem = $"`{binding.Workflow}` v{number} is no longer saved here." };
        if (version.Steps is null) return held with { Problem = $"`{binding.Workflow}` v{number} cannot be read here: {version.Problem}" };
        if (binding.Digest is { } digest && !string.Equals(digest, version.Digest, StringComparison.Ordinal))
        {
            return held with
            {
                Problem = $"`{binding.Workflow}` v{number} is not the version its run was bound to: its steps changed since, and a version "
                    + "is never edited.",
            };
        }

        return Of(config, repository, workspace, binding, version.Steps) with { Run = run };
    }

    /// <summary>
    /// The process of a quest's work in <paramref name="repository"/> (design §4.5): its run named by the chain's first quest there,
    /// read from the service's quests. Where they do not answer, the quest's own id is tried, since a chain's first quest names its
    /// run; and where that names none, the work is held unread only if a named workflow is bound to any run here, since one might
    /// govern it.
    /// </summary>
    public static async Task<WorkflowProcess> ReadAsync(
        string home, DriverConfig config, IReviewWorld world, string quest, string repository, string? workspace, CancellationToken ct = default)
    {
        var id = quest.TrimStart('#');
        try
        {
            var chain = ReviewGate.ChainOf(await world.QuestsAsync(ct).ConfigureAwait(false), id);
            return Read(home, config, repository, workspace, WorkflowRunBindings.RunOf(chain, repository) ?? id);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or InvalidOperationException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            var own = Read(home, config, repository, workspace, WorkflowRunBindings.IsRunId(id) ? id : null);
            if (own.Binding is not null || !WorkflowRunBindings.AnyNamed(home, repository)) return own;
            return own with { Unread = $"its chain could not be read, so whether a named workflow governs it is not known: {error.Message.TrimEnd().TrimEnd('.')}." };
        }
    }

    /// <summary>
    /// A named version's process over what the repository declares (design §2.3, §3.2, §4.6). Pure: the state table
    /// (<c>WorkflowGateTests</c>) holds every row without a file.
    /// </summary>
    public static WorkflowProcess Of(DriverConfig config, string repository, string? workspace, WorkflowRunBinding binding, IReadOnlyList<NamedStep> steps)
    {
        var declared = Current(config, repository, workspace);

        // The landing: its form and its press the step's; a pattern and a plugin its own where it names them, the rule's otherwise.
        var land = steps.First(step => step.Kind == WorkflowKinds.Landing);
        var rule = declared.Landing.Rule;
        string? landingCannot = null;
        LandingRule landing;
        if ((string?)land.Field("form") == LandingForm.Merge)
        {
            landing = new LandingRule(LandingForm.Merge, Tidy: rule.Tidy);
        }
        else
        {
            var branch = rule.Form == LandingForm.Branch;
            var pattern = (string?)land.Field("pattern") ?? (branch ? rule.Pattern : null);
            var plugin = (string?)land.Field("plugin") switch
            {
                "none" => null,
                null => branch ? rule.Plugin : null,
                var id => id,
            };
            if (pattern is null)
            {
                landingCannot = $"its landing `{land.Id}` puts the work on a branch, and neither the step nor `{repository}`'s landing rule "
                    + $"names a pattern for it: `daoris driver landing {repository} branch <pattern>` declares one.";
            }

            landing = new LandingRule(LandingForm.Branch, pattern, rule.Tidy, plugin, (string?)land.Field("accept") == "automatic");
        }

        // The look (D154 rows 5 and 6): its presence is `required`, in an environment the review rule declares.
        WorkflowLook look;
        if (steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Look) is not { } seen)
        {
            look = new WorkflowLook(null, null);
        }
        else if (declared.Review is not { Rule.IsNone: false } reviews)
        {
            var asked = (string?)seen.Field("environment");
            look = new WorkflowLook(asked, $"its look `{seen.Id}` reads the work in {(asked is null ? "an environment" : $"`{asked}`")}, and "
                + $"`{repository}` declares no review environment: `daoris driver review {repository} {asked ?? "<environment>"} --kind "
                + "local|deployed --procedure <path>` declares one.");
        }
        else
        {
            var environment = (string?)seen.Field("environment") ?? reviews.Rule.Environments[0].Name;
            look = reviews.Rule.Environments.Any(each => each.Name == environment)
                ? new WorkflowLook(environment, null)
                : new WorkflowLook(environment, $"its look `{seen.Id}` reads the work in `{environment}`, which `{repository}` does not declare (it declares "
                    + $"{And(reviews.Rule.Environments.Select(each => each.Name))}): `daoris driver review {repository} {environment} --kind "
                    + "local|deployed --procedure <path>` declares it.");
        }

        // The second opinion: when it reads, whether it is required and read again the step's; which reviewers among those declared.
        ResolvedOpinion? opinion = null;
        string? opinionCannot = null;
        if (steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Opinion) is { } reads)
        {
            var named = reads.Field("reviewers") as IReadOnlyList<string>;
            IReadOnlyList<string> on = (bool?)reads.Field("steps") == true ? [OpinionRules.Landing, OpinionRules.Steps] : [OpinionRules.Landing];
            var required = (bool?)reads.Field("required") == true;
            var recheck = (bool?)reads.Field("recheck") != false;
            if (declared.Opinion is not { Rule.IsNone: false } listed)
            {
                opinionCannot = $"its second opinion `{reads.Id}` reads with reviewers `{repository}` declares, and it declares none: "
                    + $"`daoris driver opinion {repository} --reviewers <adapter,adapter>` declares them.";
                opinion = new ResolvedOpinion(new OpinionRule(named ?? [], on, required, Recheck: recheck), declared.Opinion?.Source ?? OpinionSource.Repository);
            }
            else
            {
                var beyond = (named ?? []).Where(each => !listed.Rule.Reviewers.Contains(each, StringComparer.OrdinalIgnoreCase)).ToList();
                if (beyond.Count > 0)
                {
                    opinionCannot = $"its second opinion `{reads.Id}` names {And(beyond)}, which `{repository}` does not declare among its reviewers "
                        + $"({And(listed.Rule.Reviewers)}): a step chooses among them, and `daoris driver opinion {repository} --reviewers "
                        + "<adapter,adapter>` declares more.";
                }

                // Named reviewers in the step's order, spelled as the rule declares them, since the walk finds them by that name.
                var chosen = named is null
                    ? listed.Rule.Reviewers
                    : [.. named.Select(each => listed.Rule.Reviewers.FirstOrDefault(name => string.Equals(name, each, StringComparison.OrdinalIgnoreCase)) ?? each)];
                opinion = new ResolvedOpinion(listed.Rule with { Reviewers = chosen, On = on, Required = required, Recheck = recheck }, listed.Source);
            }
        }

        return declared with
        {
            Landing = new Landing(landing, declared.Landing.Source),
            Opinion = opinion,
            Binding = binding,
            Steps = steps,
            Look = look,
            OpinionCannot = opinionCannot,
            LandingCannot = landingCannot,
        };
    }

    /// <summary>
    /// The workflow a run would follow without its task's kind (design §4.4): §4.1 resolved again with the task's kind left out, as
    /// the choices stand now. A named workflow's newest version that reads, or Current drawn; null where the named one does not read.
    /// </summary>
    /// <returns>Its name as said, and its steps, or null steps where they could not be read.</returns>
    public static (string Name, IReadOnlyList<NamedStep>? Steps) Otherwise(string home, DriverConfig config, string repository, string? workspace)
    {
        var selected = WorkflowSelection.Resolve(config, repository, workspace, null);
        if (selected.Workflow == WorkflowSelection.Current)
        {
            return ("Current", Involvement.StepsOf(WorkflowCurrent.Derive(config, repository, workspace, [])));
        }

        var found = WorkflowNamed.IsId(selected.Workflow) ? WorkflowStore.Load(home, selected.Workflow) : null;
        var newest = found?.Read.Versions.LastOrDefault(each => each.Steps is not null);
        return newest is null ? ($"`{selected.Workflow}`", null) : ($"`{selected.Workflow}` v{newest.Version}", newest.Steps);
    }

    /// <summary>Names in backticks, the last after <c>and</c>.</summary>
    internal static string And(IEnumerable<string> names)
    {
        var ticked = names.Select(name => $"`{name}`").ToList();
        return ticked.Count <= 1 ? string.Concat(ticked) : $"{string.Join(", ", ticked.Take(ticked.Count - 1))} and {ticked[^1]}";
    }
}

/// <summary>
/// The person's part in a workflow, compared (WORKFLOW1f, D157 point 10; the workflow design §4.3–§4.4): lowering is a table, not a
/// reading. A look or an opinion dropped, an opinion made not required, a landing or a stage made automatic, a go-ahead removed or a
/// pull request skipped each lower it; raising one never does. WORKFLOW1i's intake reads the same table.
/// </summary>
public static class Involvement
{
    /// <summary>
    /// How <paramref name="chosen"/> asks less of the person than <paramref name="otherwise"/> (design §4.3), each said as what it
    /// does; empty where it lowers nothing.
    /// </summary>
    public static IReadOnlyList<string> Lowers(IReadOnlyList<NamedStep> chosen, IReadOnlyList<NamedStep> otherwise)
    {
        NamedStep? Of(IReadOnlyList<NamedStep> steps, string kind) => steps.FirstOrDefault(step => step.Kind == kind);
        var lowered = new List<string>();
        if (Of(otherwise, WorkflowKinds.Look) is not null && Of(chosen, WorkflowKinds.Look) is null) lowered.Add("it drops your look");

        var opinion = Of(otherwise, WorkflowKinds.Opinion);
        var kept = Of(chosen, WorkflowKinds.Opinion);
        if (opinion is not null && kept is null) lowered.Add("it drops the second opinion");
        else if ((bool?)opinion?.Field("required") == true && (bool?)kept?.Field("required") != true)
        {
            lowered.Add("it no longer requires the second opinion");
        }

        if ((string?)Of(otherwise, WorkflowKinds.Landing)?.Field("accept") == "you" && (string?)Of(chosen, WorkflowKinds.Landing)?.Field("accept") == "automatic")
        {
            lowered.Add("it lands with no press of yours");
        }

        if (Of(otherwise, WorkflowKinds.PullRequest) is not null && Of(chosen, WorkflowKinds.PullRequest) is null) lowered.Add("it skips the pull request");

        var acts = All(chosen).Where(step => step.Kind == WorkflowKinds.GoAhead).Select(step => (string?)step.Field("act")).ToHashSet(StringComparer.Ordinal);
        foreach (var goAhead in All(otherwise).Where(step => step.Kind == WorkflowKinds.GoAhead && !acts.Contains((string?)step.Field("act"))))
        {
            lowered.Add($"it removes the go-ahead \"{goAhead.Field("act")}\"");
        }

        var started = All(chosen).Where(step => step.Kind == WorkflowKinds.Stage && (string?)step.Field("start") == "automatic")
            .Select(step => (string?)step.Field("stage")).ToHashSet(StringComparer.Ordinal);
        foreach (var stage in All(otherwise).Where(step => step.Kind == WorkflowKinds.Stage && (string?)step.Field("start") == "you"
                     && started.Contains((string?)step.Field("stage"))))
        {
            lowered.Add($"it starts `{stage.Field("stage")}` with no press of yours");
        }

        return lowered;
    }

    /// <summary>
    /// Current as steps (design §2.7's <i>Save as a workflow…</i>, the CLI's <c>stepsOfCurrent</c>): the opinion with its reviewers
    /// and switches, the look in its environment, the landing with its form and press, the pull request; the standing answer and
    /// the landing's <c>tidy</c> stay declarations (§2.3).
    /// </summary>
    public static IReadOnlyList<NamedStep> StepsOf(CurrentWorkflow current) =>
    [
        .. current.Steps.Select(step =>
        {
            object? Setting(string name) => step.Settings.FirstOrDefault(setting => setting.Name == name)?.Value;
            IReadOnlyList<WorkflowSetting> fields = step.Kind switch
            {
                WorkflowKinds.Opinion =>
                [
                    new("reviewers", Setting("reviewers")), new("required", Setting("required")),
                    new("steps", (Setting("on") as IEnumerable<string>)?.Contains(OpinionRules.Steps) == true), new("recheck", Setting("recheck")),
                ],
                WorkflowKinds.Look => [new("environment", Setting("environment"))],
                WorkflowKinds.Landing =>
                [
                    new("form", Setting("form")), new("accept", Setting("accept")), new("pattern", Setting("pattern")),
                    new("plugin", Setting("plugin")),
                ],
                _ => [],
            };
            return new NamedStep(step.Id, step.Kind, fields);
        }),
    ];

    private static IEnumerable<NamedStep> All(IReadOnlyList<NamedStep> steps) => steps.Concat(steps.SelectMany(step => step.Members));
}

/// <summary>
/// A kind's paths (WORKFLOW1f, the workflow design §4.2, §4.4): the globs work of that kind keeps to, matched against the paths a
/// chain changed. <c>*</c> stays inside one folder, <c>**</c> crosses folders (<c>**/</c> matching none too), <c>?</c> is one
/// character of a name, <c>{a,b}</c> is either; the rest is itself, compared exactly, as git names a path.
/// </summary>
public static class KindPaths
{
    /// <summary>Whether <paramref name="path"/>, as git names it with <c>/</c> between its parts, keeps to <paramref name="glob"/>.</summary>
    public static bool Matches(string glob, string path) =>
        Regex.IsMatch(path, Pattern(glob), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>The paths that keep to none of <paramref name="globs"/>, in the order given.</summary>
    public static IReadOnlyList<string> Outside(IReadOnlyList<string> globs, IReadOnlyList<string> paths) =>
        [.. paths.Where(path => !globs.Any(glob => Matches(glob, path)))];

    /// <summary>A glob as a pattern a whole path is held to.</summary>
    internal static string Pattern(string glob)
    {
        var pattern = new StringBuilder("^");
        var braces = 0;
        for (var at = 0; at < glob.Length; at++)
        {
            var c = glob[at];
            switch (c)
            {
                case '*' when at + 1 < glob.Length && glob[at + 1] == '*':
                    // `**/` is any folders, none among them; `**` elsewhere is anything at all.
                    if (at + 2 < glob.Length && glob[at + 2] == '/')
                    {
                        pattern.Append("(?:.*/)?");
                        at += 2;
                    }
                    else
                    {
                        pattern.Append(".*");
                        at += 1;
                    }

                    break;
                case '*':
                    pattern.Append("[^/]*");
                    break;
                case '?':
                    pattern.Append("[^/]");
                    break;
                case '{':
                    braces++;
                    pattern.Append("(?:");
                    break;
                case '}' when braces > 0:
                    braces--;
                    pattern.Append(')');
                    break;
                case ',' when braces > 0:
                    pattern.Append('|');
                    break;
                default:
                    pattern.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        // An unclosed brace is the glob's own words, not a group: its text is taken as written.
        return braces > 0 ? "^" + Regex.Escape(glob) + "$" : pattern.Append('$').ToString();
    }
}

/// <summary>
/// What a run's kind paths came to at the gate (design §4.4): what its workflow lowers against the one it would follow without the
/// kind, that one's name, and the paths the chain changed, or null where git could not say.
/// </summary>
/// <param name="Lowered">How the kind's workflow asks less of the person (<see cref="Involvement.Lowers"/>); empty where it lowers nothing.</param>
/// <param name="Otherwise">The workflow the work would follow without the kind, as said.</param>
/// <param name="Changed">The paths the chain changed, from where its work leaves the line to its tip; null where git could not say.</param>
public sealed record KindPathsRead(IReadOnlyList<string> Lowered, string Otherwise, IReadOnlyList<string>? Changed);

/// <summary>
/// The run's own part of the landing gate (WORKFLOW1f; the workflow design §3.7, §4.4–§4.5, §5.2): the process it read, and what of
/// the run's own holds the work before the second opinion and the look.
/// </summary>
/// <param name="State">One of <see cref="WorkflowGateStates"/>.</param>
public sealed record WorkflowGateState(string State, WorkflowProcess Process)
{
    /// <summary>The working session the door lands, which the terminal's doors name.</summary>
    public string? Session { get; init; }

    /// <summary>The changed paths outside the kind's, for <see cref="WorkflowGateStates.KindPaths"/>.</summary>
    public IReadOnlyList<string> Outside { get; init; } = [];

    /// <summary>How the kind's workflow asks less of the person, where the paths were checked.</summary>
    public IReadOnlyList<string> Lowered { get; init; } = [];

    /// <summary>The workflow the work would follow without its kind, where the paths were checked.</summary>
    public string? Otherwise { get; init; }

    /// <summary>Why it could not be read, for <see cref="WorkflowGateStates.Unread"/>.</summary>
    public string? Problem { get; init; }

    /// <summary>Whether the run's own part lets the work on to the second opinion and the look.</summary>
    public bool LetsGo => State is WorkflowGateStates.None or WorkflowGateStates.Follows or WorkflowGateStates.Kept;

    /// <summary>What it says, in the driver's words: the terminal's line and every door's refusal.</summary>
    public string Says => WorkflowGate.Says(this);
}

/// <summary>
/// The run's own part of the landing gate (WORKFLOW1f, D157 points 6, 10 and 11; the workflow design §3.7, §4.4–§4.6, §5): whether
/// the workflow its run was bound to can be read here, and whether a kind's paths hold. A table: the binding, the version, git's
/// changed paths and the person's say-so, and no model reads any of it (§9).
/// </summary>
/// <remarks>
/// <para><b>One gate, in its order</b> (D155 §7, design §2.2, §4.4): the content the line already holds, then the run's own part,
/// then the second opinion, then the person's look, then the landing step's own declarations. Every landing door reads it through
/// <see cref="SessionTrees.GateAsync"/>, and <see cref="SessionTrees.LandAsync"/> refuses the first part that holds.</para>
///
/// <para><b>A kind's paths</b> (§4.4) are checked only where the kind chose the workflow (its level is the repository's or the
/// workspace's for the kind), the kind declares paths, and its workflow asks less of the person than the one the work would follow
/// without the kind. Inside them the work goes on; outside them it holds for the person, whose <i>Keep</i> is kept on the run. It
/// never switches by itself, and nothing infers a kind from the paths.</para>
/// </remarks>
public static class WorkflowGate
{
    /// <summary>
    /// The run's own part for a process and, where its kind's paths were read, what they came to (design §4.4): Current is today's;
    /// a workflow that cannot be read holds; a kind's paths hold where the kind's workflow lowers the person's part and the chain
    /// changed a path outside them, unless the person kept it.
    /// </summary>
    public static WorkflowGateState Judge(WorkflowProcess process, KindPathsRead? paths = null)
    {
        if (process.Unread is { } unread) return new WorkflowGateState(WorkflowGateStates.Unread, process) { Problem = unread };
        if (!process.Named) return new WorkflowGateState(WorkflowGateStates.None, process);
        if (process.Problem is not null) return new WorkflowGateState(WorkflowGateStates.CannotStart, process);
        if (paths is null || paths.Lowered.Count == 0) return new WorkflowGateState(WorkflowGateStates.Follows, process);

        var read = new WorkflowGateState(WorkflowGateStates.Follows, process) { Lowered = paths.Lowered, Otherwise = paths.Otherwise };
        if (process.Binding!.Kept is not null) return read with { State = WorkflowGateStates.Kept };
        if (paths.Changed is not { } changed)
        {
            return read with
            {
                State = WorkflowGateStates.Unread,
                Problem = "git could not say which paths this work changed, so whether it keeps to its kind's paths is not known.",
            };
        }

        var outside = KindPaths.Outside(process.Binding.Paths, changed);
        return outside.Count == 0 ? read : read with { State = WorkflowGateStates.KindPaths, Outside = outside };
    }

    /// <summary>Whether a kind's paths are checked for this run (design §4.4): the kind chose its workflow, and declares paths.</summary>
    public static bool ChecksPaths(WorkflowProcess process) =>
        process is { Named: true, Problem: null, Binding: { Level: WorkflowLevels.RepositoryKind or WorkflowLevels.WorkspaceKind, Paths.Count: > 0 } };

    /// <summary>What the run's own part says (design §3.7, §4.4, §5.2): what holds it and the door that moves it, or what it follows.</summary>
    public static string Says(WorkflowGateState gate)
    {
        var process = gate.Process;
        var binding = process.Binding;
        var why = binding is null ? "" : $", {WorkflowRunBindings.Why(binding)}";
        return gate.State switch
        {
            WorkflowGateStates.None => "It follows Current: the rules as they stand, read at each gate.",
            WorkflowGateStates.Follows => $"It follows {process.Name}{why}." + (gate.Lowered.Count > 0 ? " It keeps to its kind's paths." : ""),
            WorkflowGateStates.Kept => $"It follows {process.Name}{why}: you kept it for this work, though it changed paths outside its kind's"
                + (binding?.Kept?.Words is { Length: > 0 } words ? $", saying: \"{words}\"" : "") + ".",
            WorkflowGateStates.CannotStart =>
                $"Its run was bound to {process.Name}{why}, which cannot start it here: {process.Problem} Nothing lands until it can be read "
                + $"(`daoris driver workflow show {binding!.Workflow}` says what is wrong with it), and no other workflow is followed in its place.",
            WorkflowGateStates.KindPaths =>
                $"Holds: this work changed {Plural(gate.Outside.Count, "path")} outside {Kind(binding!)}'s ({WorkflowProcesses.And(binding!.Paths)}): "
                + $"{Listed(gate.Outside)}; and {process.Name}, chosen for it, asks less of you than {gate.Otherwise} would without the kind "
                + $"({string.Join("; ", gate.Lowered)}). `daoris-driver workflow keep {gate.Session ?? "<session>"} \"…\"` keeps {process.Name} for "
                + "this work with your words; nothing switches by itself.",
            _ => $"Whether this work's workflow holds it could not be read: {gate.Problem} Nothing lands until it can be.",
        };
    }

    /// <summary>What a landing step that cannot start says (design §3.7): the door that declares what it needs.</summary>
    public static string LandingSays(WorkflowProcess process) =>
        $"It follows {process.Name}, and {process.LandingCannot} Nothing was landed.";

    /// <summary>The kind as said: its label, or its id.</summary>
    private static string Kind(WorkflowRunBinding binding) => binding.Label ?? $"`{binding.Kind}`";

    /// <summary>The first three paths, then how many more.</summary>
    private static string Listed(IReadOnlyList<string> paths) =>
        string.Join(", ", paths.Take(3).Select(path => $"`{path}`")) + (paths.Count > 3 ? $" and {paths.Count - 3} more" : "");

    private static string Plural(int count, string noun) =>
        count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? noun : noun + "s");
}

/// <summary>
/// The machine log's workflow lines (WORKFLOW1f, the workflow design §5.5, D94): <c>workflow.chosen</c> and <c>workflow.held</c>,
/// codes, ids and counts only, never a sentence, a path or anyone's words. They ride the landing line's channel
/// (<see cref="ServiceClient.LandingSaid"/>), which writes the event each names.
/// </summary>
public static class WorkflowLines
{
    /// <summary>A run bound at its first start: which level chose, whether a named workflow or Current, and its version.</summary>
    public static LandingLine Chosen(WorkflowRunBinding binding) =>
        new("workflow.chosen",
        [
            ("run", binding.Run), ("repository", binding.Repository), ("workspace", binding.Workspace), ("level", binding.Level),
            ("kind", binding.Kind), ("named", !binding.IsCurrent), ("workflow", binding.IsCurrent ? null : binding.Workflow),
            ("version", binding.Version), ("problem", binding.Problem is not null),
        ]);

    /// <summary>A landing the run's own part held: whose, where, its code, the step that cannot start, and the door it was tried at.</summary>
    /// <param name="step">The kind of step that cannot start, for a landing step's own; null for the run's.</param>
    public static LandingLine Held(string session, string? repository, string? workspace, WorkflowGateState gate, string door, string? step = null) =>
        new("workflow.held",
        [
            ("session", session), ("repository", repository), ("workspace", workspace),
            ("code", step is null ? gate.State : WorkflowGateStates.CannotStart), ("step", step),
            ("workflow", gate.Process.Named ? gate.Process.Binding!.Workflow : null), ("version", gate.Process.Binding?.Version),
            ("outside", gate.State == WorkflowGateStates.KindPaths ? gate.Outside.Count : null), ("door", door),
        ]);
}
