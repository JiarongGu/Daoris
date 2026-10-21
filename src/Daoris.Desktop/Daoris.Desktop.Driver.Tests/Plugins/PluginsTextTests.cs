using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (PUSH1f2f, D194 point 6, the alone-path design §3): each case of <c>daoris plugin</c>, as the CLI's
/// TypeScript verb said it before it retired (<c>fixtures/plugins-text.json</c>), said again by <see cref="PluginCommand"/> acting alone
/// on a root of its own, an install's folder whose <c>data/</c> is the home: the same lines on each stream, the same exit, and every file
/// under the root after it the same, a JSON file compared as JSON. A row the port changes on purpose says why under <c>changed</c>.
/// </summary>
public sealed class PluginsTextTests
{
    private static readonly Lazy<JsonElement> Table = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "plugins-text.json"))).RootElement.Clone());

    public static TheoryData<string> Names()
    {
        var names = new TheoryData<string>();
        foreach (var row in Table.Value.GetProperty("rows").EnumerateArray()) names.Add(row.GetProperty("name").GetString()!);
        return names;
    }

    [Fact]
    public void The_table_holds_every_verb_the_command_answers()
    {
        var rows = Table.Value.GetProperty("rows").EnumerateArray().ToList();
        Assert.True(rows.Count >= 50, $"only {rows.Count} rows, so this proves little");
        var verbs = rows.Select(row => row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()).FirstOrDefault() ?? "list")
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(PluginCommand.Verbs, verb => Assert.Contains(verb, verbs));
        Assert.Equal(rows.Count, rows.Select(row => row.GetProperty("name").GetString()).Distinct().Count());
    }

    /// <summary>A JSON reader's own reason for the table's unreadable files, which each runtime words its own way: spelled <c>&lt;why&gt;</c>.</summary>
    private static readonly string Why = ((Func<string>)(() =>
    {
        try
        {
            using var _ = JsonDocument.Parse("{ not json");
            return "";
        }
        catch (JsonException error)
        {
            return error.Message;
        }
    }))();

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Each_case_says_today_s_text_and_leaves_the_same_files(string name)
    {
        var row = Table.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        var root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "plugins-text", Guid.NewGuid().ToString("N")[..8]);
        var home = Path.Combine(root, "data");
        Directory.CreateDirectory(home);
        try
        {
            foreach (var file in row.GetProperty("before").EnumerateObject())
            {
                var path = Path.Combine([root, .. file.Name.Split('/')]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var text = file.Value.GetString()!;
                File.WriteAllText(path, text.Replace("<root>", file.Name.EndsWith(".json", StringComparison.Ordinal)
                    ? JsonEncodedText.Encode(root).ToString()
                    : root, StringComparison.Ordinal));
            }

            var output = new StringWriter();
            var error = new StringWriter();
            var named = !row.TryGetProperty("home", out var hasHome) || hasHome.GetBoolean();
            var world = new PluginWorld
            {
                Instance = new InstanceWorld
                {
                    Home = named ? home : null,
                    Environment = variable => variable == DaorisHome.Variable && named ? home : null,
                    BaseDirectory = Path.Combine(root, "application"),
                    Out = output,
                    Error = error,
                    Verb = "daoris",
                },
                WorkingDirectory = root,
            };

            string[] argv = [.. row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()!.Replace("<root>", root, StringComparison.Ordinal))];
            var exit = await PluginCommand.RunAsync(argv, world, CancellationToken.None);

            string Spelled(string text) => text.Replace(JsonEncodedText.Encode(root).ToString(), "<root>", StringComparison.Ordinal)
                .Replace(root, "<root>", StringComparison.Ordinal).Replace("\\\\", "/", StringComparison.Ordinal).Replace('\\', '/')
                .Replace(Why, "<why>", StringComparison.Ordinal);
            static IReadOnlyList<string> Lines(string text) =>
                text.Length == 0 ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            string[] said = [.. Lines(output.ToString()).Select(Spelled)];
            string[] refused = [.. Lines(error.ToString()).Select(Spelled)];
            var shown = $"`{name}` said, exit {exit}:\n{string.Join('\n', said)}\non the error stream:\n{string.Join('\n', refused)}";
            Assert.True(Expected(row, "out").SequenceEqual(said), shown);
            Assert.True(Expected(row, "err").SequenceEqual(refused), shown);
            Assert.True(row.GetProperty("exit").GetInt32() == exit, shown);

            var after = row.GetProperty("after").EnumerateObject().ToDictionary(file => file.Name, file => file.Value.GetString()!, StringComparer.Ordinal);
            var left = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), path => Spelled(File.ReadAllText(path)), StringComparer.Ordinal);
            Assert.True(after.Keys.Order(StringComparer.Ordinal).SequenceEqual(left.Keys.Order(StringComparer.Ordinal)),
                $"`{name}` left {string.Join(", ", left.Keys.Order(StringComparer.Ordinal))}; expected {string.Join(", ", after.Keys.Order(StringComparer.Ordinal))}");
            foreach (var (file, expected) in after)
            {
                var actual = left[file];
                if (file.EndsWith(".json", StringComparison.Ordinal) && Parsed(expected) is { } wanted && Parsed(actual) is { } got)
                {
                    Assert.True(JsonNode.DeepEquals(wanted, got), $"{file} after `{name}`:\n{actual}\nexpected:\n{expected}");
                }
                else
                {
                    Assert.True(expected.ReplaceLineEndings("\n") == actual.ReplaceLineEndings("\n"), $"{file} after `{name}`:\n{actual}\nexpected:\n{expected}");
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

    private static JsonNode? Parsed(string text)
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
}
