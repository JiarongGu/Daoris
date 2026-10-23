using System.Text.Json.Nodes;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <i>What needs you</i>'s accounts (UX6d, D150 §6.2–§6.3; TOOL4m's row), judged in .NET (PUSH1d4): a start waiting for accounts and a
/// signed-out account of the set, ported cell for cell from <c>accountAttention.test.ts</c> and <c>attention.test.ts</c>'s last block.
/// An account's name, the row's title and its line of why are the page's words over what is pushed here, held by its own tests.
/// </summary>
/// <remarks>
/// Rewritten by POOL1a2 (D197 point 1; the account-pool design §2.4, §5.2): <c>ACCOUNTS</c> answers one scope, the agent's set, and
/// <c>HARNESSES</c> no defaults, so the fixtures are one set; every account of it holds work, none runs only somewhere (no <c>runs</c>),
/// and none is outside it, so no row offers an account to let in or to read first. Each changed case says what it held before.
/// </remarks>
public sealed class AccountAttentionSliceTests
{
    private const string Read = "2026-10-05T10:42:00Z";
    private const string Until = "2026-10-06T04:42:00Z";
    private const string Cooling = """{"until":"2026-10-06T04:42:00Z","stated":true,"seen":"2026-10-05T10:00:00Z","assumedZone":false,"notBelieved":false}""";

    private static JsonObject Door(string over = "{}") => With(
        """
        {"harness":"claude-code","present":true,"product":"Claude Code","maker":"Anthropic","signsIn":true,"wire":"pipe",
         "ownLogin":"unknown","ownRead":null,
         "profiles":[
           {"name":"account-1","home":"H/account-1","login":"out","read":"2026-10-05T10:42:00Z","account":"you@work.example"},
           {"name":"account-2","home":"H/account-2","login":"in","read":"2026-10-05T09:00:00Z","displayName":"home"},
           {"name":"account-3","home":"H/account-3","login":"out","read":"2026-10-05T10:42:00Z"}]}
        """,
        over);

    /// <summary>The door's profiles with one of them changed.</summary>
    private static string Profiles(string name, string over) =>
        new JsonArray([.. Door()["profiles"]!.AsArray().Select(profile => (JsonNode)(profile!["name"]!.GetValue<string>() == name
            ? With(profile.AsObject(), over)
            : profile.DeepClone()))]).ToJsonString();

    /// <summary>The door with one of its profiles changed.</summary>
    private static JsonObject DoorWith(string name, string over) => With(Door(), "{\"profiles\":" + Profiles(name, over) + "}");

    private static string Scope(string over = "{}") => With(
        """{"workspace":null,"default":null,"list":[],"begins":null,"use":{"use":"goal","keep":null,"early":true,"near":90},"unknown":[],"problem":null,"near":[]}""",
        over).ToJsonString();

    /// <summary>
    /// The owner's install as D150 §0 read it, as one set: every workspace may run on all three accounts, one cooling, two signed out.
    /// </summary>
    private static JsonObject Use(string? set = null)
    {
        var listed = set ?? Scope("""{"list":["account-2","account-1","account-3"],"begins":"account-2"}""");
        return JsonNode.Parse(
            """{"agents":[{"agent":"claude-code","speaks":true,"own":{},"accounts":[{"name":"account-2","cooling":""" + Cooling + "}],\"scopes\":[" + listed + "]}]}")!.AsObject();
    }

    private static JsonObject AskIn(string id, string asked) => JsonNode.Parse($$"""
        {"id":"{{id}}","workspace":"work","sentence":"ask {{id}}","state":"Proposed","tier":"declarations","asked":"{{asked}}","updated":"{{asked}}",
         "links":[],"attachments":[],"proposal":[],"quests":[]}
        """)!.AsObject();

    private static JsonObject QuestIn(string over = "{}") => With(
        """{"id":"q1","from":"game","to":"engine","title":"Expose a streaming budget","body":"b","status":"Open","filed":"2026-10-05T10:30:00Z","updated":"2026-10-05T10:30:00Z","workspace":"work"}""",
        over);

    private static readonly JsonObject[] WorkRegistry = [JsonNode.Parse("""{"repository":"engine","workspace":"work","adopted":true,"registered":true,"owns":[],"accepts":[],"packs":[],"entries":1}""")!.AsObject()];

    /// <summary>The intake for two asks, held behind <c>account-2</c>'s cool-off, having passed the two signed out (D150 §6.3's drawing).</summary>
    private static JsonObject Wait(string over = "{}") => With(
        """{"agent":"claude-code","account":"account-2","name":"home","workspace":"work","until":"2026-10-06T04:42:00Z","stated":true,"quests":[],"asks":["a1","a2"],"signedOut":["account-1","account-3"]}""",
        over);

    private static JsonObject[] Asked => [AskIn("a1", "2026-10-05T10:25:00Z"), AskIn("a2", "2026-10-05T10:27:00Z")];

    /// <summary>The account rows alone, in the judge's order: an ask's proposal beside them is another kind's.</summary>
    private static List<JsonObject> Derive(
        JsonObject[]? doors = null, JsonObject? use = null, JsonObject[]? waits = null, JsonObject[]? considered = null,
        JsonObject[]? quests = null, JsonObject[]? asks = null, bool noUse = false) =>
    [
        .. Rows(new()
        {
            Doors = doors ?? [Door()], Use = noUse ? null : use ?? Use(), Waits = waits ?? [],
            Considered = considered ?? [], Quests = quests ?? [], Asks = asks ?? Asked, Registry = WorkRegistry,
        }).Where(row => row["kind"]!.GetValue<string>() is "account-wait" or "signed-out"),
    ];

    private static string Named(JsonObject row) =>
        string.Join(",", row["account"]!["named"]!.AsArray().Select(each => $"{each!["id"]?.GetValue<string>() ?? "own"}={each["state"]!.GetValue<string>()}"));

    private static string Ids(IEnumerable<JsonObject> rows) => string.Join(",", rows.Select(row => row["id"]!.GetValue<string>()));

    // ---- a start waiting for accounts ----

    /// <summary>What it holds, where, since when, and each account that would serve it as last known: the page words them.</summary>
    [Fact]
    public void A_waiting_start_carries_what_it_holds_where_since_when_and_each_account_that_would_serve_it()
    {
        var row = Assert.Single(Derive(waits: [Wait()]));

        Assert.Equal(("account-wait", "work", true), (row["kind"]!.GetValue<string>(), row["where"]!.GetValue<string>(), row["wholeWorkspace"]!.GetValue<bool>()));
        // Since the first ask was asked: its accounts were held before it.
        Assert.Equal("2026-10-05T10:25:00Z", row["since"]!.GetValue<string>());
        Is("""{"quests":[],"asks":["a1","a2"]}""", row["holding"]!.AsObject());
        Assert.Equal(("claude-code", "Claude Code", "claude-code", true),
            (row["account"]!["agent"]!.GetValue<string>(), row["account"]!["product"]!.GetValue<string>(), row["account"]!["harness"]!.GetValue<string>(), row["account"]!["signsIn"]!.GetValue<bool>()));
        Assert.Equal("account-2=cooling,account-1=out,account-3=out", Named(row));
        Is($$"""{"id":"account-2","name":"home","state":"cooling","read":"2026-10-05T09:00:00Z","until":"{{Until}}"}""", row["account"]!["named"]![0]!.AsObject());
    }

    /// <summary>
    /// LIMITRESUME1d2 (D206 §3.1): a cool-off <c>ACCOUNTS</c> says is held in memory only, the file not having taken it, is carried on
    /// its named account, so the band says Daoris could not keep it beside the account; one kept says nothing more.
    /// </summary>
    [Fact]
    public void A_cool_off_held_in_memory_only_is_carried_on_its_named_account()
    {
        var use = Use();
        use["agents"]![0]!["accounts"]![0]!["cooling"]!["unwritten"] = true;

        var row = Assert.Single(Derive(use: use, waits: [Wait()]));

        Is($$"""{"id":"account-2","name":"home","state":"cooling","read":"2026-10-05T09:00:00Z","until":"{{Until}}","unwritten":true}""",
            row["account"]!["named"]![0]!.AsObject());
        Assert.Null(Assert.Single(Derive(waits: [Wait()]))["account"]!["named"]![0]!["unwritten"]);
    }

    [Fact]
    public void A_waiting_start_holding_one_quest_is_in_its_repository_since_the_quest_was_filed()
    {
        Is("""{"quests":[],"asks":["a1"]}""", Assert.Single(Derive(waits: [Wait("""{"asks":["a1"]}""")]))["holding"]!.AsObject());

        var row = Assert.Single(Derive(waits: [Wait("""{"asks":[],"quests":["q1"]}""")], quests: [QuestIn()]));
        Assert.Equal(("engine", false, "2026-10-05T10:30:00Z"), (row["where"]!.GetValue<string>(), row["wholeWorkspace"]!.GetValue<bool>(), row["since"]!.GetValue<string>()));
    }

    [Fact]
    public void A_waiting_start_holding_quests_and_intakes_carries_both() =>
        Is("""{"quests":["q1","q2"],"asks":["a1","a2"]}""",
            Assert.Single(Derive(waits: [Wait("""{"quests":["q1","q2"]}""")], quests: [QuestIn(), QuestIn("""{"id":"q2","to":"game"}""")]))["holding"]!.AsObject());

    /// <summary>
    /// Waiting is counted from what the start was asked for, or from the limit that held it where that came later: a quest filed last
    /// week behind an account cooled an hour ago has waited an hour.
    /// </summary>
    [Fact]
    public void A_waiting_start_waits_since_its_work_was_asked_for_or_since_the_limit_where_that_came_later() =>
        Assert.Equal("2026-10-05T10:00:00Z",
            Assert.Single(Derive(waits: [Wait("""{"asks":[],"quests":["q1"]}""")], quests: [QuestIn("""{"filed":"2026-09-28T09:00:00Z"}""")]))["since"]!.GetValue<string>());

    /// <summary>A start whose work no record names waits since its limit, else since nothing known: never the clock's now.</summary>
    [Fact]
    public void A_waiting_start_whose_work_is_not_in_hand_waits_since_its_limit_or_nothing_known()
    {
        Assert.Equal("2026-10-05T10:00:00Z", Assert.Single(Derive(waits: [Wait()], asks: []))["since"]!.GetValue<string>());
        Assert.Null(Assert.Single(Derive(waits: [Wait("""{"account":"account-1"}""")], asks: []))["since"]);
    }

    private static readonly string HeldOnCooling = """
        {"quest":"q1","repository":"engine","verdict":"Blocked","reason":"cooling",
         "waitsFor":{"agent":"claude-code","account":"account-2","name":"home","until":"2026-10-06T04:42:00Z","stated":true},
         "signedOut":{"agent":"claude-code","accounts":["account-1","account-3"],"names":[null,null]}}
        """;

    /// <summary>An older shell's tick carries no waits: a quest's own wait is read from its consideration, as Overview reads it.</summary>
    [Fact]
    public void A_waiting_start_is_read_from_a_held_quest_s_consideration_where_the_tick_carries_no_wait()
    {
        var held = JsonNode.Parse(HeldOnCooling)!.AsObject();
        var row = Assert.Single(Derive(considered: [held, With(held, """{"quest":"q2"}""")], quests: [QuestIn(), QuestIn("""{"id":"q2"}""")], asks: []));

        Assert.Equal(("account-wait", "engine", false), (row["kind"]!.GetValue<string>(), row["where"]!.GetValue<string>(), row["wholeWorkspace"]!.GetValue<bool>()));
        Is("""{"quests":["q1","q2"],"asks":[]}""", row["holding"]!.AsObject());
    }

    /// <summary>A quest the tick's wait already holds is that wait's, never a row of its own beside it.</summary>
    [Fact]
    public void A_quest_is_listed_once_under_the_tick_s_wait()
    {
        var held = JsonNode.Parse("""{"quest":"q1","repository":"engine","verdict":"Blocked","reason":"cooling","waitsFor":{"agent":"claude-code","account":"account-2","until":"2026-10-06T04:42:00Z","stated":true}}""")!.AsObject();
        Assert.Single(Derive(waits: [Wait("""{"quests":["q1"]}""")], considered: [held], quests: [QuestIn()]));
    }

    /// <summary>TOOL6g: a start held on accounts read signed out with none cooling waits for a person, not a time.</summary>
    [Fact]
    public void A_waiting_start_on_signed_out_accounts_alone_names_each()
    {
        var held = JsonNode.Parse("""{"quest":"q1","repository":"engine","verdict":"Blocked","reason":"none ready","signedOut":{"agent":"claude-code","accounts":["account-1","account-3"]}}""")!.AsObject();
        var row = Assert.Single(Derive(use: Use(Scope("""{"list":["account-1","account-3"]}""")), considered: [held], quests: [QuestIn()], asks: []));

        Assert.Equal("account-1=out,account-3=out", Named(row));
        Assert.Equal(Read, row["account"]!["named"]![0]!["read"]!.GetValue<string>());
    }

    /// <summary>§6.3: an account the row names that no read has answered is unknown, never read, and never guessed signed in or out.</summary>
    [Fact]
    public void An_account_no_read_answered_is_unknown_never_read()
    {
        var doors = Door("""
            {"profiles":[{"name":"account-2","home":"H/account-2","login":"in","read":"2026-10-05T09:00:00Z","displayName":"home"},
                         {"name":"account-5","home":"H/account-5","login":"unknown","read":null}]}
            """);
        var row = Assert.Single(Derive(doors: [doors], use: Use(Scope("""{"list":["account-2","account-5"]}""")), waits: [Wait("""{"signedOut":[]}""")]));

        Assert.Equal("account-2=cooling,account-5=unknown", Named(row));
        Assert.Null(row["account"]!["named"]![1]!["read"]);
    }

    /// <summary>D130 point 6: a driven start drops the account kept for conversations, so the row does not name it as one that serves.</summary>
    [Fact]
    public void The_account_kept_for_conversations_is_left_out()
    {
        var use = Use(Scope("""{"list":["account-2","account-1","account-3"],"use":{"use":"goal","keep":"account-3","early":true,"near":90}}"""));
        // account-3, signed out and named by no wait, is a row of its own beside it: every account of the set holds work (POOL1a2).
        var rows = Derive(use: use, waits: [Wait("""{"signedOut":["account-1"]}""")]);
        Assert.Equal("account-2=cooling,account-1=out", Named(rows.Single(row => row["kind"]!.GetValue<string>() == "account-wait")));
    }

    /// <summary>
    /// Rewritten by POOL1a2 (D197 point 1): where every account a workspace could use was cooling and another was ready, the row
    /// offered to let that one run it (D130 §3.3) or to read an unread one first (UXFIX3), by the list the join added it to. No
    /// account is outside the set now, so a ready one, an unread one or a refused key is never offered, whatever the wait.
    /// </summary>
    [Fact]
    public void No_account_is_offered_to_let_in_or_to_read_first_since_none_is_outside_the_set()
    {
        var profiles = JsonNode.Parse(Profiles("account-1", "{}"))!.AsArray();
        profiles.Add(JsonNode.Parse("""{"name":"account-4","home":"H/account-4","login":"in","read":"2026-10-05T09:00:00Z"}"""));
        profiles.Add(JsonNode.Parse("""{"name":"account-0","home":"H/account-0","login":"unknown","read":null}"""));
        profiles.Add(JsonNode.Parse("""{"name":"account-5","home":"H/account-5","login":"out","key":"…abcd","read":"2026-10-05T09:00:00Z"}"""));
        var doors = new[] { With(Door(), "{\"profiles\":" + profiles.ToJsonString() + "}") };

        foreach (var wait in new[] { Wait("""{"signedOut":[]}"""), Wait("""{"workspace":null,"signedOut":[]}""") })
        {
            // POOL1c: the page reads neither now, so neither is written (they were null since POOL1a2).
            var facts = Derive(doors: doors, waits: [wait]).Single(row => row["kind"]!.GetValue<string>() == "account-wait")["account"]!.AsObject();
            Assert.False(facts.ContainsKey("outside"));
            Assert.False(facts.ContainsKey("readFirst"));
        }
    }

    /// <summary>ACCTUX1: a refused key the start would run on is said as refused, which no sign-in answers.</summary>
    [Fact]
    public void A_key_its_start_would_run_on_is_refused() =>
        Assert.Equal("account-2=cooling,account-1=out,account-3=refused",
            Named(Assert.Single(Derive(doors: [DoorWith("account-3", """{"key":"…abcd"}""")], waits: [Wait("""{"signedOut":["account-1"]}""")]))));

    /// <summary>
    /// The tool's own sign-in, cooling: named by the page as the person knows it. Rewritten by POOL1a2 (D197 point 3): it was the
    /// account where nothing named one; it is a conversation begun there now, which the set never serves, so the wait names it alone
    /// whatever the set holds.
    /// </summary>
    [Fact]
    public void The_tool_s_own_sign_in_cooling_is_named_alone_whatever_the_set_holds()
    {
        var row = Assert.Single(Derive(waits: [Wait("""{"account":null,"name":null,"signedOut":[]}""")]), row => row["kind"]!.GetValue<string>() == "account-wait");
        Is($$"""{"id":null,"name":null,"state":"cooling","read":null,"until":"{{Until}}"}""", Assert.Single(row["account"]!["named"]!.AsArray())!.AsObject());
    }

    /// <summary>Before the roster and the accounts' files answer, the tick's own facts still say the wait, with the names it read.</summary>
    [Fact]
    public void The_wait_is_said_from_the_tick_alone_before_anything_else_answers()
    {
        var row = Assert.Single(Derive(doors: [], noUse: true, waits: [Wait()]));

        Assert.Equal(("claude-code", false), (row["account"]!["product"]!.GetValue<string>(), row["account"]!["signsIn"]!.GetValue<bool>()));
        Assert.Null(row["account"]!["harness"]);
        Assert.Equal("account-2=cooling,account-1=out,account-3=out", Named(row));
        Assert.Equal(["home", null, null], row["account"]!["named"]!.AsArray().Select(each => each!["name"]?.GetValue<string>()));
    }

    // ---- a start held on signed-out accounts with none cooling (UX6d2) ----

    private static readonly string HeldSignedOut =
        """{"agent":"claude-code","account":null,"name":null,"workspace":"work","until":null,"stated":false,"quests":[],"asks":["a1"],"signedOut":["account-1"]}""";

    /// <summary>
    /// The agent's set is <c>account-1</c> alone. Rewritten by POOL1a2: <c>work</c> ran on it alone beside <c>account-4</c>, outside
    /// its list and never read; no account is outside the set now.
    /// </summary>
    private static List<JsonObject> SignedOutAlone(string door = "{}", JsonObject[]? waits = null, JsonObject[]? considered = null, JsonObject[]? quests = null, JsonObject[]? asks = null) =>
        Derive(
            doors: [With(Door("""{"profiles":[{"name":"account-1","home":"H/account-1","login":"out","read":"2026-10-05T10:42:00Z"}]}"""), door)],
            use: Use(Scope("""{"list":["account-1"],"begins":"account-1"}""")),
            waits: waits ?? [JsonNode.Parse(HeldSignedOut)!.AsObject()], considered: considered, quests: quests, asks: asks);

    /// <summary>A wait with no <c>until</c> is no cool-off: its accounts and its ask, and nothing to read first (POOL1a2).</summary>
    [Fact]
    public void A_wait_with_no_time_names_its_accounts_and_its_ask_and_never_the_own_sign_in_as_cooling()
    {
        var row = Assert.Single(SignedOutAlone());

        Assert.Equal(("wait:claude-code/:signed-out", "work", true, "2026-10-05T10:25:00Z"),
            (row["id"]!.GetValue<string>(), row["where"]!.GetValue<string>(), row["wholeWorkspace"]!.GetValue<bool>(), row["since"]!.GetValue<string>()));
        Assert.Equal("account-1=out", Named(row));
        Assert.False(row["account"]!.AsObject().ContainsKey("outside"));
        Assert.False(row["account"]!.AsObject().ContainsKey("readFirst"));
    }

    /// <summary>OWNSPACE1b-A: the tool's own sign-in read signed out holds no work, so it has no row.</summary>
    [Fact]
    public void The_tool_s_own_signed_out_sign_in_has_no_row() =>
        Assert.Equal("wait:claude-code/:signed-out", Ids(SignedOutAlone($$"""{"ownLogin":"out","ownRead":"{{Read}}"}""")));

    /// <summary>A cool-off of the tool's own sign-in names no account either: the two waits of one agent are two rows, two keys.</summary>
    [Fact]
    public void A_wait_with_no_time_is_a_row_of_its_own_beside_a_cool_off_of_the_own_sign_in()
    {
        var own = JsonNode.Parse($$"""{"agent":"claude-code","account":null,"name":null,"workspace":"aurora","until":"{{Until}}","stated":true,"quests":["q2"],"asks":[],"signedOut":[]}""")!.AsObject();
        var rows = SignedOutAlone(waits: [own, JsonNode.Parse(HeldSignedOut)!.AsObject()], quests: [QuestIn("""{"id":"q2","to":"game","workspace":"aurora"}""")]);

        Assert.Equal("wait:claude-code/,wait:claude-code/:signed-out", Ids(rows));
        Assert.Equal("own=cooling", Named(rows[0]));
        Assert.Equal("account-1=out", Named(rows[1]));
    }

    [Fact]
    public void A_quest_a_wait_with_no_time_holds_is_listed_once_under_it()
    {
        var held = JsonNode.Parse("""{"quest":"q1","repository":"engine","verdict":"Blocked","reason":"none ready","signedOut":{"agent":"claude-code","accounts":["account-1"]}}""")!.AsObject();
        var row = Assert.Single(SignedOutAlone(waits: [With(HeldSignedOut, """{"quests":["q1"],"asks":[]}""")], considered: [held], quests: [QuestIn()], asks: []));
        Assert.Equal("wait:claude-code/:signed-out", row["id"]!.GetValue<string>());
        Is("""{"quests":["q1"],"asks":[]}""", row["holding"]!.AsObject());
    }

    // ---- a start held with no Daoris account signed in (OWNSPACE1b-B, D175 point 3) ----

    private static readonly string NoAccount =
        """{"agent":"claude-code","account":null,"name":null,"workspace":"work","until":null,"stated":false,"quests":[],"asks":["a1"],"signedOut":[],"noAccount":true}""";

    private static List<JsonObject> None(string door = "{}", JsonObject[]? waits = null, JsonObject[]? considered = null, JsonObject[]? quests = null, JsonObject[]? asks = null) =>
        Derive(doors: [With(Door("""{"profiles":[]}"""), door)], use: Use(Scope()), waits: waits ?? [JsonNode.Parse(NoAccount)!.AsObject()],
            considered: considered, quests: quests, asks: asks);

    [Fact]
    public void No_account_signed_in_is_a_row_keyed_apart_naming_no_account()
    {
        var row = Assert.Single(None());

        Assert.Equal(("wait:claude-code:no-account", "work", true), (row["id"]!.GetValue<string>(), row["where"]!.GetValue<string>(), row["wholeWorkspace"]!.GetValue<bool>()));
        Assert.Equal((true, true, "claude-code"), (row["account"]!["noAccount"]!.GetValue<bool>(), row["account"]!["signsIn"]!.GetValue<bool>(), row["account"]!["harness"]!.GetValue<string>()));
        Assert.Empty(row["account"]!["named"]!.AsArray());
    }

    /// <summary>The tick's own fact, not an empty <c>signedOut</c>: a wait without it is the older wait on none cooling, however empty.</summary>
    [Fact]
    public void No_account_is_read_from_the_fact_never_from_an_empty_list()
    {
        var row = Assert.Single(None(waits: [With(NoAccount, """{"noAccount":false}""")]));
        Assert.Equal("wait:claude-code/:signed-out", row["id"]!.GetValue<string>());
        Assert.False(row["account"]!.AsObject().ContainsKey("noAccount"));
    }

    [Fact]
    public void No_account_is_read_from_a_held_quest_s_consideration_where_the_tick_carries_no_wait()
    {
        var held = JsonNode.Parse("""{"quest":"q1","repository":"engine","verdict":"Blocked","reason":"No account.","signedOut":{"agent":"claude-code","accounts":[],"noAccount":true}}""")!.AsObject();
        var row = Assert.Single(None(waits: [], considered: [held, With(held, """{"quest":"q2"}""")], quests: [QuestIn(), QuestIn("""{"id":"q2"}""")], asks: []));

        Assert.Equal(("engine", true), (row["where"]!.GetValue<string>(), row["account"]!["noAccount"]!.GetValue<bool>()));
        Is("""{"quests":["q1","q2"],"asks":[]}""", row["holding"]!.AsObject());
    }

    /// <summary>The one place the tool's own sign-in is said is its read-only row (D175 point 1): the hold never falls back to it.</summary>
    [Fact]
    public void No_account_never_names_the_tool_s_own_sign_in() =>
        Assert.Empty(Assert.Single(None($$"""{"ownLogin":"out","ownRead":"{{Read}}"}"""))["account"]!["named"]!.AsArray());

    // ---- a signed-out account of the set ----

    /// <summary>
    /// UXFIX3: only when it was read is known, never since when it holds work, so it carries its reading and no wait. Rewritten by
    /// POOL1a2: it said where it ran, <c>runs</c>; every account of the set serves every workspace now, so it says none, and since
    /// POOL1c no <c>outside</c> either.
    /// </summary>
    [Fact]
    public void A_signed_out_account_of_the_set_is_a_row_of_its_own_carrying_its_reading_and_no_wait()
    {
        var rows = Derive();

        Assert.Equal("signed-out:claude-code/account-1,signed-out:claude-code/account-3", Ids(rows));
        Is($$$"""
           {"kind":"signed-out","id":"signed-out:claude-code/account-1","since":null,"read":"{{{Read}}}","where":"Claude Code",
            "account":{"agent":"claude-code","product":"Claude Code","harness":"claude-code","signsIn":true,
             "named":[{"id":"account-1","name":null,"state":"out","read":"{{{Read}}}","until":null}]}}
           """, rows[0]);
    }

    /// <summary>ACCTUX1: a key its provider refused waits on the person as a sign-out does: a signed-out row, said refused.</summary>
    [Fact]
    public void A_key_its_provider_refused_is_a_signed_out_row_said_refused()
    {
        var rows = Derive(doors: [DoorWith("account-3", """{"key":"…abcd"}""")]);

        Assert.Equal("signed-out:claude-code/account-1,signed-out:claude-code/account-3", Ids(rows));
        Assert.Equal("account-3=refused", Named(rows[1]));
        Assert.Equal("Claude Code", rows[1]["where"]!.GetValue<string>());
        Assert.False(rows[1].ContainsKey("runs"));
    }

    /// <summary>ACCOUNTID1b: two accounts of one email are two rows, each named by the page from the roster it holds.</summary>
    [Fact]
    public void Two_signed_out_accounts_of_one_email_are_two_rows()
    {
        var doors = Door("""
            {"profiles":[{"name":"acct-0000000a","home":"H/a","login":"out","read":"2026-10-05T10:42:00Z","account":"person@example.invalid","org":"Org A"},
                         {"name":"acct-0000000b","home":"H/b","login":"out","read":"2026-10-05T10:42:00Z","account":"person@example.invalid","org":"Org B"}]}
            """);
        var rows = Derive(doors: [doors], use: Use(Scope("""{"list":["acct-0000000a","acct-0000000b"]}""")));
        Assert.Equal("signed-out:claude-code/acct-0000000a,signed-out:claude-code/acct-0000000b", Ids(rows));
    }

    [Fact]
    public void A_signed_out_account_says_no_wait_however_recently_it_was_read_again()
    {
        var row = Derive(doors: [DoorWith("account-1", """{"read":"2026-10-05T10:50:00.000Z"}""")])[0];
        Assert.Equal(("signed-out:claude-code/account-1", "2026-10-05T10:50:00.000Z"), (row["id"]!.GetValue<string>(), row["read"]!.GetValue<string>()));
        Assert.Null(row["since"]);
    }

    /// <summary>§6.3: a start waiting only on signed-out accounts carries their sign-in, and they are not rows of their own.</summary>
    [Fact]
    public void A_signed_out_account_a_waiting_start_names_is_listed_once() =>
        Assert.Equal(["account-wait"], Derive(waits: [Wait()]).Select(row => row["kind"]!.GetValue<string>()));

    /// <summary>
    /// Rewritten by POOL1a2 (D197 point 1): a signed-out account that no list and no default held was no row, holding no work. Every
    /// account of the set holds work now, so it is a row however the set was answered; an agent not installed has none.
    /// </summary>
    [Fact]
    public void A_signed_out_account_is_a_row_whatever_the_set_answered_and_of_an_agent_not_installed_is_none()
    {
        Assert.Equal("signed-out:claude-code/account-1,signed-out:claude-code/account-3", Ids(Derive(use: Use(Scope()))));
        Assert.Empty(Derive(doors: [Door("""{"present":false}""")]));
    }

    /// <summary>OWNSPACE1b-A (D175 point 1): the own sign-in holds no work, so it is never listed.</summary>
    [Fact]
    public void The_tool_s_own_sign_in_which_no_start_runs_on_is_left_out() =>
        Assert.Empty(Derive(doors: [Door($$"""{"profiles":[],"ownLogin":"out","ownRead":"{{Read}}"}""")], use: Use(Scope())));

    [Fact]
    public void Nothing_is_listed_with_no_tick_no_roster_and_no_accounts() =>
        Assert.Empty(Derive(doors: [], noUse: true, waits: [], asks: []));

    /// <summary>
    /// UXFIX3: a signed-out account's row knows when it was read, not since when it holds work: no wait however its reading moves.
    /// Rewritten by POOL1a2: it was held by a door's default and a workspace's, as a list holds one; every account of the set holds
    /// work now, with no accounts' answer read at all.
    /// </summary>
    [Fact]
    public void A_signed_out_account_of_the_set_beside_a_parked_session_has_no_wait()
    {
        JsonObject DoorRead(string one, string two) => Door($$"""
            {"profiles":[{"name":"account-1","home":"H/account-1","login":"out","read":"{{one}}"},{"name":"account-2","home":"H/account-2","login":"out","read":"{{two}}"}]}
            """);
        AttentionFacts Facts(JsonObject door) => new() { Sessions = [Session("""{"state":"awaiting-person"}""")], Registry = [Registration("engine")], Doors = [door] };

        var before = Rows(Facts(DoorRead("2026-09-21T09:30:00Z", "2026-09-21T08:00:00Z")));
        var after = Rows(Facts(DoorRead("2026-09-21T11:58:00Z", "2026-09-21T08:00:00Z")));

        Assert.Equal(["parked:s1a2b3c4", "signed-out:signed-out:claude-code/account-1", "signed-out:signed-out:claude-code/account-2"], Keys(after));
        Assert.Equal(Keys(before), Keys(after));
        Assert.All(after.Where(row => row["kind"]!.GetValue<string>() == "signed-out"), row => Assert.Null(row["since"]));
    }

    /// <summary>The accounts' rows carry only what the allow-list names, the account facts and each account named picked to their own.</summary>
    [Fact]
    public void Every_field_an_account_row_writes_is_named_in_the_allow_list()
    {
        var rows = Rows(new()
        {
            Doors = [Door()], Use = Use(),
            Waits = [Wait("""{"signedOut":[]}"""), JsonNode.Parse(NoAccount)!.AsObject(), Wait("""{"agent":"codex","workspace":"work"}""")],
            Asks = Asked, Registry = WorkRegistry,
        });

        // No account is outside the set (POOL1a2), and since POOL1c neither field the page no longer reads is written.
        Assert.DoesNotContain(rows, row => row["account"] is JsonObject account && (account.ContainsKey("outside") || account.ContainsKey("readFirst")));
        Assert.Contains(rows, row => row["account"]?["noAccount"] is not null);
        foreach (var row in rows) Assert.Equal(Normal(row), Normal(AttentionStateSlice.Picked(row)));
    }
}
