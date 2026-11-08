using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;
using CatalogueScope = Daoris.Driver.RoleScope;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A steward's act outside its grants as an <c>allow</c> subject <c>{ ask }</c> over the real pipe: raised at its target's place, allowed
/// once as the steward's own act, left or unanswered for ten minutes to the person's card once, withdrawn by the steward, an allowed
/// <c>land</c> held for the window and overruled or withdrawn inside it, and never offered to a supervisor sharing the steward's holder.
/// </summary>
/// <remarks>Each test serves its own pipe in this process over the real driver module. Windows only.</remarks>
public sealed class StewardAllowTests : DriverModuleBridge
{
    private const string Supervisor = "supervisor-allows";

    private const string Hold = """{"repository":"engine","held":true}""";

    private static readonly SupervisorReason[] Reasons = [new("It only holds.", "the call's title")];

    private static readonly string AllowSteward = SupervisorKinds.Qualified(SupervisorKinds.Allow, "steward");

    private sealed class Service : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(request.RequestUri!.AbsolutePath == "/api/sessions" ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            {
                Content = new StringContent(request.RequestUri.AbsolutePath == "/api/sessions" ? "[]" : "{}", Encoding.UTF8, "application/json"),
            });
    }

    private sealed class Stand(
        DriverLoop loop, DriverModule module, StandingConfirmation judge, InstancePipe pipe, ServiceProvider services, RoleKit.Probe probe, string stewardKey,
        string? supervisorKey, MachineLog log) : IDisposable
    {
        public DriverLoop Loop { get; } = loop;

        public DriverModule Module { get; } = module;

        public StandingConfirmation Judge { get; } = judge;

        public InstancePipe Pipe { get; } = pipe;

        public RoleKit.Probe Probe { get; } = probe;

        public string StewardKey { get; } = stewardKey;

        public string SupervisorKey { get; } = supervisorKey ?? stewardKey;

        public void Dispose()
        {
            Pipe.Dispose();
            services.Dispose();
            log.Dispose();
        }
    }

    /// <param name="share">That the allow supervisor's terminal is the steward's own, one holder holding both.</param>
    /// <param name="act">The steward's act the table names; <c>land</c> to test the held landing.</param>
    private async Task<Stand> OpenAsync(bool share = false, string act = RoleKit.Probe.Named, CatalogueScope? scope = null)
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop) { OwnerScopes = new OwnerSetTests.Scopes() };
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.AddSingleton<IIpcModule>(module);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Service())));
        var probe = new RoleKit.Probe { PersonRoute = "SET_HOLD", Form = ["repository", "held"] };
        var log = new MachineLog(Home, "desktop");
        var judge = new StandingConfirmation(Home, log, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
        }.Probing(probe);
        judge.TableOf = role => role == AppointedRoles.Steward
            ? new Dictionary<string, IRoleAct>(StringComparer.Ordinal) { [act] = probe }
            : new Dictionary<string, IRoleAct>(StringComparer.Ordinal);
        judge.Staff(RoleKit.Steward([]), RoleKit.Supervisor(Supervisor, scope ?? CatalogueScope.Everywhere, decides: [SupervisorKinds.Allow], acts: [AllowSteward]));
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, log, standing: judge) { Grounds = new StubGrounds() };
        Assert.NotNull(pipe.Serve());
        loop.Pipe = pipe;
        pipe.StewardAllows.Places = (_, _) => Task.FromResult<RolePlace?>(new RolePlace(Repository: "engine"));
        var (stewarded, stewardKey) = RoleKit.Start(judge, RoleKit.Stewarding(share: null));
        Assert.True(stewarded.Answer.Done, stewarded.Answer.Message);
        var holder = judge.Now().Holders!.Single().Id;
        var (supervising, supervisorKey) = RoleKit.Start(judge, new HoldRequest(Supervisor, share ? holder : "own"));
        Assert.True(supervising.Answer.Done, supervising.Answer.Message);
        return new Stand(loop, module, judge, pipe, provider, probe, stewardKey!, supervisorKey, log);
    }

    private static string? NonceOf(JsonElement hello) => hello.GetProperty("data").GetProperty("nonce").GetString();

    private static async Task UntilAsync(Func<bool> condition, Func<string> because)
    {
        var until = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, because());
            await Task.Delay(10);
        }
    }

    /// <summary>The steward's act sent on a connection of its own, proved by its key; the connection and the request's id.</summary>
    private async Task<(PipeClient Client, string Id)> SendActAsync(Stand stand, string act = RoleKit.Probe.Named)
    {
        var client = await PipeClient.ConnectAsync(stand.Pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeIdentity.Read(Home)!))!;
        var generation = RoleKit.GenerationOf(stand.Judge, AppointedRoles.Steward);
        return (client, await client.SendAsync(DriverModule.Name, RoleRoutes.Act, RoleKit.Act(stand.StewardKey, nonce, generation, act: act, form: Hold)));
    }

    private async Task<JsonElement> SupervisorSendAsync(Stand stand, string route, Func<string, object> form)
    {
        await using var client = await PipeClient.ConnectAsync(stand.Pipe.Name!);
        var nonce = NonceOf(await client.HelloAsync(HomeIdentity.Read(Home)!))!;
        var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, route, form(nonce)));
        Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
        return answer.GetProperty("data");
    }

    private async Task<JsonElement> AskedAsync(Stand stand)
    {
        JsonElement listed = default;
        await UntilAsync(() =>
        {
            var waiting = AnswerAsync(stand.Module, "SUPERVISOR_WAITING").GetAwaiter().GetResult();
            listed = waiting.GetProperty("waiting").EnumerateArray().FirstOrDefault(each => each.GetProperty("kind").GetString() == SupervisorKinds.Allow);
            return listed.ValueKind == JsonValueKind.Object;
        }, () => "the steward's request was not listed for the supervisor");
        return listed;
    }

    private Task<JsonElement> AllowAsync(Stand stand, string ask, string generation) =>
        SupervisorSendAsync(stand, "SUPERVISOR_DECIDE", nonce => new SupervisorDecideForm(SupervisorSubject.StewardAsk(ask), generation, "", "steward", null, Reasons)
            .Proved(stand.SupervisorKey, nonce).Payload());

    private static string AskOf(JsonElement listed) => listed.GetProperty("subject").GetProperty("ask").GetString()!;

    private static string GenerationOf(JsonElement listed) => listed.GetProperty("generation").GetString()!;

    private List<JsonElement> Lines(string @event)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.GetFiles(folder).SelectMany(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            })
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(line => line.GetProperty("event").GetString() == @event),
        ];
    }

    [Fact]
    public async Task An_act_outside_the_grants_is_listed_for_the_supervisor_with_no_card_and_allowed_once_as_the_stewards_own()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        var (client, id) = await SendActAsync(stand);
        await using var connected = client;

        var listed = await AskedAsync(stand);
        Assert.Equal(["steward"], listed.GetProperty("acts").EnumerateArray().Select(act => act.GetString()));
        Assert.Empty(stand.Pipe.Confirmations.Now().Waiting);
        Assert.Equal(0, stand.Probe.Ran);

        var kept = await AllowAsync(stand, AskOf(listed), GenerationOf(listed));
        Assert.True(kept.GetProperty("kept").GetBoolean(), kept.GetRawText());

        var answer = await client.AnswerAsync(id);
        Assert.True(answer.GetProperty("data").GetProperty("done").GetBoolean(), answer.GetRawText());
        Assert.Equal(1, stand.Probe.Ran);
        Assert.Empty(stand.Pipe.Confirmations.Now().Waiting);
        var admitted = Lines("role.act").Single(line => line.GetProperty("data").TryGetProperty("by", out _));
        Assert.Equal(("steward", kept.GetProperty("decision").GetString()), (admitted.GetProperty("data").GetProperty("by").GetString(), admitted.GetProperty("data").GetProperty("decision").GetString()));

        // Once alone: the next act of the same kind asks again.
        var (next, nextId) = await SendActAsync(stand);
        await using var again = next;
        var second = await AskedAsync(stand);
        Assert.NotEqual(AskOf(listed), AskOf(second));
        Assert.Equal(1, stand.Probe.Ran);
    }

    [Fact]
    public async Task A_request_the_supervisor_leaves_falls_to_the_persons_card_and_is_not_run()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        var (client, _) = await SendActAsync(stand);
        await using var connected = client;
        var listed = await AskedAsync(stand);

        var left = await SupervisorSendAsync(stand, "SUPERVISOR_LEAVE", nonce => new SupervisorLeaveForm(
            SupervisorSubject.StewardAsk(AskOf(listed)), GenerationOf(listed), "", SupervisorBounds.All[0], Reasons).Proved(stand.SupervisorKey, nonce).Payload());

        Assert.True(left.GetProperty("kept").GetBoolean(), left.GetRawText());
        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, () => "the card did not follow the leave");
        Assert.Equal("SET_HOLD", stand.Pipe.Confirmations.Now().Waiting[0].Route);
        Assert.Equal(0, stand.Probe.Ran);
    }

    [Fact]
    public async Task An_allow_after_the_ten_minutes_is_dropped_the_card_asked_once_and_the_act_run_once()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        stand.Pipe.StewardAllows.Limit = TimeSpan.FromMilliseconds(1500);
        var (client, id) = await SendActAsync(stand);
        await using var connected = client;
        var listed = await AskedAsync(stand);

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, () => "the card did not follow the ten minutes");
        var late = await AllowAsync(stand, AskOf(listed), GenerationOf(listed));

        Assert.Equal((false, SupervisorWhy.Answered), (late.GetProperty("kept").GetBoolean(), late.GetProperty("why").GetString()));
        Assert.Equal(0, stand.Probe.Ran);
        Assert.Single(stand.Pipe.Confirmations.Now().Waiting);
        Assert.Equal(PipeAnswered.Answered, (await stand.Pipe.Confirmations.AnswerAsync(stand.Pipe.Confirmations.Now().Waiting[0].Id, confirm: false)).Outcome);
        Assert.False((await client.AnswerAsync(id)).GetProperty("success").GetBoolean());
        Assert.Equal(0, stand.Probe.Ran);
    }

    [Fact]
    public async Task The_stewards_withdrawal_settles_the_entry_before_an_allow_and_nothing_runs()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        var (client, id) = await SendActAsync(stand);
        await using var connected = client;
        var listed = await AskedAsync(stand);

        var withdrawn = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "WITHDRAW", new { id, key = client.KeyOf(id) }));
        Assert.Equal("withdrawn", withdrawn.GetProperty("data").GetProperty("state").GetString());
        var late = await AllowAsync(stand, AskOf(listed), GenerationOf(listed));

        Assert.Equal((false, SupervisorWhy.Answered), (late.GetProperty("kept").GetBoolean(), late.GetProperty("why").GetString()));
        Assert.Equal(0, stand.Probe.Ran);
        Assert.Empty(stand.Pipe.Confirmations.Now().Waiting);
    }

    [Fact]
    public async Task A_supervisor_on_the_stewards_own_holder_is_not_offered_the_act_and_the_card_is_asked_at_once()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync(share: true);
        var holder = stand.Judge.Now().Holders!.Single().Id;
        Assert.Equal(SupervisorWhy.NotGranted, stand.Judge.AdmitStewardAllow(new RolePlace(Repository: "engine"), holder).Why);

        var (client, _) = await SendActAsync(stand);
        await using var connected = client;

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, () => "the card was not asked at once");
        var waiting = await AnswerAsync(stand.Module, "SUPERVISOR_WAITING");
        Assert.DoesNotContain(waiting.GetProperty("waiting").EnumerateArray(), each => each.GetProperty("kind").GetString() == SupervisorKinds.Allow);
        Assert.Equal(0, stand.Probe.Ran);
    }

    [Fact]
    public async Task A_target_that_names_no_place_goes_to_the_persons_card()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        stand.Pipe.StewardAllows.Places = (_, _) => Task.FromResult<RolePlace?>(null);

        var (client, _) = await SendActAsync(stand);
        await using var connected = client;

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, () => "the card was not asked");
        Assert.Empty(stand.Pipe.StewardAllows.Waiting());
    }

    [Fact]
    public async Task An_act_on_a_target_the_owner_acted_on_is_refused_owner_set_when_allowed_and_not_run()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync();
        var (client, id) = await SendActAsync(stand);
        await using var connected = client;
        var listed = await AskedAsync(stand);
        stand.Judge.MarkOwner(new OwnerResolved([new RoleScope(OwnerKinds.Repository, "engine")], [], false));

        var kept = await AllowAsync(stand, AskOf(listed), GenerationOf(listed));

        Assert.True(kept.GetProperty("kept").GetBoolean(), kept.GetRawText());
        var answer = await client.AnswerAsync(id);
        Assert.Equal(RoleWhy.OwnerSet, answer.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, stand.Probe.Ran);
    }

    [Fact]
    public async Task An_allowed_land_is_held_for_the_window_and_runs_when_it_ends()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync(act: StewardActNames.Land);
        stand.Pipe.StewardAllows.Window = TimeSpan.FromMilliseconds(400);
        var (client, id) = await SendActAsync(stand, StewardActNames.Land);
        await using var connected = client;
        var listed = await AskedAsync(stand);

        Assert.True((await AllowAsync(stand, AskOf(listed), GenerationOf(listed))).GetProperty("kept").GetBoolean());

        Assert.Equal(0, stand.Probe.Ran);
        Assert.NotNull(stand.Pipe.StewardAllows.Find(AskOf(listed)));
        Assert.True((await client.AnswerAsync(id)).GetProperty("data").GetProperty("done").GetBoolean());
        Assert.Equal(1, stand.Probe.Ran);
        Assert.Empty(stand.Pipe.Confirmations.Now().Waiting);
    }

    [Fact]
    public async Task An_allowed_land_overruled_inside_the_window_falls_to_the_persons_card_and_does_not_land()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync(act: StewardActNames.Land);
        stand.Pipe.StewardAllows.Window = TimeSpan.FromMinutes(5);
        var (client, _) = await SendActAsync(stand, StewardActNames.Land);
        await using var connected = client;
        var listed = await AskedAsync(stand);
        Assert.True((await AllowAsync(stand, AskOf(listed), GenerationOf(listed))).GetProperty("kept").GetBoolean());

        Assert.True(stand.Pipe.StewardAllows.Overrule(AskOf(listed)));

        await UntilAsync(() => stand.Pipe.Confirmations.Now().Waiting.Count == 1, () => "the card did not follow the overrule");
        Assert.Equal(0, stand.Probe.Ran);
        Assert.False(stand.Pipe.StewardAllows.Overrule(AskOf(listed)));
    }

    [Fact]
    public async Task An_allowed_land_the_steward_withdraws_inside_the_window_is_dropped_with_no_card_and_does_not_land()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stand = await OpenAsync(act: StewardActNames.Land);
        stand.Pipe.StewardAllows.Window = TimeSpan.FromMilliseconds(300);
        var (client, id) = await SendActAsync(stand, StewardActNames.Land);
        await using var connected = client;
        var listed = await AskedAsync(stand);
        Assert.True((await AllowAsync(stand, AskOf(listed), GenerationOf(listed))).GetProperty("kept").GetBoolean());

        var withdrawn = await client.AnswerAsync(await client.SendAsync(InstancePipe.Module, "WITHDRAW", new { id, key = client.KeyOf(id) }));
        Assert.Equal("withdrawn", withdrawn.GetProperty("data").GetProperty("state").GetString());
        await Task.Delay(700);

        Assert.Equal(0, stand.Probe.Ran);
        Assert.Empty(stand.Pipe.Confirmations.Now().Waiting);
        Assert.Equal(InstancePipe.Withdrawn, (await client.AnswerAsync(id)).GetProperty("error").GetProperty("code").GetString());
    }
}
