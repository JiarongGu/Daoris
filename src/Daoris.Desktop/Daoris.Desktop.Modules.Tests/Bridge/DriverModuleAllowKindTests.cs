using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Ipc;
using CatalogueScope = Daoris.Driver.RoleScope;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The allow kind over the supervisor's door: a permission request held in the asking mode allowed once or rejected through the hold's own
/// end, a standing option refused; a widening a session proposed decided at its rule's reach through the proposal's one settlement,
/// recorded by the role, and a widening the person alone may decide listed for no supervisor.
/// </summary>
public sealed class DriverModuleAllowKindTests : DriverModuleBridge
{
    private const string Supervisor = "supervisor-allows";

    private static readonly SupervisorReason[] Reasons = [new("It only reads.", "the call's title")];

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T10:00:00Z");

    private const string Records =
        """[{"id":"s1","repository":"engine","state":"awaiting-person","quest":"q1","kind":"driven","created":"2026-10-22T08:00:00Z"}]""";

    private sealed class Service : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/api/sessions"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Records, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
    }

    private static readonly IReadOnlyList<string> Acts =
        [SupervisorKinds.Qualified(SupervisorKinds.Allow, "once"), SupervisorKinds.Qualified(SupervisorKinds.Allow, "reject"), SupervisorKinds.Qualified(SupervisorKinds.Allow, "standing")];

    private sealed record Door(DriverLoop Loop, StandingConfirmation Judge, Func<string, Func<string, object>, Task<JsonElement>> Send, string Key, ServiceProvider Services, InstancePipe Pipe) : IDisposable
    {
        public void Dispose()
        {
            Pipe.Dispose();
            Services.Dispose();
            SupervisorDoor.Starter = null;
        }
    }

    private async Task<Door> OpenAsync(CatalogueScope? scope = null)
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var services = new ServiceCollection();
        services.AddSingleton<Shenora.Core.Events.IEventBus>(Bus);
        services.AddSingleton<IIpcModule>(module);
        services.UseMessageDispatcher((_, _) => { });
        var provider = services.BuildServiceProvider();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Service())));
        var judge = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
        }.Staff(RoleKit.Supervisor(Supervisor, scope ?? CatalogueScope.Everywhere, decides: [SupervisorKinds.Allow], acts: Acts));
        var pipe = new InstancePipe(provider.GetRequiredService<IMessageDispatcher>(), Bus, Home, standing: judge);
        Assert.NotNull(pipe.Serve());
        loop.Pipe = pipe;
        string? key = null;
        SupervisorDoor.Starter = open =>
        {
            key = open.Key;
            return Task.FromResult(StandingConfirmationTests.Opened(open));
        };
        Assert.True((await AnswerAsync(module, "START_ROLE_TERMINAL", new { role = Supervisor })).GetProperty("done").GetBoolean());

        async Task<JsonElement> SendAsync(string route, Func<string, object> form)
        {
            await using var client = await PipeClient.ConnectAsync(pipe.Name!);
            var nonce = (await client.HelloAsync(HomeIdentity.Read(Home)!)).GetProperty("data").GetProperty("nonce").GetString()!;
            var answer = await client.AnswerAsync(await client.SendAsync(DriverModule.Name, route, form(nonce)));
            Assert.True(answer.GetProperty("success").GetBoolean(), answer.GetRawText());
            return answer.GetProperty("data");
        }

        return new Door(loop, judge, SendAsync, key!, provider, pipe);
    }

    private static readonly PermissionChoice[] Offered =
    [
        new("allow", "Allow once", PermissionChoice.AllowOnce), new("always", "Always allow", PermissionChoice.AllowAlways),
        new("no", "Reject", PermissionChoice.RejectOnce),
    ];

    private static (Task<PermissionEnd> Ended, SupervisorSubject Subject) Hold(DriverLoop loop, string request = "r1")
    {
        var held = HeldPermissions.Of(loop.Events);
        var ended = held.For("s1").Hold(request, "call-1", "Run the tests", Offered, "execute")!;
        return (ended, SupervisorSubject.Request("s1", request, Moments.Write(held.Held("s1").Single(each => each.Request == request).Since)));
    }

    private string Generation(Door door, string repository, string act) =>
        door.Judge.Admit(SupervisorKinds.Allow, new RolePlace(Repository: repository), act).Generation!;

    private async Task<JsonElement> DecideAsync(Door door, SupervisorSubject subject, string generation, string act) =>
        await door.Send("SUPERVISOR_DECIDE", nonce => new SupervisorDecideForm(subject, generation, "", act, null, Reasons).Proved(door.Key, nonce).Payload());

    private string Propose(string id, string action, string scope = "machine", string? name = null, string? list = null, string? rule = null,
        string? defaultId = null, bool? on = null)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.RuleProposals);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{id}.json");
        File.WriteAllText(path, new JsonObject
        {
            ["id"] = id, ["proposed"] = At.ToString("O"), ["by"] = new JsonObject { ["session"] = "s1" },
            ["change"] = new JsonObject { ["action"] = action, ["scope"] = scope, ["name"] = name, ["list"] = list, ["rule"] = rule, ["default"] = defaultId, ["on"] = on },
            ["why"] = "Needed.", ["state"] = "waiting",
        }.ToJsonString());
        return path;
    }

    private static JsonElement Listed(JsonElement waiting, string kind) =>
        waiting.GetProperty("waiting").EnumerateArray().FirstOrDefault(each => each.GetProperty("kind").GetString() == kind);

    [Fact]
    public async Task A_held_request_is_listed_with_its_two_acts_and_allowed_once_through_the_holds_own_end()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var door = await OpenAsync();
        var (ended, subject) = Hold(door.Loop);
        var module = new DriverModule(Bus, door.Loop);

        var listed = Listed(await AnswerAsync(module, "SUPERVISOR_WAITING"), SupervisorKinds.Allow);
        Assert.Equal(["once", "reject"], listed.GetProperty("acts").EnumerateArray().Select(act => act.GetString()));
        Assert.Equal(subject.Held, listed.GetProperty("subject").GetProperty("held").GetString());

        var kept = await DecideAsync(door, subject, Generation(door, "engine", "once"), "once");

        Assert.True(kept.GetProperty("kept").GetBoolean(), kept.GetRawText());
        Assert.Equal("allow", (await ended).Chosen!.Id);
        Assert.False(HeldPermissions.Of(door.Loop.Events).Answer("s1", "r1", "allow").Answered);
        var recorded = Assert.Single(door.Loop.Supervisor.AllowsOf("s1"));
        Assert.Equal(
            (kept.GetProperty("decision").GetString(), SupervisorAllowShapes.Request, "r1", "once", Supervisor, SupervisorState.WentOn),
            (recorded.Id, recorded.Shape, recorded.Subject, recorded.Do, recorded.Role, recorded.State));
    }

    [Fact]
    public async Task A_held_request_is_rejected_and_a_standing_option_or_another_moment_is_refused()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var door = await OpenAsync();
        var (ended, subject) = Hold(door.Loop);
        var generation = Generation(door, "engine", "once");

        var standing = await DecideAsync(door, subject, generation, "standing");
        var other = await DecideAsync(door, SupervisorSubject.Request("s1", "r1", "2020-01-01T00:00:00.0000000+00:00"), generation, "once");
        Assert.Equal((false, SupervisorWhy.NotOffered), (standing.GetProperty("kept").GetBoolean(), standing.GetProperty("why").GetString()));
        Assert.Equal((false, SupervisorWhy.NotHeld), (other.GetProperty("kept").GetBoolean(), other.GetProperty("why").GetString()));
        Assert.False(ended.IsCompleted);
        Assert.Empty(door.Loop.Supervisor.AllowsOf("s1"));

        Assert.True((await DecideAsync(door, subject, generation, "reject")).GetProperty("kept").GetBoolean());
        Assert.Equal("no", (await ended).Chosen!.Id);
        Assert.Equal("reject", Assert.Single(door.Loop.Supervisor.AllowsOf("s1")).Do);
    }

    [Fact]
    public async Task A_request_the_person_answered_first_is_refused_to_the_supervisor_unheld()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var door = await OpenAsync();
        var (ended, subject) = Hold(door.Loop);
        Assert.True(HeldPermissions.Of(door.Loop.Events).Answer("s1", "r1", "no").Answered);

        var late = await DecideAsync(door, subject, Generation(door, "engine", "once"), "once");

        Assert.Equal((false, SupervisorWhy.NotHeld), (late.GetProperty("kept").GetBoolean(), late.GetProperty("why").GetString()));
        Assert.Equal("no", (await ended).Chosen!.Id);
    }

    [Fact]
    public async Task A_widening_is_decided_at_its_rules_reach_recorded_by_the_role_and_a_deny_of_the_persons_is_listed_for_no_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var door = await OpenAsync();
        var module = new DriverModule(Bus, door.Loop);
        var path = Propose("p1", "add", scope: "repository", name: "engine", list: "allow", rule: "Bash(npm run build:*)");
        Propose("p2", "add", list: "allow", rule: "Bash(rm:*)");
        PermissionChange.Change(Home, rules => PermissionRules.Add(rules, RuleScope.Machine, null, RuleList.Deny, "Bash(rm:*)"));

        var waiting = await AnswerAsync(module, "SUPERVISOR_WAITING");
        var listed = Assert.Single(waiting.GetProperty("waiting").EnumerateArray().Where(each => each.GetProperty("kind").GetString() == SupervisorKinds.Allow));
        Assert.Equal("p1", listed.GetProperty("subject").GetProperty("proposal").GetString());
        Assert.Equal(["reject", "standing"],listed.GetProperty("acts").EnumerateArray().Select(act => act.GetString()));

        var generation = door.Judge.Admit(SupervisorKinds.Allow, new RolePlace(Repository: "engine"), "standing").Generation!;
        var kept = await DecideAsync(door, SupervisorSubject.Proposal("p1"), generation, "standing");

        Assert.True(kept.GetProperty("kept").GetBoolean(), kept.GetRawText());
        Assert.Equal(["Bash(npm run build:*)"], PermissionRules.Load(Home).Repositories["engine"].Allow);
        var settled = JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("settled");
        Assert.Equal(("the supervisor", Supervisor), (settled.GetProperty("by").GetString(), settled.GetProperty("role").GetString()));
        var recorded = Assert.Single(door.Loop.Supervisor.AllowsOf("s1"));
        Assert.Equal(
            (settled.GetProperty("decision").GetString(), SupervisorAllowShapes.Widening, "p1", "standing", Supervisor, "engine"),
            (recorded.Id, recorded.Shape, recorded.Subject, recorded.Do, recorded.Role, recorded.Repository));

        var denied = await DecideAsync(door, SupervisorSubject.Proposal("p2"), generation, "standing");
        Assert.Equal((false, WideningAllows.RemovesDeny), (denied.GetProperty("kept").GetBoolean(), denied.GetProperty("why").GetString()));
        Assert.DoesNotContain(PermissionRules.Load(Home).Machine.Allow, rule => rule == "Bash(rm:*)");
        Assert.Single(door.Loop.Supervisor.AllowsOf("s1"));
    }

    [Fact]
    public async Task A_machine_widening_proposed_from_a_repository_is_not_decided_by_that_repositorys_supervisor()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var door = await OpenAsync(RoleKit.Repository("engine"));
        var module = new DriverModule(Bus, door.Loop);
        Propose("p3", "add", list: "allow", rule: "Bash(make:*)");

        Assert.Equal(JsonValueKind.Undefined, Listed(await AnswerAsync(module, "SUPERVISOR_WAITING"), SupervisorKinds.Allow).ValueKind);
        var generation = Generation(door, "engine", "standing");
        var refused = await DecideAsync(door, SupervisorSubject.Proposal("p3"), generation, "standing");

        Assert.False(refused.GetProperty("kept").GetBoolean());
        Assert.Empty(PermissionRules.Load(Home).Machine.Allow);
    }

    [Fact]
    public async Task A_widening_cannot_be_allowed_once_and_a_leave_keeps_the_subject_for_the_person()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var door = await OpenAsync();
        var module = new DriverModule(Bus, door.Loop);
        Propose("p4", "add", list: "allow", rule: "Bash(make:*)");
        var generation = Generation(door, "engine", "once");

        var once = await DecideAsync(door, SupervisorSubject.Proposal("p4"), generation, "once");
        Assert.Equal((false, SupervisorWhy.NotOffered), (once.GetProperty("kept").GetBoolean(), once.GetProperty("why").GetString()));

        var left = await door.Send("SUPERVISOR_LEAVE", nonce => new SupervisorLeaveForm(SupervisorSubject.Proposal("p4"), generation, "", SupervisorBounds.All[0], Reasons).Proved(door.Key, nonce).Payload());
        Assert.True(left.GetProperty("kept").GetBoolean(), left.GetRawText());
        Assert.Equal(JsonValueKind.Undefined, Listed(await AnswerAsync(module, "SUPERVISOR_WAITING"), SupervisorKinds.Allow).ValueKind);
        Assert.Equal("waiting", JsonDocument.Parse(File.ReadAllText(RuleProposals.PathOf(Home, "p4"))).RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_leave_of_a_request_in_a_repository_the_holders_role_does_not_cover_is_refused_and_the_request_stays_held()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var door = await OpenAsync(RoleKit.Repository("elsewhere"));
        var (_, subject) = Hold(door.Loop);
        var generation = Generation(door, "elsewhere", "once");

        var left = await door.Send("SUPERVISOR_LEAVE", nonce => new SupervisorLeaveForm(subject, generation, "", SupervisorBounds.All[0], Reasons).Proved(door.Key, nonce).Payload());

        Assert.False(left.GetProperty("kept").GetBoolean());
        Assert.Equal(SupervisorWhy.NotStaffed, left.GetProperty("why").GetString());
        Assert.Contains(HeldPermissions.Of(door.Loop.Events).Held("s1"), each => each.Request == subject.Id);
    }
}
