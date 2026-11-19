using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// The one door: `question_ask` names the role asked and, for an act, the act, and the request goes out on the channel that role's
/// holder takes it on. `go_ahead_ask` and `helper_ask` are channels of the same request, so a caller of either lands on it. Each seen
/// failing first.
/// </summary>
public sealed class McpRoleRequestTests : McpToolsFixture
{
    private static readonly DateTimeOffset At = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private string Home => Path.Combine(_root, "request-home");

    private async Task<(SessionLedger Ledger, AskDesk Desk, AskOutcome Asked)> AskedAsync()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, asks);
        var desk = new AskDesk(_service, asks, new QuestExchange(_service, _quests, files: _files), _files);
        var asked = await desk.AskAsync(new AskRequest("default", "The dashboard figure reads zero.") { To = "Owner" }, At);
        return (ledger, desk, asked);
    }

    private QuestionTools Door(SessionLedger ledger, string session) =>
        new(new IntakeScope(null, session), ledger, new QuestionBox(Home), new HelperBox(Home));

    [Fact]
    public async Task An_act_asked_through_the_door_is_a_go_ahead_on_the_ask_and_the_old_tool_joins_it()
    {
        var (ledger, desk, asked) = await AskedAsync();
        var quest = asked.Quest!;
        var first = (await ledger.OpenAsync(quest.Id, "stub", At, tree: Path.Combine(_root, "trees", "a"))).Session!;
        var second = (await ledger.OpenAsync(quest.Id, "stub", At, tree: Path.Combine(_root, "trees", "b"))).Session!;

        var asking = await Door(ledger, first.Id).AskAsync(
            "The tile's target needs the production configuration changed; I would set it to 1,200.", act: "write dashboard configuration", on: "production");
        var joining = await new GoAheadTools(new IntakeScope(null, second.Id), ledger).AskGoAheadAsync("put", "prod", "the dashboard's configuration", "Still needed.");

        Assert.Contains($"Asked as go-ahead 1 on ask `#{asked.Ask!.Id}`", asking);
        Assert.Contains("Go-ahead 1", joining);
        var held = Assert.Single((await desk.FindAsync(asked.Ask.Id))!.GoAheads);
        Assert.Equal([first.Id, second.Id], held.Asked.Select(request => request.Session));
        Assert.False(File.Exists(QuestionBox.PathOf(Home, first.Id)));
    }

    [Fact]
    public async Task The_helper_role_asked_through_the_door_is_a_helpers_reading_as_helper_ask_writes_it()
    {
        var (ledger, _, asked) = await AskedAsync();
        var session = (await ledger.OpenAsync(asked.Quest!.Id, "stub", At, tree: Path.Combine(_root, "trees", "h"))).Session!;

        var said = await Door(ledger, session.Id).AskAsync("Does the retry loop in Report.cs stop at five?", role: "helper");
        var refused = await Door(ledger, session.Id).AskAsync("Release it for me.", role: "helper", act: "release the app", on: "production");

        Assert.Contains("driver takes it at its next look", said);
        var file = Assert.Single(Directory.GetFiles(DaorisHome.Entry(Home, HomeEntry.Helpers), "*.ask.json"));
        using var kept = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal((session.Id, "Does the retry loop in Report.cs stop at five?"),
            (kept.RootElement.GetProperty("asker").GetString(), kept.RootElement.GetProperty("question").GetString()));
        Assert.Contains("takes no act", refused);
        Assert.False(File.Exists(QuestionBox.PathOf(Home, session.Id)));
    }

    [Fact]
    public async Task The_credentials_keeper_is_asked_for_a_sign_in_kept_with_its_role_and_another_repositorys_role_travels_as_a_quest()
    {
        var (ledger, _, asked) = await AskedAsync();
        var session = (await ledger.OpenAsync(asked.Quest!.Id, "stub", At, tree: Path.Combine(_root, "trees", "k"))).Session!;

        var said = await Door(ledger, session.Id).AskAsync(
            "The release identity's sign-in expired: sign in so I can start stage 403.", role: "credentials-keeper", site: "https://dev.example/org/_signin");
        var other = await Door(ledger, session.Id).AskAsync("Change the gateway's route.", role: "infra:operations");
        var unshaped = await Door(ledger, session.Id).AskAsync("Who holds this?", role: "the person");

        Assert.Contains("sign-in", said);
        var kept = QuestionBox.Read(Home, session.Id)!;
        Assert.Equal(("sign-in", "https://dev.example/org/_signin", "credentials-keeper"), (kept.Owner, kept.Site, kept.Role));
        Assert.Contains("quest_publish", other);
        Assert.Contains("Nothing was asked", unshaped);
    }
}
