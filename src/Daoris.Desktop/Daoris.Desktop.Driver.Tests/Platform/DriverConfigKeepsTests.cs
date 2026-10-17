using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-GRAPH1's review, finding 1 (FIX-LOG 2026-10-17): a door that edits <c>driver.json</c> (Setup, Ask Daoris's proposals, the
/// terminal) keeps every key the writer does not own, in its place, and changes only what it edits. One table both twins read,
/// <c>test/fixtures/driver-config-keeps.json</c>, row for row with the CLI's <c>configkeeps.test.ts</c>: an existing file in the writer's
/// layout with keys neither twin knows at each level, one door's edit, and the bytes both write.
/// </summary>
public sealed class DriverConfigKeepsTests
{
    [Theory]
    [MemberData(nameof(Rows))]
    public void An_edit_changes_only_what_it_edits(string name, string edit, string expected)
    {
        var config = Applied(DriverConfig.Parse(File), JsonDocument.Parse(edit).RootElement);
        Assert.Equal((name, expected), (name, config.ToJson()));
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
            "notify" => config.WithNotify(edit.GetProperty("on").GetBoolean()),
            "work-cycle-clear" => config.WithWorkCycle(Text("repository"), Text("kind"), null),
            var op => throw new InvalidOperationException($"the table names an edit this test does not make: {op}"),
        };
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

    public static TheoryData<string, string, string> Rows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var row in Table.RootElement.GetProperty("rows").EnumerateArray())
        {
            var expected = File;
            foreach (var change in row.GetProperty("changes").EnumerateArray())
            {
                var from = Lines(change.GetProperty("from"));
                var at = expected.IndexOf(from, StringComparison.Ordinal);
                if (at < 0 || expected.IndexOf(from, at + 1, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException($"a change's `from` is in the file once: {from}");
                }

                expected = expected[..at] + Lines(change.GetProperty("to")) + expected[(at + from.Length)..];
            }

            rows.Add(row.GetProperty("name").GetString()!, row.GetProperty("edit").GetRawText(), expected);
        }

        return rows;
    }
}
