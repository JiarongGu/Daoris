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
public sealed partial class StandingConfirmationTests : Bridge
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
}
