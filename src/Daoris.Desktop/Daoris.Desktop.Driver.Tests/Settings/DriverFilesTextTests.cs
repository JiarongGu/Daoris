using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The port keeps today's text (ONEWAY1u, D194 point 6): each case of <c>daoris remote</c> and <c>daoris browser</c>, as the CLI's
/// TypeScript verbs said it before they retire (<c>fixtures/driver-files-text.json</c>), said again by the library's operations and
/// words on a home of their own: the same lines on each stream, the same exit, and the files after the same. A row of the
/// <c>command</c> door is the verb's own grammar, which the command tree's table reads when the verb retires.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class DriverFilesTextTests
{
    private static readonly Lazy<JsonElement> Read = new(() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "driver-files-text.json"))).RootElement.Clone());

    public static TheoryData<string> Names()
    {
        var names = new TheoryData<string>();
        foreach (var row in Read.Value.GetProperty("rows").EnumerateArray())
        {
            if (row.GetProperty("door").GetString() == "operation") names.Add(row.GetProperty("name").GetString()!);
        }

        return names;
    }

    [Fact]
    public void The_table_holds_every_operation_the_verbs_make()
    {
        var rows = Read.Value.GetProperty("rows").EnumerateArray().ToList();
        Assert.True(rows.Count >= 50, $"only {rows.Count} rows, so this proves little");
        var ops = rows.Where(row => row.TryGetProperty("op", out _)).Select(row => row.GetProperty("op").GetString()).ToHashSet();
        string[] made =
        [
            "remote-list", "remote-add", "remote-remove", "favorite-list", "favorite-add", "favorite-remove",
            "browser-extensions", "browser-use", "browser-links",
        ];
        Assert.All(made, op => Assert.Contains(op, ops));
        Assert.Contains(rows, row => row.GetProperty("door").GetString() == "command");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Each_case_says_today_s_text_and_writes_the_same_files(string name)
    {
        var row = Read.Value.GetProperty("rows").EnumerateArray().First(each => each.GetProperty("name").GetString() == name);
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-files-text", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var remotes = DaorisHome.Entry(home, HomeEntry.Remotes);
            string PathOf(string file) => file switch
            {
                "remotes" => remotes,
                "favorites" => BrowserFavorites.FilePath(home),
                "settings" => BrowserSettings.FilePath(home),
                _ => throw new InvalidOperationException($"the table names a file this test does not know: {file}"),
            };

            foreach (var file in row.GetProperty("before").EnumerateObject())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathOf(file.Name))!);
                File.WriteAllText(PathOf(file.Name), file.Value.GetString());
            }

            var environment = row.TryGetProperty("env", out var set)
                ? set.EnumerateObject().ToDictionary(each => each.Name, each => each.Value.GetString())
                : [];
            var args = row.TryGetProperty("args", out var given) ? given : default;
            string? Arg(string field) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(field, out var value) ? value.GetString() : null;
            var noHome = row.TryGetProperty("noHome", out var none) && none.GetBoolean();

            var said = Say(row.GetProperty("op").GetString()!, home, remotes, noHome ? null : remotes, environment, Arg);

            string Spelled(string text) => Parse().Replace(text.Replace(home, "<home>", StringComparison.Ordinal).Replace('\\', '/'), "is not readable JSON (<parse error>)");
            Assert.Equal(Expected(row, "out"), said.Out.Select(Spelled).ToList());
            Assert.Equal(Expected(row, "err"), said.Err.Select(Spelled).ToList());
            Assert.Equal(row.GetProperty("exit").GetInt32(), said.Exit);

            foreach (var file in row.GetProperty("after").EnumerateObject())
            {
                var path = PathOf(file.Name);
                if (file.Value.ValueKind == JsonValueKind.Null)
                {
                    Assert.False(File.Exists(path), $"{file.Name} after `{name}` should not exist");
                    continue;
                }

                Assert.True(File.Exists(path), $"{file.Name} after `{name}` should exist");
                var written = File.ReadAllText(path);
                var expected = file.Value.GetString()!;
                if (Json(expected) is { } want && Json(written) is { } got) Assert.True(JsonNode.DeepEquals(want, got), $"{file.Name} after `{name}`:\n{written}\nexpected:\n{expected}");
                else Assert.Equal(expected, written);
            }
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }

    /// <summary>What the command makes of one case: the operation over the home, and the words over its result.</summary>
    private static FileSaid Say(string op, string home, string remotes, string? mapPath, Dictionary<string, string?> environment, Func<string, string?> arg)
    {
        try
        {
            switch (op)
            {
                case "remote-list":
                    return RemoteWords.List(RemoteWiring.Read(variable => environment.GetValueOrDefault(variable), mapPath));
                case "remote-add":
                {
                    var set = RemoteOperations.Set(remotes, arg("workspace")!, arg("url")!, arg("key")!);
                    List<string> lines = [RemoteWords.Wiring(set.Workspace)];
                    if (arg("keyFrom") == "environment") lines.Add(RemoteWords.KeyFromEnvironment());
                    return FileSaid.Said([.. lines, .. RemoteWords.Added(set, remotes)]);
                }

                case "remote-remove":
                    return FileSaid.Said(RemoteWords.Removed(RemoteTarget.Workspace(arg("workspace")), RemoteOperations.Remove(remotes, arg("workspace")!)));
                case "favorite-list":
                    return BrowserWords.Favorites(home, BrowserFavorites.Read(home));
                case "favorite-add":
                    return FileSaid.Said([BrowserWords.Kept(BrowserFavorites.Add(home, arg("address")!, arg("title")))]);
                case "favorite-remove":
                    return FileSaid.Said([BrowserWords.Dropped(arg("address")!, BrowserFavorites.Remove(home, arg("address")!))]);
                case "browser-extensions" or "browser-use" or "browser-links":
                {
                    var area = op["browser-".Length..];
                    if (arg("value") is not { } value) return BrowserWords.Setting(area, BrowserSettings.Read(home));
                    switch (area)
                    {
                        case "extensions": BrowserSettings.SetExtensions(home, value); break;
                        case "use": BrowserSettings.SetBrowser(home, value); break;
                        default: BrowserSettings.SetLinks(home, value); break;
                    }

                    return FileSaid.Said([BrowserWords.Set(area, value)]);
                }

                default:
                    throw new InvalidOperationException($"the table names an operation this test does not make: {op}");
            }
        }
        catch (HomeFileRefusal refused)
        {
            return FileSaid.Refused(refused.Message);
        }
    }

    private static JsonNode? Json(string text)
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

    private static IReadOnlyList<string> Expected(JsonElement row, string stream) =>
        [.. row.GetProperty(stream).EnumerateArray().Select(each => each.GetString()!)];

    [GeneratedRegex(@"is not readable JSON \(.*?\)(?=[.;]|$)")]
    private static partial Regex Parse();
}
