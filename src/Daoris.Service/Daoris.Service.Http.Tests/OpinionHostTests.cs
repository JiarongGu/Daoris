using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// XAGENT1c (D155 points 7 and 11; the second agent design §6.2–§6.3): a local host's doors to a second opinion. The driver
/// asks a pass, which opens its reviewer's record beside it, reads it back, and hands its findings to the working session;
/// each answered only to a caller on this machine. Another agent's findings wait on the working session's record by whom,
/// never in `answer`, and never reach the ask as the person's words; and the say door refuses a reviewer's record.
/// </summary>
public sealed class OpinionHostTests(LocalHost host) : IClassFixture<LocalHost>
{
    private const string Base = "1111111111111111111111111111111111111111";
    private const string Tip = "2222222222222222222222222222222222222222";

    /// <summary>A session that worked an ask's quest in a tree of its own, and ended: the work a pass reads.</summary>
    private async Task<(string Ask, string Session)> WorkedAsync(string sentence, string tree)
    {
        var asked = await host.PostAsync("/api/asks", new { workspace = "default", sentence, to = "Keeper" });
        Assert.Equal(200, asked.Status);
        var opened = await host.PostAsync("/api/sessions", new
        {
            quest = asked.Json.GetProperty("quest").GetProperty("id").GetString(), adapter = "stub",
            tree = Path.Combine(host.Repositories, "trees", tree),
        });
        Assert.Equal(200, opened.Status);
        var id = opened.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working", "completed" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{id}/state", new { state })).Status);
        }

        return (asked.Json.GetProperty("ask").GetProperty("id").GetString()!, id);
    }

    private object Pass(string working, string clone, string occasion = "landing", int? minutes = 20) => new
    {
        occasion, pass = "first", working, posture = "copy-alone", minutes,
        tree = Path.Combine(host.Repositories, "clones", clone),
        candidate = new { repository = "Keeper", @base = Base, tip = Tip, commits = new[] { Tip } },
        reviewer = new { adapter = "codex-acp", label = "another-maker", product = "Codex", maker = "OpenAI", account = "work" },
        families = new[] { "claude-code" },
    };

    private async Task<JsonElement?> ListedSessionAsync(string id, System.Net.IPAddress? from = null) =>
        (await host.GetAsync("/api/sessions?includeClosed=true", from)).Json.EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(row => row!.Value.GetProperty("id").GetString() == id);

    /// <summary>
    /// The driver asks a pass: 200 with the opinion and its reviewer's record, a chat that names it and serves no quest. Both
    /// opinion routes read it back as `reading`, and the session's `opinion` answers this machine only. A pass out of shape
    /// is 400, one on a session nobody holds 404, and one past the rule's bound 409, each with the desk's sentence; and a
    /// caller off this machine is answered no opinion at all.
    /// </summary>
    [Fact]
    public async Task A_pass_opens_with_its_reviewers_record_and_reads_back_to_this_machine_only()
    {
        var (_, working) = await WorkedAsync("Build the weekly page.", "xagent1c-pass");

        var asked = await host.PostAsync("/api/opinions", Pass(working, "pass-1"));

        Assert.Equal(200, asked.Status);
        var opinion = asked.Json.GetProperty("opinion");
        var id = opinion.GetProperty("id").GetString()!;
        Assert.Equal(("landing", "first", working, "reading"),
            (opinion.GetProperty("occasion").GetString(), opinion.GetProperty("pass").GetString(), opinion.GetProperty("working").GetString(),
             opinion.GetProperty("state").GetString()));
        Assert.Equal((Tip, "Codex", "another-maker"),
            (opinion.GetProperty("candidate").GetProperty("tip").GetString(), opinion.GetProperty("reviewer").GetProperty("product").GetString(),
             opinion.GetProperty("reviewer").GetProperty("label").GetString()));
        var reviewer = asked.Json.GetProperty("session");
        var reviewerId = reviewer.GetProperty("id").GetString()!;
        Assert.Equal((reviewerId, "chat", id), (opinion.GetProperty("session").GetString(), reviewer.GetProperty("kind").GetString(), reviewer.GetProperty("opinion").GetString()));
        Assert.False(reviewer.TryGetProperty("quest", out _));
        Assert.StartsWith($"Second opinion `{id}`", asked.Json.GetProperty("message").GetString());

        Assert.Equal("reading", (await host.GetAsync($"/api/opinions/{id}")).Json.GetProperty("state").GetString());
        Assert.Equal([id], (await host.GetAsync($"/api/opinions?working={working}")).Json.EnumerateArray().Select(row => row.GetProperty("id").GetString()));
        Assert.Equal(id, (await ListedSessionAsync(reviewerId))!.Value.GetProperty("opinion").GetString());
        Assert.False((await ListedSessionAsync(reviewerId, DaorisHost.OffMachine))!.Value.TryGetProperty("opinion", out _));

        foreach (var path in new[] { $"/api/opinions/{id}", $"/api/opinions?working={working}" })
        {
            var offMachine = await host.GetAsync(path, DaorisHost.OffMachine);
            Assert.Equal(404, offMachine.Status);
            Assert.Contains("only to a caller on this machine", offMachine.Error);
            Assert.DoesNotContain(Tip, offMachine.Body);
        }

        Assert.Equal(404, (await host.PostAsync("/api/opinions", Pass(working, "pass-2"), DaorisHost.OffMachine)).Status);
        var unfit = await host.PostAsync("/api/opinions", Pass(working, "pass-3", minutes: null));
        Assert.Equal(400, unfit.Status);
        Assert.Contains("`minutes`", unfit.Error);
        Assert.Equal(404, (await host.PostAsync("/api/opinions", Pass("nobody12", "pass-4"))).Status);
        var bound = await host.PostAsync("/api/opinions", Pass(working, "pass-5", "steps"));
        Assert.Equal(409, bound.Status);
        Assert.Contains($"`{id}`", bound.Error);
        Assert.Equal(404, (await host.GetAsync("/api/opinions/nothing1")).Status);

        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{reviewerId}/state", new { state = "stopped" })).Status);
    }

    /// <summary>
    /// 🔴 The hand door keeps the findings on the working session's record as another agent's words (design §6.3): `said`
    /// names the opinion as `by`, `answer` holds none of them, and when the session takes them they are never kept on the
    /// ask, where they would read as the person's. A second hand is 409. The say door refuses words to the reviewer's record
    /// by its word, `opinion`.
    /// </summary>
    [Fact]
    public async Task Findings_wait_as_another_agents_words_and_never_reach_the_ask()
    {
        var (ask, working) = await WorkedAsync("Add the compare setting.", "xagent1c-hand");
        var asked = await host.PostAsync("/api/opinions", Pass(working, "hand-1"));
        var id = asked.Json.GetProperty("opinion").GetProperty("id").GetString()!;
        var reviewer = asked.Json.GetProperty("session").GetProperty("id").GetString()!;
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(200, (await host.PostAsync($"/api/sessions/{reviewer}/state", new { state })).Status);
        }

        Assert.Equal(409, (await host.PostAsync($"/api/opinions/{id}/hand", new { })).Status);
        var given = await host.Composed.Opinions.GiveAsync(
            reviewer, [new OpinionFinding("must", "src/Page.cs:7", "The setting is never read.", "The page ignores it.", "Opened it: no change.", "sure")],
            "src/Page.cs", null, [], DateTimeOffset.UtcNow);
        Assert.Equal(OpinionRefusal.None, given.Refusal);

        var handed = await host.PostAsync($"/api/opinions/{id}/hand", new { });

        Assert.Equal(200, handed.Status);
        var word = handed.Json.GetProperty("said");
        var wordId = word.GetProperty("id").GetString()!;
        Assert.Equal((id, true), (word.GetProperty("by").GetString(), word.GetProperty("reopens").GetBoolean()));
        Assert.StartsWith("Another agent, Codex by OpenAI,", word.GetProperty("text").GetString());
        Assert.Equal((working, wordId), (handed.Json.GetProperty("opinion").GetProperty("handed").GetProperty("session").GetString(),
            handed.Json.GetProperty("opinion").GetProperty("handed").GetProperty("word").GetString()));
        var listed = (await ListedSessionAsync(working))!.Value;
        Assert.Equal(id, Assert.Single(listed.GetProperty("said").EnumerateArray().ToList()).GetProperty("by").GetString());
        Assert.False(listed.TryGetProperty("answer", out _));
        Assert.Equal(409, (await host.PostAsync($"/api/opinions/{id}/hand", new { })).Status);

        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{working}/state", new { state = "working" })).Status);
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{working}/taken", new { said = new[] { wordId } })).Status);
        var words = (await host.GetAsync($"/api/asks/{ask}")).Json.GetProperty("words").EnumerateArray().ToList();
        Assert.Equal(["asked"], words.Select(each => each.GetProperty("kind").GetString()));

        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{reviewer}/state", new { state = "completed" })).Status);
        var refused = await host.PostAsync($"/api/sessions/{reviewer}/say", new { text = "Look at the tests too." });
        Assert.Equal(409, refused.Status);
        Assert.Equal(("opinion", id), (refused.Json.GetProperty("refusal").GetString(), refused.Json.GetProperty("opinion").GetString()));
        Assert.Equal(200, (await host.PostAsync($"/api/sessions/{working}/state", new { state = "stopped" })).Status);
    }
}
