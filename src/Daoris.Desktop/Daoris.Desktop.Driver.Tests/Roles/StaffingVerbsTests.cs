using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s part 5 (the design §19.15, "Its own verbs"): <c>found</c> lists what the check kept, <c>found take|dismiss &lt;n&gt;</c>
/// pressing it, on a home the copy still reaches. <c>export</c> and <c>import</c> are retired (ONEWAY1c-ii): the role door sets and says the roles.
/// </summary>
public sealed class StaffingVerbsTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staffing-verbs", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public StaffingVerbsTests()
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

    private RoleStaffing Staffing => new(_home, () => StoreSettled.Yes);

    private Task<int> Run(params string[] args) => DriverChoicesCommand.RunAsync(["actor", .. args], new DriverChoicesWorld
    {
        Verbs = DriverChoiceVerbs.All,
        Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
        ConfigPath = File_,
        Folder = _home,
    }, CancellationToken.None);

    [Theory]
    [InlineData("export")]
    [InlineData("export", "staffing.json")]
    [InlineData("import", "staffing.json")]
    public async Task ONEWAY1c_ii_actor_export_and_import_are_retired_naming_the_role_door_and_write_nothing(params string[] args)
    {
        File.WriteAllText(Path.Combine(_home, "staffing.json"), """{ "actors": { "build": [{ "agent": "other-acp" }] } }""");
        var before = File.ReadAllText(File_);

        Assert.Equal(2, await Run(args));

        Assert.Contains("daoris role set", _error.ToString());
        Assert.Equal(before, File.ReadAllText(File_));
        Assert.Equal("claude-code-acp", DriverConfig.Load(File_).Adapter);
        Assert.Equal("""{ "actors": { "build": [{ "agent": "other-acp" }] } }""", File.ReadAllText(Path.Combine(_home, "staffing.json")));
    }

    [Fact]
    public async Task Found_lists_what_the_check_kept_and_take_and_dismiss_press_it()
    {
        File.WriteAllText(File_, File.ReadAllText(File_).Replace("claude-code-acp", "other-acp"));
        Staffing.Check(StaffingCheckAt.Look);

        Assert.Equal(0, await Run("found"));
        var id = Assert.Single(Staffing.Found()).Id;
        Assert.Contains($"#{id}", _out.ToString());
        Assert.Contains("build: 1 claude-code-acp · account default · account default (as before) → 1 other-acp", _out.ToString());
        Assert.Contains("other-acp", _out.ToString());

        Assert.Equal(0, await Run("found", "take", id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal("other-acp", DriverConfig.Load(File_).Adapter);

        File.WriteAllText(File_, File.ReadAllText(File_).Replace("\"codex-acp\"", "\"gone-acp\""));
        Staffing.Check(StaffingCheckAt.Look);
        var second = Assert.Single(Staffing.Found()).Id;
        Assert.Equal(0, await Run("found", "dismiss", second.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Empty(Staffing.Found());
        Assert.Equal("codex-acp", DriverConfig.Load(File_).IntakeAdapter);
    }
}
