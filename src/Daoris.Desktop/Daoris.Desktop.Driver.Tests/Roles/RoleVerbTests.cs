using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1y-i (the foundation design §2.7): <c>daoris role</c>, the terminal's door into the role catalogue. It lists the roles,
/// adds, changes and removes a working role through the role door, and answers the judge's refusal before anything is written.
/// </summary>
public sealed class RoleVerbTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-verb", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public RoleVerbTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, """{ "adapter": "claude-code-acp", "intakeAdapter": "codex-acp" }""");
        RoleStaffing.Wire(_home, () => StoreSettled.Yes).ImportFile();
        // The seeded Design role takes design machine-wide; the cases below add one that does, so it is removed first.
        RoleCatalog.Write(DesktopStore.LocationIn(_home), RoleChange.Remove("design"), () => StoreSettled.Yes);
    }

    public void Dispose()
    {
        RoleStaffingPort.Unwire(File_);
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private RoleCatalog Catalogue() => RoleCatalog.Read(DesktopStore.LocationIn(_home));

    private Task<int> Run(params string[] args) => DriverChoicesCommand.RunAsync(["role", .. args], new DriverChoicesWorld
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
        ConfigPath = File_,
        Folder = _home,
    }, CancellationToken.None);

    [Fact]
    public async Task Set_adds_a_role_with_its_staffing_a_second_set_changes_it_and_remove_takes_it_out()
    {
        Assert.Equal(0, await Run("set", "designer", "--name", "Designer", "--zh", "设计师", "--takes", "design", "claude-code-acp opus high"));

        var designer = Catalogue().Slots.Single(slot => slot.Id == "designer").Role!;
        Assert.Equal(("Designer", "设计师", RoleScope.Everywhere), (designer.Name.In("en"), designer.Name.In("zh"), designer.Scope));
        Assert.Equal([new ActorChoice("claude-code-acp", "opus", "high")], DriverConfig.Load(File_).Actors["designer"]);
        Assert.Contains("designer", _out.ToString());

        Assert.Equal(0, await Run("set", "designer", "--scope", "workspace:studio"));
        Assert.Equal(new RoleScope(RoleScope.Workspace, "studio"), Catalogue().Slots.Single(slot => slot.Id == "designer").Role!.Scope);

        Assert.Equal(0, await Run("remove", "designer"));
        Assert.DoesNotContain(Catalogue().Slots, slot => slot.Id == "designer");
    }

    /// <summary>I18NOPEN1 (D222): <c>--name</c> takes one text with no other language, <c>--name-in</c> adds any language, and <c>--zh</c> stays as <c>--name-in zh=…</c>.</summary>
    [Fact]
    public async Task A_role_is_named_by_one_text_with_any_other_languages_added_by_tag()
    {
        Assert.Equal(0, await Run("set", "reader", "--name", "Reader", "--takes", "design", "claude-code-acp opus high"));
        Assert.Equal("\"Reader\"", Catalogue().Slots.Single(slot => slot.Id == "reader").Role!.Name.Json());

        Assert.Equal(0, await Run("set", "reader", "--name-in", "fr=Lecteur", "--name-in", "zh=读者"));
        Assert.Equal("""{"en":"Reader","fr":"Lecteur","zh":"读者"}""", Catalogue().Slots.Single(slot => slot.Id == "reader").Role!.Name.Json());

        Assert.Equal(0, await Run("set", "lecteur", "--name-in", "fr=Lecteur", "--takes", "design", "--scope", "workspace:w1", "claude-code-acp opus high"));
        Assert.Equal("""{"fr":"Lecteur"}""", Catalogue().Slots.Single(slot => slot.Id == "lecteur").Role!.Name.Json());

        Assert.Equal(0, await Run("set", "old", "--name", "Old", "--zh", "旧", "--takes", "design", "--scope", "workspace:w2", "claude-code-acp opus high"));
        Assert.Equal("""{"en":"Old","zh":"旧"}""", Catalogue().Slots.Single(slot => slot.Id == "old").Role!.Name.Json());

        Assert.Equal(2, await Run("set", "bad", "--name", "Bad", "--name-in", "not a tag=x", "--takes", "design", "--scope", "workspace:w3", "claude-code-acp opus high"));
        Assert.Contains("language tag", _error.ToString());
    }

    [Fact]
    public async Task A_change_the_judge_refuses_is_said_with_its_sentence_and_nothing_is_written()
    {
        Assert.Equal(2, await Run("remove", "build"));
        Assert.Contains(RoleCatalog.Of([]).Judge(RoleChange.Remove("build"))!.Said, _error.ToString());

        Assert.Equal(2, await Run("set", "lead", "--name", "Lead", "--takes", "gardening", "claude-code-acp"));
        Assert.Contains("`gardening` is not a work", _error.ToString());
        Assert.All(Catalogue().Slots.Where(slot => slot.Id != "design"), slot => Assert.Equal(RoleState.Seed, slot.State));
    }

    /// <summary>
    /// ONEWAY1a (the oneway design §2 S1): the role door is a seeded role's one door at the terminal too. `set` writes Build's and Intake's
    /// orders, `off` turns Intake off where the retired `driver intake off` did, and Build is refused off by the same judge.
    /// </summary>
    [Fact]
    public async Task ONEWAY1a_set_staffs_a_seeded_role_and_off_turns_intake_off_through_the_role_door()
    {
        Assert.Equal(0, await Run("set", "build", "claude-code opus high"));
        Assert.Equal([new ActorChoice("claude-code", "opus", "high")], DriverConfig.Load(File_).Actors["build"]);

        Assert.Equal(0, await Run("set", "intake", "off"));
        Assert.Empty(DriverConfig.Load(File_).Actors["intake"]);
        Assert.Null(ActorChoices.FirstAgent("intake", DriverConfig.Load(File_)));

        Assert.Equal(2, await Run("set", "build", "off"));
        Assert.Contains("build cannot be off", _error.ToString());
        Assert.Equal(2, await Run("set", "intake", "off", "claude-code"));
        Assert.Contains("`off` is a role's whole staffing", _error.ToString());
    }

    /// <summary>
    /// ONEWAY1a: `default` gives a seeded role its default staffing too, what the window's *Clear order* became (*Use the default*):
    /// a stored order goes back to the role's default, which until ONEWAY1b is its key's as before.
    /// </summary>
    [Fact]
    public async Task ONEWAY1a_default_gives_a_seeded_role_its_default_staffing_back()
    {
        Assert.Equal(0, await Run("set", "build", "claude-code opus high"));
        Assert.True(DriverConfig.Load(File_).Actors.ContainsKey("build"));

        Assert.Equal(0, await Run("default", "build"));

        // ONEWAY1b: the default is the seed's written order, never as before.
        Assert.Equal([new ActorChoice("claude-code")], DriverConfig.Load(File_).Actors["build"]);
        Assert.Equal("claude-code", ActorChoices.FirstAgent("build", DriverConfig.Load(File_)));
    }

    /// <summary>STAFFPOLICY1b (the staff policy design §6.4): the terminal's twin of the editor, under the same judge and the same door.</summary>
    [Fact]
    public async Task Policy_sets_a_spread_reads_it_back_with_the_next_start_and_a_preset_or_a_clear_returns_the_order()
    {
        Assert.Equal(0, await Run("set", "designer", "--name", "Designer", "--takes", "design", "claude-code-acp opus high", "claude-code-acp sonnet high"));

        Assert.Equal(0, await Run("policy", "designer", "--rule", "weight", "--weight", "1=3", "--weight", "2=1", "--share", "1=70", "--budget", "2=6/day", "--takes", "2=design"));

        var staffing = RoleStaffing.Of(File_)!;
        var read = staffing.Policy("designer");
        Assert.Equal((StaffPolicyRead.Set, 1L), (read.State, read.Revision));
        Assert.Equal([3, 1], read.Policy!.Choices.Select(choice => choice.Weight));
        Assert.Equal(70, read.Policy.Choices[0].Share);
        Assert.Equal(6, read.Policy.Choices[1].Budget!.Starts);
        Assert.Contains("spread by weight 3:1", _out.ToString());

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("designer"));
        Assert.Contains("policy (revision 1): spread by weight 3:1", _out.ToString());
        Assert.Contains("next new start: choice ", _out.ToString());

        Assert.Equal(0, await Run("policy", "designer", "--preset", "first"));
        Assert.Equal(StaffRules.Order, staffing.Policy("designer").Policy!.Rule);
        Assert.Equal(0, await Run("policy", "designer", "--clear"));
        Assert.Equal(StaffPolicyRead.Default, staffing.Policy("designer").State);
    }

    [Fact]
    public async Task Policy_refuses_with_the_judges_sentence_a_field_not_enforced_yet_and_a_flag_that_is_not_one()
    {
        Assert.Equal(0, await Run("set", "designer", "--name", "Designer", "--takes", "design", "claude-code-acp opus high", "claude-code-acp sonnet high"));

        Assert.Equal(2, await Run("policy", "designer", "--on", "signed-out=hold"));
        // A move on a failure is a supervisor rule's now, so the policy's own flag for it is gone.
        Assert.Contains("`--on` is not a flag of `driver role policy`", _error.ToString());
        Assert.Equal(2, await Run("policy", "designer", "--rule", "weight", "--weight", "1=11"));
        Assert.Contains("a weight is from 0 to 10", _error.ToString());
        Assert.Equal(2, await Run("policy", "designer", "--weight", "3=1"));
        Assert.Contains("the role has 2 choices", _error.ToString());
        Assert.Equal(2, await Run("policy", "designer", "--fast", "1"));
        Assert.Contains("is not a flag of `driver role policy`", _error.ToString());
        Assert.Equal(2, await Run("policy", "nobody", "--clear"));
        Assert.Contains("no role `nobody`", _error.ToString());
        Assert.Equal(StaffPolicyRead.Default, RoleStaffing.Of(File_)!.Policy("designer").State);
    }

    /// <summary>
    /// STAFFPOLICY1g (D205 point 9): in full access Ask Daoris has a shell, and the policy verb it runs there is a session's terminal, not the
    /// person's: with no window to press it in, it is refused with the way to the person's Apply and writes nothing.
    /// </summary>
    [Fact]
    public async Task The_policy_verb_run_from_a_session_s_terminal_is_refused_to_the_person_s_apply_and_writes_nothing()
    {
        var world = new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld
            {
                Home = _home, Out = _out, Error = _error, Verb = "daoris",
                Environment = name => name == DriverChoicesCommand.SessionVariable ? "s1a2b3c4" : null,
            },
            ConfigPath = File_,
            Folder = _home,
        };
        Assert.Equal(0, await Run("set", "designer", "--name", "Designer", "--takes", "design", "claude-code-acp opus high"));
        _out.GetStringBuilder().Clear();

        var exit = await DriverChoicesCommand.RunAsync(["role", "policy", "designer", "--preset", "even"], world, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, _error.ToString());
        Assert.Equal(StaffPolicyRead.Default, RoleStaffing.Of(File_)!.Policy("designer").State);
    }

    [Fact]
    public void The_policy_verb_changes_the_home_and_so_joins_the_leased_changes()
    {
        Assert.True(DriverChoicesCommand.Changes(["role", "policy", "build", "--preset", "even"]));
        Assert.False(DriverChoicesCommand.Changes(["role", "build"]));
        Assert.False(DriverChoicesCommand.Changes(["role", "policy", "build", "--plan"]));
    }

    [Fact]
    public async Task The_list_says_every_role_its_work_scope_and_staffing_and_a_removed_role_s_work_holds()
    {
        Assert.Equal(0, await Run("remove", "intake"));
        _out.GetStringBuilder().Clear();

        Assert.Equal(0, await Run());

        var said = _out.ToString();
        Assert.Contains("build", said);
        Assert.Contains("intake", said);
        Assert.Contains("removed", said);
        Assert.Contains("hold", said);

        Assert.Equal(0, await Run("default", "intake"));
        Assert.Equal(RoleState.Seed, Catalogue().Slots.Single(slot => slot.Id == "intake").State);
    }

    [Fact]
    public async Task Suggested_says_the_table_and_changes_nothing_until_use_sets_each_role_through_the_door()
    {
        Assert.Equal(0, await Run("suggested"));

        var said = _out.ToString();
        Assert.Contains("1 claude-code · sonnet · high, 2 claude-code · opus · high", said);
        Assert.Contains("can spend more", said);
        Assert.Empty(DriverConfig.Load(File_).Actors);

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("suggested", "--use"));

        var actors = DriverConfig.Load(File_).Actors;
        foreach (var each in ActorSuggestion.Table) Assert.Equal(each.Choices, actors[each.Role]);
        Assert.DoesNotContain("intake", actors.Keys);
        Assert.Equal(2, await Run("suggested", "--now"));
    }

    private SupervisorRuleStore Rules => new(_home, () => StoreSettled.Yes);

    private void Reach() => Assert.Equal(1, Rules.Write("machine", 0,
    [
        new SupervisorRule("m-aa", "answers-alone", "in-a-row", 2, "hold", null, "machine", null),
        new SupervisorRule("m-t", "tries", "episode", 5, "hold", null, "machine", null),
    ], "test", new SupervisorRuleWorld(["build", "chat"])).Revision);

    /// <summary>CONTEXTDRIVE1d (the context counters design §7.6): the terminal's twin of the supervisor's rules, through the role door.</summary>
    [Fact]
    public async Task CONTEXTDRIVE1d_role_rules_writes_a_place_s_rules_whole_and_role_supervisor_says_them_with_the_wider_place_s()
    {
        Reach();

        Assert.Equal(0, await Run("rules", "--place", "repository:daoris", "--set", "answers-alone=3:hold@build"));

        var set = Rules.Of("repository:daoris");
        var rule = Assert.Single(set.Rules);
        Assert.Equal((1L, "answers-alone", 3, "hold", "build"), (set.Revision, rule.Count, rule.At, rule.Do, rule.Roles!.Single()));
        Assert.Contains("the supervisor's rules for repository:daoris set: answers-alone at 3: hold for build", _out.ToString());

        Assert.Equal(0, await Run("rules", "--place", "repository:daoris", "--clear", "answers-alone@build", "--off", "answers-alone@build"));
        Assert.Equal(("off", 2L), (Assert.Single(Rules.Of("repository:daoris").Rules).Do, Rules.Of("repository:daoris").Revision));

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("supervisor", "--place", "repository:daoris"));
        var said = _out.ToString();
        Assert.Contains("supervisor  repository:daoris · rules revision 2", said);
        Assert.Contains("tries", said);
        Assert.Contains("machine", said);

        Assert.True(DriverChoicesCommand.Changes(["role", "rules", "--place", "machine", "--set", "tries=4:hold"]));
        Assert.False(DriverChoicesCommand.Changes(["role", "supervisor"]));
    }

    [Fact]
    public async Task CONTEXTDRIVE1d_role_rules_is_refused_by_the_judge_s_code_with_nothing_written()
    {
        Reach();
        Assert.Equal(2, await Run("rules", "--place", "repository:daoris", "--set", "quiet=1:next"));
        Assert.Contains("(moves-unbounded)", _error.ToString());
        Assert.Equal(2, await Run("rules", "--place", "repository:daoris", "--set", "answers-alone=three"));
        Assert.Contains("is not one `<at>:<do>[:<minutes>]`", _error.ToString());
        Assert.Equal(0, Rules.Of("repository:daoris").Revision);
    }

    /// <summary>CONTEXTDRIVE1q: every executor acts on one rule per count, so a ladder is refused at the verb and at the judge.</summary>
    [Fact]
    public async Task CONTEXTDRIVE1q_a_ladder_is_refused_at_the_terminal_and_by_the_judge()
    {
        Reach();
        Assert.Equal(2, await Run("rules", "--place", "machine", "--set", "limits=1:weighted,1:wait:30@build", "--set", "moves=3:ask@build"));
        Assert.Contains("is not one `<at>:<do>[:<minutes>]`", _error.ToString());
        Assert.Equal(0, await Run("rules", "--place", "machine", "--set", "limits=1:weighted@build", "--set", "moves=3:ask@build"));
        Assert.Equal(2, await Run("rules", "--place", "machine", "--set", "limits=1:wait:30@build,chat"));
        Assert.Contains("(ladder)", _error.ToString());
    }

    /// <summary>CONTEXTDRIVE1k: a card's twin is one command that writes the press's rules whole as the judge accepted them, roles per rule.</summary>
    [Fact]
    public async Task CONTEXTDRIVE1k_the_card_s_twin_is_one_judged_write_of_the_same_rules()
    {
        Reach();
        var press = new SupervisorRulesPress("repository:daoris", 0,
        [
            new SupervisorRule("", "limits", "episode", 1, "weighted", null, "repository:daoris", ["build"]),
            new SupervisorRule("", "limits", "episode", 1, "wait", 30, "repository:daoris", ["chat"]),
            new SupervisorRule("", "answers-alone", "in-a-row", 3, "hold", null, "repository:daoris", null),
            new SupervisorRule("", "moves", "day", 3, "ask", null, "repository:daoris", ["build"]),
            new SupervisorRule("", "failures", "since-try-again", 1, "off", null, "repository:daoris", ["chat"]),
        ]);
        var twin = SupervisorRulesFlags.Terminal(press);

        Assert.Equal(
            "daoris role rules --place repository:daoris --clear all --set limits=1:weighted@build --set limits=1:wait:30@chat --set answers-alone=3:hold --set moves=3:ask@build --off failures@chat", twin);
        var exit = await Run(twin.Split(' ')[2..]);
        Assert.True(exit == 0, _error.ToString());

        var written = Rules.Of("repository:daoris").Rules;
        static IEnumerable<string> Said(IEnumerable<SupervisorRule> rules) =>
            rules.Select(rule => $"{rule.Count} {rule.At} {rule.Do} {rule.UpTo} {string.Join(",", rule.Roles ?? [])}").Order();
        Assert.Equal(Said(press.Rules), Said(written));
    }

    /// <summary>CONTEXTDRIVE1d (D205 point 9): a rule is a staffing-class press, so a session's terminal under full access is refused it.</summary>
    [Fact]
    public async Task CONTEXTDRIVE1d_role_rules_run_from_a_session_s_terminal_is_refused_to_the_person_s_apply_and_writes_nothing()
    {
        Reach();
        var world = new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.All,
            Instance = new InstanceWorld
            {
                Home = _home, Out = _out, Error = _error, Verb = "daoris",
                Environment = name => name == DriverChoicesCommand.SessionVariable ? "s1a2b3c4" : null,
            },
            ConfigPath = File_,
            Folder = _home,
        };

        var exit = await DriverChoicesCommand.RunAsync(["role", "rules", "--place", "machine", "--set", "answers-alone=5:hold"], world, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains(DriverChoicesCommand.SessionsTerminal, _error.ToString());
        Assert.Equal(2, Rules.Of("machine").Rules.Single(rule => rule.Count == "answers-alone").At);
    }
}
