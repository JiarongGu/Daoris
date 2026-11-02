using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1y-i (the foundation design §2.7): <c>daoris driver role</c>, the terminal's door into the role catalogue. It lists the roles,
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
        Assert.All(Catalogue().Slots, slot => Assert.Equal(RoleState.Seed, slot.State));
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
        Assert.Contains("`moves.signed-out` as `hold`, which this build does not enforce yet", _error.ToString());
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
}
