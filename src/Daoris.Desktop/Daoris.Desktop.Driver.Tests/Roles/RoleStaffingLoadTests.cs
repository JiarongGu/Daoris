using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s part 3 (the design §19.15, "the home, and what reads it"): a load of the home's own <c>driver.json</c> fills the staffing's
/// five fields from the store once the root wired the port and the store holds the import; a save of any other key writes the file's
/// staffing paths back as found. A file nobody wired reads as it always did.
/// </summary>
public sealed class RoleStaffingLoadTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-staffing-load", Guid.NewGuid().ToString("N")[..8]);

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public RoleStaffingLoadTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, Imported);
    }

    public void Dispose()
    {
        RoleStaffingPort.Unwire(File_);
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private const string Imported =
        """
        {
          "cap": 3,
          "adapter": "claude-code-acp",
          "intakeAdapter": "codex-acp",
          "actors": { "build": [ { "agent": "claude-code-acp", "model": "opus" } ], "intake": [ { "agent": "codex-acp" } ], "opinion": "x" }
        }
        """;

    private const string HandEdited =
        """
        {
          "cap": 3,
          "adapter": "other-acp",
          "actors": { "build": [ { "agent": "other-acp" } ], "intake": [] }
        }
        """;

    private RoleStaffing Imports() => RoleStaffing.Wire(_home, () => StoreSettled.Yes);

    [Fact]
    public void A_wired_home_before_its_import_reads_the_file_and_after_it_reads_the_store_whatever_the_file_says_since()
    {
        Imports();
        var before = DriverConfig.Load(File_);
        Assert.Equal("claude-code-acp", before.Adapter);
        Assert.Equal("codex-acp", before.IntakeAdapter);
        Assert.Contains("opinion", before.ActorErrors.Keys);

        Assert.Equal(StaffingImported.Done, new RoleStaffing(_home, () => StoreSettled.Yes).ImportFile().How);
        File.WriteAllText(File_, HandEdited);

        var after = DriverConfig.Load(File_);
        Assert.Equal("claude-code-acp", after.Adapter);
        Assert.Equal("codex-acp", after.IntakeAdapter);
        Assert.Null(after.HelperAdapter);
        Assert.Equal(["claude-code-acp"], after.Actors["build"].Select(choice => choice.Agent));
        Assert.Equal(["codex-acp"], after.Actors["intake"].Select(choice => choice.Agent));
        Assert.Contains("opinion", after.ActorErrors.Keys);
    }

    [Fact]
    public void A_file_nobody_wired_is_read_as_the_file_says_even_beside_a_store_that_holds_the_import()
    {
        Imports();
        new RoleStaffing(_home, () => StoreSettled.Yes).ImportFile();
        File.WriteAllText(File_, HandEdited);
        var other = Path.Combine(_home, "named.json");
        File.WriteAllText(other, HandEdited);

        var named = DriverConfig.Load(other);

        Assert.Equal("other-acp", named.Adapter);
        Assert.Empty(named.Actors["intake"]);
    }

    [Fact]
    public void A_save_of_another_key_leaves_the_files_staffing_paths_as_the_file_holds_them_whatever_the_store_holds()
    {
        Imports();
        new RoleStaffing(_home, () => StoreSettled.Yes).ImportFile();
        File.WriteAllText(File_, HandEdited);

        DriverConfig.Load(File_).WithCap(7).Save(File_);

        var saved = DriverConfig.Parse(File.ReadAllText(File_));
        Assert.Equal(7, saved.Cap);
        Assert.Equal("other-acp", saved.Adapter);
        Assert.Null(saved.IntakeAdapter);
        Assert.Empty(saved.Actors["intake"]);
        Assert.Equal(["other-acp"], saved.Actors["build"].Select(choice => choice.Agent));
    }
}
