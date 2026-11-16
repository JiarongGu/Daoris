using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A role's answers at the person's accept-step doors: a session's staged memory kept, a memory end decided, a departure accepted, each
/// held for the role's window with <i>Overrule</i>, its file naming the role and its agent, then posted to its door as the role's. Without
/// the role's grant the same answer is refused and nothing is posted.
/// </summary>
public sealed class SupervisorDoorAnswerTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-door", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-16T10:00:00Z");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    /// <summary>A role <c>daoris-landing</c> holding exactly <paramref name="granted"/>, as the admission reads its catalogue.</summary>
    private sealed class Holds(params string[] granted) : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            act is null || granted.Contains(SupervisorKinds.Qualified(kind, act))
                ? new("g-1", null) { Window = TimeSpan.FromMinutes(10), Role = "daoris-landing" }
                : DecisionAdmitted.Refused(SupervisorWhy.NotGranted);
    }

    private static readonly SupervisorHolder Codex = SupervisorHolders.OfAgent(new("daoris-landing", 3, "codex-acp", "gpt-5", "high", "gpt-5", "read-only", "t1"));

    private static SupervisorDoorAnswer Staged(string choice = SupervisorDoorActs.Keep) => new(
        new SupervisorSubject(SupervisorKinds.Memory, "s1", SupervisorDoorActs.Staged), "reports", "g-1", "keep", choice,
        [new SupervisorReason("The staged rows restate the area.", "the session's memory")], Codex) { Pin = "op-1,op-2" };

    private static SupervisorDoorAnswer Departure() => new(
        new SupervisorSubject(SupervisorKinds.Departure, "s1", "q7"), "reports", "g-1", SupervisorDoorActs.Accept, null,
        [new SupervisorReason("The departure is the brief's own fallback.", "the quest's answers")], Codex) { Pin = "quest:1" };

    /// <summary>A release whose subject reads <paramref name="pin"/> now, whose door answers <paramref name="why"/>, and whose service shows <paramref name="made"/>.</summary>
    private static DoorRelease Posting(List<SupervisorDoorDue> posted, string? why = null, Func<SupervisorDoorDue, string?>? pin = null, bool? made = true) => new(
        (due, _) => Task.FromResult((pin ?? (each => each.Pin))(due) == due.Pin ? null : SupervisorWhy.SubjectChanged),
        (due, _) =>
        {
            posted.Add(due);
            return Task.FromResult(why);
        },
        (_, _) => Task.FromResult(made));

    [Fact]
    public async Task A_role_holding_memory_keep_keeps_a_sessions_staged_memory_after_its_window_and_the_record_names_the_role_and_its_agent()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep"));
        var kept = await decisions.DoorAsync(Staged(), null, At);

        Assert.True(kept.Kept, kept.Why);
        Assert.Equal(At.AddMinutes(10), kept.GoesOnAt);
        var file = File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".json"));
        Assert.Contains("\"role\": \"daoris-landing\"", file);
        Assert.Contains("\"agent\": \"codex-acp \\u00B7 gpt-5 \\u00B7 high\"", file);
        Assert.False(decisions.RefersDoor(new SupervisorSubject(SupervisorKinds.Memory, "s1", SupervisorDoorActs.Staged)));

        var posted = new List<SupervisorDoorDue>();
        Assert.Empty(await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(5)));
        Assert.Empty(posted);
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        var due = Assert.Single(posted);
        Assert.Equal(("daoris-landing", "codex-acp · gpt-5 · high", true), (due.Role, due.Agent, due.Yes));
    }

    /// <summary>🔴 The page reads a session's held answer, its subject, act, choice, role, agent and moment, to offer <i>Overrule</i> on it; an overrule says so.</summary>
    [Fact]
    public async Task A_sessions_held_door_answer_is_read_for_the_page_with_its_role_its_agent_and_its_moment()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep", "departure.accept"));
        var kept = await decisions.DoorAsync(Staged(SupervisorDoorActs.Drop), null, At);
        await decisions.DoorAsync(Departure(), null, At.AddMinutes(1));

        var read = decisions.DoorsOf("s1");

        Assert.Equal(2, read.Count);
        var staged = read[0];
        Assert.Equal((kept.Decision, SupervisorKinds.Memory, SupervisorDoorActs.Staged, "keep", SupervisorDoorActs.Drop), (staged.Id, staged.Kind, staged.Subject, staged.Do, staged.Choice));
        Assert.Equal(("daoris-landing", "codex-acp · gpt-5 · high", SupervisorState.Held, At, At.AddMinutes(10)), (staged.Role, staged.Agent, staged.State, staged.KeptAt, staged.GoesOnAt));
        Assert.Equal((SupervisorKinds.Departure, "q7", SupervisorDoorActs.Accept), (read[1].Kind, read[1].Subject, read[1].Do));
        Assert.Empty(decisions.DoorsOf("s2"));

        await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(2));
        Assert.Equal(SupervisorState.Overruled, decisions.DoorsOf("s1")[0].State);
    }

    [Fact]
    public async Task The_same_keep_without_the_role_is_refused_and_nothing_is_posted()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("landing.accept"));
        var refused = await decisions.DoorAsync(Staged(), null, At);

        Assert.False(refused.Kept);
        Assert.Equal(SupervisorWhy.NotGranted, refused.Why);
        var posted = new List<SupervisorDoorDue>();
        Assert.Empty(await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Empty(posted);
        Assert.Equal(SupervisorWhy.NotOffered, (await new QuestionSupervisorDecisions(_home, new Holds("memory.keep")).DoorAsync(Staged("approve"), null, At)).Why);
    }

    [Fact]
    public async Task The_owners_overrule_inside_the_window_undoes_a_keep_and_the_subject_is_theirs()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep"));
        var kept = await decisions.DoorAsync(Staged(SupervisorDoorActs.Drop), null, At);

        Assert.Equal(SupervisorState.Overruled, await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(1)));
        var posted = new List<SupervisorDoorDue>();
        Assert.Empty(await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Empty(posted);
        Assert.Equal(SupervisorWhy.Overruled, (await decisions.DoorAsync(Staged(), null, At.AddMinutes(12))).Why);
        Assert.False(decisions.RefersDoor(new SupervisorSubject(SupervisorKinds.Memory, "s1", SupervisorDoorActs.Staged)));
    }

    [Fact]
    public async Task A_role_holding_departure_accept_accepts_after_its_window_and_the_owners_overrule_or_a_missing_grant_stops_it()
    {
        var posted = new List<SupervisorDoorDue>();
        Assert.Equal(SupervisorWhy.NotGranted, (await new QuestionSupervisorDecisions(_home, new Holds("memory.keep")).DoorAsync(Departure(), null, At)).Why);

        var decisions = new QuestionSupervisorDecisions(_home, new Holds("departure.accept"));
        var kept = await decisions.DoorAsync(Departure(), null, At);
        Assert.True(kept.Kept, kept.Why);
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Equal(("q7", SupervisorDoorActs.Accept, "daoris-landing"), (posted.Single().Subject.Id, posted.Single().Do, posted.Single().Role));

        var overruled = new QuestionSupervisorDecisions(_home, new Holds("departure.accept"));
        var held = await overruled.DoorAsync(Departure() with { Subject = new SupervisorSubject(SupervisorKinds.Departure, "s2", "q8") }, null, At);
        Assert.Equal(SupervisorState.Overruled, await overruled.OverruleAsync("s2", held.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(2)));
        Assert.Empty(await overruled.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Single(posted);
    }

    [Fact]
    public async Task A_post_its_door_refuses_goes_stale_and_the_kinds_are_role_set_vocabulary()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep"));
        await decisions.DoorAsync(Staged(), null, At);

        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseDoorsAsync(Posting([], "running"), At.AddMinutes(11)));
        Assert.Contains("\"why\":\"running\"", States("s1"));
        Assert.Equal(["memory.keep", "memory.decide", "departure.accept"],
            SupervisorKinds.Acts.Where(act => act.StartsWith("memory.", StringComparison.Ordinal) || act.StartsWith("departure.", StringComparison.Ordinal)));
    }

    /// <summary>A role granted while <see cref="Granted"/> holds; the release asks it again.</summary>
    private sealed class Switch : IDecisionAdmission
    {
        public bool Granted { get; set; } = true;

        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            Granted ? new("g-1", null) { Window = TimeSpan.FromMinutes(10), Role = "daoris-landing" } : DecisionAdmitted.Refused(SupervisorWhy.NotGranted);
    }

    private string States(string session) => string.Concat(Directory.EnumerateFiles(
        DaorisHome.Entry(_home, HomeEntry.Sessions, session + QuestionSupervisorDecisions.DecisionsSuffix), "*.states*").Select(File.ReadAllText));

    /// <summary>🔴 A keep is tied to the staged rows it judged; a row staged since makes it stale at its release, nothing posted, and the subject the person's.</summary>
    [Fact]
    public async Task A_held_answer_whose_subject_changed_since_it_was_judged_goes_stale_and_posts_nothing()
    {
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep", "departure.accept"));
        await decisions.DoorAsync(Staged(), null, At);
        await decisions.DoorAsync(Departure(), null, At);
        var posted = new List<SupervisorDoorDue>();

        var moved = await decisions.ReleaseDoorsAsync(Posting(posted, pin: due => due.Pin + ",changed"), At.AddMinutes(11));

        Assert.Equal([SupervisorState.Stale, SupervisorState.Stale], moved);
        Assert.Empty(posted);
        Assert.Contains("\"why\":\"subject-changed\"", States("s1"));
        Assert.False(decisions.RefersDoor(Staged().Subject));
        // An answer pinned to nothing judged nothing that waits: it is refused, keeping nothing.
        Assert.Equal(SupervisorWhy.Answered, (await decisions.DoorAsync(Staged() with { Pin = null, Subject = new(SupervisorKinds.Memory, "s3", SupervisorDoorActs.Staged) }, null, At)).Why);
    }

    /// <summary>🔴 A grant taken away after the keep and before its moment refuses the release: nothing is posted.</summary>
    [Fact]
    public async Task A_grant_removed_after_the_keep_refuses_the_release()
    {
        var role = new Switch();
        var decisions = new QuestionSupervisorDecisions(_home, role);
        Assert.True((await decisions.DoorAsync(Staged(), null, At)).Kept);
        role.Granted = false;
        var posted = new List<SupervisorDoorDue>();

        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseDoorsAsync(Posting(posted), At.AddMinutes(11)));
        Assert.Empty(posted);
        Assert.Contains("\"why\":\"role-changed\"", States("s1"));
    }

    /// <summary>
    /// 🔴 A post a restart cut off after its <c>released</c> line is settled by what the service shows: applied, it is
    /// <c>went-on</c> and nothing is posted again; not applied, <c>stale</c> as restarted. A held one the restart finds goes stale.
    /// </summary>
    [Theory]
    [InlineData(true, SupervisorState.WentOn)]
    [InlineData(false, SupervisorState.Stale)]
    public async Task A_released_answer_a_restart_cut_off_is_settled_by_what_the_service_shows(bool made, SupervisorState reached)
    {
        var before = new QuestionSupervisorDecisions(_home, new Holds("memory.keep", "departure.accept"));
        await before.DoorAsync(Staged(), null, At);
        await before.DoorAsync(Departure() with { Subject = new SupervisorSubject(SupervisorKinds.Departure, "s2", "q8") }, null, At.AddMinutes(5));
        var cut = new DoorRelease((_, _) => Task.FromResult<string?>(null), (_, _) => throw new OperationCanceledException(), (_, _) => Task.FromResult<bool?>(null));
        await Assert.ThrowsAsync<OperationCanceledException>(() => before.ReleaseDoorsAsync(cut, At.AddMinutes(11)));
        Assert.Contains("\"state\":\"released\"", States("s1"));

        var after = new QuestionSupervisorDecisions(_home, new Holds("memory.keep", "departure.accept"));
        after.SweepAtStart(At.AddMinutes(12));
        var posted = new List<SupervisorDoorDue>();

        Assert.Equal([reached], await after.ReleaseDoorsAsync(Posting(posted, made: made), At.AddMinutes(13)));
        Assert.Empty(posted);
        Assert.Equal(reached, Assert.Single(after.DoorsOf("s1")).State);
        Assert.Contains("\"why\":\"restarted\"", States("s2"));
        Assert.Empty(after.DoorsOf("s2"));
        Assert.False(after.RefersDoor(new SupervisorSubject(SupervisorKinds.Departure, "s2", "q8")));
    }

    /// <summary>
    /// 🔴 The release posts a keep and an approve as a yes and a drop and a decline as a no, with the role, its agent and its turn; a keep
    /// or a drop names the staged rows its pin holds, so the service moves those and no others.
    /// </summary>
    [Theory]
    [InlineData(SupervisorDoorActs.Staged, SupervisorDoorActs.Keep, "/api/sessions/s1/memory", "\"keep\":true")]
    [InlineData(SupervisorDoorActs.Staged, SupervisorDoorActs.Drop, "/api/sessions/s1/memory", "\"keep\":false")]
    [InlineData("op-9", SupervisorDoorActs.Approve, "/api/memory/op-9/decide", "\"approve\":true")]
    [InlineData("op-9", SupervisorDoorActs.Decline, "/api/memory/op-9/decide", "\"approve\":false")]
    public async Task The_release_posts_a_keep_as_a_yes_and_a_drop_or_decline_as_a_no(string id, string choice, string path, string said)
    {
        var doors = new Doors();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(doors));
        var due = new SupervisorDoorDue("d1", new SupervisorSubject(SupervisorKinds.Memory, "s1", id), "reports", "keep", choice, "daoris-landing", "codex-acp")
        {
            Turn = "t-1", Pin = id == SupervisorDoorActs.Staged ? "op-1,op-2" : id,
        };

        Assert.Null(await DoorReleaseLook.PostAsync(service, due, default));

        var (posted, body) = Assert.Single(doors.Posted);
        Assert.Equal(path, posted);
        Assert.Contains(said, body);
        Assert.Contains("\"role\":{\"role\":\"daoris-landing\",\"agent\":\"codex-acp\",\"decision\":\"d1\",\"session\":\"t-1\"}", body);
        Assert.Equal(id == SupervisorDoorActs.Staged, body.Contains("\"ops\":[\"op-1\",\"op-2\"]", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 The release's own reads of the service: the staged rows read again against the pin the keep judged, then the keep posted naming
    /// exactly those rows; a row staged since makes the pin another, and nothing is posted.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_release_reads_the_staged_rows_again_and_keeps_only_the_rows_it_judged(bool stagedSince)
    {
        var doors = new Doors();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(doors));
        var decisions = new QuestionSupervisorDecisions(_home, new Holds("memory.keep"));
        var pin = await SupervisorDoorSubjects.PinAsync(service, Staged().Subject, default);
        Assert.True((await decisions.DoorAsync(Staged() with { Pin = pin }, null, At)).Kept);
        if (stagedSince) doors.Staged += """,{"id":"op-0","state":"staged","change":{"kind":"write","text":"Staged since."}}""";

        var moved = await decisions.ReleaseDoorsAsync(DoorReleaseLook.Release(service), At.AddMinutes(11));

        if (stagedSince)
        {
            Assert.Equal([SupervisorState.Stale], moved);
            Assert.Empty(doors.Posted);
            Assert.Contains("\"why\":\"subject-changed\"", States("s1"));
            return;
        }

        Assert.Equal([SupervisorState.WentOn], moved);
        var (path, body) = Assert.Single(doors.Posted);
        Assert.Equal(("/api/sessions/s1/memory", "op-1"), (path, pin));
        Assert.Contains("\"ops\":[\"op-1\"]", body);
    }

    /// <summary>
    /// A service holding session s1's page and four quests: q7 held by its departure, q6 by a departure beside a met requirement's evidence
    /// awaited, q8 by its unread evidence, q9 by a reopened source. What is posted to it is kept in <see cref="Posted"/>.
    /// </summary>
    internal sealed class Doors : HttpMessageHandler
    {
        public string Staged { get; set; } = """{"id":"op-1","state":"staged","change":{"kind":"write","text":"Keeper keeps a garden."}}""";

        public List<(string Path, string Body)> Posted { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            static string Quest(string id, string hold, bool awaits = false) => $$"""
                {"id":"{{id}}","from":"ask #a1","to":"reports","title":"Build the weekly report","body":"Through the bridge.","status":"Done","held":true,"hold":"{{hold}}",
                 "awaitsEvidence":{{(awaits ? "true" : "false")}},"requirements":[{"quote":"using the common-report","check":"It is one."}],
                 "answers":[{"requirement":1,"departed":"A type of its own.","quote":"using the common-report"}],"updated":"2026-11-16T09:00:00Z"}
                """;
            if (request.Method == HttpMethod.Post)
            {
                Posted.Add((request.RequestUri!.AbsolutePath, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"message":"kept","moved":1}""", System.Text.Encoding.UTF8, "application/json"),
                };
            }

            var body = request.RequestUri!.AbsolutePath switch
            {
                "/api/sessions/s1/memory" => $$$"""
                    {"ops":[{{{Staged}}},
                     {"id":"op-9","state":"awaiting","change":{"kind":"end","target":"e-4","revision":3,"text":"The area moved to the driving page."},"by":{"session":"s0","role":"builder"}}]}
                    """,
                "/api/quests" => $"[{Quest("q7", "departed")},{Quest("q6", "departed", awaits: true)},{Quest("q8", "evidence-unread")},{Quest("q9", "source-reopened")}]",
                _ => null,
            };
            return new HttpResponseMessage(body is null ? System.Net.HttpStatusCode.NotFound : System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body ?? "{}", System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    /// <summary>
    /// 🔴 What a role judges is Daoris's own record of the subject: the staged rows and their texts, the end's target, reason
    /// and proposer, the departure beside what the quest required; pinned as it stands, so a row staged since is another pin. A quest held
    /// for its evidence or a reopened source, or departed while another requirement's evidence is awaited, has no facts and no pin: it is
    /// never a role's.
    /// </summary>
    [Fact]
    public async Task A_door_subjects_facts_and_pin_are_what_the_service_holds_and_a_departure_alone_is_a_roles()
    {
        var doors = new Doors();
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(doors));
        SupervisorSubject Of(string kind, string id) => new(kind, "s1", id);

        var staged = await SupervisorDoorSubjects.FactsAsync(service, Of(SupervisorKinds.Memory, SupervisorDoorActs.Staged), default);
        var end = await SupervisorDoorSubjects.FactsAsync(service, Of(SupervisorKinds.Memory, "op-9"), default);
        var departure = await SupervisorDoorSubjects.FactsAsync(service, Of(SupervisorKinds.Departure, "q7"), default);

        Assert.Contains("Keeper keeps a garden.", staged!.Parts.Single().Text);
        Assert.Contains("op-9: end of entry e-4", end!.Parts[0].Text);
        Assert.Contains("proposed by session: s0, role builder", end.Parts[0].Text);
        Assert.Equal("The area moved to the driving page.", end.Parts[1].Text);
        Assert.Contains("\"using the common-report\": checked by It is one.", departure!.Parts[0].Text);
        Assert.Contains("departed from \"using the common-report\": A type of its own.", departure.Parts[2].Text);
        foreach (var held in new[] { "q6", "q8", "q9" })
        {
            Assert.Null(await SupervisorDoorSubjects.FactsAsync(service, Of(SupervisorKinds.Departure, held), default));
            Assert.Null(await SupervisorDoorSubjects.PinAsync(service, Of(SupervisorKinds.Departure, held), default));
        }

        Assert.Equal("op-1", await SupervisorDoorSubjects.PinAsync(service, Of(SupervisorKinds.Memory, SupervisorDoorActs.Staged), default));
        Assert.Equal("op-9", await SupervisorDoorSubjects.PinAsync(service, Of(SupervisorKinds.Memory, "op-9"), default));
        Assert.StartsWith("quest:", await SupervisorDoorSubjects.PinAsync(service, Of(SupervisorKinds.Departure, "q7"), default));
        doors.Staged += """,{"id":"op-0","state":"staged","change":{"kind":"write","text":"Staged since."}}""";
        Assert.Equal("op-0,op-1", await SupervisorDoorSubjects.PinAsync(service, Of(SupervisorKinds.Memory, SupervisorDoorActs.Staged), default));
        Assert.Null(await SupervisorDoorSubjects.PinAsync(service, Of(SupervisorKinds.Memory, "op-8"), default));
    }
}
