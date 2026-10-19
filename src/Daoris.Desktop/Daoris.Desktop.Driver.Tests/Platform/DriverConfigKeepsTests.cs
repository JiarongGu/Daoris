using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-GRAPH1's review, finding 1 (FIX-LOG 2026-10-17): a door that edits <c>driver.json</c> (Setup, Ask Daoris's proposals, the
/// terminal) keeps every key the writer does not own, in its place, and changes only what it edits. One table both twins read,
/// <c>test/fixtures/driver-config-keeps.json</c>, row for row with the CLI's <c>configkeeps.test.ts</c>: an existing file in the writer's
/// layout with keys neither twin knows at each level, one door's edit, and the bytes both write. The re-check (findings 1 and 3) adds
/// another writer's change made between this one's read and its write, a file in CRLF, a file that no longer reads, and a key written twice.
/// </summary>
public sealed class DriverConfigKeepsTests
{
    [Theory]
    [MemberData(nameof(Rows))]
    public void An_edit_changes_only_what_it_edits(string name, string edit, bool missing, string start, string? between, string? refused, string expected)
    {
        var folder = Path.Combine(Path.GetTempPath(), "daoris-keeps-" + Guid.NewGuid().ToString("N")[..8]);
        var path = Path.Combine(folder, "driver.json");
        try
        {
            Directory.CreateDirectory(folder);
            // The second re-check's finding 2: a row `missing` is read where no file was.
            if (!missing) System.IO.File.WriteAllText(path, start);
            var config = DriverConfig.Load(path);
            // The re-check's finding 1: another writer's change, made after this one read the file and before it writes.
            if (between is not null) System.IO.File.WriteAllText(path, between);
            var edited = Applied(config, JsonDocument.Parse(edit).RootElement);
            if (refused is null) edited.Save(path);
            else Assert.Contains(refused, Assert.Throws<DriverConfigUnreadableException>(() => edited.Save(path)).Message);
            Assert.Equal((name, expected), (name, System.IO.File.ReadAllText(path)));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The re-check's finding 3: one rule for a key written twice in one object, the table's <c>twice</c>.</summary>
    [Fact]
    public void A_key_written_twice_in_one_object_reads_as_its_last_in_its_first_place_and_refuses_every_edit_naming_it()
    {
        var twice = Table.RootElement.GetProperty("twice");
        var text = Changed(twice.GetProperty("changes"), crlf: false);
        var config = DriverConfig.Parse(text);
        Assert.Equal(twice.GetProperty("notify").GetBoolean(), config.Notify);
        Assert.Equal(
            twice.GetProperty("workCycles").EnumerateObject()
                .SelectMany(owner => owner.Value.EnumerateObject().Select(kind => $"{owner.Name}/{kind.Name} {NodeGraphHomes.Compact(kind.Value)}")),
            config.WorkCycles.SelectMany(owner => owner.Value.Select(kind => $"{owner.Key}/{kind.Key} {kind.Value}")));

        var folder = Path.Combine(Path.GetTempPath(), "daoris-keeps-" + Guid.NewGuid().ToString("N")[..8]);
        var path = Path.Combine(folder, "driver.json");
        try
        {
            Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(path, text);
            var refusal = Assert.Throws<DriverConfigUnreadableException>(() => DriverConfig.Load(path).WithNotify(true).Save(path));
            Assert.Contains(twice.GetProperty("refused").GetString()!, refusal.Message);
            Assert.Equal(text, System.IO.File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The edit through a door's own method on the config, as Setup, Ask Daoris and the terminal call it.</summary>
    private static DriverConfig Applied(DriverConfig config, JsonElement edit)
    {
        string Text(string field) => edit.GetProperty(field).GetString()!;
        return Text("op") switch
        {
            "none" => config,
            "drivable" => config.WithDrivable(Text("repository"), edit.GetProperty("on").GetBoolean()),
            "landing" => config.WithLanding(Text("repository"), Rule(edit.GetProperty("rule"))),
            "workspace-landing" => config.WithWorkspaceLanding(Text("workspace"), Rule(edit.GetProperty("rule"))),
            "landing-clear" => config.WithLanding(Text("repository"), null),
            "line" => config.WithLine(Text("repository"), Text("branch")),
            "intake" => config.WithIntake(Text("adapter")),
            "help-mode" => config.WithHelpMode(Text("mode")),
            // AUTONOMY1a: the level the row names, through the access edit the route and the terminal make.
            "access" => config.WithAccess(Access(edit, new AccessEdit { Repository = Text("repository") })),
            "machine-access" => config.WithAccess(Access(edit, new AccessEdit { Machine = true })),
            "notify" => config.WithNotify(edit.GetProperty("on").GetBoolean()),
            "work-cycle-clear" => config.WithWorkCycle(Text("repository"), Text("kind"), null),
            var op => throw new InvalidOperationException($"the table names an edit this test does not make: {op}"),
        };
    }

    /// <summary>A row's <c>setting</c> as an edit at <paramref name="level"/>: <c>"full"</c>, or each pause it names.</summary>
    private static AccessEdit Access(JsonElement edit, AccessEdit level)
    {
        var setting = edit.GetProperty("setting");
        return setting.ValueKind == JsonValueKind.String
            ? level with { Full = true }
            : level with { Set = [.. setting.EnumerateObject().Select(pause => new KeyValuePair<string, string?>(pause.Name, pause.Value.GetString()))] };
    }

    private static LandingRule Rule(JsonElement rule)
    {
        string? Text(string field) => rule.TryGetProperty(field, out var value) ? value.GetString() : null;
        bool On(string field) => rule.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.True;
        return new LandingRule(Text("form")!, Text("pattern"), Tidy: On("tidy"), Plugin: Text("plugin"), AutoAccept: On("autoAccept"), Key: Text("key"), Squash: On("squash"));
    }

    private static readonly JsonDocument Table =
        JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "driver-config-keeps.json")));

    private static string Lines(JsonElement lines) => string.Join("\n", lines.EnumerateArray().Select(line => line.GetString()));

    private static readonly string File = Lines(Table.RootElement.GetProperty("file")) + "\n";

    /// <summary>The file with <paramref name="changes"/> made, its lines ended by CRLF where <paramref name="crlf"/> says.</summary>
    private static string Changed(JsonElement? changes, bool crlf)
    {
        var text = File;
        foreach (var change in changes is { } made ? made.EnumerateArray().ToList() : [])
        {
            var from = Lines(change.GetProperty("from"));
            var at = text.IndexOf(from, StringComparison.Ordinal);
            if (at < 0 || text.IndexOf(from, at + 1, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException($"a change's `from` is in the file once: {from}");
            }

            text = text[..at] + Lines(change.GetProperty("to")) + text[(at + from.Length)..];
        }

        return crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    public static TheoryData<string, string, bool, string, string?, string?, string> Rows()
    {
        var rows = new TheoryData<string, string, bool, string, string?, string?, string>();
        foreach (var row in Table.RootElement.GetProperty("rows").EnumerateArray())
        {
            var crlf = row.TryGetProperty("crlf", out var ends) && ends.GetBoolean();
            var missing = row.TryGetProperty("missing", out var none) && none.GetBoolean();
            var between = row.TryGetProperty("between", out var made) ? Changed(made, crlf) : null;
            var refused = row.TryGetProperty("refused", out var said) ? said.GetString() : null;
            rows.Add(row.GetProperty("name").GetString()!, row.GetProperty("edit").GetRawText(), missing, Changed(null, crlf), between, refused,
                Changed(row.GetProperty("changes"), crlf));
        }

        return rows;
    }

    /// <summary>
    /// The second re-check's finding 2, its plain side: a config read where no file was, saved where there still is none, writes the edit's
    /// changed keys alone (the defaults it never changed stay unwritten, as the reader reads them), and carries the path and <c>{}</c>.
    /// </summary>
    [Fact]
    public void A_config_read_where_no_file_was_writes_the_edits_keys_alone()
    {
        var folder = Path.Combine(Path.GetTempPath(), "daoris-keeps-" + Guid.NewGuid().ToString("N")[..8]);
        var path = Path.Combine(folder, "driver.json");
        try
        {
            var config = DriverConfig.Load(path);
            Assert.Equal(("{}", Path.GetFullPath(path)), (config.Source, config.SourceFile));
            config.WithNotify(false).Save(path);
            Assert.Equal("{\n  \"notify\": false\n}\n", System.IO.File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
