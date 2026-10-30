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
        Assert.Equal(("Designer", "设计师", RoleScope.Everywhere), (designer.NameEn, designer.NameZh, designer.Scope));
        Assert.Equal([new ActorChoice("claude-code-acp", "opus", "high")], DriverConfig.Load(File_).Actors["designer"]);
        Assert.Contains("designer", _out.ToString());

        Assert.Equal(0, await Run("set", "designer", "--scope", "workspace:studio"));
        Assert.Equal(new RoleScope(RoleScope.Workspace, "studio"), Catalogue().Slots.Single(slot => slot.Id == "designer").Role!.Scope);

        Assert.Equal(0, await Run("remove", "designer"));
        Assert.DoesNotContain(Catalogue().Slots, slot => slot.Id == "designer");
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
}
