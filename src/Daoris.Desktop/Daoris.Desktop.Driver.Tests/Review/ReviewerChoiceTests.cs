using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1b (D155 point 4, the second-agent design §3): the reviewer chosen. A walk over the rule's reviewers, in the person's
/// order, tries another maker's agent first, then one whose maker is not declared, then the working agent's own family, and
/// calls the account walk unchanged for each (<see cref="HarnessRoster.SelectAsync"/>): the workspace's scope, cool-offs,
/// sign-ins, the kept account and pins. Where no other maker's agent can read the work it says why, with a code (§3.3).
/// </summary>
/// <remarks>
/// Nothing here starts a process. Each agent is present by a file look (<see cref="HarnessToolchain.ProbeByPresence"/>) on a
/// command this test writes, and is asked nobody's sign-in; its owner, product and maker are copied from the declaration this
/// build carries (<see cref="AdapterSet.Built"/>), so the families are the real ones.
/// </remarks>
public sealed class ReviewerChoiceTests : IDisposable
{
    private const string Another = ReviewerLabels.AnotherMaker;
    private const string NoMaker = ReviewerLabels.MakerNotDeclared;
    private const string Same = ReviewerLabels.SameAgent;
    private const string NoReviewer = ReviewerUnavailable.NoReviewer;
    private const string Cooling = ReviewerUnavailable.Cooling;
    private const string SignedOut = ReviewerUnavailable.SignedOut;
    private const string NotIndependent = ReviewerUnavailable.NotIndependent;
    private const string Refused = ReviewerUnavailable.Refused;

    private const string Nowhere = "daoris-xagent1b-no-such-command";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(2026, 10, 9, 9, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 10, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-reviewer-" + Guid.NewGuid().ToString("N")[..8]);

    public ReviewerChoiceTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    /// <summary>
    /// The agent this build carries by <paramref name="name"/>, its family as declared (whose accounts, its product, its maker)
    /// and nothing else: present by a file look, or absent where <paramref name="binary"/> names nothing.
    /// </summary>
    private static HarnessToolchain Like(string name, string binary)
    {
        var real = AdapterSet.Built().Resolve(name).Toolchain!;
        return new HarnessToolchain(
            Binary: [binary], VersionArguments: [], ProfileVariable: real.ProfileVariable, AccountOf: real.AccountOf,
            ProbeByPresence: true, Product: real.Product, Maker: real.Maker);
    }

    private static readonly string[] Agents = ["claude-code", "claude-code-acp", "codex-acp", "dsh", "stub"];

    /// <summary>
    /// The roster: each of <see cref="Agents"/>, those in <paramref name="absent"/> installed nowhere and those in
    /// <paramref name="bare"/> run by a bare name a pin resolves; and every plugin under the home.
    /// </summary>
    private HarnessRoster Roster(string[]? absent = null, string[]? bare = null)
    {
        var adapters = new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Agents)
        {
            var binary = absent?.Contains(name) == true ? Nowhere : bare?.Contains(name) == true ? name + "-pinned" : Command;
            adapters[name] = new Adapter(name, Like(name, binary));
        }

        var set = new AdapterSet(adapters).WithPlugins(PluginCatalog.Load(_home, adapters.Keys));
        return new HarnessRoster(set, Settings) { Clock = () => Now, Zone = Zone };
    }

    private void Wire(Func<HarnessSettings, HarnessSettings> edit) => edit(new HarnessSettings()).Save(Settings);

    private void Account(string agent, string account) => Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, account));

    private CoolingEntry Cool(string agent, string? account = null)
    {
        var entry = new CoolingEntry(agent, account, Until, true, "weekly", Now.AddHours(-1), "s1");
        AccountCooling.Cool(_home, entry, Now);
        return entry;
    }

    /// <summary>A plugin declaring one agent, with the product and maker it says, if any; present by a file look.</summary>
    private void Plugin(string id, string agent, string? maker = null, string? product = null)
    {
        var folder = Path.Combine(_home, "plugins", id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "agent.mjs"), "");
        var said = (product is null ? "" : $", \"product\": \"{product}\"") + (maker is null ? "" : $", \"maker\": \"{maker}\"");
        File.WriteAllText(
            Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "harnesses": [ { "name": "{{agent}}", "command": ["${plugin}/agent.mjs"]{{said}} } ] }""");
    }

    private static DriverConfig Config => DriverConfig.Empty with { Adapter = "claude-code" };

    private static OpinionRule Rule(string reviewers) => new([.. reviewers.Split(',')], [OpinionRules.Landing]);

    private static Task<ReviewerChoice> Choose(HarnessRoster roster, string working, string reviewers, string? workspace = null) =>
        ReviewerChoice.ChooseAsync(roster, Rule(reviewers), working.Split(','), Config, workspace);

    // ——— The table (§3.1–§3.3).

    /// <summary>What each row needs on this machine before it is asked, and the roster it is asked of.</summary>
    private HarnessRoster Arrange(string row)
    {
        switch (row)
        {
            case "another account of the same agent":
                Account("claude-code", "first");
                Account("claude-code", "second");
                Wire(s => s.WithDefault("claude-code", "second"));
                return Roster();
            case "cooling, then the next":
            case "cooling, and no other":
                Cool("codex");
                return Roster();
            case "signed out":
                Account("codex", "work");
                Wire(s => s.WithDefault("codex", "work"));
                var roster = Roster();
                roster.SignedOut("codex-acp", "work");
                return roster;
            case "cooling and signed out":
                Cool("codex");
                Account("dsh", "work");
                Wire(s => s.WithDefault("dsh", "work"));
                var mixed = Roster();
                mixed.SignedOut("dsh", "work");
                return mixed;
            case "a pin nobody installed, then the next":
            case "a pin nobody installed, and no other":
                Wire(s => s.WithVersion("codex-acp", "1.0.0"));
                return Roster(bare: ["codex-acp"]);
            case "a plugin declaring another maker":
                Plugin("acme.agent", "acme-agent", maker: "Acme", product: "Acme Agent");
                return Roster();
            case "a plugin declaring the same maker":
                Plugin("kin.agent", "kin-agent", maker: "anthropic");
                return Roster();
            case "a plugin declaring no maker":
                Plugin("bare.agent", "bare-agent");
                return Roster();
            case "the same agent by name, no other installed":
            case "none installed":
                return Roster(absent: ["codex-acp", "dsh"]);
            case "the same agent only, cooling":
                Cool("claude-code");
                return Roster();
            default:
                return Roster();
        }
    }

    [Theory]
    // Another maker's agent: read by the rule's first reviewer, with nothing more to say.
    [InlineData("another maker's agent", "claude-code", "codex-acp", "codex-acp", Another, null)]
    // Both Claude Code doors are one agent (AGT7's owner, and one maker), either way round.
    [InlineData("both Claude doors", "claude-code", "claude-code-acp", "claude-code-acp", Same, NotIndependent)]
    [InlineData("both Claude doors, the other way", "claude-code-acp", "claude-code", "claude-code", Same, NotIndependent)]
    // Listed first, the same agent still waits for every other maker's agent.
    [InlineData("the same agent listed first", "claude-code", "claude-code-acp,codex-acp", "codex-acp", Another, null)]
    // Another account is a credential, not a second reader.
    [InlineData("another account of the same agent", "claude-code", "claude-code", "claude-code", Same, NotIndependent)]
    // A maker not declared is never taken as independent: listed first, it waits for one that is.
    [InlineData("no maker, alone", "claude-code", "stub", "stub", NoMaker, NotIndependent)]
    [InlineData("no maker, listed first", "claude-code", "stub,codex-acp", "codex-acp", Another, null)]
    // The account walk's holds, each passed for the next entry, or said.
    [InlineData("cooling, then the next", "claude-code", "codex-acp,dsh", "dsh", Another, null)]
    [InlineData("cooling, and no other", "claude-code", "codex-acp", null, null, Cooling)]
    [InlineData("signed out", "claude-code", "codex-acp", null, null, SignedOut)]
    [InlineData("cooling and signed out", "claude-code", "codex-acp,dsh", null, null, Refused)]
    [InlineData("a pin nobody installed, then the next", "claude-code", "codex-acp,dsh", "dsh", Another, null)]
    [InlineData("a pin nobody installed, and no other", "claude-code", "codex-acp", null, null, Refused)]
    // A plugin's agent counts by the maker it declares, as the built-in ones do.
    [InlineData("a plugin declaring another maker", "claude-code", "acme-agent", "acme-agent", Another, null)]
    [InlineData("a plugin declaring the same maker", "claude-code", "kin-agent", "kin-agent", Same, NotIndependent)]
    [InlineData("a plugin declaring no maker", "claude-code", "bare-agent", "bare-agent", NoMaker, NotIndependent)]
    // The same family by name, taken when no other can run, and the code says why the others could not.
    [InlineData("the same agent by name, no other installed", "claude-code", "codex-acp,dsh,claude-code-acp", "claude-code-acp", Same, NoReviewer)]
    // None available.
    [InlineData("none installed", "claude-code", "codex-acp,dsh", null, null, NoReviewer)]
    [InlineData("no such agent", "claude-code", "nobody-here", null, null, NoReviewer)]
    [InlineData("the same agent only, cooling", "claude-code", "claude-code-acp", null, null, NotIndependent)]
    public async Task The_reviewer_chosen(string row, string working, string reviewers, string? reviewer, string? label, string? code)
    {
        var choice = await Choose(Arrange(row), working, reviewers);

        Assert.Equal((reviewer, label, code), (choice.Reviewer, choice.Label, choice.Code));
        Assert.Equal(reviewer is not null, choice.Chosen);
        Assert.Equal(reviewer is not null, choice.Selection is { Allowed: true });
    }

    // ——— Families (§3.1): by owner (AGT7) and by maker, as this build declares them.

    [Theory]
    [InlineData("claude-code", "claude-code-acp", true)]
    [InlineData("Claude-Code", "claude-code-acp", true)]
    [InlineData("claude-code", "codex-acp", false)]
    [InlineData("codex-acp", "dsh", false)]
    [InlineData("stub", "acp-stub", true)]
    [InlineData("stub", "claude-code", false)]
    [InlineData("nobody-here", "nobody-here", true)]
    [InlineData("nobody-here", "claude-code", false)]
    public void One_family_is_one_owner_or_one_declared_maker(string a, string b, bool one)
    {
        var built = AdapterSet.Built();

        Assert.Equal(one, AgentFamily.Of(built, a).Same(AgentFamily.Of(built, b)));
        Assert.Equal(one, OpinionRules.OneFamily(a, b));
    }

    [Fact]
    public void A_family_names_its_owner_product_and_maker_and_a_maker_not_declared_is_none()
    {
        var built = AdapterSet.Built();

        Assert.Equal(new AgentFamily("claude-code-acp", "claude-code", "Claude Code", "Anthropic"), AgentFamily.Of(built, "claude-code-acp"));
        Assert.Equal(new AgentFamily("codex-acp", "codex", "Codex", "OpenAI"), AgentFamily.Of(built, "codex-acp"));
        Assert.Equal(new AgentFamily("acp-stub", "stub", null, null), AgentFamily.Of(built, "acp-stub"));
        Assert.Equal(new AgentFamily("nobody-here", "nobody-here", null, null), AgentFamily.Of(built, " nobody-here "));
    }

    [Fact]
    public async Task A_plugin_s_maker_is_its_plugin_s_word_and_said_as_such()
    {
        Plugin("acme.agent", "acme-agent", maker: "Acme", product: "Acme Agent");

        var choice = await Choose(Roster(), "claude-code", "acme-agent");

        Assert.Equal(new AgentFamily("acme-agent", "acme-agent", "Acme Agent", "Acme", "acme.agent"), choice.Family);
        Assert.Equal(
            "`acme-agent` reads it: Acme Agent (Acme, as its plugin `acme.agent` declares), another maker's agent than the one "
            + "that did the work, Claude Code (Anthropic).",
            choice.Sentence);
    }

    [Fact]
    public async Task The_working_families_are_named_once_each_whichever_door_wrote()
    {
        var choice = await Choose(Roster(), "claude-code,claude-code-acp,dsh", "codex-acp");

        Assert.Equal(["claude-code", "dsh"], choice.Working.Select(family => family.Owner));
        Assert.Equal(
            "`codex-acp` reads it: Codex (OpenAI), another maker's agent than the one that did the work, Claude Code (Anthropic) "
            + "and DeepSeek Harness (DeepSeek).",
            choice.Sentence);
    }

    // ——— What is said (§3.3, §3.4): never downgraded silently.

    [Fact]
    public async Task The_same_agent_is_labelled_and_says_why_no_other_maker_s_could_read_it()
    {
        var choice = await Choose(Roster(absent: ["codex-acp"]), "claude-code", "codex-acp,claude-code-acp");

        Assert.Equal(("claude-code-acp", Same, NoReviewer), (choice.Reviewer, choice.Label, choice.Code));
        Assert.StartsWith(
            "`claude-code-acp` reads it: Claude Code (Anthropic), the same agent as the one that did the work, in a fresh "
            + "conversation: not an independent reading. No other maker's agent could read it: no listed reviewer of another "
            + "maker is installed. `codex-acp`: `codex-acp` is not installed on this machine",
            choice.Sentence);
        var tried = Assert.Single(choice.Tried);
        Assert.Equal(("codex-acp", Another, ReviewerHeld.NotInstalled), (tried.Reviewer, tried.Label, tried.Held));
    }

    [Fact]
    public async Task A_maker_not_declared_says_so_and_why_no_other_maker_s_agent_read_it()
    {
        var choice = await Choose(Roster(), "claude-code", "stub");

        Assert.Equal(
            "`stub` reads it: `stub` (maker not declared), whose reading is not counted as another maker's. No other maker's "
            + "agent could read it: no listed reviewer is another maker's agent.",
            choice.Sentence);
    }

    [Fact]
    public async Task None_installed_says_no_second_opinion_naming_each_reviewer()
    {
        var choice = await Choose(Roster(absent: ["codex-acp", "dsh"]), "claude-code", "codex-acp,dsh,nobody-here");

        Assert.Null(choice.Reviewer);
        Assert.Null(choice.Selection);
        Assert.StartsWith("No second opinion: no listed reviewer of another maker is installed. `codex-acp`: ", choice.Sentence);
        Assert.Contains(" `dsh`: `dsh` is not installed on this machine", choice.Sentence);
        Assert.EndsWith(
            " `nobody-here`: `nobody-here` is no agent on this machine: neither this build nor an installed plugin declares it.",
            choice.Sentence);
        Assert.Equal(
            [("codex-acp", ReviewerHeld.NotInstalled), ("dsh", ReviewerHeld.NotInstalled), ("nobody-here", ReviewerHeld.NotInstalled)],
            choice.Tried.Select(tried => (tried.Reviewer, tried.Held)));
    }

    [Fact]
    public async Task Cooling_is_said_with_each_entry_s_hold_and_the_first_reset()
    {
        var codex = Cool("codex");

        var choice = await Choose(Roster(), "claude-code", "codex-acp");

        Assert.Equal(codex, choice.Cooling);
        var tried = Assert.Single(choice.Tried);
        Assert.Equal((ReviewerHeld.Cooling, codex), (tried.Held, tried.Cooling));
        Assert.Equal(
            $"No second opinion: every listed reviewer of another maker is cooling. `codex-acp`: {CoolingWords.Hold(codex, Zone)}",
            choice.Sentence);
    }

    [Fact]
    public async Task Passed_for_cooling_the_next_reviewer_reads_and_the_entry_passed_is_kept()
    {
        Cool("codex");

        var choice = await Choose(Roster(), "claude-code", "codex-acp,dsh");

        Assert.Equal(("dsh", (string?)null), (choice.Reviewer, choice.Code));
        Assert.Equal([("codex-acp", ReviewerHeld.Cooling)], choice.Tried.Select(tried => (tried.Reviewer, tried.Held)));
        // Another maker's agent read it: nothing was downgraded, so nothing waits on the reset.
        Assert.Null(choice.Cooling);
    }

    [Fact]
    public async Task Signed_out_names_the_sign_in()
    {
        Account("codex", "work");
        Wire(s => s.WithDefault("codex", "work"));
        var roster = Roster();
        roster.SignedOut("codex-acp", "work");

        var choice = await Choose(roster, "claude-code", "codex-acp");

        Assert.Equal(ReviewerHeld.SignedOut, Assert.Single(choice.Tried).Held);
        Assert.StartsWith("No second opinion: no listed reviewer of another maker has an account signed in. `codex-acp`: ", choice.Sentence);
        Assert.Contains("daoris agent login codex --profile work", choice.Sentence);
    }

    [Fact]
    public async Task A_pin_nobody_installed_is_refused_naming_the_pin_and_never_runs_PATH_s()
    {
        Wire(s => s.WithVersion("codex-acp", "1.0.0"));

        var choice = await Choose(Roster(bare: ["codex-acp"]), "claude-code", "codex-acp");

        Assert.Equal(ReviewerHeld.PinNotInstalled, Assert.Single(choice.Tried).Held);
        Assert.StartsWith("No second opinion: every listed reviewer of another maker was refused. `codex-acp`: ", choice.Sentence);
        Assert.Contains("`daoris agent pin codex-acp 1.0.0` installs it", choice.Sentence);
    }

    [Fact]
    public async Task The_same_agent_only_and_held_says_it_is_not_independent_and_why_it_could_not_run_either()
    {
        var claude = Cool("claude-code");

        var choice = await Choose(Roster(), "claude-code", "claude-code-acp");

        Assert.Equal(claude, choice.Cooling);
        Assert.Equal(
            $"No second opinion: no listed reviewer is another maker's agent. `claude-code-acp`: {CoolingWords.Hold(claude, Zone)}",
            choice.Sentence);
        Assert.Equal([("claude-code-acp", Same, ReviewerHeld.Cooling)], choice.Tried.Select(tried => (tried.Reviewer, tried.Label, tried.Held)));
    }

    // ——— The account walk, unchanged (§3.2 step 2): what it preserves, a reviewer gets.

    [Fact]
    public async Task A_reviewer_runs_on_the_account_its_agent_s_scope_gives_the_workspace()
    {
        Account("codex", "home");
        Account("codex", "office");
        Wire(s => s.WithDefault("codex", "home").WithWorkspaceDefault("work", "codex", "office"));

        var machine = await Choose(Roster(), "claude-code", "codex-acp");
        var work = await Choose(Roster(), "claude-code", "codex-acp", workspace: "work");

        Assert.Equal("home", machine.Selection!.Profile);
        Assert.Equal("office", work.Selection!.Profile);
    }

    [Fact]
    public async Task A_reviewer_never_runs_on_the_account_kept_for_conversations()
    {
        Account("codex", "account-1");
        Account("codex", "account-2");
        Wire(s => s.WithRotation("codex", ["account-1", "account-2"]).WithUse("codex", new UseChange(Keep: "account-2")));
        Cool("codex", "account-1");

        var choice = await Choose(Roster(), "claude-code", "codex-acp");

        Assert.Equal(Cooling, choice.Code);
        Assert.EndsWith(" `account-2` is kept for conversations.", choice.Sentence);
    }

    [Fact]
    public async Task A_reviewer_runs_its_pinned_version_where_one_is_installed_and_PATH_s_where_none_is_pinned()
    {
        var managed = Path.Combine(
            HarnessSettings.ManagedHome(_home, "codex-acp", "1.0.0"), "bin", OperatingSystem.IsWindows() ? "codex-acp-pinned.exe" : "codex-acp-pinned");
        Directory.CreateDirectory(Path.GetDirectoryName(managed)!);
        File.WriteAllText(managed, "");
        Wire(s => s.WithVersion("codex-acp", "1.0.0"));

        var pinned = await Choose(Roster(bare: ["codex-acp"]), "claude-code", "codex-acp");
        var unpinned = await Choose(Roster(), "claude-code", "dsh");

        Assert.Equal(managed, pinned.Selection!.Binary);
        Assert.True(unpinned.Selection!.Allowed);
        Assert.Null(unpinned.Selection.Binary);
    }

    /// <summary>The walk says why the agent itself could not run, beside its sentence, so the choice reads a fact, not words.</summary>
    [Fact]
    public async Task The_account_walk_says_when_the_agent_itself_is_not_there()
    {
        Wire(s => s.WithVersion("codex-acp", "1.0.0"));
        var roster = Roster(absent: ["dsh"], bare: ["codex-acp"]);

        var absent = await roster.SelectAsync("dsh", Config, null, null);
        var pin = await roster.SelectAsync("codex-acp", Config, null, null);
        var here = await roster.SelectAsync("claude-code", Config, null, null);

        Assert.Equal(AgentAbsence.NotInstalled, absent.Absent);
        Assert.Equal(AgentAbsence.PinNotInstalled, pin.Absent);
        Assert.Null(here.Absent);
    }
}
