using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// TERMTRUST1a part (ii) (D201, the terminal-trust design §2, §4 and §8 case 1): the standing confirmation's judge. Each press in its
/// verb's form, from a caller in no agent job, is covered; each near miss asks with its code; no other route of the person's is ever
/// covered; a resume is covered only for a hold a covered hold made this run; and a grant names its own presses.
/// </summary>
/// <remarks>
/// The judge alone, its caller's look a stand-in (<see cref="Placed"/>): where the terminal's process stands is
/// <c>StandingCallerTests</c>' and the pipe's end <c>PipeConfirmTests</c>'.
/// </remarks>
public sealed class StandingConfirmationTests : Bridge
{
    private const string Driver = DriverModule.Name;

    private static readonly string Machine = StandingConfirmation.ThisMachine();

    /// <summary>A caller's look that answers one place for every client.</summary>
    internal sealed class Placed(StandingPlace place) : IStandingCaller
    {
        public StandingPlace Answer { get; set; } = place;

        public StandingPlace Place(PipeClientProcess? client) => Answer;
    }

    private StandingConfirmation Granted(
        StandingPlace place = StandingPlace.Outside, IReadOnlyList<string>? presses = null, HashSet<string>? held = null,
        HashSet<string>? running = null, MachineLog? log = null)
    {
        var standing = new StandingConfirmation(Home, log, new Placed(place), offered: true)
        {
            HeldNow = repository => held?.Contains(repository) == true,
            RunsHere = id => running?.Contains(id) == true,
        };
        standing.Load();
        Assert.True(standing.Set(presses ?? StandingPresses.Built).Done);
        return standing;
    }

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static StandingJudgement Judge(StandingConfirmation standing, string route, string json) =>
        standing.Judge(Driver, route, Payload(json), null);

    public static TheoryData<string, string, string> Forms => new()
    {
        { "STOP_SESSION", """{"id":"s1a2b3c4","said":true}""", StandingPresses.Stop },
        { "RESOLVE_SESSION", """{"id":"s1a2b3c4","state":"completed","said":true}""", StandingPresses.Finish },
        { "SET_HOLD", """{"repository":"engine","held":true}""", StandingPresses.Hold },
    };

    [Theory]
    [MemberData(nameof(Forms))]
    public void Each_press_in_its_verbs_form_from_outside_every_agent_job_is_covered(string route, string json, string press)
    {
        var standing = Granted();

        var judged = Judge(standing, route, json);

        Assert.Equal(new StandingJudgement(true, null, press, true), judged);
        Assert.Equal(1, standing.Now().Used);
    }

    public static TheoryData<string, string, string> NearMisses => new()
    {
        // A finish with the person's words, without `said` (the page's form), declining, or one field more.
        { "RESOLVE_SESSION", """{"id":"s1","state":"completed","said":true,"note":"Merged by hand."}""", StandingWhy.Form },
        { "RESOLVE_SESSION", """{"id":"s1","state":"completed"}""", StandingWhy.Form },
        { "RESOLVE_SESSION", """{"id":"s1","state":"declined","said":true}""", StandingWhy.Form },
        { "RESOLVE_SESSION", """{"id":"s1","state":"stopped","said":true}""", StandingWhy.Form },
        // A stop with an extra field, with `said` false or a string, with no id, an empty one, or one padded.
        { "STOP_SESSION", """{"id":"s1","said":true,"keep":true}""", StandingWhy.Form },
        { "STOP_SESSION", """{"id":"s1","said":false}""", StandingWhy.Form },
        { "STOP_SESSION", """{"id":"s1","said":"true"}""", StandingWhy.Form },
        { "STOP_SESSION", """{"said":true}""", StandingWhy.Form },
        { "STOP_SESSION", """{"id":"","said":true}""", StandingWhy.Form },
        { "STOP_SESSION", """{"id":" s1","said":true}""", StandingWhy.Form },
        // A name twice, in two casings: the bridge reads names ignoring case, so it is no form.
        { "STOP_SESSION", """{"id":"s1","ID":"s2","said":true}""", StandingWhy.Form },
        { "SET_HOLD", """{"repository":"engine","held":1}""", StandingWhy.Form },
        { "SET_HOLD", """{"repository":"engine","held":true,"why":"x"}""", StandingWhy.Form },
        // The keeping stop, a work's resume and Try again are never named (§2.2).
        { "CANCEL_TURN", """{"id":"s1"}""", StandingWhy.NotNamed },
        { "WORK_RESUME", """{"ask":"a1"}""", StandingWhy.NotNamed },
        { "RETRY_QUEST", """{"quest":"q1"}""", StandingWhy.NotNamed },
    };

    [Theory]
    [MemberData(nameof(NearMisses))]
    public void Each_near_miss_asks_with_its_code(string route, string json, string why)
    {
        var standing = Granted();

        var judged = Judge(standing, route, json);

        Assert.False(judged.Covered);
        Assert.Equal(why, judged.Why);
        Assert.Equal(0, standing.Now().Used);
    }

    /// <summary>A census sweep: no route of the person's but the table's three is ever covered, whatever it carries.</summary>
    [Fact]
    public void No_other_route_of_the_persons_is_ever_covered()
    {
        var standing = Granted();
        var routes = PipeCensus.Daoris.Routes.Where(route => route.Class == PipeClass.Person && route.Route is not null).ToList();
        Assert.NotEmpty(routes);

        foreach (var route in routes.Where(each => each.Route is not ("STOP_SESSION" or "RESOLVE_SESSION" or "SET_HOLD")))
        {
            foreach (var json in new[]
            {
                """{"id":"s1","said":true}""", """{"id":"s1","state":"completed","said":true}""", """{"repository":"engine","held":true}""",
            })
            {
                Assert.False(standing.Judge(route.Module, route.Route!, Payload(json), null).Covered, route.Route);
            }
        }

        // The table's own routes, sent as another module's, are not the table's.
        Assert.False(standing.Judge("DAORIS.BROWSER", "STOP_SESSION", Payload("""{"id":"s1","said":true}"""), null).Covered);
        Assert.Equal(0, standing.Now().Used);
    }

    [Fact]
    public void A_finish_of_a_session_with_a_process_here_asks_running_here()
    {
        var standing = Granted(running: ["s1"]);

        Assert.Equal(StandingWhy.RunningHere, Judge(standing, "RESOLVE_SESSION", """{"id":"s1","state":"completed","said":true}""").Why);
        Assert.True(Judge(standing, "RESOLVE_SESSION", """{"id":"s2","state":"completed","said":true}""").Covered);
    }

    [Theory]
    [InlineData(StandingPlace.InAgentJob, StandingWhy.InAgentJob)]
    [InlineData(StandingPlace.Untold, StandingWhy.Untold)]
    public void A_caller_in_an_agent_job_or_one_it_cannot_place_asks(StandingPlace place, string why)
    {
        var standing = Granted(place);

        var judged = Judge(standing, "STOP_SESSION", """{"id":"s1","said":true}""");

        Assert.Equal((false, why, true), (judged.Covered, judged.Why, judged.Said));
    }

    /// <summary>§4.2: a resume is covered only for a hold a covered hold made from not held to held during this run, and is its memory's once.</summary>
    [Fact]
    public void A_resume_is_covered_only_for_a_hold_a_covered_hold_made_this_run()
    {
        var held = new HashSet<string>();
        var standing = Granted(held: held);
        const string Resume = """{"repository":"engine","held":false}""";

        // Not held by this run at all.
        Assert.Equal(StandingWhy.NotHeldHere, Judge(standing, "SET_HOLD", Resume).Why);

        Assert.True(Judge(standing, "SET_HOLD", """{"repository":"engine","held":true}""").Covered);
        held.Add("engine");
        Assert.Equal(["engine"], standing.Now().Held);
        Assert.True(Judge(standing, "SET_HOLD", Resume).Covered);
        held.Remove("engine");
        Assert.Empty(standing.Now().Held);
        // Once: the memory is the resume's.
        held.Add("engine");
        Assert.Equal(StandingWhy.NotHeldHere, Judge(standing, "SET_HOLD", Resume).Why);
    }

    [Fact]
    public void A_resume_of_a_hold_from_before_the_start_or_one_the_window_touched_since_asks()
    {
        // Held before the start: a covered hold of a repository already held remembers nothing.
        var held = new HashSet<string> { "engine", "game" };
        var standing = Granted(held: held);
        Assert.True(Judge(standing, "SET_HOLD", """{"repository":"engine","held":true}""").Covered);
        Assert.Equal(StandingWhy.NotHeldHere, Judge(standing, "SET_HOLD", """{"repository":"engine","held":false}""").Why);

        // Held by a covered hold, then released and held again by the window: forgotten.
        held.Remove("game");
        Assert.True(Judge(standing, "SET_HOLD", """{"repository":"game","held":true}""").Covered);
        held.Add("game");
        standing.Forget("game");
        Assert.Equal(StandingWhy.NotHeldHere, Judge(standing, "SET_HOLD", """{"repository":"game","held":false}""").Why);

        // Held by a covered hold, then released by someone else (the file no longer holds it): forgotten too.
        held.Remove("tools");
        Assert.True(Judge(standing, "SET_HOLD", """{"repository":"tools","held":true}""").Covered);
        Assert.Equal(StandingWhy.NotHeldHere, Judge(standing, "SET_HOLD", """{"repository":"tools","held":false}""").Why);

        // And a start remembers nothing.
        held.Remove("again");
        Assert.True(Judge(standing, "SET_HOLD", """{"repository":"again","held":true}""").Covered);
        held.Add("again");
        standing.Load();
        Assert.Equal(StandingWhy.NotHeldHere, Judge(standing, "SET_HOLD", """{"repository":"again","held":false}""").Why);
    }

    [Fact]
    public void The_grant_off_covers_nothing_and_says_no_standing_why()
    {
        var standing = Granted();
        Assert.True(standing.Set([]).Done);

        var judged = Judge(standing, "STOP_SESSION", """{"id":"s1","said":true}""");

        Assert.Equal((false, StandingWhy.Off, false), (judged.Covered, judged.Why, judged.Said));
        Assert.False(standing.Now().On);
    }

    /// <summary>§2.3: a grant naming fewer presses covers those alone; one it does not name asks, and says nothing of standing.</summary>
    [Fact]
    public void A_grant_naming_fewer_presses_covers_those_alone()
    {
        var standing = Granted(presses: [StandingPresses.Stop]);

        Assert.True(Judge(standing, "STOP_SESSION", """{"id":"s1","said":true}""").Covered);
        var finish = Judge(standing, "RESOLVE_SESSION", """{"id":"s1","state":"completed","said":true}""");
        Assert.Equal((false, StandingWhy.NotNamed, false), (finish.Covered, finish.Why, finish.Said));
        Assert.Equal([StandingPresses.Stop], standing.Now().Presses);
    }

    /// <summary>§2.3: a press a later build adds, which the record names, covers nothing this build's table does not name.</summary>
    [Fact]
    public void A_press_the_build_does_not_name_is_never_granted_or_read()
    {
        var standing = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: true);
        Assert.Equal(StandingConfirmation.UnknownPress, standing.Set(["stop", "done"]).Why);
        Assert.False(File.Exists(standing.RecordPath));

        Directory.CreateDirectory(Path.GetDirectoryName(standing.RecordPath)!);
        File.WriteAllText(
            standing.RecordPath,
            $$"""{"schemaVersion":1,"presses":["done","hold"],"since":"2026-10-23T06:05:00Z","machine":"{{Machine}}"}""");
        standing.Load();

        Assert.Equal([StandingPresses.Hold], standing.Now().Presses);
        Assert.False(standing.Judge(Driver, "QUEST_DONE", Payload("""{"quest":"q1","said":true}"""), null).Covered);
    }

    /// <summary>D201's note on order: until this build offers it, a grant is refused as data, nothing is written, and a record covers nothing.</summary>
    [Fact]
    public void A_build_that_does_not_offer_it_refuses_a_grant_and_covers_nothing()
    {
        Assert.False(StandingConfirmation.OfferedInThisBuild);
        var standing = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside));
        Assert.Equal(StandingConfirmation.NotOffered, standing.Set(StandingPresses.Built).Why);
        Assert.False(File.Exists(standing.RecordPath));

        Directory.CreateDirectory(Path.GetDirectoryName(standing.RecordPath)!);
        File.WriteAllText(
            standing.RecordPath, $$"""{"schemaVersion":1,"presses":["stop"],"since":"2026-10-23T06:05:00Z","machine":"{{Machine}}"}""");
        standing.Load();

        Assert.False(standing.Now().On);
        Assert.Equal(StandingWhy.Off, standing.Judge(Driver, "STOP_SESSION", Payload("""{"id":"s1","said":true}"""), null).Why);
        // A revoke is always taken.
        Assert.True(standing.Set([]).Done);
    }

    // ——— The record (§4.2, §8 case 10).

    [Fact]
    public void A_grant_is_written_and_read_back_at_the_next_start_and_a_revoke_writes_it_off()
    {
        var clock = new HeldClock();
        var first = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), clock, offered: true);
        first.Load();
        Assert.True(first.Set([StandingPresses.Hold, StandingPresses.Stop]).Done);

        using (var document = JsonDocument.Parse(File.ReadAllText(first.RecordPath)))
        {
            var root = document.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(["stop", "hold"], root.GetProperty("presses").EnumerateArray().Select(each => each.GetString()));
            Assert.Equal(Machine, root.GetProperty("machine").GetString());
            Assert.Equal(clock.GetUtcNow(), root.GetProperty("since").GetDateTimeOffset());
        }

        var next = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: true);
        next.Load();
        Assert.Equal((true, clock.GetUtcNow()), (next.Now().On, next.Now().Since!.Value));
        Assert.Equal([StandingPresses.Stop, StandingPresses.Hold], next.Now().Presses);

        Assert.True(next.Set([]).Done);
        var after = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: true);
        after.Load();
        Assert.False(after.Now().On);
    }

    public static TheoryData<string, string> NotHonoured => new()
    {
        { """{"schemaVersion":1,"presses":["stop"],"machine":"0000"}""", StandingRead.OtherMachine },
        { "{ not json", StandingRead.Unread },
        { """{"presses":["stop"]}""", StandingRead.Unread },
        { """{"schemaVersion":2,"presses":["stop"],"machine":"MACHINE"}""", StandingRead.Newer },
    };

    [Theory]
    [MemberData(nameof(NotHonoured))]
    public void Another_machines_an_unreadable_or_a_newer_record_is_off_said_and_left(string text, string why)
    {
        using var log = new MachineLog(Home, "desktop");
        var standing = new StandingConfirmation(Home, log, new Placed(StandingPlace.Outside), offered: true);
        Directory.CreateDirectory(Path.GetDirectoryName(standing.RecordPath)!);
        var written = text.Replace("MACHINE", Machine, StringComparison.Ordinal);
        File.WriteAllText(standing.RecordPath, written);

        standing.Load();

        Assert.Equal((false, why), (standing.Now().On, standing.Now().Unread));
        Assert.Equal(StandingWhy.Off, standing.Judge(Driver, "STOP_SESSION", Payload("""{"id":"s1","said":true}"""), null).Why);
        Assert.Contains(why, Logged("state"));
        // Left as it was: a revoke does not write over another machine's or a newer record.
        Assert.True(standing.Set([]).Done);
        if (why != StandingRead.Unread) Assert.Equal(written, File.ReadAllText(standing.RecordPath));
        // A newer one is never written over by a grant either.
        if (why == StandingRead.Newer) Assert.Equal(StandingRead.Newer, standing.Set([StandingPresses.Stop]).Why);
    }

    /// <summary>§4.3: a revoke whose write fails still turns it off for this run, and says so; a grant whose write fails turns nothing on.</summary>
    [Fact]
    public void A_failed_write_turns_a_revoke_off_for_this_run_and_a_grant_on_not_at_all()
    {
        var standing = Granted();
        using (new FileStream(standing.RecordPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var revoked = standing.Set([]);
            Assert.Equal((true, StandingConfirmation.Unwritten), (revoked.Done, revoked.Why));
            Assert.False(standing.Now().On);
            Assert.True(standing.Now().Unwritten);
            Assert.Equal(StandingWhy.Off, Judge(standing, "STOP_SESSION", """{"id":"s1","said":true}""").Why);

            var granted = standing.Set([StandingPresses.Stop]);
            Assert.Equal((false, StandingConfirmation.Unwritten), (granted.Done, granted.Why));
            Assert.False(standing.Now().On);
        }
    }

    [Fact]
    public void The_log_says_a_grant_a_revoke_and_what_a_start_found_by_codes_alone()
    {
        using var log = new MachineLog(Home, "desktop");
        var standing = new StandingConfirmation(Home, log, new Placed(StandingPlace.Outside), offered: true);
        standing.Load();
        standing.Set([StandingPresses.Stop, StandingPresses.Finish]);
        standing.Set([]);

        Assert.Equal(["found", "granted", "revoked"], Logged("state"));
        Assert.Equal(["", "stop,finish", ""], Logged("presses"));
    }

    private List<string?> Logged(string field)
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
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(line => line.GetProperty("event").GetString() == StandingConfirmation.LogEvent)
            .Select(line => line.GetProperty("data").GetProperty(field).GetString()),
        ];
    }

    // SUPERVISOR1a-ii (D208 points 2 and 3; the supervisor design §2.2, §2.3, §5 and §11 row 1a-ii): the owner's appointments in D201's
    // record. An appointment is read, written and turned off apart from the presses; its key's hash lives in memory alone, so one found at
    // a start is dormant; a role's act is covered only for its repository, holder and key, by a proof over the connection's nonce; every
    // write is one mutation step, and a revoke sweeps its decisions after it, never inside.
    private const string Engine = "engine";

    /// <summary>A walk that answers one thing for every act (STEWARDROLE1a-i): null admits; the walk itself is <c>PipeStandingTests</c>' and <c>HolderWalkTests</c>'.</summary>
    internal sealed class Walked(string? why = null) : IHolderWalk
    {
        public string? Why { get; set; } = why;

        public string? Check(PipeClientProcess? client, HolderProcess? harness) => Why;
    }

    /// <summary>A holder's tab as a stand-in opens it: named by the holder, its harness a process nobody started.</summary>
    internal static HolderOpened Opened(HolderOpen open) =>
        new($"t-{open.Holder}", new HolderProcess(4242, DateTime.UnixEpoch, @"C:\home\toolchain\claude-code\2.1.300\bin\claude.exe", @"C:\home\config"), null, null);

    /// <summary>The supervisor's appointment for <paramref name="repository"/>, a terminal of its own where a holder lives.</summary>
    internal static AppointRequest Supervising(string repository, bool on = true, string? share = "own") =>
        new(Roles.Supervisor, RoleScopes.Repository(repository), RoleHolders.Terminal, on, share);

    /// <summary>A judge whose supervisor is offered, with <see cref="Engine"/> a repository it drives.</summary>
    private StandingConfirmation Appointing(StandingPlace place = StandingPlace.Outside, MachineLog? log = null, bool steward = false)
    {
        var standing = new StandingConfirmation(Home, log, new Placed(place), offered: true, supervisor: true, steward: steward, walk: new Walked())
        {
            Drives = repository => repository is Engine or "other",
        };
        standing.Load();
        return standing;
    }

    /// <summary>Appoint the supervisor for <paramref name="repository"/>, the tab's start a stand-in that keeps the key it was handed.</summary>
    private static (AppointmentChange Change, string? Key) Appoint(StandingConfirmation standing, string repository = Engine, string? share = "own")
    {
        string? handed = null;
        var change = standing.Appoint(Supervising(repository, share: share), open =>
        {
            handed = open.Key;
            return Opened(open);
        });
        return (change, handed);
    }

    private static AppointmentChange TurnOff(StandingConfirmation standing, string repository = Engine) =>
        standing.Appoint(Supervising(repository, on: false), _ => throw new InvalidOperationException("a dismissal opens no tab"));

    private static string GenerationOf(StandingConfirmation standing, string repository = Engine) =>
        standing.Admit(repository).Generation ?? throw new InvalidOperationException("not admitted");

    /// <summary>An answer's payload, proved by <paramref name="key"/> over <paramref name="nonce"/>.</summary>
    private static JsonElement Answer(string key, string nonce, string generation, string choice = "Monthly", string? extra = null)
    {
        SupervisorReason[] reasons = [new("The ticket says monthly.", "the quest's line 2")];
        var proof = SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical("s1", "q-1", generation, choice, reasons))!;
        var form = new SupervisorForm("s1", "q-1", generation, proof, choice, reasons);
        var json = JsonSerializer.Serialize(form.Payload());
        if (extra is not null) json = json[..^1] + "," + extra + "}";
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static RoleJudgement JudgeRole(StandingConfirmation standing, JsonElement payload, string? nonce) =>
        standing.JudgeRole(Driver, SupervisorRoutes.Answer, payload, null, nonce);

    [Fact]
    public void An_appointment_is_written_read_and_turned_off_apart_from_the_presses_and_the_record_holds_no_key()
    {
        var standing = Appointing();
        Assert.True(standing.Set([StandingPresses.Stop]).Done);
        var (change, key) = Appoint(standing);
        Assert.True(change.Answer.Done);
        Assert.Empty(change.Ended);

        var text = File.ReadAllText(standing.RecordPath);
        using (var document = JsonDocument.Parse(text))
        {
            var appointment = Assert.Single(document.RootElement.GetProperty("appointments").EnumerateArray());
            // STEWARDROLE1a-i (D209 §2.3): `scope`, `acts` and `setup` in place of the built `repository`; never which holder holds it.
            // SUPERVISOR1e: the supervisor's overrule window last.
            Assert.Equal(
                ["role", "scope", "holder", "generation", "since", "acts", "setup", "window"],
                appointment.EnumerateObject().Select(each => each.Name));
            Assert.Equal((Roles.Supervisor, RoleScopes.Repository(Engine), RoleHolders.Terminal), (
                appointment.GetProperty("role").GetString(), appointment.GetProperty("scope").GetString(),
                appointment.GetProperty("holder").GetString()));
            Assert.Equal(["stop"], document.RootElement.GetProperty("presses").EnumerateArray().Select(each => each.GetString()));
        }

        // Neither the key nor its hash in any file of the home.
        var hash = Convert.ToHexStringLower(HolderKey.Hash(key)!);
        foreach (var file in Directory.EnumerateFiles(Home, "*", SearchOption.AllDirectories))
        {
            var written = ReadShared(file);
            Assert.DoesNotContain(key!, written, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(hash, written, StringComparison.OrdinalIgnoreCase);
        }

        // The presses turned off keep the appointment; the appointment turned off keeps the presses.
        Assert.True(standing.Set([]).Done);
        Assert.Single(standing.Now().Appointments!);
        Assert.True(standing.Set([StandingPresses.Hold]).Done);
        var off = TurnOff(standing);
        Assert.True(off.Answer.Done);
        Assert.Single(off.Ended);
        Assert.Empty(standing.Now().Appointments!);
        Assert.Equal([StandingPresses.Hold], standing.Now().Presses);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
    }

    /// <summary>
    /// §2.3: an appointment read at a start is dormant, whatever the record carries beside it, a key's hash an agent wrote there included;
    /// the row says it, nothing admits under it, and <i>Appoint again</i> is a new generation with a new key.
    /// </summary>
    [Fact]
    public void An_appointment_found_at_a_start_is_dormant_and_a_hash_written_beside_it_is_never_read()
    {
        var first = Appointing();
        var (_, key) = Appoint(first);
        var generation = GenerationOf(first);

        // An agent's ordinary reach: its own key's hash written into the record.
        var forged = HolderKey.Draw();
        var text = File.ReadAllText(first.RecordPath).Replace(
            "\"holder\":", $"\"hash\":\"{Convert.ToHexStringLower(HolderKey.Hash(forged)!)}\",\"key\":\"{forged}\",\"holder\":", StringComparison.Ordinal);
        File.WriteAllText(first.RecordPath, text);

        var next = Appointing();
        var found = Assert.Single(next.Now().Appointments!);
        Assert.True(found.Dormant);
        Assert.Equal(SupervisorWhy.Dormant, next.Admit(Engine).Why);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.Dormant, JudgeRole(next, Answer(forged, nonce, generation), nonce).Why);
        Assert.Equal(SupervisorWhy.Dormant, JudgeRole(next, Answer(key!, nonce, generation), SupervisorProof.Nonce()).Why);

        var (again, renewed) = Appoint(next);
        Assert.True(again.Answer.Done);
        Assert.Equal([generation], again.Ended);
        Assert.NotEqual(key, renewed);
        Assert.False(Assert.Single(next.Now().Appointments!).Dormant);
        Assert.NotEqual(generation, GenerationOf(next));
    }

    /// <summary>§2.2, §3.1, §4.6: covered only for its appointment's generation and key, by a proof over this connection's unused nonce.</summary>
    [Fact]
    public void A_role_act_is_covered_only_by_the_holders_proof_over_its_own_nonce_and_form()
    {
        var standing = Appointing();
        var (_, key) = Appoint(standing);
        var generation = GenerationOf(standing);
        var nonce = SupervisorProof.Nonce();

        var covered = JudgeRole(standing, Answer(key!, nonce, generation), nonce);
        Assert.True(covered.Covered);
        Assert.Equal((Engine, generation), (covered.Repository, covered.Generation));
        Assert.Equal(1, Assert.Single(standing.Now().Appointments!).Answers);

        // No proof's nonce (used, or expired), another connection's nonce, another key, an altered form.
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(key!, nonce, generation), null).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(key!, SupervisorProof.Nonce(), generation), nonce).Why);
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, Answer(HolderKey.Draw(), nonce, generation), nonce).Why);
        var altered = Answer(key!, nonce, generation);
        var changed = JsonDocument.Parse(altered.GetRawText().Replace("\"Monthly\"", "\"Weekly\"", StringComparison.Ordinal)).RootElement;
        Assert.Equal(SupervisorWhy.NotHolder, JudgeRole(standing, changed, nonce).Why);

        // Anything more or less than the form; another generation; a route the role names no method for.
        Assert.Equal(SupervisorWhy.Form, JudgeRole(standing, Answer(key!, nonce, generation, extra: "\"key\":\"x\""), nonce).Why);
        Assert.Equal(SupervisorWhy.AppointmentChanged, JudgeRole(standing, Answer(key!, nonce, "older"), nonce).Why);
        Assert.Equal(StandingWhy.NotNamed, standing.JudgeRole(Driver, "SESSION_INPUT", Answer(key!, nonce, generation), null, nonce).Why);

        // A press granted or none changes nothing of it: judged on its own, never open.
        Assert.False(standing.Now().On);
    }

    /// <summary>A defer's payload, proved by <paramref name="key"/> over <paramref name="nonce"/> and the canonical bytes of <paramref name="route"/>.</summary>
    private static JsonElement Defer(string key, string nonce, string generation, string bound = "sign-in", string route = SupervisorRoutes.Defer)
    {
        SupervisorReason[] reasons = [new("A sign-in is the owner's.", "D191 point 3")];
        var proof = SupervisorProof.Prove(key, nonce, SupervisorProof.Canonical(route, "s1", "q-1", generation, bound, reasons))!;
        return JsonDocument.Parse(JsonSerializer.Serialize(new SupervisorDeferForm("s1", "q-1", generation, proof, bound, reasons).Payload()))
            .RootElement.Clone();
    }

    /// <summary>
    /// SUPERVISOR1b (§3.1 <c>SUPERVISOR_DEFER</c>): the defer is the role's second act, judged as the answer is, its proof over its own
    /// route's canonical bytes: a defer proved as an answer, or an answer sent to the defer's route, is never covered; a bound §4.1 does
    /// not name is no form; a route the role names no act for is still not named.
    /// </summary>
    [Fact]
    public void A_defer_is_judged_as_the_answer_is_each_proved_over_its_own_route()
    {
        var standing = Appointing();
        var (_, key) = Appoint(standing);
        var generation = GenerationOf(standing);
        var nonce = SupervisorProof.Nonce();
        RoleJudgement Judge(JsonElement payload, string route = SupervisorRoutes.Defer) => standing.JudgeRole(Driver, route, payload, null, nonce);

        Assert.True(Judge(Defer(key!, nonce, generation)).Covered);
        Assert.Equal(SupervisorWhy.NotHolder, Judge(Defer(key!, nonce, generation, route: SupervisorRoutes.Answer)).Why);
        Assert.Equal(SupervisorWhy.Form, Judge(Answer(key!, nonce, generation)).Why);
        Assert.Equal(SupervisorWhy.Form, Judge(Defer(key!, nonce, generation), SupervisorRoutes.Answer).Why);
        Assert.Equal(SupervisorWhy.Form, Judge(Defer(key!, nonce, generation, bound: "because")).Why);
        Assert.Equal(SupervisorWhy.NotHolder, Judge(Defer(HolderKey.Draw(), nonce, generation)).Why);
        Assert.Equal(["SUPERVISOR_ANSWER", "SUPERVISOR_DEFER"], SupervisorRoutes.Acts);
    }

    [Theory]
    [InlineData(StandingPlace.InAgentJob, StandingWhy.InAgentJob)]
    [InlineData(StandingPlace.Untold, StandingWhy.Untold)]
    public void A_contained_or_unplaced_caller_is_refused_before_its_proof_is_read(StandingPlace place, string why)
    {
        var standing = Appointing(place);
        var (_, key) = Appoint(standing);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(why, JudgeRole(standing, Answer(key!, nonce, GenerationOf(standing)), nonce).Why);
    }

    /// <summary>§2.3: a revoke and a re-appointment of the same holder differ in generation, so an act under the old one is refused.</summary>
    [Fact]
    public void A_revoke_and_a_new_appointment_of_the_same_holder_refuse_the_old_generation()
    {
        var standing = Appointing();
        var (_, key) = Appoint(standing);
        var old = GenerationOf(standing);
        TurnOff(standing);
        var (_, renewed) = Appoint(standing);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.AppointmentChanged, JudgeRole(standing, Answer(key!, nonce, old), nonce).Why);
        Assert.True(JudgeRole(standing, Answer(renewed!, nonce, GenerationOf(standing)), nonce).Covered);
    }

    /// <summary>
    /// D208 §2.3: this build does not offer appointments; one that offers them only where D201's grant is offered; an unknown role, an
    /// actor holder, a repository Daoris does not drive, and a terminal that could not start each keep nothing.
    /// </summary>
    [Fact]
    public void An_appointment_this_build_does_not_offer_or_cannot_start_keeps_nothing()
    {
        Assert.False(StandingConfirmation.AppointmentsOffered);
        Assert.False(StandingConfirmation.SupervisorOfferedInThisBuild);
        Assert.False(StandingConfirmation.StewardOfferedInThisBuild);
        var dark = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: true, walk: new Walked()) { Drives = _ => true };
        dark.Load();
        Assert.Equal(StandingConfirmation.NotOffered, Appoint(dark).Change.Answer.Why);
        // D209 §2.4: a role's flag is its own, whatever D201's grant is.
        Assert.True(new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: false, supervisor: true).AppointsOffered);
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.NotOffered, JudgeRole(dark, Answer(HolderKey.Draw(), nonce, "g"), nonce).Why);
        Assert.Equal(SupervisorWhy.NotOffered, dark.Admit(Engine).Why);

        var standing = Appointing();
        var started = 0;
        HolderOpened Start(HolderOpen open)
        {
            started++;
            return Opened(open);
        }

        Assert.Equal(StandingConfirmation.UnknownRole, standing.Appoint(new AppointRequest("clerk", RoleScopes.Machine, RoleHolders.Terminal, true), Start).Answer.Why);
        Assert.Equal(StandingConfirmation.NotOffered, standing.Appoint(new AppointRequest(Roles.Steward, RoleScopes.Machine, RoleHolders.Terminal, true), Start).Answer.Why);
        Assert.Equal(StandingConfirmation.NotOffered, standing.Appoint(Supervising(Engine) with { Holder = RoleHolders.Actor }, Start).Answer.Why);
        Assert.Equal(StandingConfirmation.UnknownRepository, standing.Appoint(Supervising("elsewhere"), Start).Answer.Why);
        Assert.Equal(StandingConfirmation.Scope, standing.Appoint(Supervising(Engine) with { Scope = RoleScopes.Machine }, Start).Answer.Why);
        Assert.Equal(0, started);
        Assert.Equal(StandingConfirmation.Unstarted,
            standing.Appoint(Supervising(Engine), _ => new HolderOpened(null, null, StandingConfirmation.Unstarted, null)).Answer.Why);
        Assert.Equal(HolderHarness.Refused,
            standing.Appoint(Supervising(Engine), _ => new HolderOpened(null, null, HolderHarness.Refused, "no version of claude-code is pinned")).Answer.Why);
        Assert.False(File.Exists(standing.RecordPath));
        Assert.Empty(standing.Now().Appointments!);
        Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
    }

    /// <summary>§2.3: the role is never one of the presses, nor taken by a grant.</summary>
    [Fact]
    public void The_role_is_never_a_press()
    {
        Assert.DoesNotContain(Roles.Supervisor, StandingPresses.Built);
        Assert.DoesNotContain("supervise", StandingPresses.Built);
        var standing = Granted();
        Assert.Equal(StandingConfirmation.UnknownPress, standing.Set(["supervise"]).Why);
        Assert.Equal(StandingConfirmation.UnknownPress, standing.Set([Roles.Supervisor]).Why);
    }

    /// <summary>An older shape, presses alone, reads with no appointment; an appointment then written carries the presses through.</summary>
    [Fact]
    public void A_record_of_the_older_shape_reads_and_its_presses_are_carried_through_an_appointment()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(new StandingConfirmation(Home).RecordPath)!);
        var path = new StandingConfirmation(Home).RecordPath;
        File.WriteAllText(path, $$"""{"schemaVersion":1,"presses":["stop","finish"],"since":"2026-10-23T06:05:00Z","machine":"{{Machine}}"}""");
        var standing = Appointing();
        Assert.Empty(standing.Now().Appointments!);
        Assert.True(Appoint(standing).Change.Answer.Done);
        var after = StandingConfirmation.Read(File.ReadAllText(path), Machine);
        Assert.Equal([StandingPresses.Stop, StandingPresses.Finish], after.Presses);
        Assert.Single(after.Appointments);
    }

    /// <summary>
    /// §2.3: a press grant, an appointment change and a revoke raced, many times over: every write is one step, so the record always
    /// says what memory installed and nothing is lost.
    /// </summary>
    [Fact]
    public async Task A_grant_an_appointment_and_a_revoke_raced_lose_nothing()
    {
        var standing = Appointing();
        for (var round = 0; round < 25; round++)
        {
            var presses = round % 2 == 0 ? new[] { StandingPresses.Stop } : [];
            await Task.WhenAll(
                Task.Run(() => standing.Set(presses)),
                Task.Run(() => Appoint(standing, Engine)),
                Task.Run(() => standing.Appoint(Supervising("other", on: round % 3 != 0), Opened)),
                Task.Run(() => standing.Set([StandingPresses.Hold])));

            var memory = standing.Now();
            var written = StandingConfirmation.Read(File.ReadAllText(standing.RecordPath), Machine);
            Assert.Equal(memory.Presses, written.Presses);
            Assert.Equal(
                memory.Appointments!.Select(each => each.Repository).Order(),
                written.Appointments.Select(each => each.Repository).Order());
            Assert.Contains(written.Appointments, each => each.Repository == Engine);
        }
    }

    /// <summary>
    /// §5, §3.6: the real revoke raced against a keeping, a release and an admission, which 1a-i could only stub. The mutation step
    /// ends first and the sweep follows under each decision's lock: no lock taken in reverse, nothing deadlocks, and nothing is kept,
    /// released or admitted under the dead generation.
    /// </summary>
    [Fact]
    public async Task The_real_revoke_raced_against_a_keeping_a_release_and_an_admission_hands_nothing_under_a_dead_generation()
    {
        for (var round = 0; round < 20; round++)
        {
            var standing = Appointing();
            Appoint(standing);
            var generation = GenerationOf(standing);
            var decisions = new QuestionSupervisorDecisions(Home, standing);
            var at = DateTimeOffset.Parse("2026-10-24T09:00:00Z");
            var session = $"r{round}";
            var asked = new ParkQuestion($"q-{round}", session, "Which report?", [new("Monthly", "I write it."), new("Weekly", "I write that.")], 1, null, at);
            var park = new SupervisedPark(Engine, true, true, asked, false, false, false);
            var answer = new SupervisorAnswer(session, asked.Id, generation, "Monthly", [new("The ticket says monthly.", "line 2")], new("terminal", "pwsh"));
            if (round % 2 == 0) await decisions.KeepAsync(answer, park, at);

            var taken = new List<string>();
            Task<bool> TakeBack(string _, string word, CancellationToken __)
            {
                lock (taken) taken.Add(word);
                return Task.FromResult(true);
            }

            async Task<SupervisorSaid> Say(string s, string d, string q, string c, string k, CancellationToken ct)
            {
                await Task.Yield();
                return new SupervisorSaid("w-" + d, null);
            }

            var revoke = Task.Run(async () =>
            {
                var change = TurnOff(standing);
                foreach (var ended in change.Ended) await decisions.RevokedAsync(ended, TakeBack, at.AddMinutes(5));
            });
            var keep = Task.Run(() => decisions.KeepAsync(answer, park, at));
            var release = Task.Run(() => decisions.ReleaseAsync(session, Say, at.AddMinutes(5)));
            var admit = Task.Run(() => decisions.AdmitAsync(session, _ => Task.FromResult(true), "go", at.AddMinutes(6)));
            var all = Task.WhenAll(revoke, keep, release, admit);
            Assert.Same(all, await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(20))));

            // Swept once more after every race, as the next look would read it: nothing live is handable under the dead generation.
            await decisions.RevokedAsync(generation, TakeBack, at.AddMinutes(7));
            Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
            foreach (var view in decisions.LiveOf(session))
            {
                Assert.Equal(generation, view.Generation);
                // An admission made before the revoke is a taken-in act; anything else is stale or never moved past held.
                Assert.True(view.State is SupervisorState.Stale or SupervisorState.Admitted, $"round {round}: {view.State}");
                if (view.State == SupervisorState.Stale && view.Word is { } word) Assert.Contains(word, taken);
                Assert.False(view.Word is { } live && decisions.Handable(session, live));
            }

            Assert.False((await decisions.KeepAsync(answer with { Question = "later" }, park with { Filed = asked with { Id = "later" } }, at)).Kept);
        }
    }

    // STEWARDROLE1a-i (D209 §2.2 to §2.4, §3.4; the steward design §10 row 1a-i's StandingConfirmationTests): a holder apart from its
    // appointment, shared or its own; a role revoked alone, the holder's hash kept to its last; a holder ended by its last role or its tab;
    // the record's scope, acts and setup, a planted record dormant and Appoint again naming the build's acts with the tick off; and each
    // role offered by its own flag.

    /// <summary>A judge offering both roles, the steward's table holding <paramref name="probe"/>.</summary>
    private StandingConfirmation Both(RoleKit.Probe probe, List<(string Tab, string Line, string? Title)>? told = null, List<IReadOnlyList<string>>? ended = null)
    {
        var standing = Appointing(steward: true).Probing(probe);
        standing.Tell = (tab, line, title) => told?.Add((tab, line, title));
        standing.Ended = generations => ended?.Add(generations);
        return standing;
    }

    [Fact]
    public void A_second_role_is_added_to_the_living_holder_or_given_a_terminal_of_its_own_and_neither_is_preselected()
    {
        var told = new List<(string Tab, string Line, string? Title)>();
        var standing = Both(new RoleKit.Probe(), told);
        var (_, key) = Appoint(standing);
        var holder = Assert.Single(standing.Now().Holders!);
        Assert.Equal([Roles.Supervisor], holder.Roles);

        // A holder lives: no choice is refused, and nothing opens.
        var opened = 0;
        Assert.Equal(StandingConfirmation.ChooseTerminal,
            standing.Appoint(RoleKit.Stewarding(share: null), open => { opened++; return Opened(open); }).Answer.Why);
        Assert.Equal(StandingConfirmation.NoHolder, standing.Appoint(RoleKit.Stewarding(share: "h-gone"), Opened).Answer.Why);
        Assert.Equal(0, opened);

        // Added to the terminal Daoris started: the same key, no second tab, a new appointment and generation; its title and words told.
        var added = standing.Appoint(RoleKit.Stewarding(share: holder.Id), open => { opened++; return Opened(open); });
        Assert.True(added.Answer.Done, added.Answer.Message);
        Assert.Equal(0, opened);
        var shared = Assert.Single(standing.Now().Holders!);
        Assert.Equal(holder.Id, shared.Id);
        Assert.Equal([Roles.Steward, Roles.Supervisor], shared.Roles);
        var line = Assert.Single(told);
        Assert.Equal(($"t-{holder.Id}", "Daoris: the steward's and supervisor's terminal"), (line.Tab, line.Title));
        Assert.Contains("The steward was added to this terminal", line.Line, StringComparison.Ordinal);
        var nonce = SupervisorProof.Nonce();
        Assert.True(RoleKit.JudgeAct(standing, RoleKit.Act(key!, nonce, RoleKit.GenerationOf(standing, Roles.Steward)), nonce).Covered);

        // A terminal of its own: a second holder with a key of its own.
        var (own, second) = Appoint(standing, "other");
        Assert.True(own.Answer.Done);
        Assert.NotEqual(key, second);
        Assert.Equal(2, standing.Now().Holders!.Count);
    }

    [Fact]
    public void A_role_revoked_this_run_is_never_added_back_to_the_holder_that_held_it()
    {
        var standing = Both(new RoleKit.Probe());
        Appoint(standing);
        var holder = Assert.Single(standing.Now().Holders!).Id;
        Assert.True(standing.Appoint(RoleKit.Stewarding(share: holder), Opened).Answer.Done);
        Assert.True(standing.Appoint(RoleKit.Stewarding(on: false), Opened).Answer.Done);

        Assert.Equal(StandingConfirmation.RevokedHolder, standing.Appoint(RoleKit.Stewarding(share: holder), Opened).Answer.Why);
        var (own, _) = RoleKit.Appoint(standing, RoleKit.Stewarding(share: "own"));
        Assert.True(own.Answer.Done);
        Assert.Equal(2, standing.Now().Holders!.Count);
    }

    /// <summary>D209 §2.2, Codex's final read, finding 10: revoking either role of a shared holder ends that appointment alone, and the other goes on.</summary>
    [Theory]
    [InlineData(Roles.Steward)]
    [InlineData(Roles.Supervisor)]
    public void A_shared_holder_s_role_revoked_alone_leaves_the_other_answering_and_its_hash_goes_with_the_last(string revoked)
    {
        var told = new List<(string Tab, string Line, string? Title)>();
        var probe = new RoleKit.Probe();
        var standing = Both(probe, told);
        var (_, key) = Appoint(standing);
        var holder = Assert.Single(standing.Now().Holders!).Id;
        Assert.True(standing.Appoint(RoleKit.Stewarding(share: holder), Opened).Answer.Done);
        var supervisor = GenerationOf(standing);
        var steward = RoleKit.GenerationOf(standing, Roles.Steward);

        var off = revoked == Roles.Steward ? standing.Appoint(RoleKit.Stewarding(on: false), Opened) : TurnOff(standing);

        Assert.Equal([revoked == Roles.Steward ? steward : supervisor], off.Ended);
        var nonce = SupervisorProof.Nonce();
        var answer = JudgeRole(standing, Answer(key!, nonce, supervisor), nonce);
        var act = RoleKit.JudgeAct(standing, RoleKit.Act(key!, nonce, steward), nonce);
        if (revoked == Roles.Steward)
        {
            Assert.True(answer.Covered, answer.Why);
            Assert.Equal(SupervisorWhy.NotAppointed, act.Why);
            Assert.Equal([Roles.Supervisor], answer.Roles);
        }
        else
        {
            Assert.True(act.Covered, act.Why);
            Assert.Equal(SupervisorWhy.NotAppointed, standing.Admit(Engine).Why);
            Assert.Equal([Roles.Steward], act.Roles);
        }

        Assert.Equal(holder, Assert.Single(standing.Now().Holders!).Id);
        Assert.DoesNotContain(told, each => each.Line == HolderTerminal.TurnedOff);

        // The last role off: the holder's hash goes, and its tab is told.
        var last = revoked == Roles.Steward ? TurnOff(standing) : standing.Appoint(RoleKit.Stewarding(on: false), Opened);
        Assert.Single(last.Ended);
        Assert.Empty(standing.Now().Holders!);
        Assert.Contains(told, each => each.Tab == $"t-{holder}" && each.Line == HolderTerminal.TurnedOff);
        var again = SupervisorProof.Nonce();
        Assert.False(RoleKit.JudgeAct(standing, RoleKit.Act(key!, again, steward), again).Covered);
    }

    /// <summary>
    /// D209 §2.2: a holder's tab ending, by whichever of its paths (the module tells each once, <c>HolderTerminalTests</c>), drops its hash
    /// and every appointment it holds in one step, swept after it; a tab that ends while its appointment opens it leaves nothing written.
    /// </summary>
    [Fact]
    public void A_holder_ends_with_its_tab_and_every_appointment_it_holds_goes_with_it()
    {
        var ended = new List<IReadOnlyList<string>>();
        var standing = Both(new RoleKit.Probe(), ended: ended);
        var (_, key) = Appoint(standing);
        var holder = Assert.Single(standing.Now().Holders!).Id;
        Assert.True(standing.Appoint(RoleKit.Stewarding(share: holder), Opened).Answer.Done);
        var generations = new[] { GenerationOf(standing), RoleKit.GenerationOf(standing, Roles.Steward) };

        Assert.Equal(generations.Order(), standing.HolderEnded($"t-{holder}").Order());

        Assert.Equal(generations.Order(), Assert.Single(ended).Order());
        Assert.Empty(standing.Now().Holders!);
        Assert.Empty(standing.Now().Appointments!);
        Assert.Empty(StandingConfirmation.Read(File.ReadAllText(standing.RecordPath), Machine).Appointments);
        var nonce = SupervisorProof.Nonce();
        Assert.False(JudgeRole(standing, Answer(key!, nonce, generations[0]), nonce).Covered);
        Assert.Empty(standing.HolderEnded($"t-{holder}"));

        // The next Appoint draws a new key and a new tab, asked for no choice since no holder lives.
        var (renewed, fresh) = Appoint(standing, share: null);
        Assert.True(renewed.Answer.Done);
        Assert.NotEqual(key, fresh);

        // A tab that ends while its appointment still opens it: the appointment is never written.
        var exiting = standing.Appoint(Supervising("other"), open =>
        {
            standing.HolderEnded($"t-{open.Holder}");
            return Opened(open);
        });
        Assert.Equal(StandingConfirmation.Unstarted, exiting.Answer.Why);
        Assert.DoesNotContain(standing.Now().Appointments!, each => each.Repository == "other");
    }

    /// <summary>D209 §2.3: a steward record planted while Daoris was down turns nothing on; Appoint again names the build's acts with the tick off, never the file's.</summary>
    [Fact]
    public void A_planted_steward_record_is_dormant_and_appoint_again_names_the_build_s_acts_with_the_tick_off()
    {
        var path = new StandingConfirmation(Home).RecordPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            {"schemaVersion":1,"presses":[],"machine":"{{Machine}}","appointments":[{"role":"steward","scope":"machine","holder":"terminal",
            "generation":"planted","since":"2026-10-24T06:00:00Z","acts":["drive","setup","trees-off"],"setup":true}]}
            """);
        var probe = new RoleKit.Probe();
        var standing = Both(probe);

        var found = Assert.Single(standing.Now().Appointments!);
        Assert.Equal((Roles.Steward, RoleScopes.Machine, true, (string?)null), (found.Role, found.Scope, found.Dormant, found.Repository));
        var nonce = SupervisorProof.Nonce();
        Assert.Equal(SupervisorWhy.Dormant, RoleKit.JudgeAct(standing, RoleKit.Act(HolderKey.Draw(), nonce, "planted"), nonce).Why);

        var (again, _) = RoleKit.Appoint(standing, RoleKit.Stewarding(share: null, setup: false));
        Assert.True(again.Answer.Done);
        Assert.Equal(["planted"], again.Ended);
        var written = Assert.Single(StandingConfirmation.Read(File.ReadAllText(path), Machine).Appointments);
        Assert.Equal([RoleKit.Probe.Named], written.Acts);
        Assert.False(written.Setup);
        Assert.NotEqual("planted", written.Generation);
    }

    /// <summary>D209 §2.3: a record the built door wrote, <c>repository</c> and no <c>scope</c>, is read as <c>repository:&lt;name&gt;</c>.</summary>
    [Fact]
    public void The_built_supervisor_record_is_read_as_its_scope()
    {
        var path = new StandingConfirmation(Home).RecordPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            {"schemaVersion":1,"presses":[],"machine":"{{Machine}}","appointments":[{"role":"supervisor","repository":"engine","holder":"terminal",
            "generation":"built","since":"2026-10-24T06:00:00Z"}]}
            """);
        var standing = Appointing();

        var found = Assert.Single(standing.Now().Appointments!);
        Assert.Equal((RoleScopes.Repository(Engine), Engine, true), (found.Scope, found.Repository, found.Dormant));
        Assert.Equal(SupervisorWhy.Dormant, standing.Admit(Engine).Why);
        Assert.Equal(["built"], Appoint(standing).Change.Ended);
    }

    /// <summary>D209 §2.4: each role by its own flag: the steward on never opens the supervisor, nor the supervisor the steward.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Each_role_is_offered_by_its_own_flag_alone(bool steward, bool supervisor)
    {
        var standing = new StandingConfirmation(Home, null, new Placed(StandingPlace.Outside), offered: false, supervisor: supervisor, steward: steward, walk: new Walked())
        {
            Drives = repository => repository == Engine,
        }.Probing(new RoleKit.Probe());
        standing.Load();
        Assert.Equal((supervisor, steward, true), (standing.SupervisorOffered, standing.StewardOffered, standing.AppointsOffered));

        var (supervising, supervisorKey) = Appoint(standing, share: null);
        var (stewarding, stewardKey) = RoleKit.Appoint(standing, RoleKit.Stewarding(share: "own"));
        var nonce = SupervisorProof.Nonce();
        if (steward)
        {
            Assert.Equal(StandingConfirmation.NotOffered, supervising.Answer.Why);
            Assert.True(stewarding.Answer.Done);
            Assert.Equal(SupervisorWhy.NotOffered, JudgeRole(standing, Answer(HolderKey.Draw(), nonce, "g"), nonce).Why);
            Assert.Equal(SupervisorWhy.NotOffered, standing.JudgeRole(Driver, SupervisorRoutes.Defer, Defer(HolderKey.Draw(), nonce, "g"), null, nonce).Why);
            Assert.Equal(SupervisorWhy.NotOffered, standing.Admit(Engine).Why);
            Assert.True(RoleKit.JudgeAct(standing, RoleKit.Act(stewardKey!, nonce, RoleKit.GenerationOf(standing, Roles.Steward)), nonce).Covered);
        }
        else
        {
            Assert.True(supervising.Answer.Done);
            Assert.Equal(StandingConfirmation.NotOffered, stewarding.Answer.Why);
            Assert.Equal(SupervisorWhy.NotOffered, RoleKit.JudgeAct(standing, RoleKit.Act(HolderKey.Draw(), nonce, "g"), nonce).Why);
            Assert.True(JudgeRole(standing, Answer(supervisorKey!, nonce, GenerationOf(standing)), nonce).Covered);
        }
    }

    /// <summary>D209 §3.4: a role's act in the role's table but not the appointment's acts, an act no table names, and another form, each refused.</summary>
    [Fact]
    public void A_role_act_is_judged_in_order_and_admitted_once_with_an_id_daoris_draws()
    {
        var probe = new RoleKit.Probe();
        var standing = Both(probe);
        var (_, key) = RoleKit.Appoint(standing, RoleKit.Stewarding(share: null));
        var generation = RoleKit.GenerationOf(standing, Roles.Steward);
        RoleJudgement Judge(JsonElement payload, string nonce) => RoleKit.JudgeAct(standing, payload, nonce);
        string Nonce() => SupervisorProof.Nonce();

        var n = Nonce();
        var admitted = Judge(RoleKit.Act(key!, n, generation), n);
        Assert.True(admitted.Covered, admitted.Why);
        Assert.Equal((Roles.Steward, RoleKit.Probe.Named, (string?)null), (admitted.Role, admitted.Act, admitted.Repository));
        Assert.Equal(RoleActStates.Admitted, standing.ActState(admitted.ActId!));

        n = Nonce();
        Assert.Equal(RoleWhy.NotRole, Judge(RoleKit.Act(key!, n, generation, act: "drive"), n).Why);
        n = Nonce();
        Assert.Equal(SupervisorWhy.Form, Judge(RoleKit.Act(key!, n, generation, form: """{"repository":"engine","extra":1}"""), n).Why);
        n = Nonce();
        Assert.Equal(SupervisorWhy.AppointmentChanged, Judge(RoleKit.Act(key!, n, "older"), n).Why);
        n = Nonce();
        Assert.Equal(SupervisorWhy.NotHolder, Judge(RoleKit.Act(HolderKey.Draw(), n, generation), n).Why);

        // An act the appointment's ask did not name: the table grew after it.
        standing.TableOf = role => new Dictionary<string, IRoleAct>(StringComparer.Ordinal) { [RoleKit.Probe.Named] = probe, ["later"] = probe };
        n = Nonce();
        Assert.Equal(RoleWhy.NotGranted, Judge(RoleKit.Act(key!, n, generation, act: "later"), n).Why);
        // STEWARDROLE1b: the build offers §3.1 rows 1 to 8, each in the table; none of rows 9 to 12 yet.
        Assert.Equal(["drive", "undrive", "trees", "hold", "resume", "stop", "finish", "retry"], StewardActs.Offered);
        Assert.Equal(StewardActs.Offered.Order(StringComparer.Ordinal), StewardActs.Table.Keys.Order(StringComparer.Ordinal));
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
