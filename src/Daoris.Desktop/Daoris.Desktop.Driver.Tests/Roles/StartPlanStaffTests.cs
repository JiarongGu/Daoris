using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1c (D226; the staff policy design §3, §9 row c): the start plan arranges the role's own order by its set policy, reserves the
/// top pick beside the account's count, gives it back with that count, and writes the start down at its open. The agents are present by a
/// file look and nothing spawns, as <c>StartPlanTests</c>' are; the store is the home's own, with its staffing imported and its policy
/// saved through the role door.
/// </summary>
public sealed class StartPlanStaffTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);

    private static readonly ActorChoice One = new("cool", "m1", null);

    private static readonly ActorChoice Two = new("fine", "m2", null);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staffpolicy1c-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly AdapterSet _adapters;

    private HarnessRoster _roster;

    private readonly PassMemory _memory = new();

    private readonly RoleStaffing _staffing;

    private MachineLog? _log;

    public StartPlanStaffTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
        foreach (var agent in new[] { "cool", "fine" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
        new HarnessSettings().WithDefault("cool", "account-1").WithDefault("fine", "account-1").Save(Settings);
        var present = new HarnessToolchain(Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        _adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["cool"] = new Adapter("cool", present),
            ["fine"] = new Adapter("fine", present),
            // Runs in the person's own tool home: no Daoris account, so the roster counts nothing for its starts (Number 0).
            ["bare"] = new Adapter("bare", present with { ProfileVariable = null }),
        });
        _roster = Roster();

        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "cool", "actors": { "build": [ { "agent": "cool", "model": "m1" }, { "agent": "fine", "model": "m2" } ] } }""");
        _staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, _staffing.ImportFile().How);
    }

    public void Dispose()
    {
        _log?.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private HarnessRoster Roster() => new(_adapters, Settings) { Clock = () => Now, Zone = TimeZoneInfo.Utc };

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "agent-here");

    private sealed class Adapter(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SpawnRequest request) => throw new InvalidOperationException("nothing spawns here");
    }

    private StaffLedger Ledger => _roster.StaffFor(_home, () => StoreSettled.Yes);

    private static string Entry(ActorChoice choice, string extra) => $$"""{"agent":"{{choice.Agent}}","model":"{{choice.Model}}"{{extra}}}""";

    private void Policy(string rule, string oneExtra = "", string twoExtra = "")
    {
        var policy = StaffPolicy.Parse($$"""{"rule":"{{rule}}","choices":[{{Entry(One, oneExtra)}},{{Entry(Two, twoExtra)}}]}""").Policy!;
        RoleDoor.Apply(_staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(policy));
    }

    private StartPlan Plan(string work = "build", params ActorChoice[] choices)
    {
        var config = ActorChoices.With(DriverConfig.Empty with { Actors = StaffedRoles.On("cool") }, "build", choices.Length == 0 ? [One, Two] : choices);
        return new StartPlan(work, config, _roster, _adapters, _memory)
        {
            Workspace = "default", Subject = "quest:q1", Named = ("quest", "q1"), Log = _log, Staff = Ledger,
        };
    }

    /// <summary>One new start, resolved and opened as <paramref name="session"/>: the choice it took, and the note's why where it has one.</summary>
    private async Task<(string Choice, string By)> StartAsync(string session, string work = "build")
    {
        var settled = await Plan(work).ResolveAsync(CancellationToken.None);
        var bound = Ledger.Opened(settled.Selection.Number, session);
        return (settled.Chosen!.From, bound?.By ?? "");
    }

    private IReadOnlyList<string> Rows() => Query("SELECT choice FROM role_starts ORDER BY rowid", row => StaffKey.Read(row.GetString(0))!.Value.Model!);

    private IReadOnlyList<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        return reader is null ? [] : reader.Query(sql, map);
    }

    [Fact]
    public async Task Three_to_one_over_eight_new_starts_lands_six_to_two_and_each_start_is_written_down()
    {
        Policy("weight", ",\"weight\":3", ",\"weight\":1");

        var taken = new List<string>();
        for (var i = 0; i < 8; i++) taken.Add((await StartAsync($"s{i}")).Choice);

        Assert.Equal(["1", "1", "2", "1", "1", "1", "2", "1"], taken);
        Assert.Equal(6, taken.Count(choice => choice == "1"));
        Assert.Equal(["m1", "m1", "m2", "m1", "m1", "m1", "m2", "m1"], Rows());
        Assert.Empty(_roster.Staff()!.Preview("build", "build", [new ActorEffective("cool", "m1", null, "1"), new ActorEffective("fine", "m2", null, "2")]).Facts.Reserved);
    }

    [Fact]
    public async Task Two_starts_in_one_look_under_one_to_one_land_on_different_choices()
    {
        Policy("weight");

        var first = await Plan().ResolveAsync(CancellationToken.None);
        var second = await Plan().ResolveAsync(CancellationToken.None);

        Assert.Equal(["1", "2"], [first.Chosen!.From, second.Chosen!.From]);
    }

    [Fact]
    public async Task A_refused_open_gives_its_reservation_back_with_the_account_count_and_a_restart_drops_reservations_and_keeps_the_rows()
    {
        Policy("weight");
        var first = await Plan().ResolveAsync(CancellationToken.None);
        Assert.Equal("1", first.Chosen!.From);

        // The open was refused: the roster gives the count back, and the reservation goes with it, so the next start is choice 1 again.
        _roster.Release(first.Selection.Number);
        var again = await Plan().ResolveAsync(CancellationToken.None);
        Assert.Equal("1", again.Chosen!.From);
        Assert.Empty(Rows());

        Assert.NotNull(Ledger.Opened(again.Selection.Number, "s1"));
        var pending = await Plan().ResolveAsync(CancellationToken.None);
        Assert.Equal("2", pending.Chosen!.From);

        _roster = Roster();
        var restarted = await Plan().ResolveAsync(CancellationToken.None);
        Assert.Equal("2", restarted.Chosen!.From);
        Assert.Equal(["m1"], Rows());
    }

    [Fact]
    public async Task A_single_choice_start_the_walk_refuses_gives_its_reservation_back()
    {
        Policy("weight");
        AccountCooling.Cool(_home, new CoolingEntry("cool", "account-1", Now.AddHours(2), true, "weekly", Now, "s0"), Now);

        var settled = await Plan("build", One).ResolveAsync(CancellationToken.None);

        Assert.False(settled.Selection.Allowed);
        Assert.Empty(Ledger.Preview("build", "build", [new ActorEffective("cool", "m1", null, "1"), new ActorEffective("fine", "m2", null, "2")]).Facts.Reserved);
    }

    [Fact]
    public async Task A_weighted_pick_whose_accounts_all_cool_falls_to_the_next_by_weight()
    {
        Policy("weight", ",\"weight\":3", ",\"weight\":1");
        AccountCooling.Cool(_home, new CoolingEntry("cool", "account-1", Now.AddHours(2), true, "weekly", Now, "s0"), Now);

        var settled = await Plan().ResolveAsync(CancellationToken.None);

        Assert.Equal("2", settled.Chosen!.From);
        Assert.Equal([("1", StartPasses.Cooling)], settled.Passes.Select(pass => (pass.Entry.From, pass.Code)));
    }

    [Fact]
    public async Task A_start_past_a_budget_of_starts_is_passed_over_budget_and_a_role_with_no_other_choice_waits_naming_the_day()
    {
        Policy("order", ",\"budget\":{\"starts\":2}");

        var taken = new List<string>();
        for (var i = 0; i < 3; i++) taken.Add((await StartAsync($"s{i}")).Choice);

        Assert.Equal(["1", "1", "2"], taken);
        var third = await Plan().ResolveAsync(CancellationToken.None);
        Assert.Contains(third.Passes, pass => pass.Code == StaffPasses.OverBudget && pass.Entry.From == "1");
        Assert.Equal("2", third.Chosen!.From);
    }

    [Fact]
    public async Task A_role_with_one_choice_past_its_budget_waits_naming_the_day_and_an_edit_never_refills_it()
    {
        var only =StaffPolicy.Parse($$"""{"rule":"order","choices":[{{Entry(One, ",\"budget\":{\"starts\":1}")}}]}""").Policy!;
        _staffing.Set("build", [One]);
        RoleDoor.Apply(_staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(only));
        var config = ActorChoices.With(DriverConfig.Empty with { Actors = StaffedRoles.On("cool") }, "build", [One]);
        StartPlan Single() => new("build", config, _roster, _adapters, _memory) { Workspace = "default", Subject = "quest:q2", Staff = Ledger };

        var opened = await Single().ResolveAsync(CancellationToken.None);
        Assert.True(opened.Selection.Number > 0, opened.Selection.Refusal);
        Assert.NotNull(Ledger.Opened(opened.Selection.Number, "only-1"));
        var waits = await Single().ResolveAsync(CancellationToken.None);
        Assert.Null(waits.Chosen);
        Assert.Contains("over its daily budget", waits.Selection.Refusal);
        Assert.Contains("next day", waits.Selection.Refusal);

        // The day's starts stay counted through an edit: a bigger budget of 2 is met by the one start already made, a budget of 1 still holds.
        RoleDoor.Apply(_staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null,
            new RolePolicyPress(StaffPolicy.Parse($$"""{"rule":"order","choices":[{{Entry(One, ",\"budget\":{\"starts\":2}")}}]}""").Policy!));
        var next = await Single().ResolveAsync(CancellationToken.None);
        Assert.Equal("1", next.Chosen!.From);
        Assert.NotNull(Ledger.Opened(next.Selection.Number, "only-2"));
        Assert.Null((await Single().ResolveAsync(CancellationToken.None)).Chosen);
    }

    [Fact]
    public async Task A_design_start_takes_the_only_choice_taking_design_and_a_build_start_the_other()
    {
        Policy("order", ",\"takes\":[\"build\"]", ",\"takes\":[\"design\"]");

        Assert.Equal(("2", StaffBy.Work), await StartAsync("d1", RoleWork.Design));
        Assert.Equal("1", (await StartAsync("b1")).Choice);
    }

    [Fact]
    public async Task With_no_policy_and_with_an_explicit_first_row_the_walk_is_the_order_and_the_log_gains_nothing_without_a_policy()
    {
        _log = new MachineLog(_home, "driver");
        var plain = await Plan().ResolveAsync(CancellationToken.None);
        Assert.Equal("1", plain.Chosen!.From);
        Assert.Empty(plain.Passes);
        Assert.Null(Ledger.Opened(plain.Selection.Number, "plain")?.Reservation);

        Policy("order");
        for (var i = 0; i < 3; i++) Assert.Equal("1", (await StartAsync($"f{i}")).Choice);

        // The line gains its rule and its why only under a set policy; with none it is the line it always was.
        Assert.Equal(["", "order|first", "order|first", "order|first"], ChoseLines());
    }

    /// <summary>STAFFPOLICY1c2: an agent with no account count (Number 0) still writes its start's row, so its starts move the balance.</summary>
    [Fact]
    public async Task An_agent_with_no_account_count_writes_its_start_and_its_starts_move_the_weighted_balance()
    {
        ActorChoice bare = new("bare", "mb", null);
        var policy = StaffPolicy.Parse($$"""{"rule":"weight","choices":[{{Entry(bare, ",\"weight\":1")}},{{Entry(Two, ",\"weight\":1")}}]}""").Policy!;
        _staffing.Set("build", [bare, Two]);
        RoleDoor.Apply(_staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(policy));

        var taken = new List<(string Choice, long Number)>();
        for (var i = 0; i < 4; i++)
        {
            var settled = await Plan("build", bare, Two).ResolveAsync(CancellationToken.None);
            taken.Add((settled.Chosen!.From, settled.Selection.Number));
            Assert.NotNull(Ledger.Opened(settled.Selection, $"b{i}"));
        }

        Assert.Equal(0, taken[0].Number);
        Assert.Equal(["1", "2", "1", "2"], taken.Select(each => each.Choice));
        Assert.Equal(["mb", "m2", "mb", "m2"], Rows());
    }

    private sealed class Capturing(List<string> bodies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"session\":{},\"message\":\"kept\"}") };
        }
    }

    /// <summary>STAFFPOLICY1c2: the open keeps the rule and the why on the record; a role on first-that-can-run keeps none.</summary>
    [Fact]
    public async Task A_policy_start_keeps_its_rule_and_why_on_the_record_and_a_first_start_keeps_none()
    {
        var bodies = new List<string>();
        using var http = new HttpClient(new Capturing(bodies));
        using var service = new ServiceClient("http://stand-in", key: null, http);
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));

        Policy("weight", ",\"weight\":3", ",\"weight\":1");
        var weighted = await Plan().ResolveAsync(CancellationToken.None);
        await RotatedOpening.SayStaffAsync(events, Ledger, service, null, "s1", weighted.Selection, "build");
        Assert.Equal("""{"chosen":{"rule":"weight","by":"weight","condition":null,"from":null}}""", Assert.Single(bodies));

        Policy("order");
        var first = await Plan().ResolveAsync(CancellationToken.None);
        await RotatedOpening.SayStaffAsync(events, Ledger, service, null, "s2", first.Selection, "build");
        Assert.Single(bodies);
    }

    private List<string> ChoseLines()
    {
        _log?.Dispose();
        _log = null;
        var path = Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), $"{DateTime.UtcNow:yyyy-MM-dd}.driver.jsonl");
        return !File.Exists(path)
            ? []
            : [.. File.ReadLines(path).Select(line => JsonDocument.Parse(line).RootElement).Where(line => line.GetProperty("event").GetString() == "actor.chose")
                .Select(line => line.GetProperty("data")).Select(data => data.TryGetProperty("rule", out var rule) ? $"{rule.GetString()}|{data.GetProperty("by").GetString()}" : "")];
    }
}
