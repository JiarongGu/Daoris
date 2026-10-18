using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CLOSE1b: the driver's client for a closure graph (the fabric events design §E5; FABRIC-CLOSE1a and CLOSE1e's notes in D173). A
/// quest's <c>closure</c> and <c>submission</c> read, absent from a host before them; an evidence verdict on a submitted quest naming its
/// submission; the bind, the close and its refusals by their code (<c>key-reused</c> and a replayed refusal among them); and the handshake.
/// Runs no process: the fast half (MOD8).
/// </summary>
public sealed class ServiceClientClosureTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>The host as the client knocks on it: each request kept, each answer the test's.</summary>
    private sealed class Doors(Func<ServiceVerb, string, string?, ServiceReply> answer) : IServiceDoors
    {
        public List<(ServiceVerb Verb, string Path, string? Body)> Asked { get; } = [];

        public string BaseUrl => "http://ledger.test";

        public Task<string> GetAsync(string path, CancellationToken ct)
        {
            Asked.Add((ServiceVerb.Get, path, null));
            var reply = answer(ServiceVerb.Get, path, null);
            return reply.Ok ? Task.FromResult(reply.Payload) : throw new DriverException(reply.Payload);
        }

        public Task<string> PostAsync(string path, string json, CancellationToken ct) => throw new NotSupportedException();

        public Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
        {
            Asked.Add((verb, path, json));
            return Task.FromResult(answer(verb, path, json));
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void A_quests_closure_and_submission_are_read_and_absent_from_a_host_before_them()
    {
        var quests = ServiceClient.ReadQuests(
            """
            [{ "id": "q7", "from": "game", "to": "engine", "title": "Ship it", "body": "", "status": "Taken",
               "closure": "gph-0123", "submission": { "machine": "desk", "sequence": 41, "at": "2026-10-17T09:00:00+00:00", "note": "ready" } },
             { "id": "q8", "from": "game", "to": "engine", "title": "Old", "body": "", "status": "Taken" }]
            """);

        Assert.Equal("gph-0123", quests[0].Closure);
        Assert.Equal(new QuestSubmissionView("desk", 41, new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero), "ready"), quests[0].Submission);
        Assert.Null(quests[1].Closure);
        Assert.Null(quests[1].Submission);
    }

    /// <summary>
    /// FABRIC-WORKFLOW2d: a step's source (FABRIC-CLOSE1c's <c>grewFrom</c>) and the reopen standing on it (<c>sourceReopened</c>), each a
    /// submission's machine and sequence; absent from a step a done published and from a host before them, and none where named in half.
    /// </summary>
    [Fact]
    public void A_steps_source_and_its_reopen_are_read_and_absent_from_a_host_before_them()
    {
        var quests = ServiceClient.ReadQuests(
            """
            [{ "id": "q8", "from": "game", "to": "engine", "title": "Next", "body": "", "status": "Open", "parent": "q7",
               "grewFrom": { "machine": "desk", "sequence": 41 }, "sourceReopened": { "machine": "desk", "sequence": 41 } },
             { "id": "q9", "from": "game", "to": "engine", "title": "Half", "body": "", "status": "Open", "parent": "q7",
               "grewFrom": { "machine": "desk" }, "sourceReopened": { "sequence": 41 } },
             { "id": "q10", "from": "game", "to": "engine", "title": "Old", "body": "", "status": "Open", "parent": "q7" }]
            """);

        Assert.Equal(new QuestSubmissionRef("desk", 41), quests[0].GrewFrom);
        Assert.Equal(new QuestSubmissionRef("desk", 41), quests[0].SourceReopened);
        Assert.All(quests.Skip(1), quest => Assert.True(quest is { GrewFrom: null, SourceReopened: null }, quest.Id));
    }

    /// <summary>
    /// FABRIC-CLOSE1c4: a source's last reopen no later submission answered (the reopen's machine and sequence, the submission it
    /// reopened, the person's words and when) and the session that said a submission's ready; absent from a host before them, and none
    /// where the reopen is named in half.
    /// </summary>
    [Fact]
    public void A_sources_reopen_and_the_session_that_said_ready_are_read_and_absent_from_a_host_before_them()
    {
        var quests = ServiceClient.ReadQuests(
            """
            [{ "id": "q7", "from": "game", "to": "engine", "title": "Ship it", "body": "", "status": "Taken", "closure": "gph-0123",
               "reopened": { "machine": "desk", "sequence": 44, "of": { "machine": "desk", "sequence": 41 },
                             "note": "The bridge is wrong.", "at": "2026-10-18T09:00:00+00:00", "session": "s0a1b2c3" } },
             { "id": "q8", "from": "game", "to": "engine", "title": "Again", "body": "", "status": "Taken", "closure": "gph-0123",
               "submission": { "machine": "desk", "sequence": 45, "note": "ready", "session": "s0a1b2c3" } },
             { "id": "q9", "from": "game", "to": "engine", "title": "Half", "body": "", "status": "Taken", "closure": "gph-0123",
               "reopened": { "machine": "desk", "sequence": 44, "of": { "machine": "desk" } } },
             { "id": "q10", "from": "game", "to": "engine", "title": "Old", "body": "", "status": "Taken", "closure": "gph-0123",
               "submission": { "machine": "desk", "sequence": 41 } },
             { "id": "q11", "from": "game", "to": "engine", "title": "Unsaid", "body": "", "status": "Taken", "closure": "gph-0123",
               "reopened": { "machine": "desk", "sequence": 44, "of": { "machine": "desk", "sequence": 41 }, "session": "" } }]
            """);

        // FABRIC-CLOSE1c8: the reopen names the session that said the reopened ready; none where it is blank or absent.
        Assert.Equal(
            new QuestReopenView("desk", 44, new QuestSubmissionRef("desk", 41), "The bridge is wrong.",
                new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero), "s0a1b2c3"),
            quests[0].Reopened);
        Assert.Null(quests[4].Reopened!.Session);
        Assert.Null(quests[0].Submission);
        Assert.Equal("s0a1b2c3", quests[1].Submission!.Session);
        Assert.Null(quests[1].Reopened);
        Assert.Null(quests[2].Reopened);
        Assert.Null(quests[3].Reopened);
        Assert.Null(quests[3].Submission!.Session);
    }

    /// <summary>
    /// FABRIC-CLOSE1c5: a submission's commit, its evidence's (§E5), read by its full id; absent from a host before it, and none where
    /// it is no commit's id.
    /// </summary>
    [Fact]
    public void A_submissions_commit_is_read_and_absent_from_a_host_before_it()
    {
        var quests = ServiceClient.ReadQuests(
            $$"""
            [{ "id": "q7", "from": "game", "to": "engine", "title": "Ship it", "body": "", "status": "Taken", "closure": "gph-0123",
               "submission": { "machine": "desk", "sequence": 41, "note": "ready", "commit": "{{Commit.ToUpperInvariant()}}" } },
             { "id": "q8", "from": "game", "to": "engine", "title": "Older", "body": "", "status": "Taken", "closure": "gph-0123",
               "submission": { "machine": "desk", "sequence": 42, "note": "ready" } },
             { "id": "q9", "from": "game", "to": "engine", "title": "Odd", "body": "", "status": "Taken", "closure": "gph-0123",
               "submission": { "machine": "desk", "sequence": 43, "note": "ready", "commit": "main" } }]
            """);

        Assert.Equal(new QuestSubmissionView("desk", 41, null, "ready", Commit), quests[0].Submission);
        Assert.Equal(new QuestSubmissionView("desk", 42, null, "ready"), quests[1].Submission);
        Assert.Equal(new QuestSubmissionView("desk", 43, null, "ready"), quests[2].Submission);
    }

    [Fact]
    public void A_verdict_read_for_a_submission_names_it_and_a_dones_names_none()
    {
        var verdict = new EvidenceVerdict(Commit, "session-end", [new EvidenceRead(1, "docs/x.md", null, EvidenceCodes.Found)]);

        Assert.DoesNotContain("submission", verdict.Json(), StringComparison.Ordinal);

        var named = verdict with { Submission = new QuestSubmissionRef("desk", 41) };
        using var body = JsonDocument.Parse(named.Json());
        var submission = body.RootElement.GetProperty("submission");
        Assert.Equal(("desk", 41L), (submission.GetProperty("machine").GetString(), submission.GetProperty("sequence").GetInt64()));

        // As a quest answers it back: the submission it was read for, read as written.
        using var quest = JsonDocument.Parse($$"""{ "evidence": {{named.Json()}} }""");
        Assert.Equal(new QuestSubmissionRef("desk", 41), EvidenceVerdict.Read(quest.RootElement)!.Submission);
    }

    /// <summary>
    /// FABRIC-CLOSE1c3: the person's reopen names the submission by its machine and sequence, with their words written only when given;
    /// the go-on posts nothing more; each says the service's sentence, a refusal included.
    /// </summary>
    [Fact]
    public async Task The_reopen_names_its_submission_and_the_go_on_says_the_services_words()
    {
        var replies = new Queue<ServiceReply>(
        [
            new(200, """{ "quest": { "id": "q7", "status": "Taken" }, "message": "Reopened." }"""),
            new(200, """{ "quest": { "id": "q7", "status": "Taken" }, "message": "Reopened." }"""),
            new(409, """{ "error": "Quest `#q8` grew from `#q7`, whose work has no unheld submission here yet." }"""),
        ]);
        var doors = new Doors((_, _, _) => replies.Dequeue());
        using var client = new ServiceClient(doors);

        var reopened = await client.ReopenQuestAsync("#q7", new QuestSubmissionRef("desk", 41), "It seams.");
        await client.ReopenQuestAsync("q7", new QuestSubmissionRef("desk", 41), "  ");
        var refused = await client.ContinueQuestAsync("q8");

        Assert.Equal((true, "Reopened."), reopened);
        Assert.Equal((false, "Quest `#q8` grew from `#q7`, whose work has no unheld submission here yet."), refused);
        Assert.Equal(
            [(ServiceVerb.Post, "/api/quests/q7/reopen"), (ServiceVerb.Post, "/api/quests/q7/reopen"), (ServiceVerb.Post, "/api/quests/q8/continue")],
            doors.Asked.Select(asked => (asked.Verb, asked.Path)));
        using var named = JsonDocument.Parse(doors.Asked[0].Body!);
        var submission = named.RootElement.GetProperty("submission");
        Assert.Equal(("desk", 41L, "It seams."), (submission.GetProperty("machine").GetString(), submission.GetProperty("sequence").GetInt64(),
            named.RootElement.GetProperty("note").GetString()));
        using var bare = JsonDocument.Parse(doors.Asked[1].Body!);
        Assert.False(bare.RootElement.TryGetProperty("note", out _));
    }

    [Fact]
    public async Task The_close_sends_its_key_token_and_quests_and_reads_each_answer_by_its_code()
    {
        var replies = new Queue<ServiceReply>(
        [
            new(200, """{ "quests": [{ "id": "q6", "from": "a", "to": "engine", "title": "t", "body": "", "status": "Done" }], "message": "Closed #q6." }"""),
            new(409, """{ "error": "Key `k-1` was sent with another close already.", "code": "key-reused" }"""),
            new(409, """{ "error": "This close was refused when it was first sent with key `k-1`: quest `#q6`'s submission was held.", "code": "held" }"""),
        ]);
        var doors = new Doors((_, _, _) => replies.Dequeue());
        using var client = new ServiceClient(doors);
        IReadOnlyList<QuestCloseItem> named = [new("q6", "desk", 40), new("q7", "desk", 41)];

        var closed = await client.CloseQuestsAsync("q7", "k-1", "ctk-1", named);
        var reused = await client.CloseQuestsAsync("q7", "k-1", "ctk-2", named);
        var replayed = await client.CloseQuestsAsync("q7", "k-1", "ctk-1", named);

        Assert.Equal((QuestCloseOutcomes.Closed, null, "Closed #q6."), (closed.Outcome, closed.Code, closed.Message));
        Assert.Equal("Done", Assert.Single(closed.Quests).Status);
        Assert.Equal((QuestCloseOutcomes.Refused, "key-reused"), (reused.Outcome, reused.Code));
        Assert.Equal((QuestCloseOutcomes.Refused, "held"), (replayed.Outcome, replayed.Code));
        Assert.StartsWith("This close was refused when it was first sent", replayed.Message, StringComparison.Ordinal);

        var (verb, path, json) = doors.Asked[0];
        Assert.Equal((ServiceVerb.Post, "/api/quests/q7/close"), (verb, path));
        using var body = JsonDocument.Parse(json!);
        Assert.Equal(("k-1", "ctk-1"), (body.RootElement.GetProperty("key").GetString(), body.RootElement.GetProperty("token").GetString()));
        Assert.Equal(
            ["q6/desk/40", "q7/desk/41"],
            body.RootElement.GetProperty("quests").EnumerateArray()
                .Select(q => $"{q.GetProperty("id").GetString()}/{q.GetProperty("machine").GetString()}/{q.GetProperty("sequence").GetInt64()}"));
    }

    /// <summary>
    /// FABRIC-CLOSE1v: the door's scope checks, a quest of another repository, of no chain the close's quest grew from, or bound to another
    /// graph, are its own coded refusals, so each settles the close as refused, whether sent or asked after, and it is not sent again.
    /// </summary>
    [Theory]
    [InlineData("other-repository")]
    [InlineData("not-in-chain")]
    [InlineData("other-graph")]
    public async Task A_scope_refusal_of_the_door_settles_the_close_as_refused(string code)
    {
        var doors = new Doors((_, _, _) => new ServiceReply(409, $$"""{ "error": "Quest `#q6` is refused.", "code": "{{code}}" }"""));
        using var client = new ServiceClient(doors);
        IReadOnlyList<QuestCloseItem> named = [new("q6", "desk", 40), new("q7", "desk", 41)];

        var refused = await client.CloseQuestsAsync("q7", "k-1", "ctk-1", named);
        var asked = await client.CloseOutcomeAsync("q7", "k-1", "ctk-1", named);

        Assert.Equal((QuestCloseOutcomes.Refused, code, "Quest `#q6` is refused."), (refused.Outcome, refused.Code, refused.Message));
        Assert.Equal((QuestCloseOutcomes.Refused, code), (asked.Outcome, asked.Code));
        Assert.Equal(2, doors.Asked.Count);
    }

    /// <summary>
    /// FABRIC-CLOSE1u: what became of a close is asked at its own door with the close as it was sent, and read as a close is: its first outcome,
    /// or refused <c>withdrawn</c> where the door never took the key, which the close door says too of a send of that key arriving after. A
    /// host before the question answers it as no door of its own, so the close stays unknown.
    /// </summary>
    [Fact]
    public async Task What_became_of_a_close_is_asked_with_its_key_and_read_as_a_close_is()
    {
        var replies = new Queue<ServiceReply>(
        [
            new(200, """{ "quests": [{ "id": "q7", "from": "a", "to": "engine", "title": "t", "body": "", "status": "Done" }], "message": "This close was made already." }"""),
            new(409, """{ "error": "Key `k-2` was never taken by this door, and is withdrawn.", "code": "withdrawn" }"""),
            new(409, """{ "error": "This close was refused when it was first sent with key `k-2`: its key was withdrawn.", "code": "withdrawn" }"""),
            new(404, ""),
        ]);
        var doors = new Doors((_, _, _) => replies.Dequeue());
        using var client = new ServiceClient(doors);
        IReadOnlyList<QuestCloseItem> named = [new("q7", "desk", 41)];

        var closed = await client.CloseOutcomeAsync("q7", "k-1", "ctk-1", named);
        var withdrawn = await client.CloseOutcomeAsync("q7", "k-2", "ctk-1", named);
        var late = await client.CloseQuestsAsync("q7", "k-2", "ctk-1", named);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.CloseOutcomeAsync("q7", "k-3", "ctk-1", named));

        Assert.Equal((QuestCloseOutcomes.Closed, null), (closed.Outcome, closed.Code));
        Assert.Equal((QuestCloseOutcomes.Refused, QuestCloseOutcomes.Withdrawn), (withdrawn.Outcome, withdrawn.Code));
        Assert.Equal((QuestCloseOutcomes.Refused, QuestCloseOutcomes.Withdrawn), (late.Outcome, late.Code));
        var (verb, path, json) = doors.Asked[0];
        Assert.Equal((ServiceVerb.Post, "/api/quests/q7/close/outcome"), (verb, path));
        using var body = JsonDocument.Parse(json!);
        Assert.Equal(("k-1", "ctk-1", "q7/desk/41"), (body.RootElement.GetProperty("key").GetString(), body.RootElement.GetProperty("token").GetString(),
            string.Join(",", body.RootElement.GetProperty("quests").EnumerateArray()
                .Select(q => $"{q.GetProperty("id").GetString()}/{q.GetProperty("machine").GetString()}/{q.GetProperty("sequence").GetInt64()}"))));
    }

    /// <summary>
    /// FABRIC-CLOSE1l, the review's finding 7: an answer that is not the close door's own coded refusal proves nothing about whether its
    /// transaction committed (a proxy's 503 page, the host's 500, a bare 404 from a host restarting, a 409 naming no check), so the close is
    /// unknown, thrown as the host not answering, and the closer sends the same close again.
    /// </summary>
    [Theory]
    [InlineData(503, "<html><body>Service Unavailable</body></html>")]
    [InlineData(500, """{ "error": "An error occurred while saving the entity changes." }""")]
    [InlineData(404, "Not Found")]
    [InlineData(409, """{ "error": "Stand down." }""")]
    [InlineData(200, "<html></html>")]
    public async Task An_answer_that_is_not_the_doors_own_coded_refusal_leaves_the_close_unknown(int status, string payload)
    {
        using var client = new ServiceClient(new Doors((_, _, _) => new ServiceReply(status, payload)));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.CloseQuestsAsync("q7", "k-1", "ctk-1", [new("q7", "desk", 41)]));
    }

    [Fact]
    public async Task The_bind_names_the_graph_and_says_a_refusal_and_an_older_host()
    {
        var replies = new Queue<ServiceReply>(
        [
            new(200, """{ "quest": { "id": "q7" }, "message": "Quest `#q7` closes through graph `gph-1`." }"""),
            new(409, """{ "error": "Quest `#q7` closes through graph `gph-0`, bound at its start." }"""),
            new(404, "Not Found"),
        ]);
        var doors = new Doors((_, _, _) => replies.Dequeue());
        using var client = new ServiceClient(doors);

        var bound = await client.BindQuestAsync("#q7", "gph-1", "ses-q7");
        var refused = await client.BindQuestAsync("q7", "gph-1", "ses-q7");
        var older = await client.BindQuestAsync("q7", "gph-1", "ses-q7");

        Assert.Equal((true, "Quest `#q7` closes through graph `gph-1`."), bound);
        Assert.Equal((false, "Quest `#q7` closes through graph `gph-0`, bound at its start."), refused);
        Assert.False(older.Ok);
        Assert.Contains("no bind door", older.Message, StringComparison.Ordinal);
        Assert.Equal("/api/quests/q7/bind", doors.Asked[0].Path);
        Assert.Equal("gph-1", JsonDocument.Parse(doors.Asked[0].Body!).RootElement.GetProperty("graph").GetString());
        // FABRIC-CLOSE1l: the driver binds only at the take, naming the graph for it as the first session opens.
        Assert.True(JsonDocument.Parse(doors.Asked[0].Body!).RootElement.GetProperty("atTake").GetBoolean());
        // FABRIC-CLOSE1o: for the session opening, whose take alone binds it.
        Assert.Equal("ses-q7", JsonDocument.Parse(doors.Asked[0].Body!).RootElement.GetProperty("session").GetString());
    }

    [Theory]
    [InlineData("""{ "semantic": false, "tier": "lexical only", "closure": true, "closeOutcome": true }""", true)]
    // CLOSECAP1: a host from before the outcome door answers closure alone, and binds nothing.
    [InlineData("""{ "semantic": false, "tier": "lexical only", "closure": true }""", false)]
    [InlineData("""{ "semantic": false, "tier": "lexical only", "closure": true, "closeOutcome": false }""", false)]
    [InlineData("""{ "semantic": false, "tier": "lexical only", "closeOutcome": true }""", false)]
    [InlineData("""{ "semantic": false, "tier": "lexical only" }""", false)]
    [InlineData("""{ "semantic": false, "tier": "lexical only", "closure": false }""", false)]
    public async Task The_handshake_reads_whether_the_host_knows_a_closure_graph(string status, bool knows)
    {
        using var client = new ServiceClient(new Doors((_, path, _) => path == "/api/status" ? new ServiceReply(200, status) : new ServiceReply(404, "")));

        Assert.Equal(knows, await client.KnowsClosureAsync());
    }
}
