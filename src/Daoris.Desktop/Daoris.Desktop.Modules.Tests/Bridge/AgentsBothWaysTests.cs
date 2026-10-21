using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Each of the agents' presses both ways (PUSH1f2d, D194 points 4 and 6, the alone-path design §3): <c>daoris-driver agent</c> acting
/// alone, through the library's operation, and the same words' press sent to the window's route (<c>RULE_ACTION</c>,
/// <c>RULE_PROPOSAL</c>, <c>SET_AGENT_SETTINGS</c>), its answer said by the same words function, over one home from the same files. The
/// bytes written, the text, the refusal's line and the exit are equal: red where a route and its operation disagree, or where the payload
/// the command writes does not read back through the route as the command meant it.
/// </summary>
public sealed partial class AgentsBothWaysTests : DriverModuleBridge
{
    private string Rules => PermissionRules.PathOf(Home);

    private string Proposal => Path.Combine(DaorisHome.Entry(Home, HomeEntry.RuleProposals), "p0000007.json");

    private string Untakeable => Path.Combine(DaorisHome.Entry(Home, HomeEntry.RuleProposals), "p0000003.json");

    private string Settings => Path.Combine(HarnessSettings.ProfileHome(Home, "claude-code", "work"), AgentSettings.FileName);

    private string Unreadable => Path.Combine(HarnessSettings.ProfileHome(Home, "claude-code", "broken"), AgentSettings.FileName);

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
    };

    private sealed record Said(int Exit, string Out, string Error, string? Rules, string? Proposal, string? Untakeable, string? Settings, string? Unreadable);

    private static string Proposed(string id, string rule) => $$"""
        {
          "id": "{{id}}",
          "proposed": "2026-09-24T10:00:00.0000000+00:00",
          "by": { "session": "s1a2b3c4", "ask": null, "folder": "C:/somewhere/engine" },
          "change": { "action": "add", "scope": "workspace", "name": "default", "list": "allow", "rule": "{{rule}}", "default": null, "on": null },
          "why": "The tests need it.",
          "state": "waiting"
        }
        """;

    /// <summary>The home as each case starts it: the rules, two proposals waiting, and two accounts' settings, one unreadable.</summary>
    private void Laid()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Rules)!);
        File.WriteAllText(Rules, """{"future":{"kept":1},"machine":{"deny":["WebFetch"]},"repositories":{"engine":{"allow":["Bash(make:*)"]}}}""");
        Directory.CreateDirectory(Path.GetDirectoryName(Proposal)!);
        File.WriteAllText(Proposal, Proposed("p0000007", "WebFetch"));
        File.WriteAllText(Untakeable, Proposed("p0000003", "rm -rf everything"));
        Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
        File.WriteAllText(Settings, """{"theme":"dark","model":"opus"}""");
        Directory.CreateDirectory(Path.GetDirectoryName(Unreadable)!);
        File.WriteAllText(Unreadable, "{ not json");
    }

    private Said Now(StringWriter output, StringWriter error, int exit)
    {
        static string? Read(string path) => File.Exists(path) ? Settled().Replace(File.ReadAllText(path), "$1\"<at>\"") : null;
        return new(exit, output.ToString(), error.ToString(), Read(Rules), Read(Proposal), Read(Untakeable), Read(Settings), Read(Unreadable));
    }

    private async Task<Said> AloneAsync(string[] words)
    {
        Laid();
        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = await AgentsCommand.RunAsync(words, World(output, error), CancellationToken.None);
        return Now(output, error, exit);
    }

    /// <summary>The same words' press sent to the module's route, its answer said by the command's words, as the pipe's client says it.</summary>
    private async Task<Said> RouteAsync(string[] words)
    {
        Laid();
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
    [InlineData("rules allow WebFetch")]
    [InlineData("rules allow Bash(npm:*) --workspace default")]
    [InlineData("rules deny Edit(/docs/**) --repository ENGINE")]
    [InlineData("rules ask WebSearch")]
    [InlineData("rules remove Bash(make:*) --repository engine")]
    [InlineData("rules remove WebFetch --workspace nobody")]
    [InlineData("rules default no-push off")]
    [InlineData("rules default tree-guard on")]
    [InlineData("rules accept p0000007")]
    [InlineData("rules decline #p0000007 --note Not from a session.")]
    [InlineData("rules accept p0000003")]
    [InlineData("settings claude-code --account work model sonnet effort xhigh")]
    [InlineData("settings claude-code --account work effort low --for claude-opus-5")]
    [InlineData("settings claude-code --account work model unset")]
    [InlineData("settings claude-code-acp --account work effort medium")]
    [InlineData("settings claude-code --account broken model opus")]
    public async Task An_agent_press_says_and_writes_the_same_alone_and_through_its_route(string text)
    {
        // A note is one word after its flag, as a shell hands it: the rest of the sentence is put back together here.
        var split = text.Split(" --note ");
        string[] words = split.Length > 1 ? [.. split[0].Split(' '), "--note", split[1]] : text.Split(' ');

        var alone = await AloneAsync(words);
        var route = await RouteAsync(words);

        Assert.NotEqual("", alone.Out + alone.Error);
        Assert.Equal(alone, route);
    }

    /// <summary>A proposal's settling moment, each path's own: spelled <c>&lt;at&gt;</c>.</summary>
    [GeneratedRegex("(\"at\": ?)\"\\d{4}-\\d\\d-\\d\\dT[^\"]*\"")]
    private static partial Regex Settled();
}
