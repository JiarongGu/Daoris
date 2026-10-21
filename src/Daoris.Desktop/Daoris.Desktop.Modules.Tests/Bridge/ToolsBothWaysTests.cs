using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Each of the tools' presses both ways (PUSH1f2c, D194 points 4 and 6, the alone-path design §3): <c>daoris-driver tool</c> acting
/// alone, through the library's operation, and the same words' press sent to the Tools place's route (<c>TOOLS_USE</c>,
/// <c>TOOLS_DELETE</c>, <c>TOOLS_LOCATION</c>, <c>TOOLS_LOOK</c>), its answer said by the same words function, over one home from the
/// same files. The bytes written, the versions left, the text, the refusal's line and the exit are equal: red where a route and its
/// operation disagree, or where the payload the command writes does not read back through the route as the command meant it.
/// </summary>
public sealed class ToolsBothWaysTests : DriverModuleBridge
{
    private const string Before = """{"tools":{"gh":{"use":"managed","version":"2.62.0"}},"locations":["https://lists.example/r.json"]}""";

    private string ToolsFile => DaorisHome.Entry(Home, HomeEntry.Tools);

    private ToolsWorld World(StringWriter output, StringWriter error) => new()
    {
        // No address file is read here (a workspace build), so the command acts alone; the route is asked of the module itself.
        Instance = new InstanceWorld { Home = Home, Environment = _ => null, Out = output, Error = error, Verb = "daoris" },
        WorkingDirectory = Home,
        SearchPath = "",
        Platform = ToolResources.Current,
    };

    private sealed record Said(int Exit, string Out, string Error, string? File, string Versions);

    /// <summary>The home as each case starts it: <c>tools.json</c>, and GitHub CLI 2.62.0 and 2.63.0 downloaded.</summary>
    private void Laid()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ToolsFile)!);
        File.WriteAllText(ToolsFile, Before);
        foreach (var version in new[] { "2.62.0", "2.63.0" })
        {
            var folder = ToolInstall.VersionFolder(Home, "gh", version);
            Directory.CreateDirectory(Path.Combine(folder, Tools.Package, "bin"));
            File.WriteAllText(Path.Combine(folder, Tools.Package, "bin", "gh.exe"), "gh");
            File.WriteAllText(Path.Combine(folder, Tools.Record), """{"exe":"bin/gh.exe"}""");
        }

        // Bytes that are no program: the system will not start them, so they answer no version either way.
        File.WriteAllText(Path.Combine(Home, "junk.exe"), "not a program");
    }

    private Said Now(StringWriter output, StringWriter error, int exit) => new(
        exit, output.ToString(), error.ToString(), File.Exists(ToolsFile) ? File.ReadAllText(ToolsFile) : null,
        string.Join(",", ToolInstall.Downloaded(Home, "gh")));

    private async Task<Said> AloneAsync(string[] words)
    {
        Laid();
        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = await ToolsCommand.RunAsync(words, World(output, error), CancellationToken.None);
        return Now(output, error, exit);
    }

    /// <summary>The same words' press sent to the module's route, its answer said by the command's words, as the pipe's client says it.</summary>
    private async Task<Said> RouteAsync(string[] words)
    {
        Laid();
        var (output, error) = (new StringWriter(), new StringWriter());
        var press = ToolsCommand.Plan(words, World(output, error));
        Assert.NotNull(press);
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
    [InlineData("use gh system")]
    [InlineData("use git system")]
    [InlineData("use git file <home>/nowhere/git.exe")]
    [InlineData("use git file <home>/junk.exe")]
    [InlineData("delete gh 2.63.0")]
    [InlineData("delete gh 2.62.0")]
    [InlineData("delete gh 2.61.0")]
    [InlineData("delete gh ../2.62.0")]
    [InlineData("locations add https://mirror.example/r.json")]
    [InlineData("locations add https://lists.example/r.json")]
    [InlineData("locations remove https://lists.example/r.json")]
    [InlineData("locations remove https://mirror.example/r.json")]
    public async Task A_tool_s_press_says_and_writes_the_same_alone_and_through_its_route(string text)
    {
        string[] words = [.. text.Split(' ').Select(word => word.StartsWith("<home>/", StringComparison.Ordinal)
            ? Path.Combine([Home, .. word["<home>/".Length..].Split('/')])
            : word)];

        var alone = await AloneAsync(words);
        var route = await RouteAsync(words);

        Assert.NotEqual("", alone.Out + alone.Error);
        Assert.Equal(alone, route);
    }

    /// <summary>
    /// A look both ways with no location listed: the route's answer carries no looks, and the command says the tools from the lists as
    /// the alone path does. A listed location would be fetched over the network, which no test here reaches.
    /// </summary>
    [Fact]
    public async Task A_look_with_no_location_says_the_same_both_ways()
    {
        Laid();
        File.WriteAllText(ToolsFile, """{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}""");
        var (output, error) = (new StringWriter(), new StringWriter());
        var alone = await ToolsCommand.RunAsync(["look"], World(output, error), CancellationToken.None);

        var (routeOut, routeError) = (new StringWriter(), new StringWriter());
        var press = ToolsCommand.Plan(["look"], World(routeOut, routeError))!;
        var answer = await AnswerAsync(Module(), press.Route.Route, press.Payload);
        var routed = press.Words(press.Fields(answer));

        Assert.Equal((alone, output.ToString(), error.ToString()), (routed, routeOut.ToString(), routeError.ToString()));
        Assert.StartsWith("daoris: no resource location is listed", output.ToString(), StringComparison.Ordinal);
    }
}
