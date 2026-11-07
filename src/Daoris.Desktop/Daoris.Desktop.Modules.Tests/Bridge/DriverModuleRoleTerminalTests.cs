using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// D233 point 2 (the supervisor design §2.2, §3.1, §5) over the bridge: the window's start of a role's terminal, for a role the catalogue
/// staffs with the terminal, starts the holder's terminal with the key in its environment and answers no key, nor does <c>STATE</c>; a
/// role the seed holds otherwise starts nothing; the holder's answer is refused on every door but a covered terminal's, and kept as a
/// held decision on one; the listing names what waits with its generation; and a role's change makes a held decision stale after the
/// judge's step.
/// </summary>
public sealed class DriverModuleRoleTerminalTests : DriverModuleBridge
{
    private const string Supervisor = "supervisor-engine";

    private const string Records =
        """[{"id":"s1","repository":"engine","state":"awaiting-person","quest":"q1","kind":"driven","created":"2026-10-22T08:00:00Z"}]""";

    /// <summary>The records, the quest on its ask, the ask with no go-ahead open, and the take-back door, each word it took kept.</summary>
    private sealed class Service(List<string> taken) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/take-back", StringComparison.Ordinal))
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                lock (taken) taken.Add(body.RootElement.GetProperty("word").GetString()!);
                return Json("""{"done":true,"message":"taken back"}""");
            }

            return path switch
            {
                "/api/sessions" => Json(Records),
                "/api/quests" => Json("""[{"id":"q1","from":"ask #a1","to":"engine","title":"t","body":"b","status":"taken"}]"""),
                "/api/asks/a1" => Json("""{"id":"a1","workspace":"w","sentence":"s","state":"open","tier":"t","goAheads":[]}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") },
            };
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private void Asked(string session, string? owner = null)
    {
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Sessions));
        var mark = owner is null ? "null" : $"\"{owner}\"";
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Sessions, session + QuestionFiles.Suffix), $$"""
            {"id":"q-1","session":"s1","question":"Which report?","choices":[{"label":"Monthly","does":"I write it."},{"label":"Weekly","does":"I write that."}],"recommend":1,"owner":{{mark}},"at":"2026-10-24T08:00:00Z"}
            """);
    }

    private (DriverLoop Loop, DriverModule Module, IMessageDispatcher Dispatcher, ServiceProvider Services) Compose()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.AddSingleton<IIpcModule>(module);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        return (loop, module, provider.GetRequiredService<IMessageDispatcher>(), provider);
    }

    /// <summary>A judge whose catalogue staffs the repository's own supervisor with the terminal.</summary>
    private StandingConfirmation Judge() =>
        new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
            .Staff(RoleKit.Supervisor(Supervisor, RoleKit.Repository("engine")));

    [Fact]
    public async Task The_window_starts_the_roles_terminal_and_no_answer_the_page_receives_holds_the_key()
    {
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        using var pipe = new InstancePipe(dispatcher, Bus, Home, standing: Judge());
        loop.Pipe = pipe;
        var started = new List<string>();
        SupervisorDoor.Starter = open =>
        {
            started.Add(open.Key);
            return Task.FromResult(StandingConfirmationTests.Opened(open));
        };
        try
        {
            var start = await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = Supervisor });
            Assert.True(start.GetProperty("done").GetBoolean(), start.GetRawText());

            // The panel is a stand-in here: no shell, console or window starts (HolderTerminalTests holds the tab itself).
            var key = Assert.Single(started);
            Assert.NotNull(HolderKey.Read(key));

            // No answer the page receives holds the key or its hash; STATE says the hold, never its generation.
            var hash = Convert.ToHexStringLower(HolderKey.Hash(key)!);
            var state = await AnswerAsync(module, "STATE");
            foreach (var said in new[] { start.GetRawText(), state.GetRawText() })
            {
                Assert.DoesNotContain(key!, said, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(hash, said, StringComparison.OrdinalIgnoreCase);
            }

            var row = Assert.Single(state.GetProperty("standingConfirmation").GetProperty("holds").EnumerateArray());
            Assert.Equal((Supervisor, AppointedRoles.Supervisor), (row.GetProperty("role").GetString(), row.GetProperty("kind").GetString()));
            Assert.False(row.TryGetProperty("generation", out _));

            var unknown = await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = "elsewhere" });
            Assert.Equal(StandingConfirmation.UnknownRole, unknown.GetProperty("why").GetString());
            var rules = await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = AppointedRoles.Supervisor });
            Assert.Equal(StandingConfirmation.NotTerminal, rules.GetProperty("why").GetString());
            Assert.Single(started);

            SupervisorDoor.Starter = _ => Task.FromResult(new HolderOpened(null, null, StandingConfirmation.Unstarted, null));
            // A holder lives: the next start names a terminal of its own (D209 §2.2).
            var unchosen = await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = Supervisor });
            Assert.Equal((false, StandingConfirmation.ChooseTerminal), (unchosen.GetProperty("done").GetBoolean(), unchosen.GetProperty("why").GetString()));
            var unstarted = await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = Supervisor, share = "own" });
            Assert.Equal((false, "unstarted"), (unstarted.GetProperty("done").GetBoolean(), unstarted.GetProperty("why").GetString()));
            // The hold in force is the one before: nothing was installed.
            Assert.Null(pipe.StandingConfirmation.Admit("engine").Why);

            // The role door's change ends it: your rules alone hold the repository's supervisor.
            pipe.StandingConfirmation.Staff(RoleKit.Supervisor(Supervisor, RoleKit.Repository("engine"), RoleHolders.Rules));
            await SupervisorDoor.RolesChangedAsync(loop, CancellationToken.None);
            Assert.Empty((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("holds").EnumerateArray());
        }
        finally
        {
            SupervisorDoor.Starter = null;
        }
    }

    /// <summary>STEWARDROLE1a-i (D209 §3.3): <c>ROLE_ACT</c>'s own handler, off the role's door, is refused <c>not-holder</c> and runs nothing.</summary>
    [Fact]
    public async Task A_role_act_off_the_role_s_door_is_refused()
    {
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        using var pipe = new InstancePipe(dispatcher, Bus, Home, standing: Judge());
        loop.Pipe = pipe;
        var payload = new { role = "steward", act = "drive", generation = "g", form = new { repository = "engine" }, proof = new string('0', 64) };

        var refused = await AnswerAsync(module, "ROLE_ACT", payload);

        Assert.Equal((false, SupervisorWhy.NotHolder), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("why").GetString()));
        Assert.Equal(RolesBrief.Revision, refused.GetProperty("brief").GetString());
    }

    /// <summary>
    /// STEWARDROLE1b (D209 §3.3): <c>STEWARD_ACTS</c>, the steward's verbs' open read, answers from the judge's memory alone; with no
    /// steward's terminal started it names no live generation and no act, and with no pipe neither.
    /// </summary>
    [Fact]
    public async Task The_steward_s_acts_read_names_no_generation_and_no_act_while_no_terminal_holds_it()
    {
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        var bare = await AnswerAsync(module, "STEWARD_ACTS");
        Assert.Equal((JsonValueKind.Null, 0), (bare.GetProperty("generation").ValueKind, bare.GetProperty("acts").GetArrayLength()));

        using var pipe = new InstancePipe(dispatcher, Bus, Home, standing: Judge());
        loop.Pipe = pipe;
        var read = await AnswerAsync(module, "STEWARD_ACTS");
        Assert.Equal((JsonValueKind.Null, 0), (read.GetProperty("generation").ValueKind, read.GetProperty("acts").GetArrayLength()));
    }

    /// <summary>
    /// STEWARDROLE1c (D209 §6.2): the desk's routes over the bridge. While no holder holds the steward, a request is refused
    /// <c>not-staffed</c> and the desk reads empty; once the catalogue staffs it, a request is kept, read back by <c>STEWARD_DESK</c> and by
    /// <c>STATE</c>'s <c>stewardDesk</c> with its words as the caller wrote them, and never the caller's key.
    /// </summary>
    [Fact]
    public async Task The_desk_keeps_a_request_only_where_the_steward_is_staffed_and_reads_it_back()
    {
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        var request = new { act = "drive", target = "engine", why = "the next row is engine's" };
        Assert.Equal((false, DeskWhy.NotStaffed), Filed(await AnswerAsync(module, "STEWARD_REQUEST", request)));

        using (var unstaffed = new InstancePipe(dispatcher, Bus, Home, standing: Judge()))
        {
            loop.Pipe = unstaffed;
            Assert.Equal((false, DeskWhy.NotStaffed), Filed(await AnswerAsync(module, "STEWARD_REQUEST", request)));
            Assert.Equal(0, (await AnswerAsync(module, "STEWARD_DESK")).GetProperty("requests").GetArrayLength());
            Assert.Empty((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("stewardDesk").EnumerateArray());
        }

        var judge = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside),
            walk: new StandingConfirmationTests.Walked(), desk: new DeskCaller(_ => null, new SystemProcesses(), TimeProvider.System, Environment.ProcessId))
            .Staff(RoleKit.Steward([.. StewardActs.Table.Keys]));
        using var pipe = new InstancePipe(dispatcher, Bus, Home, standing: judge);
        loop.Pipe = pipe;
        var filed = await AnswerAsync(module, "STEWARD_REQUEST", request);
        Assert.True(filed.GetProperty("filed").GetBoolean(), filed.GetRawText());

        var desk = Assert.Single((await AnswerAsync(module, "STEWARD_DESK")).GetProperty("requests").EnumerateArray());
        var shown = Assert.Single((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("stewardDesk").EnumerateArray());
        foreach (var each in new[] { desk, shown })
        {
            Assert.Equal((filed.GetProperty("request").GetString(), "drive", "engine", "the next row is engine's", "open"),
                (each.GetProperty("id").GetString(), each.GetProperty("act").GetString(), each.GetProperty("target").GetString(),
                    each.GetProperty("why").GetString(), each.GetProperty("state").GetString()));
            Assert.False(each.TryGetProperty("caller", out _));
        }

        static (bool, string?) Filed(JsonElement answer) => (answer.GetProperty("filed").GetBoolean(), answer.GetProperty("why").GetString());
    }

    /// <summary>The seed's catalogue staffs no terminal: the window's start is refused and nothing starts.</summary>
    [Fact]
    public async Task The_seeds_roles_start_no_terminal()
    {
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        using var pipe = new InstancePipe(dispatcher, Bus, Home);
        loop.Pipe = pipe;
        var started = 0;
        SupervisorDoor.Starter = _ =>
        {
            started++;
            return Task.FromResult(new HolderOpened(null, null, StandingConfirmation.Unstarted, null));
        };
        try
        {
            foreach (var role in AppointedRoles.Kinds)
            {
                var refused = await AnswerAsync(module, "START_ROLE_TERMINAL", new { role });
                Assert.Equal((false, StandingConfirmation.NotTerminal), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("why").GetString()));
            }

            Assert.Equal(0, started);
            Assert.Empty((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("holds").EnumerateArray());
        }
        finally
        {
            SupervisorDoor.Starter = null;
        }
    }

    /// <summary>
    /// §3.1: the page's door, or any door the role method did not cover, is refused <c>not-holder</c> and keeps nothing; over the pipe a
    /// covered terminal's answer is kept held, listed no more, and the role's change makes it stale after the judge's step.
    /// </summary>
    [Fact]
    public async Task The_answer_is_kept_only_from_a_covered_terminal_and_a_roles_change_sweeps_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        var taken = new List<string>();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Service(taken))));
        Asked("s1");
        var judge = Judge();
        using var pipe = new InstancePipe(dispatcher, Bus, Home, standing: judge);
        Assert.NotNull(pipe.Serve());
        loop.Pipe = pipe;
        string? key = null;
        SupervisorDoor.Starter = open =>
        {
            key = open.Key;
            return Task.FromResult(StandingConfirmationTests.Opened(open));
        };
        try
        {
            Assert.True((await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = Supervisor })).GetProperty("done").GetBoolean());
            var generation = judge.Admit("engine").Generation!;

            var waiting = (await AnswerAsync(module, "SUPERVISOR_WAITING")).GetProperty("waiting");
            var listed = Assert.Single(waiting.EnumerateArray());
            Assert.Equal(("s1", "q-1", generation), (listed.GetProperty("session").GetString(), listed.GetProperty("question").GetString(), listed.GetProperty("generation").GetString()));
            Assert.DoesNotContain(key!, waiting.GetRawText(), StringComparison.Ordinal);

            SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
            object Form(string nonce) => new SupervisorForm(
                "s1", "q-1", generation, SupervisorProof.Prove(key!, nonce, SupervisorProof.Canonical("s1", "q-1", generation, "Monthly", reasons))!,
                "Monthly", reasons).Payload();

            // The page's door: refused, nothing kept.
            var page = await AnswerAsync(module, "SUPERVISOR_ANSWER", Form(SupervisorProof.Nonce()));
            Assert.Equal((false, "not-holder"), (page.GetProperty("kept").GetBoolean(), page.GetProperty("why").GetString()));
            Assert.Empty(loop.Supervisor.LiveOf("s1"));

            await using var client = await PipeClient.ConnectAsync(pipe.Name!);
            var hello = await client.HelloAsync(HomeIdentity.Read(Home)!);
            var nonce = hello.GetProperty("data").GetProperty("nonce").GetString()!;
            var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, "SUPERVISOR_ANSWER", Form(nonce)));
            Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
            Assert.True(answer.GetProperty("data").GetProperty("kept").GetBoolean(), answer.GetRawText());
            var held = Assert.Single(loop.Supervisor.LiveOf("s1"));
            Assert.Equal((SupervisorState.Held, generation, "terminal"), (held.State, held.Generation, held.Holder.Kind));
            Assert.Empty((await AnswerAsync(module, "SUPERVISOR_WAITING")).GetProperty("waiting").EnumerateArray());

            judge.Staff(RoleKit.Supervisor(Supervisor, RoleKit.Repository("engine"), RoleHolders.Rules));
            await SupervisorDoor.RolesChangedAsync(loop, CancellationToken.None);
            var swept = Assert.Single(loop.Supervisor.LiveOf("s1"));
            Assert.Equal((SupervisorState.Stale, SupervisorWhy.Revoked), (swept.State, swept.Why));
        }
        finally
        {
            SupervisorDoor.Starter = null;
        }
    }

    /// <summary>
    /// SUPERVISOR1b (§3.1, §4.1): a question its agent marked the owner's is not listed for the holder, and a covered terminal's answer to
    /// it is refused <c>owner-question</c>, keeping nothing; the holder's defer on it is kept <c>left</c> over the same door, its bound on
    /// the row the page reads, and the page's own door is refused it as the answer is.
    /// </summary>
    [Fact]
    public async Task A_marked_question_is_never_listed_nor_answered_and_a_defer_is_kept_left()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Service([]))));
        Asked("s1", owner: "sign-in");
        var judge = Judge();
        using var pipe = new InstancePipe(dispatcher, Bus, Home, standing: judge);
        Assert.NotNull(pipe.Serve());
        loop.Pipe = pipe;
        string? key = null;
        SupervisorDoor.Starter = open =>
        {
            key = open.Key;
            return Task.FromResult(StandingConfirmationTests.Opened(open));
        };
        try
        {
            Assert.True((await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = Supervisor })).GetProperty("done").GetBoolean());
            var generation = judge.Admit("engine").Generation!;
            Assert.Empty((await AnswerAsync(module, "SUPERVISOR_WAITING")).GetProperty("waiting").EnumerateArray());

            SupervisorReason[] reasons = [new("A sign-in is the owner's.", "D191 point 3")];
            async Task<JsonElement> SendAsync(string route, Func<string, object> form)
            {
                await using var client = await PipeClient.ConnectAsync(pipe.Name!);
                var nonce = (await client.HelloAsync(HomeIdentity.Read(Home)!)).GetProperty("data").GetProperty("nonce").GetString()!;
                var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, route, form(nonce)));
                Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
                return answer.GetProperty("data");
            }

            object Answer(string nonce) => new SupervisorForm(
                "s1", "q-1", generation, SupervisorProof.Prove(key!, nonce, SupervisorProof.Canonical("s1", "q-1", generation, "Monthly", reasons))!,
                "Monthly", reasons).Payload();
            object Defer(string nonce) => new SupervisorDeferForm(
                "s1", "q-1", generation,
                SupervisorProof.Prove(key!, nonce, SupervisorProof.Canonical(SupervisorRoutes.Defer, "s1", "q-1", generation, "sign-in", reasons))!,
                "sign-in", reasons).Payload();

            var answered = await SendAsync("SUPERVISOR_ANSWER", Answer);
            Assert.Equal((false, SupervisorWhy.OwnerQuestion), (answered.GetProperty("kept").GetBoolean(), answered.GetProperty("why").GetString()));
            Assert.Empty(loop.Supervisor.LiveOf("s1"));

            var page = await AnswerAsync(module, "SUPERVISOR_DEFER", Defer(SupervisorProof.Nonce()));
            Assert.Equal((false, "not-holder"), (page.GetProperty("kept").GetBoolean(), page.GetProperty("why").GetString()));

            var left = await SendAsync("SUPERVISOR_DEFER", Defer);
            Assert.True(left.GetProperty("kept").GetBoolean(), left.GetRawText());
            var decision = Assert.Single(loop.Supervisor.LiveOf("s1"));
            Assert.Equal((SupervisorState.Left, "sign-in"), (decision.State, decision.Bound));
            var supervised = await AnswerAsync(module, "SESSION_SUPERVISOR", new { id = "s1" });
            // The line's counts and the rules' acts ride beside the decisions, for *Details* and the head's chip.
            Assert.Equal(JsonValueKind.Array, supervised.GetProperty("counts").ValueKind);
            Assert.Equal(JsonValueKind.Array, supervised.GetProperty("acts").ValueKind);
            var row = Assert.Single(supervised.GetProperty("decisions").EnumerateArray());
            Assert.Equal(("left", "sign-in"), (row.GetProperty("state").GetString(), row.GetProperty("bound").GetString()));

            // The card reads it beside the question it was left on, with the agent's mark (§4.2).
            var asked = (await AnswerAsync(module, "SESSION_QUESTION", new { id = "s1" })).GetProperty("question");
            Assert.Equal("sign-in", asked.GetProperty("owner").GetString());
            var leftOn = asked.GetProperty("left");
            Assert.Equal("sign-in", leftOn.GetProperty("bound").GetString());
            var reason = Assert.Single(leftOn.GetProperty("reasons").EnumerateArray());
            Assert.Equal(("A sign-in is the owner's.", "D191 point 3"), (reason.GetProperty("says").GetString(), reason.GetProperty("source").GetString()));
        }
        finally
        {
            SupervisorDoor.Starter = null;
        }
    }
}
