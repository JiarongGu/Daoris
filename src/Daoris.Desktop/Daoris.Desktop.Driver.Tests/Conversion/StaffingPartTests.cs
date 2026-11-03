using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1b and ONEWAY1c-ii (the oneway design §2 S2, S3): the staffing part converts a home's staffing through the one door, each of the five
/// roles with an order of its own written as it runs, the four keys taken out of <c>driver.json</c> and kept for Undo, the found rows cleared;
/// a converted home's writes leave the file alone while an unconverted one is copied as today; a fresh home is seeded with written orders.
/// </summary>
public sealed class StaffingPartTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staffing-part", Guid.NewGuid().ToString("N")[..8]);

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public void Dispose()
    {
        RoleStaffingPort.Unwire(File_);
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    /// <summary>Build as before, Intake off, Ask Daoris as before, Helper unset, Chat a stored order and Brainstorm an entry that does not read.</summary>
    private const string Old =
        """
        {
          "cap": 3,
          "adapter": "claude-code-acp",
          "helperAdapter": "codex-acp",
          "actors": {
            "chat": [{ "agent": "codex-acp", "model": "gpt-5" }],
            "brainstorm": "two voices"
          }
        }
        """;

    private static readonly string[] Keys = ["actors", "adapter", "intakeAdapter", "helperAdapter"];

    private RoleStaffing Staffing => new(_home, () => StoreSettled.Yes) { LockWait = TimeSpan.FromSeconds(5), FileWait = TimeSpan.FromMilliseconds(200) };

    /// <summary>The home's file written and imported, the port wired as a root wires it.</summary>
    private RoleStaffing Begin(string? json = Old)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        if (json is not null) File.WriteAllText(File_, json);
        RoleStaffing.Wire(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, Staffing.ImportFile().How);
        return Staffing;
    }

    private ConvertDoors Doors() => new(_home, edit => edit(DriverConfig.Load(File_)).Save(File_), () => DriverConfig.Load(File_))
    {
        Others = [new StaffingPart(Staffing)], Build = "0.42.0",
    };

    private async Task<PartPlan> PlanAsync() => (await ConversionApplies.PlanAsync(Doors(), new ConversionChoices())).Of(StaffingPart.Part)!;

    private async Task<ConvertResult> ApplyAsync()
    {
        var plan = await ConversionApplies.PlanAsync(Doors(), new ConversionChoices());
        return await ConversionApplies.ApplyAsync(Doors(), new ConversionChoices(), plan.Digest);
    }

    private DriverConfig Loaded => DriverConfig.Load(File_);

    private string? First(string role) => ActorChoices.FirstAgent(role, Loaded);

    private IReadOnlyList<string> KeysOnFile()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(File_));
        return [.. Keys.Where(key => document.RootElement.TryGetProperty(key, out _))];
    }

    [Fact]
    public async Task ONEWAY1b_the_plan_writes_each_role_as_it_runs_off_where_its_key_is_absent_and_keeps_what_does_not_read()
    {
        Begin();

        var part = await PlanAsync();

        Assert.False(part.Empty);
        Assert.False(part.Census.Ended);
        Assert.Equal(4, part.Census.Count);
        var lines = string.Join("\n", part.Lines);
        Assert.Contains("Build: claude-code-acp (as before) → claude-code-acp, written", lines);
        Assert.Contains("Intake: off (no key) → off, written", lines);
        Assert.Contains("Ask Daoris: codex-acp (as before) → codex-acp, written", lines);
        Assert.Contains("Brainstorm: does not read", lines);
        Assert.Contains("daoris role set brainstorm", lines);
        Assert.DoesNotContain("Chat:", lines);
        Assert.Contains("Second opinion, Partner and Helper", lines);
        Assert.Contains("`actors`, `adapter`, `helperAdapter`", lines);
        Assert.Contains("nothing lowers and nothing spends differently", lines);
    }

    [Fact]
    public async Task ONEWAY1b_apply_writes_the_orders_takes_the_keys_out_and_a_start_runs_the_same_choice()
    {
        Begin();
        var before = (First("build"), First("intake"), First("ask-daoris"), First("chat"));

        var result = await ApplyAsync();

        Assert.True(result.Done, result.Message);
        Assert.Empty(KeysOnFile());
        Assert.Equal(3, Loaded.Cap);
        Assert.Equal(before, (First("build"), First("intake"), First("ask-daoris"), First("chat")));
        var loaded = Loaded;
        Assert.Equal([new ActorChoice("claude-code-acp")], loaded.Actors["build"]);
        Assert.False(loaded.Actors.ContainsKey("brainstorm"));
        Assert.Empty(loaded.Actors["intake"]);
        Assert.Equal([new ActorChoice("codex-acp")], loaded.Actors["ask-daoris"]);
        Assert.Equal([new ActorChoice("codex-acp", "gpt-5")], loaded.Actors["chat"]);
        Assert.True(loaded.ActorErrors.ContainsKey("brainstorm"));
        Assert.False(loaded.Actors.ContainsKey("helper"));
        Assert.Empty(ActorChoices.Effective("helper", loaded));
        Assert.DoesNotContain(ActorChoices.Ordered.Select(role => ActorView.Of(role, loaded)), view => view.State == "before");
        Assert.True((await PlanAsync()).Census.Ended);
    }

    [Fact]
    public async Task ONEWAY1c_ii_a_converted_homes_write_and_check_leave_driver_json_alone()
    {
        Begin();
        Assert.True((await ApplyAsync()).Done);
        var text = File.ReadAllText(File_);
        Assert.True(TransitionalRegister.StaffingCopyEnded(_home));

        Staffing.Set("build", [new ActorChoice("codex-acp")]);

        Assert.Equal(text, File.ReadAllText(File_));
        Assert.Equal("codex-acp", First("build"));
        Assert.Equal(StaffingChecks.Same, Staffing.Check(StaffingCheckAt.Look).How);
        Assert.Empty(Staffing.Found());
        Assert.Equal("codex-acp", First("ask-daoris"));
        Assert.Equal(text, File.ReadAllText(File_));
    }

    [Fact]
    public void ONEWAY1c_ii_an_unconverted_home_is_copied_as_today()
    {
        Begin();
        Assert.False(TransitionalRegister.StaffingCopyEnded(_home));

        Staffing.Set("build", [new ActorChoice("codex-acp")]);

        Assert.Equal("codex-acp", DriverConfig.Parse(File.ReadAllText(File_)).Adapter);
        Assert.Equal(StaffingChecks.Same, Staffing.Check(StaffingCheckAt.Look).How);
    }

    [Fact]
    public async Task ONEWAY1b_undo_puts_back_the_rows_and_the_keys_and_the_copy_runs_again()
    {
        var staffing = Begin();
        var rows = staffing.Rows();
        var paths = StaffingFile.PathsOf(File.ReadAllText(File_));
        Assert.True((await ApplyAsync()).Done);

        var undo = ConversionApplies.UndoPlan(Doors());
        Assert.Null(undo.Refusal);
        Assert.Contains(undo.Lines, line => line.Contains("driver.json's staffing keys come back", StringComparison.Ordinal));
        var result = ConversionApplies.Undo(Doors(), undo.Digest!);

        Assert.True(result.Done, result.Message);
        Assert.Equal(paths, StaffingFile.PathsOf(File.ReadAllText(File_)));
        Assert.Equal(rows, Staffing.Rows());
        Assert.False(TransitionalRegister.StaffingCopyEnded(_home));
        Assert.Equal(StaffingChecks.Same, Staffing.Check(StaffingCheckAt.Look).How);
        Staffing.Set("build", [new ActorChoice("codex-acp")]);
        Assert.Equal("codex-acp", DriverConfig.Parse(File.ReadAllText(File_)).Adapter);
    }

    [Fact]
    public async Task ONEWAY1b_undo_is_refused_once_a_role_was_edited()
    {
        Begin();
        Assert.True((await ApplyAsync()).Done);

        Staffing.Set("intake", [new ActorChoice("codex-acp")]);

        var undo = ConversionApplies.UndoPlan(Doors());
        Assert.StartsWith("moved", undo.Refusal);
        Assert.Contains("Intake", undo.Refusal);
    }

    [Fact]
    public async Task ONEWAY1b_a_found_row_still_open_is_listed_take_it_stays_offered_and_apply_clears_it()
    {
        var staffing = Begin();
        AtomicFile.WriteText(File_, File.ReadAllText(File_).Replace("\"claude-code-acp\"", "\"other-acp\"", StringComparison.Ordinal));
        Assert.Equal(StaffingChecks.Found, staffing.Check(StaffingCheckAt.Look).How);
        Assert.Single(Staffing.Found());

        var part = await PlanAsync();

        var clear = Assert.Single(part.Clears);
        Assert.Equal(1, clear.Count);
        Assert.Contains("found", clear.Said);
        Assert.True((await ApplyAsync()).Done);
        Assert.Empty(Staffing.Found());
    }

    [Theory]
    [InlineData("""{ "cap": 3 }""")]
    [InlineData(null)]
    public async Task ONEWAY1b_a_fresh_home_is_seeded_with_written_orders_and_its_writes_leave_driver_json_alone(string? json)
    {
        Begin(json);

        var loaded = Loaded;
        Assert.Equal([new ActorChoice("claude-code")], loaded.Actors["build"]);
        Assert.Equal([new ActorChoice("claude-code")], loaded.Actors["chat"]);
        Assert.Equal([new ActorChoice("claude-code")], loaded.Actors["brainstorm"]);
        Assert.Empty(loaded.Actors["intake"]);
        Assert.Empty(loaded.Actors["ask-daoris"]);
        Assert.False(loaded.Actors.ContainsKey("helper"));
        Assert.False(loaded.Actors.ContainsKey("opinion"));
        Assert.True((await PlanAsync()).Census.Ended);
        Assert.True(TransitionalRegister.StaffingCopyEnded(_home));

        Staffing.Set("build", [new ActorChoice("codex-acp")]);

        Assert.Equal("codex-acp", First("build"));
        Assert.True(!File.Exists(File_) || KeysOnFile().Count == 0);
    }

    [Fact]
    public void ONEWAY1b_the_role_doors_default_writes_the_seed_and_clears_a_role_with_no_order_of_its_own()
    {
        var staffing = Begin();
        staffing.Set("helper", [new ActorChoice("codex-acp")]);

        RoleDoor.Apply(Staffing, RoleChange.Default("intake"));
        RoleDoor.Apply(Staffing, RoleChange.Default("ask-daoris"));
        RoleDoor.Apply(Staffing, RoleChange.Default("chat"));
        RoleDoor.Apply(Staffing, RoleChange.Default("helper"));

        var loaded = Loaded;
        Assert.Empty(loaded.Actors["intake"]);
        Assert.Empty(loaded.Actors["ask-daoris"]);
        Assert.Equal([new ActorChoice("claude-code")], loaded.Actors["chat"]);
        Assert.False(loaded.Actors.ContainsKey("helper"));
    }
}
