using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 w-s part 1 (D212's note, the design §19.15): the roles' staffing in the desktop store, <c>role_staffing</c> with its
/// projection row and its found rows, imported once from <c>driver.json</c> once the start's confirmation is settled, under the staffing's
/// record lock, and read back; read by nothing, and the file never written.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment. The start's confirmation
/// is a reading each case holds in its hand; a kill before the commit is the import's own seam, which throws inside its transaction.
/// </remarks>
public sealed class RoleStaffingTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "role-staffing", Guid.NewGuid().ToString("N")[..8]);

    private StoreSettled _settled = StoreSettled.Yes;

    public RoleStaffingTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private RoleStaffing Staffing(Action? beforeCommit = null, TimeSpan? lockWait = null) =>
        new(_home, () => _settled) { BeforeCommit = beforeCommit, LockWait = lockWait ?? RecordLock.Wait };

    private string File_ => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private string Db => DesktopStore.LocationIn(_home);

    private void Write(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File_)!);
        File.WriteAllText(File_, json);
    }

    private const string Full =
        """
        {
          "cap": 3,
          "adapter": "claude-code-acp",
          "intakeAdapter": " codex-acp ",
          "actors": {
            "build": [ { "agent": "claude-code-acp", "model": "opus" }, { "agent": "codex-acp", "effort": "high" } ],
            "intake": [],
            "chat": [ { "agent": "codex-acp" } ],
            "opinion": [ { "agent": "claude-code-acp", "model": 3 } ],
            "partner": "claude-code-acp",
            "not-a-role": [ { "agent": "x" } ]
          }
        }
        """;

    private IReadOnlyList<T> Rows<T>(string sql, Func<SqliteDataReader, T> map)
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader is null ? [] : reader.Query(sql, map);
    }

    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // ——— Before the import, nothing reads and nothing is made.

    [Fact]
    public void Before_the_import_nothing_is_read_and_no_store_is_made()
    {
        Write(Full);

        var staffing = Staffing();

        Assert.Null(staffing.Read());
        Assert.False(staffing.InUse());
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void A_store_from_before_the_step_reads_nothing_and_is_not_written()
    {
        using (var older = DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(7)], Settled = () => StoreSettled.Yes }))
        {
            older.Write("probe", _ => { });
        }

        var staffing = Staffing();

        Assert.Null(staffing.Read());
        Assert.False(staffing.InUse());
    }

    // ——— The import: the file's staffing, read as today's build reads it, into the family.

    [Fact]
    public void The_file_s_staffing_is_imported_and_read_back_whole_and_the_file_is_untouched()
    {
        Write(Full);
        var bytes = File.ReadAllBytes(File_);

        var done = Staffing().ImportFile();

        Assert.Equal((StaffingImported.Done, null), (done.How, done.Said));
        Assert.Equal(bytes, File.ReadAllBytes(File_));
        Assert.False(File.Exists(File_ + ".imported"));
        var read = Assert.IsType<StaffingRead>(Staffing().Read());
        Assert.Equal(["build", "chat", "intake"], read.Actors.Keys.Order());
        Assert.Equal([new ActorChoice("claude-code-acp", "opus"), new ActorChoice("codex-acp", null, "high")], read.Actors["build"]);
        Assert.Empty(read.Actors["intake"]);
        Assert.Equal([new ActorChoice("codex-acp")], read.Actors["chat"]);
        Assert.Equal(("claude-code-acp", "codex-acp", null), (read.Adapter, read.IntakeAdapter, read.HelperAdapter));
        Assert.True(Staffing().InUse());
    }

    [Fact]
    public void An_entry_that_does_not_read_is_kept_as_found_with_why_and_holds_its_role_while_the_rest_import()
    {
        Write(Full);

        Staffing().ImportFile();

        var read = Staffing().Read()!;
        Assert.Equal(["opinion", "partner"], read.Errors.Keys.Order());
        Assert.Equal("choice 1's model or effort is not text", read.Errors["opinion"]);
        Assert.Equal("is not a list of choices", read.Errors["partner"]);
        Assert.DoesNotContain("opinion", read.Actors.Keys);
        Assert.DoesNotContain("partner", read.Actors.Keys);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""[ { "agent": "claude-code-acp", "model": 3 } ]"""), JsonNode.Parse(read.AsFound["opinion"])));
        Assert.Equal("\"claude-code-acp\"", read.AsFound["partner"]);
        var unknown = "not-a-role";
        Assert.DoesNotContain(unknown, read.Actors.Keys);
        Assert.DoesNotContain(unknown, read.Errors.Keys);
    }

    [Fact]
    public void The_four_paths_are_recorded_as_written_as_the_file_shows_them_and_an_absent_path_is_none()
    {
        Write(Full);

        Staffing().ImportFile();

        var written = Staffing().Read()!.Written;
        Assert.Equal("\"claude-code-acp\"", written.Adapter);
        Assert.Equal("\" codex-acp \"", written.IntakeAdapter);
        Assert.Null(written.HelperAdapter);
        // The path's own text, compacted, every entry of it kept as found: an unreadable one and an id this build does not know too.
        var actors = JsonNode.Parse(written.Actors!)!.AsObject();
        Assert.Equal(["build", "intake", "chat", "opinion", "partner", "not-a-role"], actors.Select(each => each.Key));
        Assert.Equal(JsonNode.Parse(Full)!["actors"]!.ToJsonString(), written.Actors);
        Assert.Equal(1, Rows("SELECT COUNT(*) FROM role_staffing_projection", row => row.GetInt32(0))[0]);
        Assert.Equal(0, Rows("SELECT COUNT(*) FROM role_staffing_found", row => row.GetInt32(0))[0]);
    }

    [Fact]
    public void A_file_with_no_staffing_imports_an_empty_one_and_every_path_is_none()
    {
        Write("""{ "cap": 2 }""");

        Assert.Equal(StaffingImported.Done, Staffing().ImportFile().How);

        var read = Staffing().Read()!;
        Assert.Empty(read.Actors);
        Assert.Empty(read.Errors);
        Assert.Equal((null, null, null), (read.Adapter, read.IntakeAdapter, read.HelperAdapter));
        Assert.Equal(new StaffingPaths(null, null, null, null), read.Written);
    }

    [Fact]
    public void The_import_is_a_projected_one_under_the_family_with_the_file_s_size_and_sha()
    {
        Write(Full);
        var sha = Sha(File_);

        Staffing().ImportFile();

        var receipt = Assert.Single(Rows(
            "SELECT family, path, sha256, class, rows, size FROM imports",
            row => (row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetInt32(4), row.GetInt64(5))));
        Assert.Equal(("role_staffing", "data/driver.json", sha, ImportClass.Projected, 5, new FileInfo(File_).Length), receipt);
        Assert.Equal("projected", ImportClass.Projected);
        Assert.Equal(1, Rows("SELECT revision FROM store_revisions WHERE family = 'role_staffing'", row => row.GetInt32(0))[0]);
    }

    // ——— Held until the start's confirmation is settled; made once.

    [Fact]
    public void While_the_confirmation_is_not_settled_nothing_is_imported_and_the_file_is_as_it_was_and_it_imports_once_settled()
    {
        Write(Full);
        var bytes = File.ReadAllBytes(File_);
        _settled = StoreSettled.Not("the install's swap journal is at `started`, which the launcher can still roll back from");

        var held = Staffing().ImportFile();

        Assert.Equal(StaffingImported.Held, held.How);
        Assert.Contains("`started`", held.Said);
        Assert.False(File.Exists(Db));
        Assert.Equal(bytes, File.ReadAllBytes(File_));
        Assert.Null(Staffing().Read());

        _settled = StoreSettled.Yes;
        Assert.Equal(StaffingImported.Done, Staffing().ImportFile().How);
        Assert.NotNull(Staffing().Read());
    }

    [Fact]
    public void The_import_is_made_once_and_a_later_edit_of_the_file_is_not_read_again_nor_a_revision_moved()
    {
        Write(Full);
        Staffing().ImportFile();
        var revision = Rows("SELECT revision FROM store_revisions WHERE family = 'role_staffing'", row => row.GetInt64(0))[0];

        Write("""{ "adapter": "codex-acp", "actors": { "build": [ { "agent": "codex-acp" } ] } }""");
        var again = Staffing().ImportFile();

        Assert.Equal(StaffingImported.Already, again.How);
        Assert.Equal(revision, Rows("SELECT revision FROM store_revisions WHERE family = 'role_staffing'", row => row.GetInt64(0))[0]);
        Assert.Equal("claude-code-acp", Staffing().Read()!.Adapter);
        Assert.Equal(1, Rows("SELECT COUNT(*) FROM imports", row => row.GetInt32(0))[0]);
    }

    // ——— A crash before the commit imports again.

    [Fact]
    public void A_kill_before_the_commit_leaves_nothing_in_the_store_and_the_next_start_imports_again_the_file_untouched()
    {
        Write(Full);
        var bytes = File.ReadAllBytes(File_);

        // A kill is no store failure the import holds for: its seam throws what nothing in the import catches.
        Assert.Throws<InvalidOperationException>(() => Staffing(beforeCommit: () => throw new InvalidOperationException("killed")).ImportFile());

        Assert.Equal(bytes, File.ReadAllBytes(File_));
        Assert.Null(Staffing().Read());
        Assert.False(Staffing().InUse());
        Assert.Equal(0, Rows("SELECT COUNT(*) FROM role_staffing", row => row.GetInt32(0))[0]);
        Assert.Equal(0, Rows("SELECT COUNT(*) FROM role_staffing_projection", row => row.GetInt32(0))[0]);
        Assert.Equal(0, Rows("SELECT COUNT(*) FROM imports", row => row.GetInt32(0))[0]);

        Assert.Equal(StaffingImported.Done, Staffing().ImportFile().How);
        Assert.Equal(["build", "chat", "intake"], Staffing().Read()!.Actors.Keys.Order());
    }

    // ——— No file, or one that does not read, is no import.

    [Fact]
    public void No_file_and_a_file_that_does_not_read_import_nothing_and_make_no_store()
    {
        var staffing = Staffing();

        Assert.Equal(StaffingImported.NoFile, staffing.ImportFile().How);

        Write("{ half a file");
        var unread = staffing.ImportFile();
        Assert.Equal(StaffingImported.Unread, unread.How);
        Assert.NotEmpty(unread.Said!);

        Write("""["not", "an", "object"]""");
        Assert.Equal(StaffingImported.Unread, staffing.ImportFile().How);

        Assert.False(File.Exists(Db));
        Assert.Equal("""["not", "an", "object"]""", File.ReadAllText(File_));
    }

    // ——— Under the staffing's record lock.

    [Fact]
    public void The_import_waits_for_the_staffing_lock_and_holds_with_nothing_written_where_it_stays_held()
    {
        Write(Full);

        using (RecordLock.Take(RecordLock.PathOf(_home, "role-staffing")))
        {
            var held = Staffing(lockWait: TimeSpan.FromMilliseconds(150)).ImportFile();

            Assert.Equal(StaffingImported.Held, held.How);
            Assert.Contains("role-staffing.lock", held.Said);
            Assert.Null(Staffing().Read());
        }

        Assert.Equal(StaffingImported.Done, Staffing().ImportFile().How);
    }

    [Fact]
    public void A_second_start_finding_the_import_already_made_under_the_lock_imports_nothing_twice()
    {
        Write(Full);
        var first = Staffing();
        var second = Staffing();

        Assert.Equal(StaffingImported.Done, first.ImportFile().How);
        Assert.Equal(StaffingImported.Already, second.ImportFile().How);
        Assert.Equal(1, Rows("SELECT COUNT(*) FROM imports WHERE family = 'role_staffing'", row => row.GetInt32(0))[0]);
    }

    // ——— The census (FOUNDATION1w-s parts 3 to 5, §19.15): who names the family, every root wired, every writer through its one write.

    private static IEnumerable<string> Production() =>
        new[] { SourceFiles.DriverProject, "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App" }
            .SelectMany(project => SourceFiles.All(project, "*.cs"));

    private static string Relative(string path) =>
        Path.GetRelativePath(SourceFiles.Project(SourceFiles.DriverProject), path).Replace(Path.DirectorySeparatorChar, '/');

    [Fact]
    public void The_staffing_family_is_named_by_its_own_files_the_roots_that_wire_it_the_loops_check_and_its_writers_doors_alone()
    {
        var own = new[] { "RoleStaffing.cs", "StaffingFile.cs", "StaffingProjection.cs", "RoleStaffingPort.cs", "DriverConfig.cs" };
        var naming = Production()
            .Where(path => !own.Contains(Path.GetFileName(path)))
            .Where(path => File.ReadAllText(path).Contains("RoleStaffing", StringComparison.Ordinal))
            .Select(Relative)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "../Daoris.Desktop.App/Program.cs", "../Daoris.Desktop.Driver.Host/Program.cs", "../Daoris.Desktop.Modules/Bridge/DriverContext.cs",
                "../Daoris.Desktop.Modules/Bridge/Routes/DriverRoutes.cs",
                "Loop/DriverWatch.cs", "Roles/ActorVerbs.cs", "Roles/StaffingVerbs.cs",
            ],
            naming);
    }

    [Fact]
    public void Each_composition_root_wires_the_staffing_before_its_first_load_of_driver_json()
    {
        foreach (var root in (string[])["Daoris.Desktop.App", "Daoris.Desktop.Driver.Host"])
        {
            var text = File.ReadAllText(Path.Combine(SourceFiles.Project(root), "Program.cs"));
            var wired = text.IndexOf("RoleStaffing.WireHome(Environment.GetEnvironmentVariable)", StringComparison.Ordinal);
            Assert.True(wired > 0, $"{root} wires no staffing");
            // The shell's own loads are its loop's and routes', made after Program wires it; the host's are its doors' and its loop's.
            foreach (var first in (string[])["DriverConfig.Load(", "DriverConfig.FindHome(", "new DriverLoop("])
            {
                var at = text.IndexOf(first, StringComparison.Ordinal);
                Assert.True(at < 0 || wired < at, $"{root} reaches `{first}` before it wires the staffing");
            }
        }
    }

    [Fact]
    public void No_production_writer_hands_a_staffing_change_to_the_configurations_write_nor_parses_driver_json_outside_its_load()
    {
        var handed = new System.Text.RegularExpressions.Regex(@"(Change|Write|Save)\(.*(ActorChoices\.With\(|\.WithIntake\(|\.WithHelper\()");
        var lines = Production()
            .SelectMany(path => File.ReadLines(path).Select((line, at) => (Path: Relative(path), At: at + 1, Line: line)))
            .ToList();

        Assert.Empty(lines.Where(each => handed.IsMatch(each.Line)).Select(each => $"{each.Path}:{each.At}"));
        Assert.Equal(["Roles/StaffingProjection.cs"], lines.Where(each => each.Line.Contains("DriverConfig.Parse(", StringComparison.Ordinal))
            .Select(each => each.Path).Distinct());
    }

    [Fact]
    public void A_root_wires_the_homes_own_file_and_never_a_file_the_variable_names()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        try
        {
            Assert.Null(RoleStaffing.WireHome(name => name switch
            {
                DaorisHome.Variable => _home,
                DriverConfig.PathVariable => file,
                _ => null,
            }));
            Assert.False(RoleStaffingPort.IsWired(file));

            Assert.NotNull(RoleStaffing.WireHome(name => name == DaorisHome.Variable ? _home : null));
            Assert.True(RoleStaffingPort.IsWired(file));
        }
        finally
        {
            RoleStaffingPort.Unwire(file);
        }
    }
}
