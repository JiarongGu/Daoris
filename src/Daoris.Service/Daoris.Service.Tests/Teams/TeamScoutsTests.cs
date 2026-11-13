using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A scout's half in the service: only a lead asks one, and for a part its plan holds; the ask carries the part and the scout's answer is kept
/// with the plan and records the scout as a member; a session opened on a part's quest is the part's worker. In memory, so each runs in milliseconds.
/// </summary>
public sealed class TeamScoutsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-14T08:00:00Z");

    private readonly string _home = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "team-scouts", Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private TeamStore _store = null!;
    private TeamLedger _ledger = null!;
    private TeamDesk _desk = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_home);
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = await TeamStore.OpenAsync(_connection);
        _ledger = await TeamLedger.OpenAsync(_connection);
        _desk = new TeamDesk(_store, _ledger, null!, (_, _) => Task.FromResult(TeamPathPolicy.None));
        var plan = new TeamPlan("a1", null, 2, "lead-1", "two parts", Now)
        {
            Parts = [new TeamPart("api", "media-api", "Api", "Do the api.", "api works"), new TeamPart("page", "media-web", "Page", "Do the page.", "page works")],
        };
        await _store.AppendAsync(plan);
        await _store.ReserveAsync("a1", "api", "Api");
        await _store.RecordQuestAsync("a1", "api", "q-api");
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static Session Intake(string ask) =>
        new("lead-1", null, "", "stub", SessionState.Working, null, null, null, Now, Now) { Ask = ask };

    private static Session Worker(string quest) => new("worker-1", quest, "media-api", "stub", SessionState.Working, null, null, null, Now, Now);

    [Fact]
    public async Task The_intake_lead_asks_a_scout_for_a_part_its_plan_holds_and_the_part_names_the_repository_it_reads()
    {
        var (scout, refusal) = await _desk.Scouts.JudgeAsync(Intake("a1"), " page ");

        Assert.Null(refusal);
        Assert.Equal(new HelperScout("a1", "page", "media-web"), scout);
    }

    [Fact]
    public async Task A_worker_splitting_its_own_part_asks_for_a_part_of_its_ask_and_a_plain_session_may_not()
    {
        Assert.Equal("a1", (await _desk.Scouts.JudgeAsync(Worker("q-api"), "api")).Scout!.Ask);

        var (none, refusal) = await _desk.Scouts.JudgeAsync(Worker("q-elsewhere"), "api");
        Assert.Null(none);
        Assert.Contains("Only a lead asks a scout", refusal);
    }

    [Fact]
    public async Task A_part_no_plan_holds_or_none_named_is_refused_with_what_to_do()
    {
        Assert.Contains("no part `nowhere`", (await _desk.Scouts.JudgeAsync(Intake("a1"), "nowhere")).Refusal);
        Assert.Contains("Name the part", (await _desk.Scouts.JudgeAsync(Intake("a1"), " ")).Refusal);
    }

    [Fact]
    public void The_ask_carries_the_scout_and_a_started_scouts_answer_door_reads_it_back_and_an_intake_asks_nothing_else()
    {
        var box = new HelperBox(_home);
        var scout = new HelperScout("a1", "page", "media-web");

        var (id, said) = box.Ask(Intake("a1"), "lead-1", "Orient the page part.", null, Now, scout: scout);
        Assert.NotNull(id);
        Assert.Contains("orient part `page`", said);

        var plain = box.Ask(Intake("a1"), "lead-1", "Anything?", null, Now);
        Assert.Null(plain.Id);

        Directory.CreateDirectory(Path.GetDirectoryName(HelperBox.PathOf(_home, id!, "state"))!);
        File.WriteAllText(HelperBox.PathOf(_home, id!, "state"), $$"""{ "id": "{{id}}", "state": "started", "helper": "scout-1", "at": "2026-11-14T08:00:00Z" }""");
        var helper = new Session("scout-1", null, "media-web", "stub", SessionState.Working, null, null, null, Now, Now) { Serves = "lead-1" };

        Assert.Equal(scout, box.ScoutOf(helper));
    }

    [Fact]
    public async Task A_scouts_answer_is_kept_with_the_plan_and_the_scout_is_a_member_of_the_part_at_the_revision_holding_it()
    {
        await _desk.Scouts.KeepAsync(new HelperScout("a1", "page", "media-web"), "scout-1", "read src/page/** and docs/page.md", Now);
        await _desk.Scouts.KeepAsync(new HelperScout("a1", "page", "media-web"), "scout-1", "a second word", Now.AddMinutes(1));

        var scouted = Assert.Single(await _desk.Scouts.ScoutedAsync("#a1"));
        Assert.Equal(("page", "scout-1", "read src/page/** and docs/page.md"), (scouted.Part, scouted.Session, scouted.Pack));
        var member = Assert.Single((await _desk.TeamAsync("a1")).Members);
        Assert.Equal((TeamMember.Scout, "page", 2), (member.Role, member.Part, member.Revision));
        var (plans, rows) = await _desk.ReadAsync("a1");
        var reading = TeamPlanReading.Of("a1", plans, rows, new Dictionary<string, string>(), [member], [], [scouted]);
        Assert.Contains("scout `scout-1` oriented part `page`", reading);
        Assert.Contains("> read src/page/** and docs/page.md", reading);
    }

    [Fact]
    public async Task A_session_opened_on_a_parts_quest_is_its_worker_and_one_on_any_other_quest_is_no_member()
    {
        Assert.True(await _desk.Workers.KeepAsync("worker-1", "q-api", Now));
        Assert.False(await _desk.Workers.KeepAsync("worker-2", "q-unplanned", Now));

        var member = Assert.Single((await _desk.TeamAsync("a1")).Members);
        Assert.Equal((TeamMember.Worker, "api", "worker-1", 2), (member.Role, member.Part, member.Session, member.Revision));
    }
}
