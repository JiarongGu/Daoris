using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>setting</c> proposal (HELP1c): one of the `daoris driver` verbs, judged by the config's own edits.</summary>
public sealed class HelpSettingProposalsTests : HelpProposalsFixture
{
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
    // HELP9: reading and writing across (D107), and the two dials the terminal alone had.
    [InlineData("across", "engine", null, "read off", "daoris driver across engine read off", "read by no agent outside it")]
    [InlineData("across", "engine", null, "read on", "daoris driver across engine read on", "never a write")]
    [InlineData("across", null, "work", "read off", "daoris driver across --workspace work read off", "Each checkout in workspace `work` that sets none of its own")]
    [InlineData("across", "engine", null, "read --clear", "daoris driver across engine read --clear", "takes its workspace's reading again")]
    [InlineData("across", "game", null, "write-to engine", "daoris driver across game write-to engine", "your standing say-so")]
    [InlineData("across", "game", null, "write-to engine --clear", "daoris driver across game write-to engine --clear", "no longer write into `engine`")]
    [InlineData("cap", null, null, "3", "daoris driver cap 3", "at most 3 sessions at once")]
    [InlineData("adapter", null, null, "claude-code-acp", "daoris driver adapter claude-code-acp", "`claude-code-acp`")]
    // HELP10: a quest its strikes parked, started again as the drawer's Retry and `daoris driver retry` do, `#` or not.
    [InlineData("retry", "q1a2b3c4", null, null, "daoris driver retry q1a2b3c4", "`#q1a2b3c4` may be started again")]
    [InlineData("retry", "#q1a2b3c4", null, null, "daoris driver retry q1a2b3c4", "what already happened is still in the records")]
    // SESSUX1b (D126 §3.4): a quest the person's stop holds, released from that stop as Try again releases it.
    [InlineData("retry", "q2taken0", null, null, "daoris driver retry q2taken0 --session s7a8b9c0", "released from your stop of session `s7a8b9c0`")]
    public void A_setting_is_planned_as_what_it_changes_and_the_command_that_does_the_same(
        string door, string? target, string? workspace, string? value, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
        Assert.NotNull(plan.Apply);
    }

    /// <summary>
    /// KNOWUSE1b (D135 §3): a standing answer for a registered repository, planned in the person's words with the terminal's
    /// spelling, and applied as `SET_STANDING` and `daoris driver standing` make it; cleared with `--clear`. One for a
    /// repository this machine does not hold, and blank words, are refused in the driver's own sentences.
    /// </summary>
    [Fact]
    public void A_standing_answer_is_planned_in_the_persons_words_and_applied_as_the_terminal_makes_it()
    {
        var plan = HelpProposals.Plan(Setting("standing", "engine", null, "dev writes allowed; prod only on a yes"), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris driver standing engine \"dev writes allowed; prod only on a yes\"", plan.Terminal);
        Assert.Contains("handed to every session in `engine`", plan.Describe);
        var config = plan.Apply!(DriverConfig.Empty);
        Assert.Equal("dev writes allowed; prod only on a yes", config.StandingFor("engine")!.Says);
        Assert.NotNull(config.StandingFor("engine")!.At);

        var clear = HelpProposals.Plan(Setting("standing", "engine", null, "--clear"), config, Facts);
        Assert.Equal("daoris driver standing engine --clear", clear.Terminal);
        Assert.Null(clear.Apply!(config).StandingFor("engine"));

        Assert.Contains("is not registered on this machine", HelpProposals.Plan(Setting("standing", "elsewhere", null, "dev only"), DriverConfig.Empty, Facts).Refusal);
        Assert.Equal(DriverConfig.StandingRefusal, HelpProposals.Plan(Setting("standing", "engine", null, new string('x', 2_001)), DriverConfig.Empty, Facts).Refusal);
    }

    /// <summary>
    /// LANG1c (D142 point 7): a session language for a registered repository or a workspace, judged by the closed table and
    /// the registry, planned with the terminal's spelling and applied as `SET_LANGUAGE` and `daoris driver language` make it.
    /// One of the kind's doors since LANG1c2, once the service's setting writer listed it.
    /// </summary>
    [Fact]
    public void A_session_language_is_judged_by_the_table_and_the_registry_and_applied_as_the_terminal_makes_it()
    {
        var plan = HelpProposals.Plan(Setting("language", "engine", null, "zh"), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("daoris driver language engine zh", plan.Terminal);
        Assert.Contains("Sessions in `engine` write to you in Simplified Chinese (简体中文)", plan.Describe);
        var config = plan.Apply!(DriverConfig.Empty);
        Assert.Equal("zh", config.Languages["engine"]);

        var shared = HelpProposals.Plan(Setting("language", null, "work", "EN"), config, Facts);
        Assert.Equal("daoris driver language --workspace work en", shared.Terminal);
        Assert.Contains("that sets none of its own write to you in English", shared.Describe);
        config = shared.Apply!(config);
        Assert.Equal("en", config.WorkspaceLanguages["work"]);

        var clear = HelpProposals.Plan(Setting("language", "engine", null, "--clear"), config, Facts);
        Assert.Equal("daoris driver language engine --clear", clear.Terminal);
        Assert.False(clear.Apply!(config).Languages.ContainsKey("engine"));

        Assert.Equal(SessionLanguages.Refusal("fr"), HelpProposals.Plan(Setting("language", "engine", null, "fr"), DriverConfig.Empty, Facts).Refusal);
        Assert.Contains("`en` or `zh`", HelpProposals.Plan(Setting("language", "engine", null, null), DriverConfig.Empty, Facts).Refusal);
        Assert.Contains("is not registered on this machine", HelpProposals.Plan(Setting("language", "elsewhere", null, "zh"), DriverConfig.Empty, Facts).Refusal);
        Assert.Contains("no workspace `elsewhere`", HelpProposals.Plan(Setting("language", null, "elsewhere", "zh"), DriverConfig.Empty, Facts).Refusal);
        Assert.Contains("a repository or a workspace", HelpProposals.Plan(Setting("language", "engine", "work", "zh"), DriverConfig.Empty, Facts).Refusal);
    }

    /// <summary>
    /// REVIEWENV1a (D154 point 2, design §1.7): a review rule for a registered repository or a workspace, in the terminal's
    /// words with a quoted command kept whole, judged by the twin's table and planned with what it lets a step do and what the
    /// landing's gate does with it (REVIEWENV1c2), nothing of it after none; the procedure looked for in each checkout it
    /// reaches, as the screen and the terminal look.
    /// </summary>
    [Theory]
    [InlineData("engine", null, "dev --kind local --procedure README.md --address http://localhost:4200 --required",
        "daoris driver review engine dev --kind local --procedure README.md --address http://localhost:4200 --required",
        "Declare the review environment `dev` for `engine`. Before work here lands, it is shown to you in `dev` and waits for you to say it is right.")]
    [InlineData("engine", null, "dev --kind deployed --procedure docs/deploying-to-dev.md",
        "daoris driver review engine dev --kind deployed --procedure docs/deploying-to-dev.md",
        "A set-up step here follows `docs/deploying-to-dev.md` toward `dev`; each deploy or write there asks your go-ahead once per ask.")]
    [InlineData(null, "work", "local --kind local --procedure README.md --address http://localhost:4200 --run \"npm run serve\"",
        "daoris driver review --workspace work local --kind local --procedure README.md --address http://localhost:4200 --run \"npm run serve\"",
        "A set-up step here may run `npm run serve` in its tree on a port nobody holds, without asking you each time; it stops once you have reviewed.")]
    [InlineData("game", null, "none", "daoris driver review game none",
        "Declare that `game` has no review environment, whatever its workspace says. No review environment here")]
    [InlineData("engine", null, "--clear", "daoris driver review engine --clear", "Clear `engine`'s review rule: it takes its workspace's again, else none.")]
    public void A_review_rule_is_planned_with_what_it_lets_a_step_do_and_applied_as_the_terminal_makes_it(
        string? target, string? workspace, string value, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Setting("review", target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
        // Nothing waits where a repository has none, and a clear says what it hands back to.
        if (value is not ("--clear" or "none")) Assert.Contains(ReviewRules.Waiting, plan.Describe);
        else Assert.DoesNotContain(ReviewRules.Waiting, plan.Describe);
        Assert.DoesNotContain("nothing reads it yet", plan.Describe);
        Assert.NotNull(plan.Apply);
    }

    [Fact]
    public void A_review_rule_applied_is_the_edit_the_terminal_makes()
    {
        var config = HelpProposals.Plan(
            Setting("review", null, "work", "local --kind local --procedure \"docs/how we run.md\" --address http://localhost:4200/ --required"),
            DriverConfig.Empty, Facts).Apply!(DriverConfig.Empty);

        Assert.Equal(
            """{"required":true,"environments":[{"name":"local","kind":"local","procedure":"docs/how we run.md","address":"http://localhost:4200"}]}""",
            ReviewRules.ToJson(config.WorkspaceReviews["work"]));
        config = HelpProposals.Plan(Setting("review", null, "work", "--not-required"), config, Facts).Apply!(config);
        Assert.False(config.WorkspaceReviews["work"].Required);
        config = HelpProposals.Plan(Setting("review", "engine", null, "none"), config, Facts).Apply!(config);
        Assert.True(config.Reviews["engine"].IsNone);
    }

    /// <summary>The twin's refusals, the registry's names, and a procedure a checkout here does not hold, each refused in its words.</summary>
    [Theory]
    [InlineData("engine", null, "prod --kind deployed --procedure README.md", "`prod` reads as production, and production is never a review environment")]
    [InlineData("engine", null, "dev --kind local --procedure README.md", "a local environment needs its `address`")]
    [InlineData("engine", "work", "--clear", "a review rule is set for a repository or a workspace — name exactly one.")]
    [InlineData(null, null, "--clear", "a review rule is set for a repository or a workspace — name exactly one.")]
    [InlineData("elsewhere", null, "none", "is not registered on this machine")]
    [InlineData(null, "elsewhere", "--clear", "no workspace `elsewhere`")]
    [InlineData(null, "work", "none", "`none` is a repository's")]
    [InlineData("engine", null, "", "`review` is `<environment> --kind local|deployed --procedure <path>")]
    [InlineData("engine", null, "dev --kind", "`--kind` needs its value")]
    [InlineData("engine", null, "dev --colour blue", "`--colour` is not a word `review` takes")]
    [InlineData("engine", null, "dev local --kind local", "`review` names one environment")]
    [InlineData("engine", null, "dev --kind local --run \"npm start", "close every quote they open")]
    [InlineData("engine", null, "--required --not-required", "say `--required` or `--not-required`, not both")]
    [InlineData("engine", null, "--required", "`engine` has no review rule of its own — add an environment to it first.")]
    public void A_review_rule_is_refused_in_the_twins_words(string? target, string? workspace, string value, string says)
    {
        var plan = HelpProposals.Plan(Setting("review", target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    [Fact]
    public void A_review_s_procedure_is_looked_for_in_each_checkout_it_reaches()
    {
        var engine = Beside("-engine");
        var game = Beside("-game");
        Directory.CreateDirectory(engine);
        Directory.CreateDirectory(game);
        System.IO.File.WriteAllText(Path.Combine(engine, "README.md"), "# Run it against dev\n");
        var facts = Facts with { Registered = [("engine", "work", engine), ("game", "work", game)] };

        var held = HelpProposals.Plan(Setting("review", "engine", null, "dev --kind deployed --procedure README.md"), DriverConfig.Empty, facts);
        Assert.Contains("`README.md` is in `engine`'s checkout here.", held.Describe);

        Assert.Equal(ReviewRules.NotHeld("game", "README.md"),
            HelpProposals.Plan(Setting("review", "game", null, "dev --kind deployed --procedure README.md"), DriverConfig.Empty, facts).Refusal);

        var shared = HelpProposals.Plan(Setting("review", null, "work", "dev --kind deployed --procedure README.md"), DriverConfig.Empty, facts);
        Assert.Null(shared.Refusal);
        Assert.Contains(ReviewRules.SitsUntil("game", "README.md"), shared.Describe);
        Assert.DoesNotContain("`engine` holds no", shared.Describe);

        Assert.Contains("Not checked: `engine` has no checkout on this machine",
            HelpProposals.Plan(Setting("review", "engine", null, "dev --kind deployed --procedure README.md"), DriverConfig.Empty, Facts).Describe);
    }

    /// <summary>
    /// XAGENT1a (D155 point 3, the second-agent design §2.5): a second-opinion rule for a registered repository or a workspace,
    /// in the terminal's words, judged by the twin's table and planned with what it lets a reviewer do, the working agent's
    /// own family named among its reviewers, and what the gate does with it (XAGENT1f4), nothing of it after none.
    /// </summary>
    [Theory]
    [InlineData("engine", null, "--reviewers codex-acp --on landing,steps --verify --minutes 30",
        "daoris driver opinion engine --reviewers codex-acp --on landing,steps --verify --minutes 30",
        "Set the second opinion for `engine`. Before work here lands, `codex-acp` reads it, in a copy of its own that nothing is taken back from")]
    [InlineData(null, "work", "--reviewers codex-acp,dsh --required", "daoris driver opinion --workspace work --reviewers codex-acp,dsh --required",
        "Set the second opinion for each repository of workspace `work` that sets none of its own. Before work here lands, `codex-acp`, else `dsh`, reads it")]
    // The machine's work runs on `claude-code`, whose family both Claude Code doors are.
    [InlineData("engine", null, "--reviewers codex-acp,claude-code-acp", "daoris driver opinion engine --reviewers codex-acp,claude-code-acp",
        "`claude-code-acp` is the same agent as the one that does the work here")]
    [InlineData("game", null, "none", "daoris driver opinion game none",
        "Declare that `game` has no second opinion, whatever its workspace says. No second opinion here")]
    [InlineData("engine", null, "--clear", "daoris driver opinion engine --clear", "Clear `engine`'s second-opinion rule: it takes its workspace's again, else none.")]
    public void An_opinion_rule_is_planned_with_what_it_lets_a_reviewer_do_and_applied_as_the_terminal_makes_it(
        string? target, string? workspace, string value, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Setting("opinion", target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
        // XAGENT1f4: what the gate does with it, as every door says it; nothing waits where a repository has none, and a clear
        // says what it hands back to.
        if (value is not ("--clear" or "none")) Assert.Contains(OpinionRules.Waiting, plan.Describe);
        else Assert.DoesNotContain(OpinionRules.Waiting, plan.Describe);
        if (value.Contains("steps", StringComparison.Ordinal)) Assert.Contains(OpinionRules.StepWaiting, plan.Describe);
        if (value.Contains("--verify", StringComparison.Ordinal)) Assert.Contains(OpinionRules.SafeNotHanded, plan.Describe);
        Assert.DoesNotContain(OpinionRules.DeclaredOnly, plan.Describe);
        Assert.NotNull(plan.Apply);
    }

    /// <summary>
    /// XAGENT1b2 (design §3.1): the door judges a plugin's agent by what its plugin declares, from this machine's plugins, so a
    /// reviewer whose plugin declares the working agent's maker is named its family, as the terminal names it and the
    /// reviewer's choice judges it.
    /// </summary>
    [Fact]
    public void An_opinion_rule_names_a_plugin_s_agent_of_the_working_agent_s_maker_from_the_machine_s_plugins()
    {
        foreach (var (id, harness, maker) in new[] { ("acme.agent", "acme-agent", "Acme"), ("acme.other", "acme-other", "ACME") })
        {
            var folder = Path.Combine(_home, PluginCatalog.Folder, id);
            Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
                $$"""{ "id": "{{id}}", "harnesses": [ { "name": "{{harness}}", "command": ["{{harness}}"], "maker": "{{maker}}" } ] }""");
        }

        var config = DriverConfig.Empty with { Adapter = "acme-agent" };
        var proposal = Setting("opinion", "engine", null, "--reviewers codex-acp,acme-other");

        Assert.Contains("`acme-other` is the same agent as the one that does the work here",
            HelpProposals.Plan(proposal, config, Facts with { Plugins = PluginCatalog.Load(_home, AdapterSet.Built().Names) }).Describe);
        // With no plugin on the machine, nothing declares its maker: it is its own family by name.
        Assert.DoesNotContain("the same agent as the one", HelpProposals.Plan(proposal, config, Facts).Describe);
    }

    [Fact]
    public void An_opinion_rule_applied_is_the_edit_the_terminal_makes()
    {
        var config = HelpProposals.Plan(
            Setting("opinion", null, "work", "--reviewers \"codex-acp, dsh\" --on steps --minutes 45 --no-recheck"),
            DriverConfig.Empty, Facts).Apply!(DriverConfig.Empty);

        Assert.Equal("""{"on":["steps"],"reviewers":["codex-acp","dsh"],"minutes":45,"recheck":false}""", OpinionRules.ToJson(config.WorkspaceOpinions["work"]));
        config = HelpProposals.Plan(Setting("opinion", null, "work", "--required --recheck"), config, Facts).Apply!(config);
        Assert.Equal("""{"on":["steps"],"reviewers":["codex-acp","dsh"],"required":true,"minutes":45}""", OpinionRules.ToJson(config.WorkspaceOpinions["work"]));
        config = HelpProposals.Plan(Setting("opinion", "engine", null, "none"), config, Facts).Apply!(config);
        Assert.True(config.Opinions["engine"].IsNone);
    }

    /// <summary>The twin's refusals, the registry's names, and a form `daoris driver opinion` does not take, each refused in its words.</summary>
    [Theory]
    [InlineData("engine", null, "--reviewers codex-acp --on merge", "`merge` is not an occasion — `landing`, `steps` or both.")]
    [InlineData("engine", null, "--reviewers codex-acp --minutes half", "`minutes` is a whole number from 5 to 120")]
    [InlineData("engine", null, "--reviewers codex-acp,codex-acp", "`codex-acp` is named twice")]
    [InlineData("engine", "work", "--clear", "a second-opinion rule is set for a repository or a workspace — name exactly one.")]
    [InlineData(null, null, "--clear", "a second-opinion rule is set for a repository or a workspace — name exactly one.")]
    [InlineData("elsewhere", null, "none", "is not registered on this machine")]
    [InlineData(null, "elsewhere", "--clear", "no workspace `elsewhere`")]
    [InlineData(null, "work", "none", "`none` is a repository's")]
    [InlineData("engine", null, "", "`opinion` is `--reviewers <adapter,adapter>")]
    [InlineData("engine", null, "--reviewers", "`--reviewers` needs its value")]
    [InlineData("engine", null, "--reviewers dsh --colour blue", "`--colour` is not a word `opinion` takes")]
    [InlineData("engine", null, "codex-acp", "`codex-acp` is not a word `opinion` takes")]
    [InlineData("engine", null, "--reviewers \"dsh", "close every quote they open")]
    [InlineData("engine", null, "--reviewers dsh --required --not-required", "say `--required` or `--not-required`, not both")]
    [InlineData("engine", null, "--reviewers dsh --verify --no-verify", "say `--verify` or `--no-verify`, not both")]
    [InlineData("engine", null, "--reviewers dsh --recheck --no-recheck", "say `--recheck` or `--no-recheck`, not both")]
    [InlineData("engine", null, "none --reviewers dsh", "one change at a time")]
    [InlineData("engine", null, "--required", "`engine` has no second-opinion rule of its own — name its reviewers first.")]
    public void An_opinion_rule_is_refused_in_the_twins_words(string? target, string? workspace, string value, string says)
    {
        var plan = HelpProposals.Plan(Setting("opinion", target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    /// <summary>
    /// LANG1c2: the service's setting writer shape-checks a language against a deliberate copy of the table's codes
    /// (<c>HelpProposalBox.Languages</c>), read here as text since the two share no code, so a language added to the table is
    /// one the box takes, and the box takes none the table does not hold.
    /// </summary>
    [Fact]
    public void The_service_box_takes_the_languages_the_table_holds()
    {
        var writer = System.IO.File.ReadAllText(Path.Combine(
            HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core", "HelpProposalBox.Setting.cs"));
        var listed = System.Text.RegularExpressions.Regex.Match(writer, @"IReadOnlyList<string> Languages\s*=\s*\[([^\]]*)\]");

        Assert.True(listed.Success, "the service's session languages are a list, `Languages`.");
        Assert.Equal(
            SessionLanguages.Table.Select(row => row.Code),
            System.Text.RegularExpressions.Regex.Matches(listed.Groups[1].Value, "\"([a-z-]+)\"").Select(match => match.Groups[1].Value));
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

    /// <summary>HELP9: across (D107), a cap and an adapter, applied as `SET_READ_ACROSS`, `SET_WRITE_ACROSS` and the terminal make them.</summary>
    [Fact]
    public void Applying_across_a_cap_and_an_adapter_makes_the_edit_the_terminal_makes()
    {
        var config = DriverConfig.Empty;
        config = HelpProposals.Plan(Setting("across", "engine", null, "read off"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("across", null, "work", "read off"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("across", "game", null, "write-to engine"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("cap", value: "3"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("adapter", value: "claude-code-acp"), config, Facts).Apply!(config);

        Assert.False(config.ReadAcross["engine"]);
        Assert.False(config.WorkspaceReadAcross["work"]);
        Assert.Equal(["engine"], config.WriteAcross["game"]);
        Assert.Equal(3, config.Cap);
        Assert.Equal("claude-code-acp", config.Adapter);

        config = HelpProposals.Plan(Setting("across", "engine", null, "read --clear"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("across", "game", null, "write-to engine --clear"), config, Facts).Apply!(config);

        Assert.False(config.ReadAcross.ContainsKey("engine"));
        Assert.False(config.WriteAcross.ContainsKey("game"));
    }

    /// <summary>
    /// HELP10: a retry is the edit <c>RETRY_QUEST</c> and <c>daoris driver retry</c> make — the quest forgiven at the
    /// strike limit as it stands when applied, not erased, so the next failures park it again.
    /// </summary>
    [Fact]
    public void Applying_a_retry_marks_the_quest_forgiven_at_the_strike_limit_as_the_drawers_route_does()
    {
        var plan = HelpProposals.Plan(Setting("retry", "#q1a2b3c4"), DriverConfig.Empty.WithStrikes(3), Facts);

        var config = plan.Apply!(DriverConfig.Empty.WithStrikes(5));

        Assert.Equal(5, config.ForgivenAt("q1a2b3c4"));
        Assert.Contains("5 more failure(s) will park it again", HelpProposals.Plan(Setting("retry", "q1a2b3c4"), config, Facts).Describe);
        Assert.Null(config.ReleasedFor("q1a2b3c4"));
    }

    /// <summary>
    /// RETRY1b: a quest parked a second time is marked at its failures as the last look counted them, as <c>RETRY_QUEST</c>
    /// marks it, and said from that count: marked at the limit, six failures less a mark of three left it parked.
    /// </summary>
    [Fact]
    public void Applying_a_retry_of_a_quest_parked_a_second_time_marks_it_at_its_failures()
    {
        var facts = Facts with { Parked = [new ParkedQuest("q1a2b3c4", "engine", 6)] };
        var config = DriverConfig.Empty.WithStrikes(3).WithForgiven("q1a2b3c4", 3);

        var plan = HelpProposals.Plan(Setting("retry", "q1a2b3c4"), config, facts);

        Assert.Equal(6, plan.Apply!(config).ForgivenAt("q1a2b3c4"));
        Assert.Contains("Counting from 6 failure(s)", plan.Describe);
        Assert.Contains("3 more failure(s) will park it again", plan.Describe);
    }

    /// <summary>
    /// SESSUX1b: a retry of a quest the person's stop holds is the release <c>RETRY_QUEST</c> writes, against the session the
    /// last look named, and never a mark: a stop is not a strike (D58), so nothing about the strikes moves.
    /// </summary>
    [Fact]
    public void Applying_a_retry_of_a_held_quest_releases_its_stop_as_the_route_does()
    {
        var plan = HelpProposals.Plan(Setting("retry", "#q2taken0"), DriverConfig.Empty, Facts);

        var config = plan.Apply!(DriverConfig.Empty.WithStrikes(5));

        Assert.Equal("s7a8b9c0", config.ReleasedFor("q2taken0"));
        Assert.Equal(0, config.ForgivenAt("q2taken0"));
    }

    /// <summary>
    /// HELP9: every door the kind names is one it plans, so the doors Ask Daoris's coverage is held against
    /// (<c>HelpCoverageTests</c>) are doors a proposal can take, and none is named that falls to the refusal.
    /// </summary>
    [Fact]
    public void Every_door_the_kind_names_is_one_it_plans()
    {
        (string Door, string? Target, string? Workspace, string? Value)[] samples =
        [
            ("drive", "engine", null, null), ("undrive", "engine", null, null), ("hold", "engine", null, null),
            ("resume", "engine", null, null), ("trees", "engine", null, "off"), ("line", "engine", null, "main"),
            ("landing", "engine", null, "merge"), ("across", "engine", null, "read on"), ("standing", "engine", null, "dev only"),
            ("language", null, "work", "zh"), ("review", "engine", null, "none"), ("opinion", "engine", null, "none"), ("intake", null, null, "off"),
            ("helper", null, null, "claude-code"), ("strikes", null, null, "0"), ("retry", "q1a2b3c4", null, null),
            ("timeout", null, null, "30"), ("notify", null, null, "on"), ("cap", null, null, "1"), ("adapter", null, null, "claude-code"),
        ];

        Assert.Equal(new HelpSettingProposals().Doors, samples.Select(sample => sample.Door));
        foreach (var (door, target, workspace, value) in samples)
        {
            Assert.Null(HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts).Refusal);
        }
    }

    /// <summary>What the route would refuse is refused here in its own words, and never shown to the person.</summary>
    [Theory]
    [InlineData("drive", "nowhere", null, null, "`nowhere` is not registered")]
    [InlineData("line", null, "elsewhere", "develop", "no workspace `elsewhere`")]
    [InlineData("line", "engine", null, "bad..name", "not a branch name git would take")]
    [InlineData("landing", "engine", null, "branch feature/fixed", "`{quest}` or `{session}`")]
    [InlineData("intake", null, null, "gpt-agent", "no agent `gpt-agent`")]
    // HELP9: what `daoris driver across` refuses, in its words, and the names a helper can invent.
    [InlineData("across", "engine", null, "write-to engine", "`engine` writes in its own tree already")]
    [InlineData("across", "engine", null, "write-to nowhere", "`nowhere` is not registered")]
    [InlineData("across", "engine", null, "write-to", "a relationship names the repository it may write into")]
    [InlineData("across", null, "work", "write-to engine", "a relationship is declared from one repository")]
    [InlineData("across", "engine", "work", "read off", "a repository or a workspace")]
    [InlineData("across", null, null, "read off", "a repository or a workspace")]
    [InlineData("across", "engine", null, "read sometimes", "`read on|off|--clear`")]
    [InlineData("across", "engine", null, "peek", "`across` sets `read on|off|--clear` or `write-to <other> [--clear]`")]
    [InlineData("cap", null, null, "0", "`cap 0` is not a change the driver makes")]
    [InlineData("adapter", null, null, "gpt-agent", "no agent `gpt-agent`")]
    // HELP10: a quest id a helper can invent, or one not parked, which forgiven would run past its strikes (D110).
    // SESSUX1b: or one no stop of the person's holds; the refusal names what the last look parked and what it held.
    [InlineData("retry", "q9none00", null, null, "`#q9none00` is neither parked nor held by your stop on this machine")]
    [InlineData("retry", "q9none00", null, null, "it parked `#q1a2b3c4` and held `#q2taken0`")]
    [InlineData("retry", "engine", null, null, "`#engine` is neither parked nor held by your stop")]
    [InlineData("retry", null, null, null, "`retry` names the quest its failed sessions parked or your stop holds")]
    public void What_the_route_would_refuse_is_refused_in_its_words(
        string door, string? target, string? workspace, string? value, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
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

    /// <summary>
    /// LAND2a (D145): a branch rule that accepts automatically, proposed the way the terminal spells it, its card saying
    /// that the plugin pushes and opens pull requests with no press, or, with none, that nothing leaves the machine.
    /// </summary>
    [Theory]
    [InlineData("branch feature/{quest}-{slug} --plugin example.lands --auto-accept", "example.lands", "`example.lands` pushes it and opens a pull request without asking you each time")]
    [InlineData("branch feature/{quest}-{slug} --auto-accept --tidy --plugin example.lands", "example.lands", "`example.lands` pushes it and opens a pull request without asking you each time")]
    [InlineData("branch feature/{quest}-{slug} --auto-accept", null, "no plugin opens a pull request, so each done's branch waits here for you to push it")]
    public void A_landing_may_accept_automatically(string value, string? plugin, string says)
    {
        var plan = HelpProposals.Plan(Setting("landing", null, "work", value), DriverConfig.Empty, WithPlugin("example.lands"));

        Assert.Null(plan.Refusal);
        Assert.Equal($"daoris driver landing --workspace work {value}", plan.Terminal);
        Assert.Contains("accepted automatically", plan.Describe);
        Assert.Contains(says, plan.Describe);
        var rule = plan.Apply!(DriverConfig.Empty).WorkspaceLandings["work"];
        Assert.True(rule.AutoAccept);
        Assert.Equal(plugin, rule.Plugin);
    }

    /// <summary>A repository's own rule saying off out loud replaces the workspace's switch; a merge and both at once are refused.</summary>
    [Fact]
    public void A_landing_that_waits_for_the_press_is_said_and_a_merge_that_accepts_automatically_is_refused()
    {
        var byHand = HelpProposals.Plan(Setting("landing", "engine", null, "branch feature/{quest}-{slug} --no-auto-accept"), DriverConfig.Empty, Facts);
        Assert.Null(byHand.Refusal);
        Assert.Contains("waits for your Accept", byHand.Describe);
        Assert.False(byHand.Apply!(DriverConfig.Empty).Landings["engine"].AutoAccept);

        Assert.Contains("only a branch rule accepts automatically",
            HelpProposals.Plan(Setting("landing", "engine", null, "merge --auto-accept"), DriverConfig.Empty, Facts).Refusal);
        Assert.Contains("`--auto-accept` or `--no-auto-accept`",
            HelpProposals.Plan(Setting("landing", "engine", null, "branch feature/{quest} --auto-accept --no-auto-accept"), DriverConfig.Empty, Facts).Refusal);
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
    public async Task A_setting_is_applied_through_the_edit_the_screens_route_makes()
    {
        var (setting, doors, _) = await ApplyAsync(Setting("drive", "engine"));

        Assert.True(setting.Applied);
        Assert.Equal(["change"], doors.Calls);
        Assert.Contains("engine", doors.Config.Drivable);
    }
}

public sealed partial class HelpStandInDoors
{
    public DriverConfig Config { get; private set; } = DriverConfig.Empty;

    public void Change(Func<DriverConfig, DriverConfig> edit)
    {
        Config = edit(Config);
        Calls.Add("change");
    }
}
