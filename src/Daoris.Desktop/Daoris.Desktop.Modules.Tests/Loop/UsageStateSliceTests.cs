using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What sessions consumed, pushed (PUSH1e, D182 points 2-4): one owner in the shell's loop reading the usage file once per wake for
/// every window, a keyed slice of two partitions, each measured session under its id and each account's share under its door and
/// account, written by <c>USAGE</c>'s own builders, so neither a focus nor a mount reads <c>USAGE</c> again in a window that holds it.
/// </summary>
/// <remarks>Every reading is the test's own entry in its home's usage file; no service runs and no process starts.</remarks>
public sealed partial class UsageStateSliceTests : DriverModuleBridge
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private (DriverLoop Loop, StateSlice Slice) Up()
    {
        var loop = Loop();
        return (loop, loop.Slices.Find(UsageStateSlice.Name)!);
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == UsageStateSlice.Name),
    ];

    private static JsonObject Written(object? data) => JsonSerializer.SerializeToNode(data, IpcJson.Options)!.AsObject();

    private void Measured(string session, string? profile, long used, int minutes, string harness = "claude-code") =>
        new SessionUsage(Home).Record(new UsageEntry(session, "engine", harness, profile, used, 200_000, T0.AddMinutes(minutes)));

    [Fact]
    public async Task Each_session_and_each_account_is_pushed_as_the_route_answers_it_with_no_service()
    {
        Measured("s1", "work", 48_000, 0);
        Measured("s2", "work", 12_000, 5);
        Measured("s3", null, 9_000, 10);
        Measured("s4", "home", 1_000, 15, harness: "codex");
        var (loop, slice) = Up();
        var module = new DriverModule(Bus, loop);

        await slice.JudgeAsync();
        var snapshot = slice.Snapshot();
        var route = JsonSerializer.SerializeToNode(await AnswerAsync(module, "USAGE"), IpcJson.Options)!.AsObject();
        var answered = await AnswerAsync(module, "STATE_SLICE", new { slice = UsageStateSlice.Name });

        Assert.Null(loop.Service);
        Assert.Equal((UsageStateSlice.Name, StateSlice.Ready), (snapshot.Slice, snapshot.Status));
        Assert.Equal([UsageStateSlice.Accounts, UsageStateSlice.Sessions], snapshot.Partitions.Select(each => each.Partition));
        Assert.Equal(UsageStateSlice.Accounts, answered.GetProperty("partitions")[0].GetProperty("partition").GetString());

        // The pull's answer and the push's, written alike: each session under its id, each account under its door and account.
        var sessions = Written(snapshot.Partitions.Single(each => each.Partition == UsageStateSlice.Sessions).Data);
        Assert.Equal(["s1", "s2", "s3", "s4"], sessions.Select(each => each.Key).Order(StringComparer.Ordinal));
        foreach (var row in route["sessions"]!.AsArray())
        {
            Assert.Equal(row!.ToJsonString(), sessions[row["session"]!.GetValue<string>()]!.ToJsonString());
        }

        var accounts = Written(snapshot.Partitions.Single(each => each.Partition == UsageStateSlice.Accounts).Data);
        Assert.Equal(["claude-code", "claude-code/work", "codex/home"], accounts.Select(each => each.Key).Order(StringComparer.Ordinal));
        foreach (var row in route["accounts"]!.AsArray())
        {
            var key = row!["profile"] is { } profile ? $"{row["harness"]!.GetValue<string>()}/{profile.GetValue<string>()}" : row["harness"]!.GetValue<string>();
            Assert.Equal(row.ToJsonString(), accounts[key]!.ToJsonString());
        }

        Assert.Equal(60_000, accounts["claude-code/work"]!["used"]!.GetValue<long>());
        Assert.Equal(2, accounts["claude-code/work"]!["sessions"]!.GetValue<int>());
        // The own home's account names none, written null as the route always wrote it: the page's `profile: string | null`.
        Assert.True(accounts["claude-code"]!.AsObject().ContainsKey("profile"));
        Assert.Null(accounts["claude-code"]!["profile"]);
    }

    /// <summary>The route still answers newest first and per account, from one read of the file: an older page pulls it.</summary>
    [Fact]
    public async Task The_route_answers_newest_first_and_each_account_once()
    {
        Measured("s1", "work", 48_000, 0);
        Measured("s2", "work", 12_000, 5);

        var usage = await AnswerAsync(Module(), "USAGE");

        Assert.Equal(["s2", "s1"], usage.GetProperty("sessions").EnumerateArray().Select(each => each.GetProperty("session").GetString()));
        Assert.Single(usage.GetProperty("accounts").EnumerateArray());
    }

    /// <summary>
    /// The allow-list's twins: every field a record of the route carries is pushed, and the page's <c>UsedSession</c> and
    /// <c>UsedAccount</c> read each of them, read from the page's source by the declaration's name.
    /// </summary>
    [Fact]
    public void Every_field_the_route_answers_is_pushed_and_every_field_the_page_reads_is_one()
    {
        var bridge = PageFile("export type UsedSession = {");
        var session = Fields(bridge, PageSession());
        var account = Fields(bridge, PageAccount());
        var routeSession = Written(UsageStateSlice.Session(new UsageEntry("s1", "engine", "claude-code", "work", 1, 2, T0))).Select(each => each.Key);
        var routeAccount = Written(UsageStateSlice.Account(new AccountUsage("claude-code", "work", 1, 2))).Select(each => each.Key);

        Assert.Equal(session.Order(StringComparer.Ordinal), UsageStateSlice.SessionFields.Order(StringComparer.Ordinal));
        Assert.Equal(account.Order(StringComparer.Ordinal), UsageStateSlice.AccountFields.Order(StringComparer.Ordinal));
        Assert.Equal(UsageStateSlice.SessionFields, routeSession);
        Assert.Equal(UsageStateSlice.AccountFields, routeAccount);
    }

    /// <summary>
    /// A session's end pushes the session it measured alone, a delta on the number held, and its account's share moved; a judgement
    /// with nothing measured since pushes nothing; the oldest session let go past the file's bound is named among the keys gone.
    /// </summary>
    [Fact]
    public async Task A_new_session_pushes_itself_and_its_account_alone_and_nothing_moved_pushes_nothing()
    {
        for (var i = 0; i < 6; i++) Measured($"s{i}", i % 2 == 0 ? "work" : "home", 1_000, i);
        Measured("t1", null, 1_000, 7, harness: "codex");
        Measured("t2", "a", 1_000, 8, harness: "codex");
        Measured("t3", "b", 1_000, 9, harness: "codex");
        var (_, slice) = Up();
        await slice.JudgeAsync();
        var first = Pushes().Count;

        await slice.JudgeAsync();
        Assert.Equal(first, Pushes().Count);

        Measured("s9", "work", 2_000, 30);
        await slice.JudgeAsync();

        var moved = Pushes().Skip(first).ToList();
        var sessions = moved.Single(push => push.Partition == UsageStateSlice.Sessions);
        Assert.NotNull(sessions.BaseSeq);
        Assert.Equal(["s9"], Written(sessions.Data).Select(each => each.Key));
        var accounts = moved.Single(push => push.Partition == UsageStateSlice.Accounts);
        Assert.NotNull(accounts.BaseSeq);
        Assert.Equal(["claude-code/work"], Written(accounts.Data).Select(each => each.Key));
        Assert.Equal(4, Written(accounts.Data)["claude-code/work"]!["sessions"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("SESSION_ENDED", true)]
    [InlineData("DRIVER_READY", true)]
    [InlineData("DRIVER_TICK", false)]
    [InlineData("STATE_CHANGED", false)]
    public void What_wakes_the_usage_slice(string type, bool wakes) =>
        Assert.Equal(wakes, UsageStateSlice.WakesOn.Any(each => each.Type == type && each.When is null));

    /// <summary>Run as the loop runs it: a session's end wakes it, and what the end measured reaches the windows.</summary>
    [Fact]
    public async Task A_session_s_end_reaches_the_windows_by_one_push()
    {
        var (loop, slice) = Up();
        using var stop = new CancellationTokenSource();
        var running = slice.RunAsync(stop.Token);
        await UntilAsync(() => Pushes().Count == 2);

        Measured("s1", "work", 5_000, 0);
        await Bus.EmitAsync(StateSlice.Module, DriverLoop.EndedType, new { Session = "s1" });

        await UntilAsync(() => Pushes().Count >= 4);
        Assert.Contains(Pushes(), push => push.Partition == UsageStateSlice.Sessions && Written(push.Data).ContainsKey("s1"));
        await stop.CancelAsync();
        await running;
        Assert.NotNull(loop);
    }

    /// <summary>The page's file declaring <paramref name="declaration"/>, found by what it declares rather than by its folder.</summary>
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
        return [.. PageField().Matches(body).Select(each => each.Groups["name"].Value)];
    }

    [GeneratedRegex(@"export type UsedSession = \{(?<body>.*?)\n\};", RegexOptions.Singleline)]
    private static partial Regex PageSession();

    [GeneratedRegex(@"export type UsedAccount = \{(?<body>.*?)\n\};", RegexOptions.Singleline)]
    private static partial Regex PageAccount();

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"(?<name>\w+)\??:")]
    private static partial Regex PageField();
}
