using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (PUSH1f2d, D194 point 6, the alone-path design §3): each case of <c>daoris agent [list]|rules|trust|settings|…</c>,
/// as the CLI's TypeScript verb said it before it retired (<c>fixtures/agents-text.json</c>), said again by <see cref="AgentsCommand"/>
/// acting alone on a root of its own: the same lines on each stream, the same exit, and every file under the root after it the same. A
/// row the port changes on purpose says why under <c>changed</c>.
/// </summary>
public sealed partial class AgentsTextTests
{
    private static readonly Lazy<JsonElement> Read = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "agents-text.json"))).RootElement.Clone());

    public static TheoryData<string> Names() => Rows(standIn: false);

    /// <summary>The rows whose agent is a stand-in program asked as a start runs it (<c>windows</c>, PUSH1f2e4's <c>list</c>), or the rest.</summary>
    private static TheoryData<string> Rows(bool standIn)
    {
        var names = new TheoryData<string>();
        foreach (var row in Read.Value.GetProperty("rows").EnumerateArray())
        {
            if ((row.TryGetProperty("windows", out var windows) && windows.GetBoolean()) == standIn) names.Add(row.GetProperty("name").GetString()!);
        }

        return names;
    }

    /// <summary>
    /// The rows that start a stand-in agent, a <c>claude.cmd</c> in the managed layout (PUSH1f2e4): real processes, so the suite's
    /// Process half runs them, and on Windows alone, which runs a <c>.cmd</c>.
    /// </summary>
    [Trait(Category.Name, Category.Process)]
    public sealed class StandIns
    {
        public static TheoryData<string> Names() => Rows(standIn: true);

        [Theory]
        [MemberData(nameof(Names))]
        public async Task Each_case_says_today_s_text_and_writes_the_same_files(string name)
        {
            if (!OperatingSystem.IsWindows()) return;
            await Said(name);
        }
    }

    [Fact]
    public void The_table_holds_every_verb_the_command_answers()
    {
        var rows = Read.Value.GetProperty("rows").EnumerateArray().ToList();
        Assert.True(rows.Count >= 60, $"only {rows.Count} rows, so this proves little");
        // No words are `list` (PUSH1f2e4).
        var verbs = rows.Select(row => row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()).FirstOrDefault() ?? "list")
            .ToHashSet(StringComparer.Ordinal);
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
    public Task Each_case_says_today_s_text_and_writes_the_same_files(string name) => Said(name);

    private static async Task Said(string name)
    {
        var row = Read.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        // A row that lays a Windows binary (PUSH1f2e's pin of a version already installed) is said on Windows alone.
        if (row.TryGetProperty("onWindows", out var onWindows) && onWindows.GetBoolean() && !OperatingSystem.IsWindows()) return;
        var root =Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agents-text", Guid.NewGuid().ToString("N")[..8]);
        foreach (var sub in new[] { "daoris", "user", "work" }) Directory.CreateDirectory(Path.Combine(root, sub));
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

            var home = !row.TryGetProperty("home", out var hasHome) || hasHome.GetBoolean() ? Path.Combine(root, "daoris") : null;
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
                    // `key`'s key (PUSH1f2e5), the row's standard input; none is an empty one.
                    In = new StringReader(row.TryGetProperty("stdin", out var stdin) ? stdin.GetString()! : ""),
                    Verb = "daoris",
                },
                Folder = root,
                UserHome = Path.Combine(root, "user"),
                // The build's agents and those the home's plugins declare, as the window's roster reads them (`list`'s plugin rows).
                Zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"),
            };
            // A row run at a moment of its own (PUSH1f2e3: a cool-off's end and a reading's age) is said at that moment.
            if (row.TryGetProperty("now", out var moment)) world = world with { Now = () => DateTimeOffset.Parse(moment.GetString()!, System.Globalization.CultureInfo.InvariantCulture) };

            string[] argv = [.. row.GetProperty("argv").EnumerateArray().Select(each => each.GetString()!)
                .Select(word => word.StartsWith("<root>/", StringComparison.Ordinal)
                    ? Path.Combine([root, .. word["<root>/".Length..].TrimEnd('/').Split('/')]) + (word.EndsWith('/') ? "\\" : "")
                    : word)];
            var exit = await AgentsCommand.RunAsync(argv, world, CancellationToken.None);

            // An account's id drawn by the run (PUSH1f2e5: a key's new account), one the row did not lay or name, is spelled `<id>`.
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var text in row.GetProperty("before").EnumerateObject().SelectMany(file => new[] { file.Name, file.Value.GetString()! })
                .Concat(row.GetProperty("folders").EnumerateArray().Select(each => each.GetString()!)).Concat(argv))
            {
                foreach (Match id in Drawn().Matches(text)) known.Add(id.Value);
            }

            // So is a fold's receipt's (POOL1a1, D197 §6.3: drawn at random, as an account's is), read inside `pooled` alone; its moment
            // is a settling moment like a proposal's, `<at>`.
            foreach (var file in row.GetProperty("before").EnumerateObject()) known.UnionWith(Receipts(file.Value.GetString()!));

            string Spelled(string text)
            {
                var spelled = Drawn().Replace(Settled().Replace(text
                    .Replace(JsonEncodedText.Encode(root).ToString(), "<root>", StringComparison.Ordinal)
                    .Replace(root, "<root>", StringComparison.Ordinal)
                    .Replace(slashed, "<root>", StringComparison.Ordinal)
                    .Replace("\\\\", "/", StringComparison.Ordinal)
                    .Replace('\\', '/')
                    .Replace($"({Why})", "(<why>)", StringComparison.Ordinal), "$1\"<at>\""), id => known.Contains(id.Value) ? id.Value : "<id>");
                foreach (var id in Receipts(spelled).Where(id => !known.Contains(id)))
                {
                    spelled = Regex.Replace(spelled, $"(\"id\": ?)\"{Regex.Escape(id)}\"", "$1\"<id>\"");
                }

                return spelled;
            }

            static IReadOnlyList<string> Lines(string text) =>
                text.Length == 0 ? [] : text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            string[] said = [.. Lines(output.ToString()).Select(Spelled)];
            string[] refused = [.. Lines(error.ToString()).Select(Spelled)];
            var shown = $"`{name}` said, exit {exit}:\n{string.Join('\n', said)}\non the error stream:\n{string.Join('\n', refused)}";
            Assert.True(Expected(row, "out").SequenceEqual(said), shown);
            Assert.True(Expected(row, "err").SequenceEqual(refused), shown);
            Assert.True(row.GetProperty("exit").GetInt32() == exit, shown);

            // The home's identity, which taking its lease mints (HOMEID1; `key` publishes under it, PUSH1f2e5), is no file the verb writes.
            var identity = home is null || row.GetProperty("before").TryGetProperty(Spelled(Path.GetRelativePath(root, HomeIdentity.PathIn(home))), out _)
                ? null
                : Path.GetFullPath(HomeIdentity.PathIn(home));
            // Nor is the machine log (POOL1a1's fold says what it changed there, D197 §6.2): a dated file the home appends to, never the verb's;
            // a named wiring file's fold writes it under that file's own home (`HarnessSettings.Change`), so that home's is set aside too.
            string[] logs = [.. new[] { home, wiring is null ? null : HarnessSettings.HomeOf(wiring) }.OfType<string>()
                .Select(each => Path.GetFullPath(DaorisHome.Entry(each, HomeEntry.Logs)) + Path.DirectorySeparatorChar)];
            var written = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(Path.GetFullPath(path), identity, StringComparison.OrdinalIgnoreCase))
                .Where(path => !logs.Any(log => Path.GetFullPath(path).StartsWith(log, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(path => Spelled(Path.GetRelativePath(root, path).Replace('\\', '/')), path => Spelled(File.ReadAllText(path)), StringComparer.Ordinal);
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

    /// <summary>
    /// The ids of the fold's receipts in a <c>harnesses.json</c> (POOL1a1, D197 §6.3: <c>pooled</c>, agent → receipts), none where the
    /// text is no such file: the run's own where the row did not lay them, the table spells them <c>&lt;id&gt;</c>.
    /// </summary>
    private static IEnumerable<string> Receipts(string text) =>
        text.Contains("\"pooled\"", StringComparison.Ordinal) && JsonNodeOf(text) is JsonObject file && file["pooled"] is JsonObject pooled
            ? pooled.Select(agent => agent.Value).OfType<JsonArray>().SelectMany(receipts => receipts.OfType<JsonObject>())
                .Select(receipt => receipt["id"]).OfType<JsonValue>()
                .Where(id => id.GetValueKind() == JsonValueKind.String).Select(id => id.GetValue<string>())
            : [];

    /// <summary>A proposal's settling moment, or a fold's receipt's, the run's own: the table spells it <c>&lt;at&gt;</c>.</summary>
    [GeneratedRegex("(\"at\": ?)\"\\d{4}-\\d\\d-\\d\\dT[^\"]*\"")]
    private static partial Regex Settled();

    /// <summary>An account's id as <see cref="AccountNames.NewId"/> draws it: a run's own, the table spells it <c>&lt;id&gt;</c>.</summary>
    [GeneratedRegex("acct-[0-9a-f]{8}")]
    private static partial Regex Drawn();
}
