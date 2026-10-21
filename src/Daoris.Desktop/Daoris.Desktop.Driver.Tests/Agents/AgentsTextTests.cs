using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (PUSH1f2d, D194 point 6, the alone-path design §3): each case of <c>daoris agent rules|trust|settings</c>,
/// as the CLI's TypeScript verb said it before it retired (<c>fixtures/agents-text.json</c>), said again by <see cref="AgentsCommand"/>
/// acting alone on a root of its own: the same lines on each stream, the same exit, and every file under the root after it the same. A
/// row the port changes on purpose says why under <c>changed</c>.
/// </summary>
public sealed partial class AgentsTextTests
{
    private static readonly Lazy<JsonElement> Read = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "agents-text.json"))).RootElement.Clone());

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
        Assert.True(rows.Count >= 60, $"only {rows.Count} rows, so this proves little");
        var verbs = rows.Select(row => row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()).First()).ToHashSet(StringComparer.Ordinal);
        Assert.All(AgentsCommand.Verbs, verb => Assert.Contains(verb, verbs));
    }

    /// <summary>A JSON reader's own reason for the table's unreadable file, which each runtime words its own way: spelled <c>&lt;why&gt;</c>.</summary>
    private static readonly string Why = ((Func<string>)(() =>
    {
        try
        {
            JsonNode.Parse("{ not json");
            return "";
        }
        catch (JsonException error)
        {
            return error.Message;
        }
    }))();

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Each_case_says_today_s_text_and_writes_the_same_files(string name)
    {
        var row = Read.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        var root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agents-text", Guid.NewGuid().ToString("N")[..8]);
        foreach (var sub in new[] { "home", "user", "work" }) Directory.CreateDirectory(Path.Combine(root, sub));
        try
        {
            var slashed = root.Replace('\\', '/');
            string Placed(string text) => text.Replace("<root>", slashed, StringComparison.Ordinal);
            string At(string relative) => Path.Combine([root, .. relative.TrimEnd('/').Split('/')]);
            foreach (var folder in row.GetProperty("folders").EnumerateArray()) Directory.CreateDirectory(At(folder.GetString()!));
            foreach (var file in row.GetProperty("before").EnumerateObject())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(At(file.Name))!);
                File.WriteAllText(At(file.Name), Placed(file.Value.GetString()!));
            }

            var home = !row.TryGetProperty("home", out var hasHome) || hasHome.GetBoolean() ? Path.Combine(root, "home") : null;
            var wiring = row.TryGetProperty("harness", out var named) && named.GetBoolean()
                ? Path.Combine(root, "other", "data", "harnesses.json")
                : null;
            var output = new StringWriter();
            var error = new StringWriter();
            var world = new AgentsWorld
            {
                Instance = new InstanceWorld
                {
                    Home = home,
                    Environment = variable => variable == DaorisHome.Variable ? home : variable == HarnessSettings.PathVariable ? wiring : null,
                    Out = output,
                    Error = error,
                    Verb = "daoris",
                },
                Folder = root,
                UserHome = Path.Combine(root, "user"),
                Adapters = _ => AdapterSet.Built(),
            };

            string[] argv = [.. row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()!)
                .Select(word => word.StartsWith("<root>/", StringComparison.Ordinal)
                    ? Path.Combine([root, .. word["<root>/".Length..].TrimEnd('/').Split('/')]) + (word.EndsWith('/') ? "\\" : "")
                    : word)];
            var exit = await AgentsCommand.RunAsync(argv, world, CancellationToken.None);

            string Spelled(string text) => Settled().Replace(text
                .Replace(JsonEncodedText.Encode(root).ToString(), "<root>", StringComparison.Ordinal)
                .Replace(root, "<root>", StringComparison.Ordinal)
                .Replace(slashed, "<root>", StringComparison.Ordinal)
                .Replace("\\\\", "/", StringComparison.Ordinal)
                .Replace('\\', '/')
                .Replace($"({Why})", "(<why>)", StringComparison.Ordinal), "$1\"<at>\"");
            static IReadOnlyList<string> Lines(string text) =>
                text.Length == 0 ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            string[] said = [.. Lines(output.ToString()).Select(Spelled)];
            string[] refused = [.. Lines(error.ToString()).Select(Spelled)];
            var shown = $"`{name}` said, exit {exit}:\n{string.Join('\n', said)}\non the error stream:\n{string.Join('\n', refused)}";
            Assert.True(Expected(row, "out").SequenceEqual(said), shown);
            Assert.True(Expected(row, "err").SequenceEqual(refused), shown);
            Assert.True(row.GetProperty("exit").GetInt32() == exit, shown);

            var written = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), path => Spelled(File.ReadAllText(path)), StringComparer.Ordinal);
            var after = row.GetProperty("after").EnumerateObject().ToDictionary(file => file.Name, file => file.Value.GetString()!, StringComparer.Ordinal);
            Assert.True(after.Keys.Order(StringComparer.Ordinal).SequenceEqual(written.Keys.Order(StringComparer.Ordinal)),
                $"the files after `{name}`: {string.Join(", ", written.Keys.Order(StringComparer.Ordinal))}\nexpected: {string.Join(", ", after.Keys.Order(StringComparer.Ordinal))}");
            foreach (var (path, expected) in after)
            {
                var actual = written[path];
                if (JsonNodeOf(expected) is { } wanted && JsonNodeOf(actual) is { } got)
                {
                    Assert.True(JsonNode.DeepEquals(wanted, got), $"{path} after `{name}`:\n{actual}\nexpected:\n{expected}");
                }
                else
                {
                    Assert.True(expected == actual, $"{path} after `{name}`:\n{actual}\nexpected:\n{expected}");
                }
            }
        }
        finally
        {
            GitSeed.Discard(root);
        }
    }

    private static IReadOnlyList<string> Expected(JsonElement row, string stream) =>
        [.. row.GetProperty(stream).EnumerateArray().Select(each => each.GetString()!)];

    private static JsonNode? JsonNodeOf(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A proposal's settling moment, the run's own: the table spells it <c>&lt;at&gt;</c>.</summary>
    [GeneratedRegex("(\"at\": ?)\"\\d{4}-\\d\\d-\\d\\dT[^\"]*\"")]
    private static partial Regex Settled();
}
