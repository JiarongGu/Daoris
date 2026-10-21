using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (PUSH1f2c, D194 point 6, the alone-path design §3): each case of <c>daoris tool</c>, as the CLI's
/// TypeScript verb said it before it retired (<c>fixtures/tools-text.json</c>), said again by <see cref="ToolsCommand"/> acting alone on
/// a home of its own with no <c>PATH</c>, as <c>win-x64</c>: the same lines on each stream, the same exit, <c>tools.json</c> after it the
/// same, and the same versions downloaded. A row the port changes on purpose says why under <c>changed</c>.
/// </summary>
public sealed partial class ToolsTextTests
{
    private static readonly Lazy<JsonElement> Read = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "tools-text.json"))).RootElement.Clone());

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
        Assert.True(rows.Count >= 40, $"only {rows.Count} rows, so this proves little");
        var verbs = rows.Select(row => row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()).FirstOrDefault() ?? "list")
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(ToolsCommand.Verbs, verb => Assert.Contains(verb, verbs));
    }

    /// <summary>A host, as a stand-in: each address the row names answers its bytes, and any other has nothing there.</summary>
    private sealed class Host(JsonElement? served) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(served is { } answers && answers.TryGetProperty(url, out var answer) && answer.TryGetProperty("base64", out var bytes)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(Convert.FromBase64String(bytes.GetString()!)) }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Each_case_says_today_s_text_and_writes_the_same_file(string name)
    {
        var row = Read.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        var root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "tools-text", Guid.NewGuid().ToString("N")[..8]);
        var home = Path.Combine(root, "home");
        var application = Path.Combine(root, "application");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(application);
        try
        {
            var tools = DaorisHome.Entry(home, HomeEntry.Tools);
            if (row.GetProperty("before").GetString() is { } before)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(tools)!);
                File.WriteAllText(tools, before.Replace("<home>", JsonEncodedText.Encode(home).ToString(), StringComparison.Ordinal));
            }

            if (row.TryGetProperty("files", out var files))
            {
                foreach (var file in files.EnumerateObject())
                {
                    var path = Path.Combine([home, .. file.Name.Split('/')]);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, file.Value.GetString());
                }
            }

            var output = new StringWriter();
            var error = new StringWriter();
            var named = !row.TryGetProperty("home", out var hasHome) || hasHome.GetBoolean();
            var world = new ToolsWorld
            {
                Instance = new InstanceWorld
                {
                    Home = named ? home : null,
                    Environment = variable => variable == DaorisHome.Variable && named ? home : null,
                    BaseDirectory = application,
                    Out = output,
                    Error = error,
                    Verb = "daoris",
                },
                WorkingDirectory = home,
                SearchPath = "",
                Platform = "win-x64",
                Transport = new Host(row.TryGetProperty("served", out var served) ? served : null),
                AskVersion = (_, _, _) => Task.FromResult<(string?, string?)>(("2.51.0", null)),
            };

            string[] argv = [.. row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()!)
                .Select(word => word.StartsWith("<home>/", StringComparison.Ordinal) ? Path.Combine([home, .. word["<home>/".Length..].Split('/')]) : word)];
            var exit = await ToolsCommand.RunAsync(argv, world, CancellationToken.None);

            string Spelled(string text) => Unreadable().Replace(
                Age().Replace(text.Replace(home, "<home>", StringComparison.Ordinal).Replace('\\', '/'), "<age> ago"), "is not readable JSON (<why>)");
            static IReadOnlyList<string> Lines(string text) =>
                text.Length == 0 ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            string[] said = [.. Lines(output.ToString()).Select(Spelled)];
            string[] refused = [.. Lines(error.ToString()).Select(Spelled)];
            var shown = $"`{name}` said, exit {exit}:\n{string.Join('\n', said)}\non the error stream:\n{string.Join('\n', refused)}";
            Assert.True(Expected(row, "out").SequenceEqual(said), shown);
            Assert.True(Expected(row, "err").SequenceEqual(refused), shown);
            Assert.True(row.GetProperty("exit").GetInt32() == exit, shown);

            var after = row.GetProperty("after").GetString();
            var written = File.Exists(tools) ? File.ReadAllText(tools) : null;
            if (after is null || written is null)
            {
                Assert.Equal(after is null, written is null);
            }
            else if (JsonNodeOf(after) is { } expected)
            {
                var actual = Strings(JsonNode.Parse(written), Spelled);
                Assert.True(JsonNode.DeepEquals(expected, actual), $"tools.json after `{name}`:\n{actual}\nexpected:\n{after}");
            }
            else
            {
                Assert.Equal(after, written);
            }

            foreach (var declared in Tools.Declared)
            {
                IReadOnlyList<string> kept = row.GetProperty("versions").TryGetProperty(declared.Id, out var versions)
                    ? [.. versions.EnumerateArray().Select(each => each.GetString()!)]
                    : [];
                Assert.Equal(kept, ToolInstall.Downloaded(home, declared.Id));
                var folder = DaorisHome.Entry(home, HomeEntry.ToolVersions, declared.Id);
                Assert.True(!Directory.Exists(folder) || !Directory.EnumerateDirectories(folder, "*.part*").Any(), $"`{name}` left a staging");
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

    /// <summary>A JSON value with every string spelled as a line is, so an escaped path reads as the table writes it.</summary>
    private static JsonNode? Strings(JsonNode? node, Func<string, string> spelled) => node switch
    {
        JsonObject map => new JsonObject(map.Select(pair => KeyValuePair.Create(pair.Key, Strings(pair.Value, spelled)))),
        JsonArray list => new JsonArray([.. list.Select(each => Strings(each, spelled))]),
        JsonValue value when value.TryGetValue<string>(out var text) => JsonValue.Create(spelled(text)),
        _ => node?.DeepClone(),
    };

    [GeneratedRegex(@"\b\d+ (second|minute|hour|day)s? ago\b")]
    private static partial Regex Age();

    /// <summary>A JSON reader's own reason, which each runtime words its own way: the table spells it <c>&lt;why&gt;</c>.</summary>
    [GeneratedRegex(@"is not readable JSON \([^)]*\)")]
    private static partial Regex Unreadable();
}
