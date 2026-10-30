using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s part 2 (D212's note, the design §19.15 step 3, C8-1.1): the file step writes the store's staffing projection onto
/// <c>driver.json</c>'s four staffing paths alone, only where the file shows the expected baseline, never writing a file that does not read,
/// and renames only while its keeping lock still holds its text. Nothing calls it yet; the cases hand it the baseline and the projection.
/// </summary>
public sealed class StaffingFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-staffing-file-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _file;

    public StaffingFileTests()
    {
        Directory.CreateDirectory(_folder);
        _file = Path.Combine(_folder, "driver.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string Found =
        """
        {
          "drivable": [ "engine" ],
          "cap": 3,
          "adapter": "claude-code-acp",
          "intakeAdapter": "codex-acp",
          "actors": {
            "build": [ { "agent": "claude-code-acp" } ],
            "intake": [ { "agent": "codex-acp" } ],
            "not-a-role": [ { "agent": "x" } ]
          },
          "notify": true,
          "unknown": { "keep": [ 1, 2 ] }
        }
        """;

    private static readonly StaffingPaths Baseline = new(
        """{"build":[{"agent":"claude-code-acp"}],"intake":[{"agent":"codex-acp"}],"not-a-role":[{"agent":"x"}]}""",
        "\"claude-code-acp\"", "\"codex-acp\"", null);

    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static StaffingPaths PathsOf(string text)
    {
        using var document = JsonDocument.Parse(text);
        string? Of(string name) => document.RootElement.TryGetProperty(name, out var value) ? RoleStaffing.Compact(value) : null;
        return new StaffingPaths(Of("actors"), Of("adapter"), Of("intakeAdapter"), Of("helperAdapter"));
    }

    private static string Rest(string text)
    {
        using var document = JsonDocument.Parse(text);
        var kept = document.RootElement.EnumerateObject()
            .Where(property => property.Name is not ("actors" or "adapter" or "intakeAdapter" or "helperAdapter"))
            .Select(property => $"{property.Name}={RoleStaffing.Compact(property.Value)}");
        return string.Join(";", kept);
    }

    private string[] Beside() => Directory.GetFiles(_folder, "*.writing");

    [Fact]
    public void Where_the_file_shows_the_baseline_the_projection_is_written_onto_the_four_paths_and_nothing_else_moves()
    {
        File.WriteAllText(_file, Found);
        var projection = new StaffingPaths(
            """{"build":[{"agent":"codex-acp","model":"opus"}],"intake":[],"not-a-role":[{"agent":"x"}]}""", "\"codex-acp\"", null, "\"helper-acp\"");

        var done = StaffingFile.Project(_file, Baseline, projection);

        Assert.Equal(StaffingFiled.Written, done.How);
        var after = File.ReadAllText(_file);
        Assert.Equal(projection, PathsOf(after));
        Assert.Equal(Rest(Found), Rest(after));
        Assert.False(File.Exists(KeepLock.PathOf(_file)));
        Assert.Empty(Beside());
    }

    [Fact]
    public void A_path_the_file_lacks_is_added_and_a_path_the_projection_lacks_is_taken_out()
    {
        File.WriteAllText(_file, "{\n  \"cap\": 3,\n  \"adapter\": \"a\",\n  \"notify\": true\n}\n");
        var expected = new StaffingPaths(null, "\"a\"", null, null);
        var projection = new StaffingPaths("""{"build":[{"agent":"b"}]}""", null, "\"i\"", null);

        Assert.Equal(StaffingFiled.Written, StaffingFile.Project(_file, expected, projection).How);

        var after = File.ReadAllText(_file);
        Assert.Equal(projection, PathsOf(after));
        Assert.Equal("cap=3;notify=true", Rest(after));
    }

    [Fact]
    public void A_text_that_escapes_a_letter_or_holds_non_ascii_reads_back_from_the_file_as_the_projection_holds_it()
    {
        File.WriteAllText(_file, "{}\n");
        var projection = new StaffingPaths("""{"build":[{"agent":"a<b&c","model":"méthode 道"}]}""", "\"é\"", null, null);

        Assert.Equal(StaffingFiled.Written, StaffingFile.Project(_file, new StaffingPaths(null, null, null, null), projection).How);

        Assert.Equal(projection, PathsOf(File.ReadAllText(_file)));
    }

    [Fact]
    public void A_file_in_crlf_stays_in_crlf()
    {
        File.WriteAllText(_file, Found.Replace("\n", "\r\n") + "\r\n");

        Assert.Equal(StaffingFiled.Written, StaffingFile.Project(_file, Baseline, Baseline with { Adapter = "\"codex-acp\"" }).How);

        Assert.DoesNotContain("\n", File.ReadAllText(_file).Replace("\r\n", ""));
    }

    [Fact]
    public void A_projection_that_is_the_baseline_writes_nothing()
    {
        File.WriteAllText(_file, Found);
        var before = File.GetLastWriteTimeUtc(_file);

        var done = StaffingFile.Project(_file, Baseline, Baseline);

        Assert.Equal(StaffingFiled.Unchanged, done.How);
        Assert.Equal(Found, File.ReadAllText(_file));
        Assert.Equal(before, File.GetLastWriteTimeUtc(_file));
    }

    [Theory]
    [InlineData("actors")]
    [InlineData("adapter")]
    [InlineData("intakeAdapter")]
    [InlineData("helperAdapter")]
    public void A_file_whose_path_moved_since_the_baseline_is_not_written_and_says_what_it_shows(string path)
    {
        var moved = path switch
        {
            "actors" => Found.Replace("\"intake\": [ { \"agent\": \"codex-acp\" } ],", "\"intake\": [],"),
            "adapter" => Found.Replace("\"adapter\": \"claude-code-acp\"", "\"adapter\": \"other\""),
            "intakeAdapter" => Found.Replace("\"intakeAdapter\": \"codex-acp\",", ""),
            _ => Found.Replace("\"cap\": 3,", "\"cap\": 3, \"helperAdapter\": \"h\","),
        };
        File.WriteAllText(_file, moved);

        var done = StaffingFile.Project(_file, Baseline, Baseline with { Adapter = "\"codex-acp\"" });

        Assert.Equal(StaffingFiled.Moved, done.How);
        Assert.Equal(PathsOf(moved), done.Found);
        Assert.Equal(moved, File.ReadAllText(_file));
        Assert.Empty(Beside());
    }

    [Theory]
    [InlineData("{ \"cap\": ")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"adapter\": \"a\", \"adapter\": \"b\" }")]
    public void A_file_that_does_not_read_is_never_written(string text)
    {
        File.WriteAllText(_file, text);

        var done = StaffingFile.Project(_file, new StaffingPaths(null, null, null, null), Baseline);

        Assert.Equal(StaffingFiled.Unread, done.How);
        Assert.False(string.IsNullOrEmpty(done.Said));
        Assert.Equal(text, File.ReadAllText(_file));
        Assert.Empty(Beside());
    }

    [Fact]
    public void A_file_that_is_gone_is_made_from_nothing_only_where_the_baseline_is_nothing()
    {
        var none = new StaffingPaths(null, null, null, null);

        Assert.Equal(StaffingFiled.Moved, StaffingFile.Project(_file, Baseline, Baseline).How);
        Assert.False(File.Exists(_file));

        Assert.Equal(StaffingFiled.Written, StaffingFile.Project(_file, none, Baseline).How);
        Assert.Equal(Baseline, PathsOf(File.ReadAllText(_file)));
    }

    [Fact]
    public void A_file_step_whose_lock_another_writer_took_before_its_rename_writes_nothing_and_says_so()
    {
        File.WriteAllText(_file, Found);
        // A second back, so another writer's text never equals this step's own, taken by this process in the same millisecond.
        var taken = $"{{\"pid\":{Environment.ProcessId},\"since\":\"{Moment(DateTimeOffset.UtcNow.AddSeconds(-1))}\"}}\n";

        var done = StaffingFile.Project(
            _file, Baseline, Baseline with { Adapter = "\"codex-acp\"" }, wait: null,
            new KeepSeams { BeforeLook = () => File.WriteAllText(KeepLock.PathOf(_file), taken) });

        Assert.Equal(StaffingFiled.Refused, done.How);
        Assert.Contains("not written", done.Said);
        Assert.Equal(Found, File.ReadAllText(_file));
        Assert.Equal(taken, File.ReadAllText(KeepLock.PathOf(_file)));
        Assert.Empty(Beside());
    }

    [Fact]
    public void A_file_step_that_finds_the_lock_held_for_the_whole_of_its_wait_writes_nothing_and_says_so()
    {
        File.WriteAllText(_file, Found);
        File.WriteAllText(KeepLock.PathOf(_file), $"{{\"pid\":{Environment.ProcessId},\"since\":\"{Moment(DateTimeOffset.UtcNow)}\"}}\n");

        var done = StaffingFile.Project(_file, Baseline, Baseline with { Adapter = "\"codex-acp\"" }, TimeSpan.FromMilliseconds(100));

        Assert.Equal(StaffingFiled.Refused, done.How);
        Assert.Contains(KeepLock.PathOf(_file), done.Said);
        Assert.Equal(Found, File.ReadAllText(_file));
    }

    [Fact]
    public void A_file_step_under_a_lock_its_caller_already_holds_writes_under_that_lock_and_leaves_it_held()
    {
        File.WriteAllText(_file, Found);

        using var held = KeepLock.Take(_file);
        var done = StaffingFile.ProjectHolding(_file, held, Baseline, Baseline with { Adapter = "\"codex-acp\"" });

        Assert.Equal(StaffingFiled.Written, done.How);
        Assert.True(held.Holds());
        Assert.Equal("\"codex-acp\"", PathsOf(File.ReadAllText(_file)).Adapter);
    }
}
