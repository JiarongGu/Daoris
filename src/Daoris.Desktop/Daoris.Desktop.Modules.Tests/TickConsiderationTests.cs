using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// One consideration as the loop's tick hands it to the page (<see cref="DriverLoop.TickConsideration"/>): the quest, its
/// repository, the verdict and the driver's sentence, and since SESSUX1d the session a person's stop holds it by.
/// </summary>
public sealed class TickConsiderationTests
{
    private static readonly JsonSerializerOptions Wire = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly QuestView Quest = new("q1", "game", "engine", "Expose a streaming budget", "A body.", "Taken");

    /// <summary>
    /// SESSUX1d (D126 §3.3): the page says a stop's sentence in the reader's language by its verdict, and the sentence
    /// names the session stopped, which the tick now carries as a fact rather than inside the driver's English.
    /// </summary>
    [Fact]
    public void A_quest_held_by_a_stop_names_the_session_that_holds_it()
    {
        var held = new Consideration(Quest, StartVerdict.Stopped, "you stopped session `s1a2b3c4`; Try again carries it on.")
        {
            HeldBy = new PriorSession("s1a2b3c4", "D:/trees/s-1", "stopped"),
        };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held), Wire);

        Assert.Equal("q1", shape.GetProperty("quest").GetString());
        Assert.Equal("engine", shape.GetProperty("repository").GetString());
        Assert.Equal("Stopped", shape.GetProperty("verdict").GetString());
        Assert.Equal("s1a2b3c4", shape.GetProperty("heldBy").GetString());
        // The tree stays here: the page is told whose stop, never where its work is.
        Assert.DoesNotContain("trees", shape.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// PAUSE1b (D132 §2.3): a quest a pause holds names whose pause, its scope and id, as facts beside the driver's sentence,
    /// so the page says it in the reader's language; every other verdict names none.
    /// </summary>
    [Fact]
    public void A_quest_a_pause_holds_names_whose_pause()
    {
        var paused = new Consideration(Quest, StartVerdict.Paused, "paused with ask `#a1`; Resume carries it on — `daoris-driver ask --resume a1`.")
        {
            PausedBy = new PausedBy(WorkScope.Ask, "a1"),
        };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(paused), Wire);
        var other = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(new Consideration(Quest, StartVerdict.Start, "starting.")), Wire);

        Assert.Equal("Paused", shape.GetProperty("verdict").GetString());
        Assert.Equal("ask", shape.GetProperty("pausedBy").GetProperty("scope").GetString());
        Assert.Equal("a1", shape.GetProperty("pausedBy").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, other.GetProperty("pausedBy").ValueKind);
    }

    /// <summary>
    /// UPDATE1 (D139 §2): a quest an update's drain holds is said so by a fact, <c>forUpdate</c>, so the page says it in the
    /// reader's language rather than the driver's English; every other verdict, a person's hold included, carries none.
    /// </summary>
    [Fact]
    public void A_quest_an_update_holds_is_marked_for_the_update_and_no_other_is()
    {
        var drained = new Consideration(Quest, StartVerdict.Blocked, InstallUpdate.HoldReason);
        var person = new Consideration(Quest, StartVerdict.Held, "`engine` is held by the person.");
        var plugin = new Consideration(Quest, StartVerdict.Blocked, "held by plugin `hold-by-title`: the title asks for a hold.");

        Assert.True(JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(drained), Wire).GetProperty("forUpdate").GetBoolean());
        Assert.Equal(JsonValueKind.Null, JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(person), Wire).GetProperty("forUpdate").ValueKind);
        Assert.Equal(JsonValueKind.Null, JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(plugin), Wire).GetProperty("forUpdate").ValueKind);
    }

    /// <summary>
    /// SESSUX1i (D126 §4.6): Overview's *What needs you* holds a quest parked on its failed sessions, waiting since its last
    /// session ended, and says why in the reader's language. The tick carries both as facts: how many failed and when the
    /// last one ended. The session and its note stay here.
    /// </summary>
    [Fact]
    public void A_parked_quest_says_how_many_failed_and_since_when()
    {
        var parked = new Consideration(Quest, StartVerdict.Exhausted, "3 session(s) have failed on `#q1` without landing anything.");
        var park = new QuestPark("q1", "engine")
        {
            Session = "s3", Strikes = 3, Note = "You've hit your limit.", Since = new DateTimeOffset(2026, 10, 1, 9, 21, 0, TimeSpan.Zero),
        };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(parked, park), Wire);

        Assert.Equal(3, shape.GetProperty("strikes").GetInt32());
        Assert.Equal(park.Since, shape.GetProperty("since").GetDateTimeOffset());
        Assert.DoesNotContain("limit", shape.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("s3", shape.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>A park the loop has not read the records for yet, and every other verdict, says neither.</summary>
    [Fact]
    public void A_quest_with_no_park_read_says_no_number_and_no_time()
    {
        var parked = new Consideration(Quest, StartVerdict.Exhausted, "parked after 3 failed sessions.");
        var waiting = new Consideration(Quest, StartVerdict.RepositoryBusy, "`engine` is busy.");

        foreach (var shape in new[]
        {
            JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(parked), Wire),
            JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(waiting, new QuestPark("q1", "engine") { Strikes = 3 }), Wire),
        })
        {
            Assert.Equal(JsonValueKind.Null, shape.GetProperty("strikes").ValueKind);
            Assert.Equal(JsonValueKind.Null, shape.GetProperty("since").ValueKind);
        }
    }

    /// <summary>
    /// TOOL4g (D125 §4): a quest held at spawn because every account its start may use is cooling waits for an account — not
    /// parked, no Retry — and the page says so in the reader's language from the wait's facts: whose account, which, until
    /// when, and whether the agent named the time. The driver's sentence still travels as its reason.
    /// </summary>
    [Fact]
    public void A_quest_waiting_for_an_account_says_whose_account_and_until_when()
    {
        var until = new DateTimeOffset(2026, 10, 3, 16, 2, 0, TimeSpan.Zero);
        var held = new Consideration(Quest, StartVerdict.Blocked, "the `claude-code` account `account-1` is cooling until Oct 3, 16:02 (UTC).");
        var wait = new AccountWait("claude-code-acp", "claude-code", "account-1", "work", until, true, held.Reason) { Quests = ["q1"] };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held, null, wait), Wire);

        Assert.Equal("Blocked", shape.GetProperty("verdict").GetString());
        var waits = shape.GetProperty("waitsFor");
        Assert.Equal("claude-code", waits.GetProperty("agent").GetString());
        Assert.Equal("account-1", waits.GetProperty("account").GetString());
        Assert.Equal(until, waits.GetProperty("until").GetDateTimeOffset());
        Assert.True(waits.GetProperty("stated").GetBoolean());
    }

    /// <summary>A wait that holds other quests, or none, names no wait on this one; the tool's own sign-in is a null account.</summary>
    [Fact]
    public void A_quest_no_wait_holds_names_none_and_the_own_sign_in_is_no_account()
    {
        var until = new DateTimeOffset(2026, 10, 3, 16, 2, 0, TimeSpan.Zero);
        var held = new Consideration(Quest, StartVerdict.Blocked, "`claude-code`'s own sign-in is cooling.");
        var other = new AccountWait("claude-code", "claude-code", null, null, until, false, held.Reason) { Quests = ["q9"] };
        var own = other with { Quests = ["q1"] };

        Assert.Equal(JsonValueKind.Null,
            JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held, null, other), Wire).GetProperty("waitsFor").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held), Wire).GetProperty("waitsFor").ValueKind);
        var waits = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held, null, own), Wire).GetProperty("waitsFor");
        Assert.Equal(JsonValueKind.Null, waits.GetProperty("account").ValueKind);
        Assert.False(waits.GetProperty("stated").GetBoolean());
    }

    /// <summary>
    /// TOOL6g: a held start that passed accounts not signed in names them, whose they are, so the page says each and its
    /// sign-in in the reader's language, beside a wait on a cooling account or with none.
    /// </summary>
    [Fact]
    public void A_held_quest_names_the_accounts_it_passed_not_signed_in()
    {
        var held = new Consideration(Quest, StartVerdict.Blocked, "no `claude-code` account this start may use is ready: …")
        {
            SignedOut = new SignedOutAccounts("claude-code", ["account-1", "account-2"]),
        };

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held), Wire);

        var signedOut = shape.GetProperty("signedOut");
        Assert.Equal("claude-code", signedOut.GetProperty("agent").GetString());
        Assert.Equal(["account-1", "account-2"], signedOut.GetProperty("accounts").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(JsonValueKind.Null,
            JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(held with { SignedOut = null }), Wire).GetProperty("signedOut").ValueKind);
    }

    /// <summary>Every other verdict holds by no session, and says none.</summary>
    [Fact]
    public void A_quest_no_stop_holds_names_no_session()
    {
        var parked = new Consideration(Quest, StartVerdict.Exhausted, "parked after 3 failed sessions.");

        var shape = JsonSerializer.SerializeToElement(DriverLoop.TickConsideration(parked), Wire);

        Assert.Equal("Exhausted", shape.GetProperty("verdict").GetString());
        Assert.Equal(JsonValueKind.Null, shape.GetProperty("heldBy").ValueKind);
    }
}
