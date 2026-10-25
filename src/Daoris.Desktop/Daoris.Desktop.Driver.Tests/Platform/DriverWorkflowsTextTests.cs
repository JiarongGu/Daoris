using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (PUSH1f2b, D194 point 6, the alone-path design §3): each case of <c>daoris driver</c>'s workflows,
/// <c>work-cycle</c>, <c>brainstorms</c> and a landing's <c>--cycle|--no-cycle</c>, as the CLI's TypeScript verb said it before it
/// retired (<c>fixtures/driver-workflows-text.json</c>), said again by <see cref="DriverChoicesCommand"/> acting alone on a home of its
/// own, the terminal in it: the same lines on each stream, the same exit, <c>driver.json</c> after it the same choices, and every other
/// file it wrote the same. A row the port changes on purpose says why under <c>changed</c>.
/// </summary>
public sealed partial class DriverWorkflowsTextTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-09T09:12:34.567Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly Lazy<JsonElement> Read = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "driver-workflows-text.json"))).RootElement.Clone());

    public static TheoryData<string> Names()
    {
        var names = new TheoryData<string>();
        foreach (var row in Read.Value.GetProperty("rows").EnumerateArray()) names.Add(row.GetProperty("name").GetString()!);
        return names;
    }

    [Fact]
    public void The_table_holds_every_verb_the_row_moved_and_every_workflow_verb()
    {
        var rows = Read.Value.GetProperty("rows").EnumerateArray().ToList();
        Assert.True(rows.Count >= 150, $"only {rows.Count} rows, so this proves little");
        var argvs = rows.Select(row => row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()!).ToList()).ToList();
        Assert.All(["work-cycle", "brainstorms", "workflow"], verb => Assert.Contains(argvs, argv => argv.FirstOrDefault() == verb));
        Assert.Contains(argvs, argv => argv.FirstOrDefault() == "landing" && argv.Contains("--cycle"));
        Assert.Contains(argvs, argv => argv.FirstOrDefault() == "landing" && argv.Contains("--no-cycle"));
        Assert.All(DriverWorkflowsCommand.Verbs, verb => Assert.Contains(argvs, argv => argv is ["workflow", var each, ..] && each == verb));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Each_case_says_today_s_text_and_writes_the_same_files(string name)
    {
        var row = Read.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-workflows-text", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var config = DaorisHome.Entry(home, HomeEntry.DriverConfig);
            Directory.CreateDirectory(Path.GetDirectoryName(config)!);
            if (row.GetProperty("before").GetString() is { } before) File.WriteAllText(config, before);
            if (row.TryGetProperty("files", out var files))
            {
                foreach (var file in files.EnumerateObject())
                {
                    var path = Path.Combine(home, file.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, file.Value.GetString());
                }
            }

            IReadOnlyList<(string Repository, string? Workspace, string? Root)>? registry = row.TryGetProperty("registry", out var listed)
                ? [.. listed.EnumerateArray().Select(each => (
                    each.GetProperty("repository").GetString()!,
                    each.GetProperty("workspace").GetString(),
                    each.GetProperty("root").GetString() is { } root ? Path.Combine(home, root) : null))]
                : null;
            var output = new StringWriter();
            var error = new StringWriter();
            var world = new DriverChoicesWorld
            {
                Verbs = DriverChoiceVerbs.With((_, _) => throw new InvalidOperationException("no row of the table asks for the verdicts")),
                Instance = new InstanceWorld
                {
                    Home = home,
                    Environment = variable => variable == DriverConfig.PathVariable ? config : variable == DaorisHome.Variable ? home : null,
                    Out = output,
                    Error = error,
                    Verb = "daoris",
                },
                ConfigPath = config,
                Folder = home,
                Now = () => Now,
                Registry = _ => Task.FromResult(registry is null ? new ChoicesRegistry(null, "the service did not answer") : new ChoicesRegistry(registry, null)),
            };

            var exit = await DriverChoicesCommand.RunAsync([.. row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()!)], world, CancellationToken.None);

            // A path under the home is written with `/`, on any machine.
            string Spelled(string text) => UnderHome().Replace(
                text.Replace(config, "<config>", StringComparison.Ordinal).Replace(home, "<home>", StringComparison.Ordinal),
                match => match.Value.Replace('\\', '/'));
            static IReadOnlyList<string> Lines(string text) =>
                text.Length == 0 ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            Assert.Equal(Expected(row, "out"), Lines(Spelled(output.ToString())));
            Assert.Equal(Expected(row, "err"), Lines(Spelled(error.ToString())));
            Assert.Equal(row.GetProperty("exit").GetInt32(), exit);

            Same($"driver.json after `{name}`", row.GetProperty("after").GetString(), File.Exists(config) ? Spelled(File.ReadAllText(config)) : null);
            if (row.TryGetProperty("written", out var written))
            {
                foreach (var file in written.EnumerateObject())
                {
                    var path = Path.Combine(home, file.Name);
                    Same($"{file.Name} after `{name}`", file.Value.GetString(), File.Exists(path) ? File.ReadAllText(path) : null);
                }
            }

            // Nothing else under the home was written: every file there now was there before, or the row says it was written.
            var said = written.ValueKind == JsonValueKind.Object ? written.EnumerateObject().Select(file => file.Name).ToHashSet(StringComparer.Ordinal) : [];
            var kept = files.ValueKind == JsonValueKind.Object ? files.EnumerateObject().Select(file => file.Name).ToHashSet(StringComparer.Ordinal) : [];
            foreach (var path in Directory.EnumerateFiles(home, "*", SearchOption.AllDirectories).Where(path => path != config))
            {
                var relative = Path.GetRelativePath(home, path).Replace('\\', '/');
                Assert.True(said.Contains(relative) || kept.Contains(relative), $"`{name}` wrote {relative}, which the row does not say");
            }
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }

    /// <summary>A file as the row says it stands: none where null; else the same JSON, every value as written, or the same text.</summary>
    private static void Same(string what, string? expected, string? actual)
    {
        if (expected is null)
        {
            Assert.True(actual is null, $"{what}: written, and the row says none:\n{actual}");
            return;
        }

        Assert.True(actual is not null, $"{what}: not written, and the row says\n{expected}");
        JsonNode? want;
        try
        {
            want = JsonNode.Parse(expected);
        }
        catch (JsonException)
        {
            Assert.Equal(expected, actual);
            return;
        }

        Assert.True(JsonNode.DeepEquals(want, JsonNode.Parse(actual!)), $"{what}:\n{actual}\nexpected:\n{expected}");
    }

    private static IReadOnlyList<string> Expected(JsonElement row, string stream) =>
        [.. row.GetProperty(stream).EnumerateArray().Select(each => each.GetString()!)];

    [GeneratedRegex("<home>[^\\s`'\"),;]*")]
    private static partial Regex UnderHome();
}
