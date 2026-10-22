using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// ASKQUESTS1 (the Ask Daoris design §9 item 12): <c>HELP_PUBLISHED</c>, a conversation's own list of what its Applies published. A
/// set-up's Apply publishes an ask and its quest, and the list answers them by the ids the service answered, newest first; a module
/// over the same home, as after a restart, answers the same. The press's world and the service stand in.
/// </summary>
public sealed class HelpPublishedRouteTests : DriverModuleBridge
{
    private readonly SetupStandIn _world = new();

    [Fact]
    public async Task A_published_ask_and_quest_are_in_their_conversations_list_and_survive_a_restart()
    {
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).WithDrivable("atlas", true).WithTrees("atlas", true).Save(DriverConfigPath);
        var module = await UpAsync();
        Proposal("""{"id":"p1a2b3c4","kind":"setup","door":"setup","target":"atlas","workspace":null,"value":null,"sentence":null,"why":"include it","state":"proposed","by":{"session":"h1a2b3c4"}}""");
        Assert.Empty((await AnswerAsync(module, "HELP_PUBLISHED", new { session = "h1a2b3c4" })).GetProperty("published").EnumerateArray());

        var applied = await ApplyShownAsync(module, "h1a2b3c4", "p1a2b3c4");
        Assert.True(applied.GetProperty("applied").GetBoolean());

        var listed = await AnswerAsync(module, "HELP_PUBLISHED", new { session = "h1a2b3c4" });
        Assert.Equal("h1a2b3c4", listed.GetProperty("session").GetString());
        var entry = Assert.Single(listed.GetProperty("published").EnumerateArray());
        Assert.Equal(
            ("p1a2b3c4", "setup", "work", "a1b2c3d4", "q1a2b3c4"),
            (entry.GetProperty("proposal").GetString(), entry.GetProperty("kind").GetString(), entry.GetProperty("workspace").GetString(),
                entry.GetProperty("ask").GetString(), entry.GetProperty("quest").GetString()));
        Assert.True(entry.TryGetProperty("at", out _));

        // A restart: a new loop and module over the same home read the same list.
        var again = await UpAsync();
        var kept = Assert.Single((await AnswerAsync(again, "HELP_PUBLISHED", new { session = "h1a2b3c4" })).GetProperty("published").EnumerateArray());
        Assert.Equal("q1a2b3c4", kept.GetProperty("quest").GetString());
    }

    /// <summary>A conversation that published nothing, or an id that is no session's, answers an empty list, never a refusal.</summary>
    [Fact]
    public async Task A_conversation_with_nothing_published_answers_an_empty_list()
    {
        var module = Module();

        Assert.Empty((await AnswerAsync(module, "HELP_PUBLISHED", new { session = "h9z8y7x6" })).GetProperty("published").EnumerateArray());
        Assert.Empty((await AnswerAsync(module, "HELP_PUBLISHED", new { session = "../escape" })).GetProperty("published").EnumerateArray());
    }

    private void Proposal(string json)
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), json);
    }

    private async Task<DriverModule> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop) { SetupWorlds = _ => _world };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SetupStandIn.EmptyHost())));
        return module;
    }
}
