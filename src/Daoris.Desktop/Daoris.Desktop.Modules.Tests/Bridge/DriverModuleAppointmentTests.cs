using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1a-ii (D208; the supervisor design §2.2, §2.3, §3.1, §5 and §11 row 1a-ii) over the bridge: the window's appointment
/// starts the holder's terminal with the key in its environment and answers no key, nor does <c>STATE</c>; this build refuses it
/// <c>not-offered</c>; the holder's answer is refused on every door but a covered terminal's, and kept as a held decision on one; the
/// listing names what waits with its generation; and turning the appointment off makes a held decision stale after the judge's step.
/// </summary>
public sealed class DriverModuleAppointmentTests : DriverModuleBridge
{
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

    private StandingConfirmation Judge(bool appointments = true) =>
        new(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), offered: true, appointments: appointments)
        {
            Drives = repository => repository == "engine",
        };

    [Fact]
    public async Task The_window_appoints_by_starting_the_holders_terminal_and_no_answer_the_page_receives_holds_the_key()
    {
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        using var pipe = new InstancePipe(dispatcher, Bus, Home, standing: Judge());
        loop.Pipe = pipe;
        var started = new List<string>();
        SupervisorDoor.Starter = handed =>
        {
            started.Add(handed);
            return Task.FromResult(true);
        };
        try
        {
            var appointed = await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "engine", holder = "terminal", on = true });
            Assert.True(appointed.GetProperty("done").GetBoolean(), appointed.GetRawText());

            // The panel is a stand-in here: no shell, console or window starts (HolderTerminalTests holds the tab itself).
            var key = Assert.Single(started);
            Assert.NotNull(HolderKey.Read(key));

            // No answer the page receives holds the key or its hash; STATE says the appointment, never its generation.
            var hash = Convert.ToHexStringLower(HolderKey.Hash(key)!);
            var state = await AnswerAsync(module, "STATE");
            foreach (var said in new[] { appointed.GetRawText(), state.GetRawText() })
            {
                Assert.DoesNotContain(key!, said, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(hash, said, StringComparison.OrdinalIgnoreCase);
            }

            var row = Assert.Single(state.GetProperty("standingConfirmation").GetProperty("appointments").EnumerateArray());
            Assert.Equal(("supervisor", "engine", false), (row.GetProperty("role").GetString(), row.GetProperty("repository").GetString(), row.GetProperty("dormant").GetBoolean()));
            Assert.False(row.TryGetProperty("generation", out _));

            var unknown = await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "elsewhere", on = true });
            Assert.Equal("unknown-repository", unknown.GetProperty("why").GetString());
            Assert.Single(started);

            SupervisorDoor.Starter = _ => Task.FromResult(false);
            var unstarted = await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "engine", on = true });
            Assert.Equal((false, "unstarted"), (unstarted.GetProperty("done").GetBoolean(), unstarted.GetProperty("why").GetString()));
            // The appointment in force is the one before: nothing was installed.
            Assert.Equal(hash, Convert.ToHexStringLower(HolderKey.Hash(key)!));
            Assert.Null(pipe.StandingConfirmation.Admit("engine").Why);

            var off = await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "engine", on = false });
            Assert.True(off.GetProperty("done").GetBoolean());
            Assert.Empty((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("appointments").EnumerateArray());
        }
        finally
        {
            SupervisorDoor.Starter = null;
        }
    }

    /// <summary>D208 §2.3: this build does not offer appointments, so the window's press is refused and nothing starts.</summary>
    [Fact]
    public async Task This_build_refuses_an_appointment_not_offered_and_starts_nothing()
    {
        var (loop, module, dispatcher, services) = Compose();
        using var composed = services;
        using var pipe = new InstancePipe(dispatcher, Bus, Home);
        loop.Pipe = pipe;
        var started = 0;
        SupervisorDoor.Starter = _ => Task.FromResult(++started > 0);
        try
        {
            var refused = await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "engine", on = true });
            Assert.Equal((false, "not-offered"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("why").GetString()));
            Assert.Equal(0, started);
            Assert.False((await AnswerAsync(module, "STATE")).GetProperty("standingConfirmation").GetProperty("appointmentsOffered").GetBoolean());
        }
        finally
        {
            SupervisorDoor.Starter = null;
        }
    }

    /// <summary>
    /// §3.1: the page's door, or any door the role method did not cover, is refused <c>not-holder</c> and keeps nothing; over the pipe a
    /// covered terminal's answer is kept held, listed no more, and the appointment turned off makes it stale after the judge's step.
    /// </summary>
    [Fact]
    public async Task The_answer_is_kept_only_from_a_covered_terminal_and_a_turn_off_sweeps_it()
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
        SupervisorDoor.Starter = handed => Task.FromResult((key = handed) is not null);
        try
        {
            Assert.True((await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "engine", on = true })).GetProperty("done").GetBoolean());
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

            Assert.True((await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "engine", on = false })).GetProperty("done").GetBoolean());
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
        SupervisorDoor.Starter = handed => Task.FromResult((key = handed) is not null);
        try
        {
            Assert.True((await AnswerAsync(module, "SET_APPOINTMENT", new { role = "supervisor", repository = "engine", on = true })).GetProperty("done").GetBoolean());
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
            var row = Assert.Single((await AnswerAsync(module, "SESSION_SUPERVISOR", new { id = "s1" })).GetProperty("decisions").EnumerateArray());
            Assert.Equal(("left", "sign-in"), (row.GetProperty("state").GetString(), row.GetProperty("bound").GetString()));
        }
        finally
        {
            SupervisorDoor.Starter = null;
        }
    }
}
