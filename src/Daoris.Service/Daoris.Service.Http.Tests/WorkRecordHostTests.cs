namespace Daoris.Service.Http.Tests;

/// <summary>
/// KEEPSIZE1b (D195's keep-size note; the keep-size design §2.2): a finished tree's record through its door, <c>POST /api/trees/record</c>.
/// The driver posts it on loopback; it names the sessions whose row names the tree, is kept once whatever the retries, and is answered to
/// nothing off this machine.
/// </summary>
public sealed class WorkRecordHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private static object Record(string folder, string tree) => new
    {
        workspace = "default",
        repository = "Keeper",
        folder,
        tree,
        moved = DateTimeOffset.UtcNow.AddMinutes(1),
        branch = "daoris/s-1",
        line = "main",
        hold = new { kind = "line", where = "main", form = "branch" },
        tip = "c2",
        grewFrom = "c0",
        commits = new[]
        {
            new { id = "c1", subject = "One", files = new[] { new { path = "a.txt", added = 1, removed = 0 } } },
            new { id = "c2", subject = "Two", files = new[] { new { path = "b.txt", added = 2, removed = 1 } } },
        },
        left = new { names = new[] { "notes.md" }, count = 1, cut = false },
    };

    /// <summary>🔴 Kept once with its session; the second post answers as the first did, <c>kept: false</c>.</summary>
    [Fact]
    public async Task A_record_is_kept_once_with_the_session_that_ran_in_its_tree()
    {
        var tree = host.RootOf("keeper-record");
        var published = await host.PostAsync("/api/quests", new { from = "Asker", to = "Keeper", title = "Recorded work", body = "Go on." });
        var quest = published.Json.GetProperty("quest").GetProperty("id").GetString()!;
        var opened = await host.PostAsync("/api/sessions", new { quest, adapter = "stub", tree, baseCommit = "c0" });
        var session = opened.Json.GetProperty("session").GetProperty("id").GetString()!;

        var first = await host.PostAsync("/api/trees/record", Record("keeper-record-20261024T100000000Z", tree));
        var again = await host.PostAsync("/api/trees/record", Record("keeper-record-20261024T100000000Z", tree));

        Assert.Equal((200, 200), (first.Status, again.Status));
        Assert.True(first.Json.GetProperty("kept").GetBoolean());
        Assert.False(again.Json.GetProperty("kept").GetBoolean());
        Assert.Equal(first.Json.GetProperty("at").GetString(), again.Json.GetProperty("at").GetString());
        var named = Assert.Single(first.Json.GetProperty("sessions").EnumerateArray());
        Assert.Equal(session, named.GetProperty("session").GetString());
        Assert.Equal(["c1", "c2"], named.GetProperty("commits").EnumerateArray().Select(each => each.GetString()));
    }

    /// <summary>
    /// History's age through its door (Codex's read of the branch, finding 8): a record no session names, kept before the cutoff, goes by its
    /// own <c>at</c>; a cutoff missing is a 400, and off this machine nothing answers.
    /// </summary>
    [Fact]
    public async Task A_record_no_session_names_goes_by_history_s_age()
    {
        var posted = await host.PostAsync("/api/trees/record", Record("keeper-age-20261001T100000000Z", host.RootOf("keeper-age-nobody")));
        Assert.Equal(200, posted.Status);

        var early = await host.PostAsync("/api/trees/record/age", new { before = DateTimeOffset.UtcNow.AddDays(-1) });
        var due = await host.PostAsync("/api/trees/record/age", new { before = DateTimeOffset.UtcNow.AddMinutes(1), workspace = "default" });

        Assert.Equal((200, 200), (early.Status, due.Status));
        Assert.Equal(0, early.Json.GetProperty("cleared").GetInt32());
        Assert.True(due.Json.GetProperty("cleared").GetInt32() >= 1);
        Assert.Equal(400, (await host.PostAsync("/api/trees/record/age", new { })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/trees/record/age", new { before = DateTimeOffset.UtcNow }, DaorisHost.OffMachine)).Status);
    }

    /// <summary>A part missing is a 400; a caller off this machine is answered nothing.</summary>
    [Fact]
    public async Task A_partial_record_is_refused_and_off_the_machine_nothing_answers()
    {
        Assert.Equal(400, (await host.PostAsync("/api/trees/record", new { workspace = "default", repository = "Keeper" })).Status);
        Assert.Equal(404, (await host.PostAsync("/api/trees/record", Record("keeper-off-20261024T100000000Z", host.RootOf("keeper-off")), DaorisHost.OffMachine)).Status);
    }
}
