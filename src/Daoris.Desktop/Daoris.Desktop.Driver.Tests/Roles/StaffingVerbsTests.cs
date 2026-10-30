using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s part 5 (the design §19.15, "Its own verbs"): <c>daoris driver actor export</c> writes the seeded eight's staffing in D205's
/// shape, <c>import</c> reads one back as one write, and <c>found</c> lists what the check kept, <c>found take|dismiss &lt;n&gt;</c> pressing it.
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

    [Fact]
    public async Task Export_writes_the_staffing_as_actors_and_import_reads_it_back_as_one_write_each_actor_it_names_the_rest_as_before()
    {
        Staffing.Set("build", [new ActorChoice("codex-acp", "o3"), new ActorChoice("claude-code-acp")]);
        Staffing.Set("intake", []);

        Assert.Equal(0, await Run("export", "staffing.json"));
        using (var exported = JsonDocument.Parse(File.ReadAllText(Path.Combine(_home, "staffing.json"))))
        {
            var actors = exported.RootElement.GetProperty("actors");
            Assert.Equal(["build", "intake", "brainstorm", "chat"], actors.EnumerateObject().Select(each => each.Name));
        }

        Staffing.Set("build", [new ActorChoice("other-acp")]);
        Staffing.Set("intake", [new ActorChoice("codex-acp")]);
        Staffing.Set("helper", [new ActorChoice("codex-acp")]);

        Assert.Equal(0, await Run("import", "staffing.json"));

        var loaded = DriverConfig.Load(File_);
        Assert.Equal(["codex-acp", "claude-code-acp"], loaded.Actors["build"].Select(each => each.Agent));
        Assert.Empty(loaded.Actors["intake"]);
        Assert.False(loaded.Actors.ContainsKey("helper"));
        Assert.Equal("codex-acp", DriverConfig.Parse(File.ReadAllText(File_)).Adapter);
    }

    [Fact]
    public async Task An_import_naming_an_entry_the_judge_refuses_writes_nothing_and_says_why()
    {
        File.WriteAllText(Path.Combine(_home, "bad.json"), """{ "actors": { "build": [] } }""");
        var before = File.ReadAllText(File_);

        Assert.Equal(2, await Run("import", "bad.json"));

        Assert.Contains("build cannot be off", _error.ToString());
        Assert.Equal(before, File.ReadAllText(File_));
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
