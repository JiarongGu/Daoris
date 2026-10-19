using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// How each agent's accounts are used, pushed (PUSH1e, D182 points 2-4 and 6): one owner in the shell's loop judging what
/// <c>ACCOUNTS</c> answers, its verdicts beside each reading, once per wake for every window, a partition per agent, so a tick and a
/// focus read <c>ACCOUNTS</c> again in no window, and a probe's result moves every window by one push.
/// </summary>
/// <remarks>Every account, reading and list is the test's own file under its home; no service runs, no agent is asked and no process starts.</remarks>
public sealed partial class AccountsStateSliceTests : DriverModuleBridge
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private (DriverLoop Loop, StateSlice Slice) Up()
    {
        var loop = Loop();
        return (loop, loop.Slices.Find(AccountsStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == AccountsStateSlice.Name),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    private void Accounts(string agent, params string[] names)
    {
        foreach (var name in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, agent, name));
    }

    private void Wiring(string json) => File.WriteAllText(HarnessSettingsPath, json);

    [Fact]
    public async Task Each_agent_is_a_partition_holding_what_the_route_answers_for_it_with_no_service()
    {
        Accounts("claude-code", "account-1", "account-2");
        Wiring("""{ "rotation": { "claude-code": ["account-1", "account-2"] } }""");
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("session", 0.95, Now.AddHours(2))], Now, "s1");
        var (loop, slice) = Up();
        var module = new DriverModule(Bus, loop);

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        var route = JsonNode.Parse((await AnswerAsync(module, "ACCOUNTS")).GetRawText())!["agents"]!.AsArray();
        var answered = await AnswerAsync(module, "STATE_SLICE", new { slice = AccountsStateSlice.Name });

        Assert.Null(loop.Service);
        Assert.Equal((AccountsStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal(route.Select(each => each!["agent"]!.GetValue<string>()), snapshot.Partitions.Select(each => each.Partition));
        Assert.Contains("claude-code", snapshot.Partitions.Select(each => each.Partition));
        // The pull's answer and the push's, written alike: the one builder, every field the page reads, the verdicts beside each reading.
        foreach (var agent in route)
        {
            var pushed = Written(snapshot.Partitions.Single(each => each.Partition == agent!["agent"]!.GetValue<string>()).Data);
            Assert.Equal(Normal(agent!), Normal(pushed));
        }

        var claude = Written(snapshot.Partitions.Single(each => each.Partition == "claude-code").Data);
        var window = claude["accounts"]![0]!["said"]!["windows"]![0]!;
        Assert.Equal((90, true), (window["curve"]!.GetValue<int>(), window["over"]!.GetValue<bool>()));
        Assert.Equal("claude-code", answered.GetProperty("partitions").EnumerateArray().First(each => each.GetProperty("partition").GetString() == "claude-code").GetProperty("partition").GetString());
    }

    /// <summary>
    /// The allow-list's twins: every field the route answers of an agent and of an account is pushed or named the pull's alone, and
    /// every field the page's <c>AgentAccounts</c> and <c>AccountFacts</c> read is pushed, read from the page's source by the
    /// declaration's name. A field the route comes to answer fails here until it is classed.
    /// </summary>
    [Fact]
    public async Task Every_field_the_route_answers_is_classed_and_every_field_the_page_reads_is_pushed()
    {
        Accounts("claude-code", "account-1");
        var route = await AnswerAsync(Module(), "ACCOUNTS");
        var agent = route.GetProperty("agents").EnumerateArray().First(each => each.GetProperty("agent").GetString() == "claude-code");
        var account = agent.GetProperty("accounts")[0];
        var source = PageFile("export type AgentAccounts = {");

        Assert.Empty(AccountsStateSlice.AgentFields.Concat(AccountsStateSlice.AccountFields).Intersect(AccountsStateSlice.PulledOnly));
        Assert.All(agent.EnumerateObject().Select(each => each.Name), field => Assert.Contains(field, AccountsStateSlice.AgentFields.Concat(AccountsStateSlice.PulledOnly)));
        Assert.All(account.EnumerateObject().Select(each => each.Name), field => Assert.Contains(field, AccountsStateSlice.AccountFields.Concat(AccountsStateSlice.PulledOnly)));
        Assert.All(Fields(source, PageAgent()), field => Assert.Contains(field, AccountsStateSlice.AgentFields));
        Assert.All(Fields(source, PageAccount()), field => Assert.Contains(field, AccountsStateSlice.AccountFields));
        Assert.Contains("said", Fields(source, PageAccount()));
    }

    /// <summary>
    /// A read's result moves the accounts' files and declares the roster's scope: the slice judges again and pushes the agent it moved,
    /// once, to every window; another agent's partition, unmoved, is pushed nothing. A judgement with nothing moved pushes nothing.
    /// </summary>
    [Fact]
    public async Task A_probe_s_result_pushes_its_agent_once_and_nothing_moved_pushes_nothing()
    {
        Accounts("claude-code", "account-1");
        Accounts("codex", "account-9");
        var (_, slice) = Up();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count >= 2);
        var first = Pushes().Count;

        var judged = slice.Judgements;
        await slice.JudgeAsync();
        Assert.Equal(first, Pushes().Count);

        // What `ACCOUNTS_READ` lands: the agent's own answer in `windows.json`, and the roster's scope declared.
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("session", 0.4, Now.AddHours(2))], Now, null, source: AccountWindows.SourceRead);
        await Bus.EmitAsync(StateSlice.Module, StatePublisher.Type, new StateChanged(["harnesses"], null));
        await UntilAsync(() => slice.Judgements > judged + 1);

        await UntilAsync(() => Pushes().Count == first + 1);
        Assert.Equal("claude-code", Pushes()[^1].Partition);
        Assert.Equal(0.4, Written(Pushes()[^1].Data)["accounts"]![0]!["said"]!["windows"]![0]!["used"]!.GetValue<double>(), 3);
        Assert.True(slice.Judgements > judged);
        await stop.CancelAsync();
        await running;
    }

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["harnesses"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions","driver"]}""", false)]
    [InlineData("SESSION_ENDED", "{}", true)]
    [InlineData("HARNESS_ENDED", "{}", true)]
    [InlineData("DRIVER_READY", "{}", true)]
    [InlineData("DRIVER_TICK", "{}", false)]
    public void What_wakes_the_accounts_slice(string type, string payload, bool wakes)
    {
        var wake = AccountsStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }

    /// <summary>
    /// Daoris's sessions running on each account, counted from the sessions the loop's records slice already holds in memory, never
    /// a read of every record of its own: unknown (null) until that slice is judged, and a move of a record that changes no count
    /// wakes nothing, while a session's start on an account wakes the slice.
    /// </summary>
    [Fact]
    public void The_running_count_is_read_from_the_records_held_and_only_a_moved_count_wakes_it()
    {
        Accounts("claude-code", "account-1");
        var loop = Loop();
        Assert.Null(AccountsStateSlice.Running(loop));

        static JsonObject Record(string id, string state, string? profile = "account-1") => new()
        {
            ["id"] = id, ["adapter"] = "claude-code-acp", ["state"] = state, ["profile"] = profile,
        };
        var held = new List<JsonObject> { Record("s1", "working"), Record("s2", "completed"), Record("team/s3", "working") };
        var counted = AccountsStateSlice.RunningOn(held, AccountsRoutes.Owners(loop.Harnesses));
        Assert.Equal(1, counted["claude-code/account-1"]);
        Assert.Single(counted);

        var woken = 0;
        var heard = AccountsStateSlice.Counter(() => AccountsStateSlice.RunningOn(held, AccountsRoutes.Owners(loop.Harnesses)), () => woken++);
        heard();
        Assert.Equal(1, woken);
        held[1] = Record("s2", "completed");
        held[1]["updated"] = "2026-10-20T09:00:00+00:00";
        heard();
        Assert.Equal(1, woken);
        held.Add(Record("s4", "working"));
        heard();
        Assert.Equal(2, woken);
    }

    /// <summary>A hostile field in a reading's file never reaches a partition: the readings are read into their own records.</summary>
    [Fact]
    public async Task A_hostile_field_in_a_file_never_reaches_a_partition()
    {
        Accounts("claude-code", "account-1");
        AccountWindows.Said(Home, "claude-code", "account-1", [new WindowReading("session", 0.4, Now.AddHours(2))], Now, "s1");
        var windows = DaorisHome.Entry(Home, HomeEntry.Windows);
        var file = JsonNode.Parse(File.ReadAllText(windows))!;
        file["token"] = "sk-hostile-top";
        File.WriteAllText(windows, file.ToJsonString());
        var (_, slice) = Up();

        await slice.JudgeAsync();

        var pushed = JsonSerializer.Serialize(Pushes().Select(push => push.Data), IpcJson.Options);
        Assert.Contains("account-1", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
    }

    /// <summary>Key order apart, as the bridge and the pump each write an object.</summary>
    private static string Normal(JsonNode node) => node switch
    {
        JsonObject each => "{" + string.Join(",", each.OrderBy(field => field.Key, StringComparer.Ordinal).Select(field => $"{field.Key}:{(field.Value is null ? "null" : Normal(field.Value))}")) + "}",
        JsonArray items => "[" + string.Join(",", items.Select(item => item is null ? "null" : Normal(item))) + "]",
        _ => node.ToJsonString(),
    };

    private static string PageFile(string declaration)
    {
        var web = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src");
        var found = Directory.EnumerateFiles(web, "*.ts", SearchOption.AllDirectories)
            .Where(file => !file.Contains(".test.", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains(declaration, StringComparison.Ordinal))
            .ToList();
        Assert.Single(found);
        return File.ReadAllText(found[0]);
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    private static IReadOnlyList<string> Fields(string source, Regex declaration)
    {
        var block = declaration.Match(source);
        Assert.True(block.Success, $"the page declares no `{declaration}`");
        var body = Comments().Replace(block.Groups["body"].Value, "");
        return [.. Field().Matches(body).Select(each => each.Groups["name"].Value)];
    }

    // The top level of the type only: a nested shape's own fields (`own: { cooling, … }`) are its own.
    [GeneratedRegex(@"export type AgentAccounts = \{(?<body>.*?)\n\};", RegexOptions.Singleline)]
    private static partial Regex PageAgent();

    [GeneratedRegex(@"export type AccountFacts = \{(?<body>.*?)\n\};", RegexOptions.Singleline)]
    private static partial Regex PageAccount();

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"^\s{2}(?<name>\w+)\??:", RegexOptions.Multiline)]
    private static partial Regex Field();
}
