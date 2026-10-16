using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HIST1f-c: a clear proposal's modules half. The judge is given what this machine keeps only when one is pending; the
/// first draw holds the units its card listed in the proposal's file, and Apply is <c>HISTORY_CLEAR</c>'s own clear of exactly
/// those. The service is stood in over a loopback socket and nothing starts a process: the fast half (MOD8).
/// </summary>
public sealed class HelpClearDoorTests : DriverModuleBridge
{
    [Theory]
    [InlineData("clear", "quest", true)]
    [InlineData("clear", "failed", true)]
    [InlineData("clear", "workspace", true)]
    [InlineData("pause", "pause", false)]
    [InlineData("delete", "quest", false)]
    public void The_history_is_read_for_a_clear_proposal_only(string kind, string door, bool read)
    {
        var proposal = new HelpProposal("p1", kind, door, "q1", null, null, null, "why", "h1", "proposed");

        Assert.Equal(read, HelpRoutes.JudgedAgainstHistory([proposal]));
        Assert.False(HelpRoutes.JudgedAgainstHistory([]));
    }

    [Fact]
    public async Task The_door_clears_exactly_the_units_it_is_sent_and_says_what_went()
    {
        Kept("s1");
        using var ledger = Ledger();
        var doors = await DoorsAsync(ledger);

        var outcome = await doors.ClearHistoryAsync(HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], CancellationToken.None);

        Assert.Equal(["q1"], outcome.Cleared.Select(each => each.Id));
        Assert.False(File.Exists(Path.Combine(Home, "sessions", "s1.log")));
    }

    [Fact]
    public async Task The_door_before_the_driver_is_up_is_the_cold_start_sentence()
    {
        var refused = await Assert.ThrowsAnyAsync<Exception>(
            () => Module().HelpDoors(null).ClearHistoryAsync(HistoryScope.Quest, "q1", [new HistoryUnitName("quest", "q1")], CancellationToken.None));

        Assert.Contains("still coming up", refused.Message);
    }

    /// <summary>§5 end to end: the first draw lists the quest and holds it in the file; Apply clears exactly what was held.</summary>
    [Fact]
    public async Task A_clear_is_drawn_and_then_applied_to_exactly_the_units_the_card_listed()
    {
        Kept("s1");
        using var ledger = Ledger();
        var module = await UpAsync(ledger);
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "p1a2b3c4.json");
        File.WriteAllText(path,
            """{"id":"p1a2b3c4","kind":"clear","door":"quest","target":"#q1","why":"done","state":"proposed","by":{"session":"h1"}}""");

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        Assert.Equal("p1a2b3c4", drawn.GetProperty("proposals")[0].GetProperty("id").GetString());
        Assert.Contains("\"units\"", File.ReadAllText(path));
        Assert.True(File.Exists(Path.Combine(Home, "sessions", "s1.log")));

        var applied = await AnswerAsync(module, "HELP_APPLY",
            new { id = "p1a2b3c4", revision = drawn.GetProperty("proposals")[0].GetProperty("revision").GetString() });

        Assert.True(applied.GetProperty("applied").GetBoolean());
        Assert.False(File.Exists(Path.Combine(Home, "sessions", "s1.log")));
    }

    /// <summary>HIST1f-d: the <c>failed</c> door is read as the quest's failed sessions, so its card lists that unit, not the quest.</summary>
    [Fact]
    public async Task A_failed_sessions_clear_is_drawn_with_the_failed_unit_read_for_its_quest()
    {
        Kept("s1");
        using var ledger = Ledger();
        var module = await UpAsync(ledger);
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "p1a2b3c4.json");
        File.WriteAllText(path,
            """{"id":"p1a2b3c4","kind":"clear","door":"failed","target":"#q1","why":"done","state":"proposed","by":{"session":"h1"}}""");

        await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        Assert.Contains("\"kind\": \"failed\"", File.ReadAllText(path));
    }

    /// <summary>A card never drawn has listed nothing, so Apply sends nothing and the proposal stands.</summary>
    [Fact]
    public async Task Apply_before_the_card_was_drawn_clears_nothing()
    {
        Kept("s1");
        using var ledger = Ledger();
        var module = await UpAsync(ledger);
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"),
            """{"id":"p1a2b3c4","kind":"clear","door":"quest","target":"#q1","why":"done","state":"proposed","by":{"session":"h1"}}""");

        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4" }));

        Assert.True(File.Exists(Path.Combine(Home, "sessions", "s1.log")));
    }

    private void Kept(string id)
    {
        Directory.CreateDirectory(Path.Combine(Home, "sessions", id, "files"));
        File.WriteAllText(Path.Combine(Home, "sessions", $"{id}.events.jsonl"), "{\"seq\":1,\"kind\":\"user\",\"text\":\"a plan\"}\n");
        File.WriteAllText(Path.Combine(Home, "sessions", $"{id}.log"), "the transcript");
    }

    private async Task<DriverModule> UpAsync(LoopbackHost ledger)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(ledger.Address, null));
        return new DriverModule(Bus, loop);
    }

    private async Task<IHelpDoors> DoorsAsync(LoopbackHost ledger)
    {
        var loop = Loop();
        var service = new ServiceClient(ledger.Address, null);
        await loop.ComeUpAsync(service);
        return new DriverModule(Bus, loop).HelpDoors(service);
    }

    /// <summary>One closed quest <c>q1</c> and its finished session, listed by the service's history door and cleared by it.</summary>
    private LoopbackHost Ledger()
    {
        var host = new LoopbackHost();
        var unit = """{"kind":"quest","id":"q1","workspace":"aurora","clearable":true,"quests":["q1"],"forgotten":[],"asks":[],"sessions":["s1"],"teammates":[],"kept":[]}""";
        host.Serve("/api/sessions?includeClosed=true", Bytes(
            """[{"id":"s1","quest":"q1","repository":"engine","adapter":"claude-code","state":"completed","kind":"driven","workspace":"aurora","created":"2026-10-02T09:00:00Z","updated":"2026-10-02T09:10:00Z"}]"""));
        host.Serve("/api/sessions", Bytes("[]"));
        host.Serve("/api/quests?includeClosed=true", Bytes(
            """[{"id":"q1","from":"game","to":"engine","title":"A title","body":"","status":"Done","workspace":"aurora"}]"""));
        host.Serve("/api/quests", Bytes("[]"));
        host.Serve("/api/asks?includeClosed=true", Bytes("[]"));
        host.Serve("/api/asks", Bytes("[]"));
        host.Serve("/api/registry", Bytes("[]"));
        host.Serve("/api/history?quest=q1", Bytes($$"""{"units":[{{unit}}]}"""));
        host.Serve("/api/history?quest=q1&failed=true", Bytes(
            """{"units":[{"kind":"failed","id":"q1","workspace":"aurora","clearable":true,"quests":[],"forgotten":[],"asks":[],"sessions":["s1"],"teammates":[],"kept":[]}]}"""));
        host.Serve("/api/history?workspace=aurora", Bytes($$"""{"units":[{{unit}}]}"""));
        host.Serve("/api/history/clear", Bytes($$"""{"units":[{"unit":{{unit}},"cleared":true,"message":"Cleared `#q1` from this machine."}]}"""));
        return host;
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
}
