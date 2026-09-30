using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's proposals, the driver's half (HELP1c, D89): the file the connector wrote, read here and
/// judged with the route's own code before the person sees it — what it changes, the command that does
/// the same (D50), or the route's refusal — then settled by the person's press.
/// </summary>
public sealed class HelpProposalsTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-plans-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpProposalsTests() => Directory.CreateDirectory(HelpProposals.FolderOf(_home));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly HelpMachineFacts Facts = new(
        Repositories: ["engine", "game"], Workspaces: ["default", "work"], Agents: ["claude-code", "claude-code-acp"]);

    /// <summary>A file as the service's `HelpProposalBox` writes it — the twin's shape.</summary>
    private string File(string id, string kind, string door, string? target = null, string? workspace = null,
        string? value = null, string? sentence = null, string session = "h1", string state = "proposed")
    {
        var node = new JsonObject
        {
            ["id"] = id, ["proposed"] = "2026-09-29T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = session },
            ["kind"] = kind, ["door"] = door, ["target"] = target, ["workspace"] = workspace, ["value"] = value,
            ["sentence"] = sentence, ["why"] = "the person asked", ["state"] = state, ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), $"{id}.json"), node.ToJsonString());
        return id;
    }

    private static HelpProposal Setting(string door, string? target = null, string? workspace = null, string? value = null) =>
        new("p1", "setting", door, target, workspace, value, null, "why", "h1", "proposed");

    [Theory]
    [InlineData("drive", "engine", null, null, "daoris driver drive engine", "Drive `engine`")]
    [InlineData("hold", "game", null, null, "daoris driver hold game", "Hold `game`")]
    [InlineData("trees", "engine", null, "on", "daoris driver trees engine on", "own tree")]
    [InlineData("line", "engine", null, "develop", "daoris driver line engine develop", "`engine`'s line to `develop`")]
    [InlineData("line", null, "work", "--clear", "daoris driver line --workspace work --clear", "Clear workspace `work`'s line")]
    [InlineData("landing", null, "work", "branch feature/{quest}-{slug} --tidy", "daoris driver landing --workspace work branch feature/{quest}-{slug} --tidy", "on a branch `feature/{quest}-{slug}`")]
    [InlineData("intake", null, null, "off", "daoris driver intake off", "Stop answering asks")]
    [InlineData("helper", null, null, "claude-code", "daoris driver helper claude-code", "`claude-code`")]
    [InlineData("strikes", null, null, "5", "daoris driver strikes 5", "5 failed sessions")]
    [InlineData("timeout", null, null, "120", "daoris driver timeout 120", "120 minutes")]
    [InlineData("notify", null, null, "off", "daoris driver notify off", "Stop saying")]
    public void A_setting_is_planned_as_what_it_changes_and_the_command_that_does_the_same(
        string door, string? target, string? workspace, string? value, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
        Assert.NotNull(plan.Apply);
    }

    [Fact]
    public void Applying_a_plan_makes_the_edit_the_screens_route_makes()
    {
        var config = DriverConfig.Empty;
        config = HelpProposals.Plan(Setting("drive", "engine"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("landing", null, "work", "branch feature/{quest}-{slug} --tidy"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("timeout", value: "120"), config, Facts).Apply!(config);

        Assert.Contains("engine", config.Drivable);
        Assert.Equal(new LandingRule("branch", "feature/{quest}-{slug}", Tidy: true), config.WorkspaceLandings["work"]);
        Assert.Equal(120, config.TimeoutMinutes);
    }

    /// <summary>What the route would refuse is refused here in its own words, and never shown to the person.</summary>
    [Theory]
    [InlineData("drive", "nowhere", null, null, "`nowhere` is not registered")]
    [InlineData("line", null, "elsewhere", "develop", "no workspace `elsewhere`")]
    [InlineData("line", "engine", null, "bad..name", "not a branch name git would take")]
    [InlineData("landing", "engine", null, "branch feature/fixed", "`{quest}` or `{session}`")]
    [InlineData("intake", null, null, "gpt-agent", "no agent `gpt-agent`")]
    public void What_the_route_would_refuse_is_refused_in_its_words(
        string door, string? target, string? workspace, string? value, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    /// <summary>A plugin installed under the test's home, as `daoris plugin add` leaves one (D64).</summary>
    private HelpMachineFacts WithPlugin(string id, string points = "\"work/land\"", bool enabled = true)
    {
        var folder = Path.Combine(_home, PluginCatalog.Folder, id);
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": [{{points}}] } }""");
        if (!enabled) PluginState.Disable(_home, id);
        return Facts with { Plugins = PluginCatalog.Load(_home) };
    }

    /// <summary>HELP8: a landing rule naming a plugin (D100), proposed the way the terminal spells it.</summary>
    [Theory]
    [InlineData("branch feature/{quest}-{slug} --plugin example.lands --tidy")]
    [InlineData("branch feature/{quest}-{slug} --tidy --plugin example.lands")]
    public void A_landing_may_name_a_plugin_that_lands_work_here(string value)
    {
        var facts = WithPlugin("example.lands");

        var plan = HelpProposals.Plan(Setting("landing", null, "work", value), DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal($"daoris driver landing --workspace work {value}", plan.Terminal);
        Assert.Contains("on a branch `feature/{quest}-{slug}`", plan.Describe);
        Assert.Contains("plugin `example.lands` pushes it and opens the pull request", plan.Describe);
        Assert.Equal(new LandingRule("branch", "feature/{quest}-{slug}", Tidy: true, Plugin: "example.lands"),
            plan.Apply!(DriverConfig.Empty).WorkspaceLandings["work"]);
    }

    /// <summary>HELP8: what `daoris driver landing` refuses about a plugin, refused here in the same words.</summary>
    [Theory]
    [InlineData("branch feature/{quest} --plugin nowhere.lands", "", "not installed on this machine")]
    [InlineData("branch feature/{quest} --plugin example.off", "off", "switched off")]
    [InlineData("branch feature/{quest} --plugin example.quiet", "quiet", "does not land work")]
    [InlineData("merge --plugin example.lands", "", "only a branch rule hands its work to a plugin")]
    [InlineData("branch feature/{quest} --plugin", "", "`--plugin` needs the id of an installed plugin")]
    [InlineData("branch feature/{quest} --plugin Not/An/Id", "", "is not a plugin id")]
    public void A_landing_plugin_the_route_would_refuse_is_refused_in_its_words(string value, string install, string says)
    {
        var facts = WithPlugin("example.lands");
        if (install == "off") facts = WithPlugin("example.off", enabled: false);
        if (install == "quiet") facts = WithPlugin("example.quiet", points: "\"session/ended\"");

        var plan = HelpProposals.Plan(Setting("landing", "engine", null, value), DriverConfig.Empty, facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    [Fact]
    public void An_ask_is_planned_as_the_ask_door_and_its_terminal_twin()
    {
        var ask = new HelpProposal("p2", "ask", "ask", null, "work", null, "fix the cold-cache stall", "why", "h1", "proposed");

        var plan = HelpProposals.Plan(ask, DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver ask --workspace work \"fix the cold-cache stall\"", plan.Terminal);
        Assert.Contains("fix the cold-cache stall", plan.Describe);
        Assert.Null(plan.Apply); // an ask is made through the ask door, not an edit to the file
        Assert.Contains("no workspace `nope`", HelpProposals.Plan(ask with { Workspace = "nope" }, DriverConfig.Empty, Facts).Refusal);
    }

    [Fact]
    public void Pending_proposals_are_read_for_one_conversation_and_a_settled_one_is_not_pending()
    {
        File("a1", "setting", "drive", target: "engine");
        File("a2", "ask", "ask", workspace: "work", sentence: "start it", session: "other");
        File("a3", "setting", "hold", target: "game", state: "applied");
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "broken.json"), "{ not json");

        var pending = HelpProposals.Pending(_home, "h1");

        var only = Assert.Single(pending);
        Assert.Equal(("a1", "drive", "engine"), (only.Id, only.Door, only.Target));

        HelpProposals.Settle(_home, "a1", "dismissed", "not now");
        Assert.Empty(HelpProposals.Pending(_home, "h1"));
        var settled = JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(HelpProposals.FolderOf(_home), "a1.json")))!;
        Assert.Equal("dismissed", settled["state"]!.GetValue<string>());
        Assert.Equal("not now", settled["note"]!.GetValue<string>());
        Assert.Equal("the person asked", settled["why"]!.GetValue<string>());
    }

    /// <summary>A twin (`twins.md`): the service's `HelpProposalBox.FolderOf` names the same folder.</summary>
    [Fact]
    public void The_folder_is_the_services_twin()
    {
        Assert.Equal(Path.Combine(_home, "help", "proposals"), HelpProposals.FolderOf(_home));
    }

    // ── HELP6: the kinds that reach every door built since ─────────────────────────────────────────

    /// <summary>
    /// The machine as the Agents screen, the quest drawer and the ask's record read it: each door's roster
    /// row, and each record with the service's own <c>deletable</c>.
    /// </summary>
    private static readonly HelpMachineFacts Machine = Facts with
    {
        Doors =
        [
            new HelpDoorFacts("claude-code")
            {
                Present = true, Updates = "tool", Channel = "claude-code-releases", Owner = "claude-code",
                Accounts = ["work"], SettingsKnown = true, Product = "Claude Code",
            },
            new HelpDoorFacts("claude-code-acp")
            {
                Present = true, Updates = "pin", Pinned = "0.84.0", Package = "@agentclientprotocol/claude-agent-acp",
                Owner = "claude-code", Accounts = ["work"], SettingsKnown = true,
            },
            new HelpDoorFacts("dsh") { Present = true, Owner = "dsh", Product = "DeepSeek" },
            new HelpDoorFacts("codex-acp") { Present = false, Updates = "tool", Package = "@zed-industries/codex-acp", Owner = "codex-acp" },
        ],
        Quests =
        [
            new HelpQuestFacts("q1a2b3c4", "Cap the chunk budget", "engine", "Open", Deletable: true),
            new HelpQuestFacts("q2taken0", "Stream the tiles", "engine", "Taken", Deletable: false),
            new HelpQuestFacts("q3done00", "Fix the stall", "game", "Done", Deletable: false),
            new HelpQuestFacts("q4declin", "Rewrite it all", "game", "Declined", Deletable: false),
            new HelpQuestFacts("q5start0", "Verify the fix", "game", "Open", Deletable: false),
        ],
        Asks =
        [
            new HelpAskFacts("a1b2c3d4", "fix the chunk streamer's stall", "work", "Published", ["q1a2b3c4"], Deletable: true),
            new HelpAskFacts("a2none00", "a test ask", "work", "Open", [], Deletable: true),
            new HelpAskFacts("a3kept00", "stream the tiles", "work", "Published", ["q2taken0", "q1a2b3c4"], Deletable: false),
        ],
    };

    private static HelpProposal Of(string kind, string door, string? target = null, string? value = null) =>
        new("p6", kind, door, target, null, value, null, "the person asked", "h1", "proposed");

    [Theory]
    [InlineData("update", "claude-code", null, "daoris agent update claude-code", "its own updater")]
    [InlineData("update", "claude-code-acp", null, "daoris agent update claude-code-acp", "from 0.84.0 to the newest release")]
    [InlineData("pin", "claude-code", "2.1.300", "daoris agent pin claude-code 2.1.300", "Pin `claude-code` to 2.1.300")]
    [InlineData("pin", "claude-code-acp", "0.85.1", "daoris agent pin claude-code-acp 0.85.1", "to 0.85.1")]
    public void An_agent_is_updated_or_pinned_where_the_agents_screen_offers_it(
        string action, string agent, string? version, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Of("agent", action, agent, version), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    /// <summary>What the Agents screen would not offer, or its route would refuse, is refused in the route's words.</summary>
    [Theory]
    [InlineData("update", "dsh", null, "offers no Update")]
    [InlineData("update", "codex-acp", null, "not installed")]
    [InlineData("update", "gpt-agent", null, "no agent `gpt-agent`")]
    [InlineData("pin", "dsh", "1.0.0", "no package or release channel")]
    [InlineData("pin", "claude-code", "latest", "never a pointer such as latest")]
    [InlineData("pin", "claude-code", "2.0.1", "before its release manifests were signed")]
    [InlineData("pin", "claude-code-acp", "latest", "one exact release")]
    public void An_agent_action_the_route_would_refuse_is_refused(string action, string agent, string? version, string says)
    {
        var plan = HelpProposals.Plan(Of("agent", action, agent, version), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    [Fact]
    public void A_quest_nobody_started_on_is_deleted_and_the_card_says_what_goes()
    {
        var plan = HelpProposals.Plan(Of("delete", "quest", "q1a2b3c4"), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris-driver quest delete q1a2b3c4", plan.Terminal);
        Assert.Contains("Delete quest `#q1a2b3c4`", plan.Describe);
        Assert.Contains("Cap the chunk budget", plan.Describe);
    }

    /// <summary>
    /// A taken, done or declined quest keeps its record (D95), and so does an open one a session started
    /// on: never shown to the person, and the refusal says what to do instead.
    /// </summary>
    [Theory]
    [InlineData("q2taken0", "is taken", "Decline it")]
    [InlineData("q3done00", "is done", "already leaves the list")]
    [InlineData("q4declin", "is declined", "already leaves the list")]
    [InlineData("q5start0", "was started on", "Decline it")]
    [InlineData("q9none00", "no quest `#q9none00`", "")]
    public void A_quest_anything_stands_on_is_never_proposed_for_deleting(string quest, string says, string instead)
    {
        var plan = HelpProposals.Plan(Of("delete", "quest", quest), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Contains(instead, plan.Refusal);
    }

    [Fact]
    public void An_ask_goes_with_its_untaken_quests_or_alone_and_one_that_must_stay_is_refused()
    {
        var with = HelpProposals.Plan(Of("delete", "ask", "a1b2c3d4"), DriverConfig.Empty, Machine);
        var alone = HelpProposals.Plan(Of("delete", "ask", "a2none00"), DriverConfig.Empty, Machine);
        var kept = HelpProposals.Plan(Of("delete", "ask", "a3kept00"), DriverConfig.Empty, Machine);

        Assert.Null(with.Refusal);
        Assert.Equal("daoris-driver ask --delete a1b2c3d4", with.Terminal);
        Assert.Contains("with the quest it became: `#q1a2b3c4` “Cap the chunk budget”", with.Describe);
        Assert.Contains("goes alone", alone.Describe);
        Assert.Contains("must stay", kept.Refusal);
        Assert.Contains("close the ask instead", kept.Refusal);
        Assert.Contains("no ask `#a9`", HelpProposals.Plan(Of("delete", "ask", "a9"), DriverConfig.Empty, Machine).Refusal);
    }

    private static HelpProposal Account(string agent, string? account, string? model, string? effort) =>
        Of("account", "settings", agent) with { Account = account, Model = model, Effort = effort };

    [Theory]
    [InlineData("claude-code", "work", "opus", "high", "daoris agent settings claude-code --account work model opus effort high", "model to `opus` and its effort to `high`")]
    [InlineData("claude-code-acp", "work", null, "xhigh", "daoris agent settings claude-code --account work effort xhigh", "`claude-code` account `work`")]
    [InlineData("claude-code", "work", "claude-opus-5", null, "daoris agent settings claude-code --account work model claude-opus-5", "model to `claude-opus-5`")]
    [InlineData("claude-code", "work", "unset", "unset", "daoris agent settings claude-code --account work model unset effort unset", "the tool's own default")]
    public void An_accounts_model_and_effort_are_planned_in_the_tools_own_words(
        string agent, string account, string? model, string? effort, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Account(agent, account, model, effort), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
    }

    [Theory]
    [InlineData("claude-code", "work", null, "max", "one session")]
    [InlineData("claude-code", "work", null, "extreme", "not an effort the tool's settings keep")]
    [InlineData("claude-code", "work", "two words", null, "not a model name")]
    [InlineData("claude-code", "play", "opus", null, "no account `play`")]
    [InlineData("dsh", "work", "deepseek-chat", null, "Daoris offers none")]
    public void An_account_setting_the_route_would_refuse_is_refused(string agent, string account, string? model, string? effort, string says)
    {
        var plan = HelpProposals.Plan(Account(agent, account, model, effort), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
    }

    private static HelpProposal Go(string view, string? domain = null, string? part = null) =>
        Of("go", "go", view) with { Domain = domain, Part = part };

    [Theory]
    [InlineData("quests", null, null, "Open Quests.")]
    [InlineData("settings", "agents", null, "Open Settings → Agents & accounts.")]
    [InlineData("settings", "workspace", "lines", "Open Settings → Workspace → Lines.")]
    [InlineData("settings", "start", "helper", "Open Settings → Get started at step 2, Daoris's own agent.")]
    [InlineData("projects", null, "import", "Open Projects → Import a folder.")]
    public void A_screen_is_a_go_that_changes_nothing(string view, string? domain, string? part, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part), DriverConfig.Empty, Machine);

        Assert.Null(plan.Refusal);
        Assert.Equal(says, plan.Describe);
        Assert.Equal("", plan.Terminal);
        Assert.Null(plan.Apply);
        Assert.Equal(new HelpPlace(view, domain, part), plan.Go);
    }

    [Theory]
    [InlineData("dashboard", null, null, "no view `dashboard`")]
    [InlineData("settings", "billing", null, "no Settings domain `billing`")]
    [InlineData("settings", "workspace", "colours", "no part `colours`")]
    [InlineData("quests", null, "drawer", "no part `drawer`")]
    [InlineData("quests", "agents", null, "a domain is a part of Settings")]
    public void A_screen_the_window_does_not_have_is_refused(string view, string? domain, string? part, string says)
    {
        var plan = HelpProposals.Plan(Go(view, domain, part), DriverConfig.Empty, Machine);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Go);
    }

    /// <summary>
    /// The places a go may name, as the page's twin (`help/places.ts`) holds them: the same views, Settings
    /// domains and parts, the setup guide's steps among them. The page's test holds the same table.
    /// </summary>
    [Fact]
    public void The_places_are_the_pages_twin()
    {
        Assert.Equal(["overview", "sessions", "quests", "projects", "map", "convergence", "search", "settings"], HelpPlaces.Views.Select(view => view.Id));
        Assert.Equal(["start", "appearance", "ai", "workspace", "driver", "agents", "permissions", "plugins", "browser", "logs"], HelpPlaces.Domains.Select(domain => domain.Id));
        Assert.Equal(
            [
                "projects/add", "projects/import",
                "start/agent", "start/helper", "start/repositories", "start/driven", "start/landing", "start/rules",
                "workspace/wiring", "workspace/lines", "workspace/landing", "workspace/sweep",
                "agents/usage", "permissions/proposals",
            ],
            HelpPlaces.Parts.Select(part => $"{part.Within}/{part.Id}"));
    }

    // ── Applying through the screen's own door, each a stand-in that records the call ──────────────

    private sealed class Doors : IHelpDoors
    {
        public List<string> Calls { get; } = [];

        public (bool Ok, string Message) Deleted { get; set; } = (true, "Deleted it.");

        public DriverConfig Config { get; private set; } = DriverConfig.Empty;

        public void Change(Func<DriverConfig, DriverConfig> edit)
        {
            Config = edit(Config);
            Calls.Add("change");
        }

        public Task<AskAnswer> AskAsync(string workspace, string sentence, CancellationToken ct)
        {
            Calls.Add($"ask {workspace} {sentence}");
            return Task.FromResult(new AskAnswer(true, "Asked.", "a1", null));
        }

        public Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct)
        {
            Calls.Add($"DELETE /api/quests/{id}");
            return Task.FromResult(Deleted);
        }

        public Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct)
        {
            Calls.Add($"DELETE /api/asks/{id}");
            return Task.FromResult(Deleted);
        }

        public Action<int, string?>? Ended { get; private set; }

        /// <summary>An action that ends before its start is answered: a pin already installed does.</summary>
        public bool EndsAtOnce { get; init; }

        public Task StartAgentActionAsync(string harness, string action, string? version, Action<int, string?> ended, CancellationToken ct)
        {
            Calls.Add($"HARNESS_ACTION {harness} {action} {version}".TrimEnd());
            Ended = ended;
            if (EndsAtOnce) ended(0, null);
            return Task.CompletedTask;
        }

        public AgentSettingsRead SetAgentSettings(string harness, string account, AgentSettingEdit? model, AgentSettingEdit? effort)
        {
            string Said(AgentSettingEdit? edit) => edit is null ? "-" : edit.Value ?? "clear";
            Calls.Add($"SET_AGENT_SETTINGS {harness} {account} {Said(model)} {Said(effort)}");
            return new AgentSettingsRead(model?.Value, effort?.Value, [], null);
        }
    }

    private async Task<(HelpApplied Applied, Doors Doors, List<string> Later)> ApplyAsync(HelpProposal proposal, Doors? doors = null)
    {
        File(proposal.Id, proposal.Kind, proposal.Door, proposal.Target, proposal.Workspace, proposal.Value, proposal.Sentence);
        doors ??= new Doors();
        var later = new List<string>();
        var plan = HelpProposals.Plan(proposal, doors.Config, Machine);
        var applied = await HelpProposals.ApplyAsync(_home, proposal, plan, doors, later.Add, CancellationToken.None);
        return (applied, doors, later);
    }

    [Fact]
    public async Task A_setting_and_an_ask_are_applied_through_the_doors_they_always_were()
    {
        var (setting, doors, _) = await ApplyAsync(Setting("drive", "engine"));
        var (ask, asked, _) = await ApplyAsync(new HelpProposal("p7", "ask", "ask", null, "work", null, "start it", "why", "h1", "proposed"));

        Assert.True(setting.Applied);
        Assert.Equal(["change"], doors.Calls);
        Assert.Contains("engine", doors.Config.Drivable);
        Assert.Equal(["ask work start it"], asked.Calls);
        Assert.Contains("Applied", ask.Told);
    }

    [Fact]
    public async Task An_agent_update_or_pin_starts_the_agents_screens_own_action_and_its_end_is_said()
    {
        var (update, doors, later) = await ApplyAsync(Of("agent", "update", "claude-code-acp"));
        var (_, pinned, _) = await ApplyAsync(Of("agent", "pin", "claude-code", "2.1.300") with { Id = "p8" });

        Assert.True(update.Applied);
        Assert.Equal(["HARNESS_ACTION claude-code-acp update"], doors.Calls);
        Assert.Equal(["HARNESS_ACTION claude-code pin 2.1.300"], pinned.Calls);
        Assert.Contains("Started", update.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
        // What the Apply did goes into the conversation first…
        Assert.Equal([update.Told], later);

        // …and the action's end after it, however it ended.
        doors.Ended!(0, null);
        doors.Ended!(1, "the pointer did not answer");
        Assert.Contains("finished", later[1]);
        Assert.Contains("the pointer did not answer", later[2]);
    }

    /// <summary>A pin already installed ends before its start is answered: its end is still said second.</summary>
    [Fact]
    public async Task An_action_that_ends_at_once_is_said_after_what_the_Apply_did()
    {
        var (applied, _, later) = await ApplyAsync(Of("agent", "pin", "claude-code", "2.1.300"), new Doors { EndsAtOnce = true });

        Assert.Equal(2, later.Count);
        Assert.Equal(applied.Told, later[0]);
        Assert.Contains("finished", later[1]);
    }

    [Fact]
    public async Task A_delete_goes_through_the_hosts_own_route_and_its_refusal_is_said_in_the_services_words()
    {
        var (quest, doors, _) = await ApplyAsync(Of("delete", "quest", "q1a2b3c4"));
        var refusing = new Doors { Deleted = (false, "Quest `#a1b2c3d4` is Taken — someone is working it.") };
        var (ask, asked, _) = await ApplyAsync(Of("delete", "ask", "a1b2c3d4") with { Id = "p9" }, refusing);

        Assert.True(quest.Applied);
        Assert.Equal(["DELETE /api/quests/q1a2b3c4"], doors.Calls);
        Assert.Equal(["DELETE /api/asks/a1b2c3d4"], asked.Calls);
        Assert.False(ask.Applied);
        Assert.Contains("someone is working it", ask.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p9")!.State);
    }

    [Fact]
    public async Task An_accounts_settings_are_written_through_the_agents_screens_own_route()
    {
        var (set, doors, _) = await ApplyAsync(Account("claude-code-acp", "work", "opus", "unset"));

        Assert.True(set.Applied);
        Assert.Equal(["SET_AGENT_SETTINGS claude-code-acp work opus clear"], doors.Calls);
    }

    [Fact]
    public async Task A_go_changes_nothing_and_hands_the_page_the_place()
    {
        var (go, doors, _) = await ApplyAsync(Go("settings", "start", "helper"));

        Assert.True(go.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal(new HelpPlace("settings", "start", "helper"), go.Go);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
    }

    [Fact]
    public async Task A_refused_plan_calls_no_door_and_is_settled_refused()
    {
        var (refused, doors, _) = await ApplyAsync(Of("delete", "quest", "q2taken0"));

        Assert.False(refused.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal("refused", HelpProposals.Find(_home, "p6")!.State);
    }

    /// <summary>
    /// A delete is judged by the service's own <c>deletable</c>, read with every quest and ask, closed ones
    /// included — the same answer the drawer and the record show their Delete by. A host from before the
    /// field answers without it, which reads as not deletable: nothing is offered that might not go.
    /// </summary>
    [Fact]
    public async Task The_records_a_delete_is_judged_by_are_the_services_own_answer()
    {
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(new Records()));

        var (quests, asks) = await HelpProposals.RecordsAsync(client, CancellationToken.None);

        Assert.Equal(
            [new HelpQuestFacts("q1", "Cap it", "engine", "Open", true), new HelpQuestFacts("q2", "Old", "engine", "Done", false)],
            quests);
        var ask = Assert.Single(asks);
        Assert.Equal(("a1", "cap it", "work", "Closed", true), (ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Deletable));
        Assert.Equal(["q1"], ask.Quests);
    }

    /// <summary>The local host's two lists, as it answers them with closed records asked for.</summary>
    private sealed class Records : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.PathAndQuery switch
            {
                "/api/quests?includeClosed=true" => """
                    [ { "id": "q1", "from": "game", "to": "engine", "title": "Cap it", "body": "", "status": "Open", "deletable": true },
                      { "id": "q2", "from": "game", "to": "engine", "title": "Old", "body": "", "status": "Done" } ]
                    """,
                "/api/asks?includeClosed=true" => """
                    [ { "id": "a1", "workspace": "work", "sentence": "cap it", "state": "Closed", "tier": "named", "quests": ["q1"], "deletable": true } ]
                    """,
                _ => null,
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    /// <summary>The new kinds' fields, read from the file the service's box writes — the twin's shape.</summary>
    [Fact]
    public void The_new_kinds_fields_are_read_from_the_file()
    {
        var node = new JsonObject
        {
            ["id"] = "k1", ["proposed"] = "2026-09-30T10:00:00.0000000+00:00", ["by"] = new JsonObject { ["session"] = "h1" },
            ["kind"] = "account", ["door"] = "settings", ["target"] = "claude-code", ["workspace"] = null, ["value"] = null,
            ["sentence"] = null, ["account"] = "work", ["model"] = "opus", ["effort"] = "high",
            ["why"] = "the person asked", ["state"] = "proposed", ["note"] = null,
        };
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k1.json"), node.ToJsonString());
        node["id"] = "k2";
        node["kind"] = "go";
        node["door"] = "go";
        node["target"] = "settings";
        node.Remove("account");
        node.Remove("model");
        node.Remove("effort");
        node["domain"] = "start";
        node["part"] = "helper";
        System.IO.File.WriteAllText(Path.Combine(HelpProposals.FolderOf(_home), "k2.json"), node.ToJsonString());

        var account = HelpProposals.Find(_home, "k1")!;
        var go = Assert.Single(HelpProposals.Pending(_home, "h1"), proposal => proposal.Kind == "go");

        Assert.Equal(("work", "opus", "high"), (account.Account, account.Model, account.Effort));
        Assert.Equal(("start", "helper"), (go.Domain, go.Part));
        Assert.Null(go.Account);
    }
}
