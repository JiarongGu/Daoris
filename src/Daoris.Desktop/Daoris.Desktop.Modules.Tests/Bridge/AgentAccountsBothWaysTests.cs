using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A pin taken back and an account's change both ways (PUSH1f2e2, D194 points 4 and 6, the alone-path design §3): <c>daoris-driver agent
/// unpin|profile …</c> acting alone, and the same words' press sent to the window's route (<c>HARNESS_ACTION</c>'s <c>unpin</c> and
/// <c>profile-*</c>, <c>ACCOUNT_USE</c>'s <c>order</c> and <c>ready</c>), its answer said by the same words function, over one home from the
/// same files. The wiring, the accounts' names, the cool-offs, the accounts' folders, the text and the exit are equal: red where a route
/// and its operation disagree, or where the payload the command writes does not read back through the route as the command meant it.
/// Both fold the laid home's scopes into one set before their edit (POOL1a1, D197 §6), so a receipt's id and moment, each run's own,
/// are spelled.
/// </summary>
public sealed class AgentAccountsBothWaysTests : DriverModuleBridge
{
    private string Wiring => DaorisHome.Entry(Home, HomeEntry.Harnesses);

    private string Names => AccountNames.PathOf(Home);

    private string Cooling => AccountCooling.PathOf(Home);

    private string Agents => Path.GetDirectoryName(HarnessSettings.ProfileHome(Home, "claude-code", "account-1"))!;

    private AgentsWorld World(StringWriter output, StringWriter error) => new()
    {
        // No address file is read here (a workspace build), so the command acts alone; the route is asked of the module itself.
        Instance = new InstanceWorld
        {
            Home = Home,
            Environment = variable => variable == DaorisHome.Variable ? Home : null,
            Out = output,
            Error = error,
            Verb = "daoris",
        },
        Folder = Home,
        UserHome = Path.Combine(Home, "user"),
        Now = () => new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero),
    };

    private sealed record Said(int Exit, string Out, string Error, string? Wiring, string? Names, string? Cooling, string Accounts);

    /// <summary>
    /// The home as each case starts it: three accounts, one with a file in it and one named; the machine's default, list and pin, a
    /// workspace's default, list and pin; and an account's cool-off, with the tool's own sign-in's where the words ask for it: the window's
    /// every change refreshes its roster, which ends that one (D125 §3.7), so it is laid only where both paths end it.
    /// </summary>
    private void Laid(bool own)
    {
        // Each path starts from the same home: what the other path made or removed is gone first.
        if (Directory.Exists(Agents)) Directory.Delete(Agents, recursive: true);
        foreach (var file in new[] { Wiring, Names, Cooling }.Where(File.Exists)) File.Delete(file);
        foreach (var account in new[] { "account-1", "account-2", "account-3" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "claude-code", account));
        }

        File.WriteAllText(Path.Combine(HarnessSettings.ProfileHome(Home, "claude-code", "account-1"), "settings.json"), "{}");
        Directory.CreateDirectory(Path.GetDirectoryName(Wiring)!);
        File.WriteAllText(Wiring, """
            {
              "defaults": { "claude-code": "account-1" },
              "workspaces": { "work": { "claude-code": "account-2" } },
              "versions": { "claude-code": "2.1.0" },
              "workspaceVersions": { "work": { "claude-code": "2.0.0" } },
              "rotation": { "claude-code": ["account-1", "account-2"] },
              "workspaceRotation": { "work": { "claude-code": ["account-2"] } }
            }
            """);
        File.WriteAllText(Names, """{ "claude-code": { "account-3": { "name": "seat" } } }""");
        File.WriteAllText(Cooling, own
            ? """{ "claude-code": { "account-2": { "until": "2999-01-01T00:00:00Z", "stated": true }, "": { "until": "2999-01-01T00:00:00Z", "stated": false } } }"""
            : """{ "claude-code": { "account-2": { "until": "2999-01-01T00:00:00Z", "stated": true } } }""");
    }

    private Said Now(StringWriter output, StringWriter error, int exit)
    {
        static string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        var accounts = Directory.Exists(Agents)
            ? string.Join(", ", Directory.EnumerateFileSystemEntries(Agents, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Agents, path).Replace('\\', '/')).Order(StringComparer.Ordinal))
            : "";
        return new(exit, output.ToString(), error.ToString(), Spelled(Read(Wiring)), Read(Names), Read(Cooling), accounts);
    }

    /// <summary>
    /// The wiring with each fold receipt's id spelled <c>&lt;id&gt;</c> and its moment <c>&lt;at&gt;</c> (POOL1a1, D197 §6.3): both
    /// paths fold the home as laid, and a receipt's id is drawn at random and its moment read from the clock, so they differ there alone.
    /// </summary>
    private static string? Spelled(string? wiring)
    {
        if (wiring is null || !wiring.Contains("\"pooled\"", StringComparison.Ordinal)) return wiring;
        JsonNode? read;
        try
        {
            read = JsonNode.Parse(wiring);
        }
        catch (JsonException)
        {
            return wiring;
        }

        if (read?["pooled"] is not JsonObject pooled) return wiring;
        foreach (var receipt in pooled.Select(agent => agent.Value).OfType<JsonArray>().SelectMany(receipts => receipts.OfType<JsonObject>()))
        {
            foreach (var (field, spelling) in new[] { ("id", "<id>"), ("at", "<at>") })
            {
                if (receipt[field] is JsonValue value && value.GetValueKind() == JsonValueKind.String)
                {
                    wiring = Regex.Replace(wiring, $"(\"{field}\": ?)\"{Regex.Escape(value.GetValue<string>())}\"", $"$1\"{spelling}\"");
                }
            }
        }

        return wiring;
    }

    private async Task<Said> AloneAsync(string[] words)
    {
        Laid(words.Contains("--own"));
        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = await AgentsCommand.RunAsync(words, World(output, error), CancellationToken.None);
        return Now(output, error, exit);
    }

    /// <summary>The same words' press sent to the module's route, its answer said by the command's words, as the pipe's client says it.</summary>
    private async Task<Said> RouteAsync(string[] words)
    {
        Laid(words.Contains("--own"));
        var (output, error) = (new StringWriter(), new StringWriter());
        var press = AgentsCommand.Plan(words, World(output, error), out _);
        Assert.NotNull(press);
        Assert.NotNull(press.Route);
        var response = await AskAsync(Module(), press.Route.Route, press.Payload);
        int exit;
        if (response.Success)
        {
            exit = press.Words(press.Fields(JsonSerializer.SerializeToElement(
                response.Data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
        }
        else
        {
            // As InstanceDoor.PressAsync says a route's refusal: `{verb}: {message} ({code})`, exit 2.
            error.WriteLine($"daoris: {response.Error!.Message} ({response.Error.Code})");
            exit = 2;
        }

        return Now(output, error, exit);
    }

    [Theory]
    [InlineData("unpin claude-code")]
    [InlineData("profile add claude-code work")]
    [InlineData("profile add claude-code account-2")]
    [InlineData("profile add claude-code-acp work")]
    [InlineData("profile remove claude-code account-1")]
    [InlineData("profile remove claude-code seat")]
    [InlineData("profile remove claude-code nobody")]
    [InlineData("profile order claude-code account-1 seat account-2")]
    [InlineData("profile order claude-code --clear")]
    [InlineData("profile ready claude-code account-2")]
    [InlineData("profile ready claude-code account-1")]
    [InlineData("profile ready claude-code --own")]
    [InlineData("profile ready claude-code-acp account-2")]
    [InlineData("profile rename claude-code account-1 you@work.example")]
    [InlineData("profile rename claude-code seat account-3")]
    [InlineData("profile use claude-code order --keep account-1")]
    [InlineData("profile use claude-code --keep account-2 --switch curve --curve weekly=0:30,100:100 --curve session=0:80")]
    [InlineData("profile use claude-code-acp --near 85 --early off")]
    [InlineData("profile use claude-code --clear")]
    /// <remarks>
    /// Not here: <c>unpin codex</c>. The window's <c>HARNESS_ACTION</c> refuses an agent no door of its roster runs (<c>codex</c> is
    /// one a door runs as), which the TypeScript verb met the same way through the route; alone, the pin is taken back.
    /// </remarks>
    public async Task An_account_press_says_and_writes_the_same_alone_and_through_its_route(string text)
    {
        var words = text.Split(' ');

        var alone = await AloneAsync(words);
        var route = await RouteAsync(words);

        Assert.NotEqual("", alone.Out + alone.Error);
        Assert.Equal(alone, route);
    }

    /// <summary>
    /// What one set leaves nothing to name (POOL1a1, D197 point 6; the account-pool design §5.2): the route refuses a default, a list
    /// to join and a workspace as <c>ACCOUNTS_ONE_SET</c>, writing nothing, while the terminal's verb alone still acts as PUSH1f2e
    /// built it. POOL1b turns each into §5.1's refusal, and its row returns to the case above, the same both ways.
    /// </summary>
    [Theory]
    [InlineData("profile default claude-code account-2")]
    [InlineData("profile default claude-code account-2 --workspace aurora")]
    [InlineData("profile default claude-code --clear")]
    [InlineData("profile default claude-code --clear --workspace work")]
    [InlineData("profile order claude-code account-2 seat --workspace work")]
    [InlineData("profile order claude-code --clear --workspace WORK")]
    [InlineData("profile join claude-code seat work --machine")]
    [InlineData("profile join claude-code account-3 --machine")]
    [InlineData("profile use claude-code --no-keep --no-curve --workspace work")]
    [InlineData("profile use claude-code --clear --workspace work")]
    public async Task What_one_set_names_nothing_for_is_refused_on_the_route_and_written_nothing(string text)
    {
        var words = text.Split(' ');

        var alone = await AloneAsync(words);
        var route = await RouteAsync(words);
        Laid(own: false);
        var laid = Now(new StringWriter(), new StringWriter(), 0);

        Assert.Equal(0, alone.Exit);
        Assert.Equal(2, route.Exit);
        Assert.Equal("", route.Out);
        Assert.StartsWith("daoris: every account signed in for Daoris serves every workspace (D197)", route.Error);
        Assert.EndsWith($"({Refusals.AccountsOneSet}){Environment.NewLine}", route.Error);
        Assert.Equal(laid with { Exit = 2, Error = route.Error }, route);
    }
}
