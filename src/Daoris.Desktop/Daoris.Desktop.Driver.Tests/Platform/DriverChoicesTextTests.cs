using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (PUSH1f2a, D194 point 6, the alone-path design §3): each case of <c>daoris driver</c>'s standing choices,
/// as the CLI's TypeScript verb said it before its branches retired (<c>fixtures/driver-choices-text.json</c>), said again by
/// <see cref="DriverChoicesCommand"/> acting alone on a home of its own: the same lines on each stream, the same exit, and
/// <c>driver.json</c> after it the same choices. A row the port changes on purpose says why under <c>changed</c>.
/// </summary>
public sealed partial class DriverChoicesTextTests
{
    private static readonly Lazy<JsonElement> Read = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "driver-choices-text.json"))).RootElement.Clone());

    public static TheoryData<string> Names()
    {
        var names = new TheoryData<string>();
        foreach (var row in Read.Value.GetProperty("rows").EnumerateArray()) names.Add(row.GetProperty("name").GetString()!);
        return names;
    }

    [Fact]
    public void The_table_holds_every_verb_the_command_answers()
    {
        var rows = Read.Value.GetProperty("rows").EnumerateArray().ToList();
        Assert.True(rows.Count >= 100, $"only {rows.Count} rows, so this proves little");
        var verbs = rows.Select(row => row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()).FirstOrDefault() ?? "list")
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(DriverChoicesCommand.Verbs, verb => Assert.Contains(verb, verbs));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Each_case_says_today_s_text_and_writes_the_same_choices(string name)
    {
        var row = Read.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-choices-text", Guid.NewGuid().ToString("N")[..8]);
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
                Instance = new InstanceWorld
                {
                    Home = home,
                    Environment = variable => variable == DriverConfig.PathVariable ? config : variable == DaorisHome.Variable ? home : null,
                    Out = output,
                    Error = error,
                    Verb = "daoris",
                },
                ConfigPath = config,
                Registry = _ => Task.FromResult(registry is null ? new ChoicesRegistry(null, "the service did not answer") : new ChoicesRegistry(registry, null)),
                Verdicts = (_, _) => throw new InvalidOperationException("no row of the table asks for the verdicts"),
            };

            var exit = await DriverChoicesCommand.RunAsync([.. row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()!)], world, CancellationToken.None);

            string Spelled(string text) => text.Replace(config, "<config>", StringComparison.Ordinal).Replace(home, "<home>", StringComparison.Ordinal);
            static IReadOnlyList<string> Lines(string text) =>
                text.Length == 0 ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            Assert.Equal(Expected(row, "out"), Lines(Spelled(output.ToString())));
            Assert.Equal(Expected(row, "err"), Lines(Spelled(error.ToString())));
            Assert.Equal(row.GetProperty("exit").GetInt32(), exit);

            var after = row.GetProperty("after").GetString();
            var written = File.Exists(config) ? Moment().Replace(Spelled(File.ReadAllText(config)), "\"at\": \"<at>\"") : null;
            if (after is null)
            {
                Assert.Null(written);
            }
            else
            {
                Assert.NotNull(written);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after), JsonNode.Parse(written)), $"driver.json after `{name}`:\n{written}\nexpected:\n{after}");
            }
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    private static IReadOnlyList<string> Expected(JsonElement row, string stream) =>
        [.. row.GetProperty(stream).EnumerateArray().Select(each => each.GetString()!)];

    [GeneratedRegex("\"at\": ?\"[^\"]+\"")]
    private static partial Regex Moment();
}
