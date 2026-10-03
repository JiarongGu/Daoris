using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4f (D125 §3) through REAL ticks: with account-1 cooling and the order <c>[account-1, account-2]</c>, the carry-on
/// of a quest a limit cut off runs on account-2, in the same tree; its record names account-2; its note names the cut-off
/// session and no account; its conversation record opens saying both; the log says <c>account.rotated</c>; and once
/// account-1 is ready again, the next start takes it.
/// </summary>
/// <remarks>
/// <para>On the pipe stub first, whose door carries text alone, so the limit there is replayed as the driver records one,
/// its account cooled by the roster's reader and its record saying <c>limit</c>. The stub agent answers the sign-in
/// question for each account from the directory it is handed, and writes down where it ran, as which account, what it
/// was handed and what its record's note said while it ran.</para>
///
/// <para>Then on the protocol door (TOOL4j), where the limit is the door's own failure: the protocol stub runs as the
/// stub's accounts, so its refused turn cools stub account 1 through the driver's own conclusion, and the carry-on opens
/// on stub account 2.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AccountRotationTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>Observation 4's reset, read: 3 October, 16:02 in the test's zone.</summary>
    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private static readonly string Refusal =
        "the ACP agent refused the call: Internal error: You've hit your individual spend limit · run /usage-credits to ask "
        + $"your admin for a higher limit · your weekly limit resets Oct 3, 4pm ({Zone.Id})";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-rotation-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public AccountRotationTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "rotation@example.com");
        Git("config", "user.name", "TOOL4f");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");

        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .Save(Path.Combine(_home, "harnesses.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private DriverConfig Config(string signedOut = "-") => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "stub",
        TimeoutMinutes = 1,
        PollSeconds = 1,
        // Two failures park it, and the quest already holds one: a limit must hold none.
        Strikes = 2,
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), Log, signedOut] },
    };

    [Fact]
    public async Task A_carry_on_whose_account_is_cooling_runs_on_the_next_account_of_the_order_in_the_same_tree()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var config = Config();
        var driver = new Daoris.Driver.Driver(client, config, adapters, _home, processes: new SessionProcesses(), harnesses: roster);
        // What the person answered an earlier session on the ask (DRIFT1b): no record this chain holds keeps it.
        service.Words = new JsonArray(
            new JsonObject { ["kind"] = "asked", ["text"] = "Add the note field", ["at"] = "2026-10-01T08:00:00+00:00" },
            new JsonObject
            {
                ["kind"] = "answered", ["text"] = "Keep the note beside the title, not under it.", ["at"] = "2026-10-01T08:10:00+00:00",
                ["session"] = "s0", ["quest"] = "q1",
            });

        // The first session runs on account-1, the default, takes the quest and is cut off.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        var cut = service.Session("s1");
        Assert.Equal(("failed", "account-1"), (cut["state"]!.GetValue<string>(), cut["profile"]!.GetValue<string>()));
        var tree = cut["tree"]!.GetValue<string>();

        // The limit, as the driver records one: account-1 cools until the reset the agent named, and the record says so.
        var limited = roster.Limited("stub", "account-1", Refusal, "s1");
        Assert.NotNull(limited);
        await client.AdvanceAsync("s1", "failed", note: cut["note"]!.GetValue<string>(), limit: true);

        // The carry-on rotates to account-2, in the same tree, and closes the quest.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var carried = service.Session("s2");
        Assert.Equal("completed", carried["state"]!.GetValue<string>());
        Assert.Equal("account-2", carried["profile"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(carried["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal("Done", service.Status("q1"));

        var runs = File.ReadAllLines(Log).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(["account-1", "account-2"], runs.Select(run => run["account"]!.GetValue<string>()));
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(runs[1]["cwd"]!.GetValue<string>()), ignoreCase: true);

        // While it ran, its note, which travels, named the cut-off session and no account.
        var note = runs[1]["note"]!.GetValue<string>();
        Assert.Contains("after session `s1` was cut off, on another account", note);
        Assert.DoesNotContain("account-1", note);
        Assert.DoesNotContain("account-2", note);

        // It was handed the cut-off session's last words and told its account changed, naming none.
        var handed = runs[1]["target"]!.GetValue<string>();
        Assert.Contains("carrying on quest `#q1`", handed);
        Assert.Contains("Its last words were:\n\n> I started the field.", handed.ReplaceLineEndings("\n"));
        Assert.Contains("It ran on another account, which reached its limit and is cooling", handed);
        Assert.DoesNotContain("account-1", handed);
        // And the person's words on the ask, read from it for this start (DRIFT1b): the answer outlives the hop.
        Assert.Contains("- They answered a session on this quest, 2026-10-01 08:10 UTC:\n\n  > Keep the note beside the title, not under it.",
            handed.ReplaceLineEndings("\n"));

        // Its conversation record opens saying both accounts, the cut-off session and its refused turn, and that no account
        // has said what it has left (TOOL6b, D130 §16.4): the goal's walk chose it, by the step that passed account-1.
        var opening = new SessionEvents(Path.Combine(_home, "sessions")).After("s2", 0).Events[0];
        Assert.Equal(SessionEventKind.Note, opening.Kind);
        Assert.Equal(
            $"carried on from session `s1` on `account-2`; the `stub` account `account-1` is cooling until Oct 3, 16:02 "
            + $"({Zone.Id}), as the agent said; its turn 1 was refused. No account has said what it has left yet.",
            opening.Text);

        lock (lines)
        {
            var rotated = Assert.Single(lines, line => line.Event == "account.rotated");
            Assert.Equal(
                new object?[] { "s2", "stub", "account-1", "account-2", "s1", "cooling", null, false, null, null },
                rotated.Data.Select(field => field.Value));
        }

        // Once account-1 is ready again, the next start takes it.
        Assert.True(roster.Ready("stub", "account-1"));
        var next = await roster.SelectAsync("stub", config, "default", null);
        Assert.Equal(("account-1", (RotatedStart?)null), (next.Profile, next.Rotated));
    }

    /// <summary>
    /// TOOL4j (D125 §1.3 rule 4, §8): on the protocol door, the refused turn itself is the limit. The first session runs on
    /// stub account 1, takes the quest and is refused with observation 4's sentence; the driver reads it, the record says
    /// <c>limit</c> and stub account 1 cools until the reset it names, for both doors onto it. The carry-on, which a strike
    /// would have parked, opens on stub account 2 in the same tree and closes the quest.
    /// </summary>
    [Fact]
    public async Task A_limit_on_the_protocol_door_cools_stub_account_1_and_the_carry_on_opens_on_stub_account_2()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            // One failure would park it: the limit must hold none.
            Strikes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", AcpAgent(), Log] },
        };
        var driver = new Daoris.Driver.Driver(client, config, adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        // The first session runs on stub account 1, the default, takes the quest, and its turn is refused for the limit.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        var cut = service.Session("s1");
        Assert.Equal(("failed", "account-1"), (cut["state"]!.GetValue<string>(), cut["profile"]!.GetValue<string>()));
        Assert.True(cut["limit"]!.GetValue<bool>());
        var cutNote = cut["note"]!.GetValue<string>();
        Assert.Contains($"The account it ran on is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said", cutNote);
        Assert.DoesNotContain("account-1", cutNote);
        var tree = cut["tree"]!.GetValue<string>();

        // Stub account 1 cools until the reset the agent named, read by the driver from the door's failure: one account,
        // so the pipe door onto it is held too.
        var cooling = roster.CoolingOf("acp-stub", "account-1")!;
        Assert.Equal(("stub", "account-1", Until, true, "s1"), (cooling.Agent, cooling.Account, cooling.Until, cooling.Stated, cooling.Session));
        Assert.Equal(cooling, roster.CoolingOf("stub", "account-1"));
        Assert.Null(roster.CoolingOf("acp-stub", "account-2"));
        lock (lines)
        {
            var limited = Assert.Single(lines, line => line.Event == "account.limited");
            Assert.Equal(
                new object?[] { "s1", "acp-stub", "account-1" },
                limited.Data.Take(3).Select(field => field.Value));
        }

        // The carry-on opens on stub account 2, in the same tree, and closes the quest.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var carried = service.Session("s2");
        Assert.Equal("completed", carried["state"]!.GetValue<string>());
        Assert.Equal("account-2", carried["profile"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(carried["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal("Done", service.Status("q1"));

        // Each run was handed its stub account through the stub's own variable.
        var runs = File.ReadAllLines(Log).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(["account-1", "account-2"], runs.Select(run => run["account"]!.GetValue<string>()));
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(runs[1]["cwd"]!.GetValue<string>()), ignoreCase: true);

        // It was told its last session ran on another account, now cooling, naming none; its note, which travels, too.
        var handed = runs[1]["prompt"]!.GetValue<string>();
        Assert.Contains("carrying on quest `#q1`", handed);
        Assert.Contains("It ran on another account, which reached its limit and is cooling", handed);
        Assert.DoesNotContain("account-1", handed);
        var note = runs[1]["note"]!.GetValue<string>();
        Assert.Contains("after session `s1` was cut off, on another account", note);
        Assert.DoesNotContain("account-1", note);
        Assert.DoesNotContain("account-2", note);

        // Its conversation record opens naming both stub accounts, the cut-off session and the turn the limit refused.
        var opening = new SessionEvents(Path.Combine(_home, "sessions")).After("s2", 0).Events[0];
        Assert.Equal(SessionEventKind.Note, opening.Kind);
        Assert.Equal(
            $"carried on from session `s1` on `account-2`; the `stub` account `account-1` is cooling until Oct 3, 16:02 "
            + $"({Zone.Id}), as the agent said; its turn 1 was refused. No account has said what it has left yet.",
            opening.Text);

        lock (lines)
        {
            var rotated = Assert.Single(lines, line => line.Event == "account.rotated");
            Assert.Equal(
                new object?[] { "s2", "acp-stub", "account-1", "account-2", "s1", "cooling", null, false, null, null },
                rotated.Data.Select(field => field.Value));
        }

        // Once stub account 1 is ready again, the protocol door's next start takes it.
        Assert.True(roster.Ready("acp-stub", "account-1"));
        var next = await roster.SelectAsync("acp-stub", config, "default", null);
        Assert.Equal(("account-1", (RotatedStart?)null), (next.Profile, next.Rotated));
    }

    /// <summary>An account the agent says is signed out is walked past as a cooling one is (§3.3), once the probe says so.</summary>
    [Fact]
    public async Task A_signed_out_default_the_order_lists_is_walked_past_to_the_next_account()
    {
        var roster = new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone };

        var selection = await roster.SelectAsync("stub", Config(signedOut: "account-1"), "default", null);

        Assert.True(selection.Allowed);
        Assert.Equal("account-2", selection.Profile);
        Assert.Equal(("account-1", "the `stub` account `account-1` is not signed in"), (selection.Rotated!.From, selection.Rotated.Why));
    }

    /// <summary>With every account of the order signed out and none cooling, the default's own refusal names the fix.</summary>
    [Fact]
    public async Task An_order_all_signed_out_is_refused_with_the_default_s_own_sentence()
    {
        var roster = new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone };

        var selection = await roster.SelectAsync("stub", Config(signedOut: "account-1,account-2"), "default", null);

        Assert.False(selection.Allowed);
        Assert.Contains("`daoris agent login stub --profile account-1`", selection.Refusal);
        Assert.Null(selection.Cooling);
    }

    // ——— MSG1g (D137 §2.2's account paragraph): a resume asks for its record's own account.

    private string ResumeLog => Path.Combine(_home, "resume.log");

    private JsonObject[] Frames() => [.. StubFile.Lines(ResumeLog).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(Path.Combine(_home, "sessions")).After(session, 0).Events;

    /// <summary>The protocol door onto the stub's accounts, its agent resuming conversations; <paramref name="signedOut"/> as the stub's sign-in answers it.</summary>
    private DriverConfig Resuming(string mode, string signedOut = "-") => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "acp-stub",
        TimeoutMinutes = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>>
        {
            ["acp-stub"] = ["node", ResumingAgent(), ResumeLog, mode],
            // The stub answers its accounts' sign-in question for its protocol door (AGT7).
            ["stub"] = ["node", Agent(), Log, signedOut],
        },
    };

    private static string Profile(SessionMessagesTickTests.SaidStandIn service, string session) =>
        JsonNode.Parse(service.Record(session))!["profile"]!.GetValue<string>();

    /// <summary>
    /// 🔴 The goal's walk (D130) would start on <c>account-2</c>, which Daoris has not started on yet; words to a session that
    /// ran on <c>account-1</c> resume its own conversation on <c>account-1</c>, in the same record.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_go_on_on_its_own_account_where_the_walk_would_start_on_another()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var driver = new Daoris.Driver.Driver(client, Resuming("closes"), adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(("completed", "Done", "account-1"), (service.State("s1"), service.Status("q1"), Profile(service, "s1")));
        Assert.Equal("account-2", roster.Next("stub", "default").Account);

        await service.SayAsync("s1", "Also log the port.");
        var look = await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        var resumed = Assert.Single(Frames(), frame => frame["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("account-1", resumed["account"]!.GetValue<string>());
        Assert.Equal([("s1", "w1", (string?)null)], service.Taken);
        Assert.Equal("completed", service.State("s1"));
    }

    /// <summary>
    /// 🔴 Its own account cooling, the words wait for its reset: the look holds the start with the account's cool-off, so the
    /// quest waits for an account (TOOL4g) and the record's conversation says why once, with the door out; nothing is spawned
    /// and the words stay waiting, unmarked. The person then chooses a new session (MSG1g's door), and the next look carries the
    /// words on, on <c>account-2</c>, in the same tree, its note saying it was their choice and naming no account.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_whose_account_is_cooling_wait_for_its_reset_until_the_person_chooses_a_new_session()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var driver = new Daoris.Driver.Driver(client, Resuming("fails"), adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(("failed", "Taken", "account-1"), (service.State("s1"), service.Status("q1"), Profile(service, "s1")));
        var tree = JsonNode.Parse(service.Record("s1"))!["tree"]!.GetValue<string>();
        AccountCooling.Cool(_home, new CoolingEntry("stub", "account-1", Until, true, "weekly", Seen, "s0"), Seen);
        await service.SayAsync("s1", "Also log the port.");

        var held = await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var seen = $"look: {string.Join(" | ", held.Events)}\nafter: {service.Record("s1")}";
        var verdict = Assert.Single(held.Considerations, c => c.Quest.Id == "q1");
        Assert.True(verdict.Verdict == StartVerdict.Blocked, seen);
        Assert.StartsWith(
            $"the `stub` account `account-1` is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said, and its conversation is "
            + "on that account, so your words wait to go on in it then.",
            verdict.Reason);
        Assert.EndsWith("`daoris-driver sessions go-on-new s1`.", verdict.Reason);
        var wait = Assert.Single(held.Waits);
        Assert.Equal(("account-1", Until), (wait.Account, wait.Until));
        Assert.Equal(["q1"], wait.Quests);
        Assert.Equal(1, service.SessionCount);
        Assert.Equal(["w1"], service.Said("s1"));
        Assert.Null(new GoOnMarks(_home).Read("s1"));
        Assert.DoesNotContain(Frames(), frame => frame["method"]!.GetValue<string>() == "session/resume");
        Assert.Single(Events("s1"), e => e.Text?.StartsWith("— it does not go on yet: the `stub` account `account-1` is cooling", StringComparison.Ordinal) == true);

        // A second look while it cools holds it again, and says nothing more in its conversation.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Single(Events("s1"), e => e.Text?.StartsWith("— it does not go on yet:", StringComparison.Ordinal) == true);
        Assert.Equal(1, service.SessionCount);

        var chose = await GoOnNew.AskAsync(client, _home, (adapter, profile) => roster.CoolingOf(adapter ?? "acp-stub", profile), "s1", Seen);
        Assert.True(chose.Sent, chose.Message);
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        seen = $"after: {service.Record("s1")}";
        Assert.True(service.SessionCount == 2, seen);
        Assert.Equal(("completed", "account-2", "Done"), (service.State("s2"), Profile(service, "s2"), service.Status("q1")));
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(JsonNode.Parse(service.Record("s2"))!["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal([("s1", "w1", "s2")], service.Taken);
        var went = Assert.Single(Events("s1"), e => e.To == "s2");
        Assert.Equal(ContinueWhy.Account, went.Why);
        Assert.EndsWith("this start runs on another. You chose a new session over waiting for that account.", went.Text);

        var carrying = Frames().Last(frame => frame["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal("account-2", carrying["account"]!.GetValue<string>());
        Assert.Contains("Also log the port.", carrying["prompt"]!.GetValue<string>());
        var note = carrying["note"]!.GetValue<string>();
        Assert.Contains(
            "A new session, because its conversation stays with the account it ran on, and this start runs on another. "
            + "You chose a new session over waiting for that account.",
            note);
        Assert.DoesNotContain("account-1", note);
        Assert.Null(new NewSessionChoices(_home).Read("s1"));
    }

    /// <summary>
    /// 🔴 Its own account signed out, which only the probe a start makes tells: the words are carried on at once on
    /// <c>account-2</c>, handed them, saying why in a coded line that names no account.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_whose_account_is_signed_out_are_carried_on_at_once_on_the_next_account()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        using var client = new ServiceClient(service.Url, null);
        var first = new Daoris.Driver.Driver(
            client, Resuming("fails"), adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone });
        await first.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(("failed", "account-1"), (service.State("s1"), Profile(service, "s1")));
        await service.SayAsync("s1", "Also log the port.");

        // account-1 signs out; a fresh roster asks the agent again, as the next start's probe does.
        var signedOut = new Daoris.Driver.Driver(
            client, Resuming("fails", signedOut: "account-1"), adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => Seen, Zone = Zone });
        var look = await signedOut.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 2, seen);
        Assert.Equal(("account-2", "Done"), (Profile(service, "s2"), service.Status("q1")));
        Assert.DoesNotContain(Frames(), frame => frame["method"]!.GetValue<string>() == "session/resume");
        var went = Assert.Single(Events("s1"), e => e.To == "s2");
        Assert.EndsWith("this start runs on another. That account is not signed in any more.", went.Text);
        var note = Frames().Last(frame => frame["method"]!.GetValue<string>() == "session/prompt")["note"]!.GetValue<string>();
        Assert.Contains("That account is not signed in any more.", note);
        Assert.DoesNotContain("account-1", note);
    }

    /// <summary>
    /// The protocol door's stand-in that resumes (MSG1g): it names its conversation <c>conv-&lt;session&gt;</c> and writes down
    /// each frame with the stub account it runs as, what it was prompted and what its record's note said. A first prompt takes
    /// the quest and, by its mode, closes it (<c>closes</c>) or fails its turn with the quest still taken (<c>fails</c>); a
    /// carry-on closes it; a resumed prompt answers.
    /// </summary>
    private string ResumingAgent()
    {
        var script = Path.Combine(_home, "resuming-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const note = async () => {
              const sessions = await (await fetch(`${url}/api/sessions`)).json();
              return sessions.find((s) => s.id === session)?.note ?? null;
            };
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session, account }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            let resumed = false;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text).join('\n');
                heard({ method: 'session/prompt', prompt, resumed, note: await note() });
                if (resumed) {
                  say('Logged the port, as you said.');
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else if (prompt.includes('carrying on quest')) {
                  await respond({ action: 'done', reason: 'carried on' });
                  say('Done.');
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else if (mode === 'fails') {
                  await respond({ action: 'take' });
                  say('I started the field.');
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: the model fell over.' } });
                } else {
                  await respond({ action: 'take' });
                  await respond({ action: 'done', reason: 'served the report' });
                  say('Served.');
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>
    /// The stand-in harness. Asked its version or an account's sign-in, it answers, signed out for the accounts named in
    /// its second argument. Run, it writes down where it ran, as which account, what it was handed and what its record's
    /// note said; then a first start takes the quest, says its last words and is cut off, and a carry-on closes it.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log, signedOut] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) {
              console.log((signedOut ?? '').split(',').includes(account) ? 'logged-out' : `logged-in as ${account}`);
              process.exit(0);
            }
            const url = process.env.DAORIS_SERVICE_URL;
            const target = process.env.DAORIS_TARGET ?? '';
            const sessions = await (await fetch(`${url}/api/sessions`)).json();
            const own = sessions.find((s) => s.id === process.env.DAORIS_SESSION_ID);
            appendFileSync(log, JSON.stringify({ cwd: process.cwd(), account, target, note: own?.note ?? null }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            if (!target.includes('carrying on quest')) {
              await respond({ action: 'take' });
              console.log('I started the field.');
              process.exit(1);
            }
            await respond({ action: 'done', reason: 'finished on the next account of the order' });
            """);
        return script;
    }

    /// <summary>
    /// The protocol door's stand-in (TOOL4j). It speaks the wire and nothing else on stdout, and for each prompt writes
    /// down where it ran, as which stub account, what it was prompted and what its record's note said. A first prompt takes
    /// the quest, says its last words and is refused with observation 4's sentence, its zone the test's; a carry-on closes
    /// the quest.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const url = process.env.DAORIS_SERVICE_URL;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n');
                const sessions = await (await fetch(`${url}/api/sessions`)).json();
                const own = sessions.find((s) => s.id === process.env.DAORIS_SESSION_ID);
                appendFileSync(log, JSON.stringify({ cwd: process.cwd(), account, prompt, note: own?.note ?? null }) + '\n');
                if (!prompt.includes('carrying on quest')) {
                  await respond({ action: 'take' });
                  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1',
                    update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'I started the field.' } } } });
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm ({{Zone.Id}})" } });
                } else {
                  await respond({ action: 'done', reason: 'finished on the next stub account of the order' });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
