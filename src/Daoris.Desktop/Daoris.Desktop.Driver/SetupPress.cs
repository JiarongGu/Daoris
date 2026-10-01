using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>Why a set-up was refused: a code a screen can key on, and the sentence a person reads, naming the door that answers it.</summary>
public sealed record SetupRefusal(string Code, string Sentence);

/// <summary>The codes of <see cref="SetupRefusal"/> (D124 §2.1), in the order the press asks.</summary>
public static class SetupRefusals
{
    public const string NotOnRegistry = "not-on-registry";
    public const string NoCheckout = "no-checkout";
    public const string NotDriven = "not-driven";
    public const string NoOwnTree = "no-own-tree";
    public const string NoHistory = "no-history";
    public const string NoNode = "no-node";
    public const string NoTool = "no-doctrine-tool";
    public const string AlreadySetUp = "already-set-up";
    public const string Open = "open";
}

/// <summary>
/// The programs a set-up's session runs, as the press found them on the <c>PATH</c> a child of Daoris starts with
/// (D124 §1.3, §2.1): <c>node</c>, which the doctrine tool runs on, and <c>daoris</c>, with what each answered for its
/// version. The files are this machine's and are never put in the quest.
/// </summary>
/// <param name="NodeProblem">Why no <c>node</c> could be named, in Tools' words, where none was.</param>
/// <param name="DaorisProblem">What was found in place of an answer, where <c>daoris</c> did not give one.</param>
public sealed record SetupTools(
    string? Node, string? NodeVersion, string? NodeProblem, string? Daoris, string? DaorisVersion, string? DaorisProblem);

/// <summary>What a set-up press reads (LAYOUT7): the service's doors, the repository's line, the tools a child finds.</summary>
/// <remarks>An interface so the press is held without a service, git or a spawn; <see cref="SetupWorld"/> is the real one.</remarks>
public interface ISetupWorld
{
    /// <summary>Every repository this machine's host holds, in every circle.</summary>
    Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct);

    /// <summary>The repository's line (D86, as the driver's choices name it), read as git objects.</summary>
    Task<LineReading> ReadLineAsync(RepoView repository, DriverConfig config, CancellationToken ct);

    /// <summary>The <c>node</c> and the <c>daoris</c> a child of Daoris would find, and what each answered.</summary>
    Task<SetupTools> ToolsAsync(CancellationToken ct);

    /// <summary>Every quest, closed ones included.</summary>
    Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct);

    /// <summary>The path of each entry the workspace's index holds for the repository, once per entry.</summary>
    Task<IReadOnlyList<string>> EntriesAsync(string repository, CancellationToken ct);

    /// <summary>What its own files say it is (D77), from its checkout; null when they say nothing.</summary>
    RepositoryDescription? Describe(string root);

    /// <summary>An ask at <paramref name="workspace"/> with its receiver named: published at once, as the person's.</summary>
    Task<AskAnswer> PublishAsync(string workspace, string sentence, string to, CancellationToken ct);
}

/// <summary>What a press would do, or why it would not: plan, never apply (the CLI's convention, held here).</summary>
public sealed record SetupPlan(string Repository)
{
    /// <summary>Its circle, which the ask is made in; null where the registry holds no row for it.</summary>
    public string? Workspace { get; init; }

    /// <summary>What its line holds, where it could be read.</summary>
    public LayoutFacts? Facts { get; init; }

    /// <summary>The programs its session needs, as found.</summary>
    public SetupTools? Tools { get; init; }

    /// <summary>Which set-up the line calls for, where it could be read.</summary>
    public SetupCase? Case { get; init; }

    /// <summary>Every refusal that applies, in the press's order; none is a press that publishes.</summary>
    public IReadOnlyList<SetupRefusal> Refusals { get; init; } = [];

    /// <summary>The quest's title (<see cref="SetupQuests"/>), where there is one to ask.</summary>
    public string? Title { get; init; }

    /// <summary>The quest's body (<see cref="SetupBrief"/>).</summary>
    public string? Body { get; init; }

    /// <summary>How its work lands, said before the press (D117 §6.3).</summary>
    public Landing? Landing { get; init; }

    /// <summary>The agent this machine's driven sessions start on, which would carry it.</summary>
    public string? Adapter { get; init; }

    /// <summary>The ask's words: the title its first line, so the quest the service makes is titled by it, then the body.</summary>
    public string? Sentence => Title is null ? null : $"{Title}\n\n{Body}";

    /// <summary>Whether a press publishes: nothing refused, and something to ask.</summary>
    public bool Pressable => Refusals.Count == 0 && Title is not null;
}

/// <summary>What a press came to.</summary>
/// <param name="Message">The service's sentence, verbatim, or why nothing was asked.</param>
/// <param name="Added">The rules this press added to the repository's scope, and kept.</param>
public sealed record SetupOutcome(bool Published, string Message, string? AskId, string? QuestId, IReadOnlyList<string> Added);

/// <summary>
/// The set-up press (LAYOUT7; D117 §6.1–§6.3 as D124 §2.1 and §2.4 amend them): what the repository's line and this
/// machine say, each refusal with its door, one ask to one repository through the ask door with its receiver named,
/// as the person's (no intake session runs), and the rule its session needs to run the doctrine tool.
/// </summary>
/// <remarks>
/// <para><b>Never Daoris writing into the repository</b> (D32): a set-up is the repository's own session's work, on its
/// branch, landed by the workspace's rule. The press only asks, and adds a rule in Daoris's own file.</para>
///
/// <para><b>The rule is exact verbs</b> (D124 §2.4), never a runner, since a session on the protocol door cannot ask for
/// another (D52): no <c>upstream</c>, which writes into the canon inside the install, and no management verb, since a
/// set-up registers nothing. <c>sync --force</c> is in: on a set-up's branch, in a tree grown from the line, what it
/// replaces is committed on the line and can be taken back. As <c>Bash(…)</c>; <c>PowerShell(…)</c> waits on the
/// canary that says a driven session on Windows uses that tool.</para>
///
/// <para><b>"Already set up" reads the line, not <c>check</c></b>: Daoris runs no doctrine command into a repository
/// (D32), and a second <c>check</c> would twin the CLI's hardest table. What the line shows of it is read
/// (<see cref="LayoutFacts.Clean"/>); the rest is that repository's own gate.</para>
/// </remarks>
public static partial class SetupPress
{
    /// <summary>The doctrine tool's verbs a set-up's session runs, exactly (D124 §2.4). 🔴 Each is a verb the body asks for.</summary>
    public static readonly IReadOnlyList<string> Verbs =
    [
        "daoris --version", "daoris init --harness agents", "daoris analyze --json", "daoris sync --dry-run",
        "daoris sync", "daoris sync --force", "daoris check", "daoris status --json", "daoris doctor",
    ];

    /// <summary>The rule the press adds to the repository's scope: each verb as an exact <c>Bash</c> allow.</summary>
    public static IReadOnlyList<string> Rules { get; } = [.. Verbs.Select(verb => $"Bash({verb})")];

    /// <summary>The oldest <c>node</c> the doctrine tool runs on: its package's <c>engines</c>.</summary>
    public const int NodeFloor = 22;

    /// <summary>What a press on <paramref name="repository"/> would do today. Reads, and writes nothing.</summary>
    /// <param name="door">The door this machine's driven sessions ride: the configured agent's wire.</param>
    public static async Task<SetupPlan> PlanAsync(
        ISetupWorld world, DriverConfig config, SessionWire door, string repository, DateOnly day, CancellationToken ct = default)
    {
        var name = repository.Trim();
        var registry = await world.RegistryAsync(ct).ConfigureAwait(false);
        var row = registry.FirstOrDefault(repo => string.Equals(repo.Repository, name, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return new SetupPlan(name)
            {
                Refusals =
                [
                    new(SetupRefusals.NotOnRegistry,
                        $"`{name}` is not on this machine's registry. Add it on Repositories → *Add a repository*, or with "
                        + "`daoris import <folder> --workspace <name>`, then press again."),
                ],
            };
        }

        var plan = new SetupPlan(row.Repository)
        {
            Workspace = row.Workspace,
            Landing = LandingRules.Choose(config, row.Repository, row.Workspace),
            Adapter = config.Adapter,
        };
        if (row.Root is null)
        {
            return plan with
            {
                Refusals =
                [
                    new(SetupRefusals.NoCheckout,
                        $"`{row.Repository}` has no checkout here: its row is a teammate's registration, and its own "
                        + "machine's driver can be asked to set it up."),
                ],
            };
        }

        var refusals = new List<SetupRefusal>();
        if (!config.Drivable.Contains(row.Repository, StringComparer.OrdinalIgnoreCase))
        {
            refusals.Add(new(SetupRefusals.NotDriven,
                $"`{row.Repository}` is not driven here: it is not opted into driving on this machine, and only a driven "
                + $"session carries a set-up. Settings → Driver, or `daoris driver drive {row.Repository}`."));
        }
        else if (!row.Adopted && door != SessionWire.Acp)
        {
            refusals.Add(new(SetupRefusals.NotDriven,
                $"`{row.Repository}` is not driven here: it has not adopted, so only a session on the protocol door is "
                + $"handed the connector it takes the quest with, and this machine's sessions start on `{config.Adapter}`, "
                + "which rides the pipe. Choose an ACP agent with `daoris driver adapter <agent>`, in Settings → Driver."));
        }

        if (!config.OpensOwnTree(row.Repository))
        {
            refusals.Add(new(SetupRefusals.NoOwnTree,
                $"`{row.Repository}`'s sessions run in its checkout, not a tree of their own, and a set-up rewrites what "
                + "every later session there reads, so it lands on a branch of its own. Give it trees on Repositories, "
                + $"or with `daoris driver trees {row.Repository} on`."));
        }

        var line = await world.ReadLineAsync(row, config, ct).ConfigureAwait(false);
        if (line.Facts is null)
        {
            refusals.Add(new(SetupRefusals.NoHistory,
                $"`{row.Repository}` has no git history to set up here: {line.Problem ?? "its line could not be read"}."));
        }

        var tools = await world.ToolsAsync(ct).ConfigureAwait(false);
        if (NodeRefusal(tools) is { } node) refusals.Add(node);
        if (ToolRefusal(tools) is { } tool) refusals.Add(tool);

        var facts = line.Facts;
        var kind = facts is null ? (SetupCase?)null : CaseOf(facts);
        if (kind == SetupCase.Done)
        {
            refusals.Add(new(SetupRefusals.AlreadySetUp,
                $"`{row.Repository}` is already set up: on its line it has taken up the doctrine on the agents layout, "
                + "finished, and declares what it owns."));
        }

        var open = (await world.QuestsAsync(ct).ConfigureAwait(false)).FirstOrDefault(quest =>
            string.Equals(quest.To, row.Repository, StringComparison.OrdinalIgnoreCase)
            && quest.Status is "Open" or "Taken"
            && SetupQuests.IsSetup(quest.Title));
        if (open is not null)
        {
            refusals.Add(new(SetupRefusals.Open,
                $"a set-up is already {open.Status.ToLowerInvariant()} for `{row.Repository}`: `#{open.Id}`, {open.Title}."));
        }

        plan = plan with { Facts = facts, Tools = tools, Case = kind, Refusals = refusals };
        if (facts is null || kind is null or SetupCase.Done) return plan;

        var neighbours = registry
            .Where(repo => repo.Workspace == row.Workspace && !string.Equals(repo.Repository, row.Repository, StringComparison.OrdinalIgnoreCase))
            .Select(repo => repo.Repository)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var (title, body) = SetupBrief.Compose(new SetupBriefInput(
            kind.Value, day, row.Repository, facts, tools.DaorisVersion is { Length: > 0 } version ? version : "(no version was found)",
            world.Describe(row.Root), await world.EntriesAsync(row.Repository, ct).ConfigureAwait(false), neighbours));
        return plan with { Title = title, Body = body };
    }

    /// <summary>
    /// Press: the rule into the repository's scope, then one ask to it. A refusal from the service takes back the rules
    /// this press added, and keeps any the person already had. A plan that is not pressable is never applied.
    /// </summary>
    /// <exception cref="DriverException">Daoris's rules file could not be read, so no rule is written over it.</exception>
    public static async Task<SetupOutcome> ApplyAsync(SetupPlan plan, ISetupWorld world, string home, CancellationToken ct = default)
    {
        if (!plan.Pressable || plan.Workspace is null)
        {
            return new(false, plan.Refusals.Count > 0 ? plan.Refusals[0].Sentence : "nothing to ask.", null, null, []);
        }

        // The rule first, so the session the next look starts is handed it: a session cannot ask for one (D52).
        var file = PermissionRules.Load(home);
        var held = file.Repositories
            .Where(pair => string.Equals(pair.Key.Trim(), plan.Repository, StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value.Allow)
            .ToHashSet(StringComparer.Ordinal);
        var added = Rules.Where(rule => !held.Contains(rule)).ToList();
        PermissionRules.Save(home, added.Aggregate(file, (rules, rule) => PermissionRules.Add(rules, RuleScope.Repository, plan.Repository, RuleList.Allow, rule)));

        var answer = await world.PublishAsync(plan.Workspace, plan.Sentence!, plan.Repository, ct).ConfigureAwait(false);
        if (answer.Ok) return new(true, answer.Message, answer.AskId, answer.QuestId, added);

        var after = PermissionRules.Load(home);
        PermissionRules.Save(home, added.Aggregate(after, (rules, rule) => PermissionRules.Remove(rules, RuleScope.Repository, plan.Repository, rule)));
        return new(false, answer.Message, answer.AskId, null, []);
    }

    /// <summary>
    /// Which set-up the line calls for: none adopted is the whole; the older layout, or a move the lock has not
    /// followed, is a move; the agents layout finished and declaring nothing is the knowledge alone; declaring too is done.
    /// </summary>
    public static SetupCase CaseOf(LayoutFacts facts) =>
        !facts.Adopted ? SetupCase.Whole
        : !facts.Clean ? SetupCase.Move
        : !facts.Declares ? SetupCase.Declare
        : SetupCase.Done;

    /// <summary>How the work will land, in the words the press says it in before it is pressed.</summary>
    public static string LandsAs(Landing landing) =>
        landing.Rule.Form == LandingForm.Branch
            ? "as a branch, for your review" + (landing.Rule.Plugin is { } plugin ? $", which `{plugin}` pushes and opens the pull request for" : "")
            : "merged into its line, where the landed history is the review";

    private static SetupRefusal? NodeRefusal(SetupTools tools)
    {
        string? found = tools.Node is null
            ? tools.NodeProblem ?? "no `node` on the PATH Daoris's children start with"
            : Major(tools.NodeVersion) is not { } major
                ? $"the `node` a child finds did not answer its version (it said `{Said(tools.NodeVersion)}`)"
                : major < NodeFloor
                    ? $"the `node` a child finds is {tools.NodeVersion!.Trim()}, older than {NodeFloor}"
                    : null;
        return found is null
            ? null
            : new(SetupRefusals.NoNode,
                $"the doctrine tool cannot run here: {found}. Name one in Settings → Tools, or with `daoris tool use node …`.");
    }

    private static SetupRefusal? ToolRefusal(SetupTools tools)
    {
        string? found = tools.Daoris is null
            ? "no `daoris` on the PATH Daoris's children start with" + (tools.DaorisProblem is { } why ? $" ({why})" : "")
            : !IsVersion(tools.DaorisVersion)
                ? $"the `daoris` a child finds did not answer its version (it said `{Said(tools.DaorisProblem ?? tools.DaorisVersion)}`)"
                : null;
        return found is null
            ? null
            : new(SetupRefusals.NoTool,
                $"the doctrine tool cannot run here: {found}. An install that does not carry the doctrine tool puts none "
                + "there, and a republish of Daoris that carries it does.");
    }

    /// <summary>The major version <c>node --version</c> printed (<c>v22.11.0</c>), or null for anything else.</summary>
    private static int? Major(string? version) =>
        version is not null && NodeVersion().Match(version.Trim()) is { Success: true } match
            && int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major)
            ? major
            : null;

    /// <summary>Whether <c>daoris --version</c> printed a version: numbers and dots, as the CLI prints its own.</summary>
    internal static bool IsVersion(string? version) => version is not null && DaorisVersion().IsMatch(version.Trim());

    private static string Said(string? text)
    {
        var first = (text ?? "").Trim().Split('\n')[0].Trim();
        return first.Length == 0 ? "nothing" : first.Length > 80 ? first[..80] + "…" : first;
    }

    [GeneratedRegex(@"^v?(\d+)\.\d+\.\d+")]
    private static partial Regex NodeVersion();

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$")]
    private static partial Regex DaorisVersion();
}
