using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Who needs the person, judged in .NET and pushed (PUSH1d4, D182 points 2-4 and 6): the judge the page ran as <c>needsAPerson</c>
/// and <c>waitingInSessions</c>, ported cell for cell from <c>attention.test.ts</c>, <c>attention.review.test.ts</c> and
/// <c>WorkMilestone.test.tsx</c>. What a row says (its title, its detail's words) and the order the band draws stay the page's, held by
/// its own tests over the rows pushed here; each row's membership, its identity and the values it carries are held here.
/// </summary>
/// <remarks>Every input is the test's own JSON in the page's shapes, as the other slices and the routes hold them: no service runs.</remarks>
public sealed class AttentionStateSliceTests : DriverModuleBridge
{
    private static JsonObject Quest(string over = "{}") => With(
        """{"id":"7a82cc","from":"platform","to":"engine","title":"Expose a streaming budget on the chunk API","body":"World streaming needs a per-frame cap.","status":"Open","filed":"2026-09-14T09:00:00Z","updated":"2026-09-14T09:00:00Z"}""",
        over);

    /// <summary>An ask the declarations tier proposed and nobody has settled (INT4a).</summary>
    private static JsonObject Ask(string over = "{}") => With(
        """
        {"id":"7c1e9a04b2d5","workspace":"aurora","sentence":"The chunk streamer stalls on a cold cache.\n\nThe trace is attached.","state":"Proposed",
         "tier":"declarations","asked":"2026-09-21T08:30:00Z","updated":"2026-09-21T08:30:00Z","links":[],"attachments":[],
         "proposal":[{"repository":"engine","score":5,"matched":["chunk","stream"]},{"repository":"game","score":2,"matched":["cache"]}],"quests":[]}
        """,
        over);

    /// <summary>The session an intake opened for the ask above (D65 §1b): a chat, in the ask's name.</summary>
    private static JsonObject Intake(string over = "{}") => With(
        Session("""{"id":"i9n8t7k6","repository":"ask #7c1e9a04b2d5","kind":"chat","adapter":"stub","ask":"7c1e9a04b2d5"}"""), over);

    private static JsonObject Only(IReadOnlyList<JsonObject> rows, string kind) => Assert.Single(rows, row => row["kind"]!.GetValue<string>() == kind);

    // ---- what needs a person ----

    [Fact]
    public void Is_empty_when_nothing_is_parked_and_every_open_quest_has_a_receiver() =>
        Assert.Empty(Rows(new() { Sessions = [Session()], Quests = [Quest()], Registry = [Registration("engine")] }));

    /// <summary>A parked session, with the analysis it is waiting on: its note whole, which the row words, its title the page's.</summary>
    [Fact]
    public void Names_a_parked_session_with_the_analysis_it_is_waiting_on()
    {
        var rows = Rows(new()
        {
            Sessions = [Session("""{"state":"awaiting-person","quest":"7a82cc","note":"two ways forward."}""")],
            Quests = [Quest("""{"status":"Taken"}""")],
            Registry = [Registration("engine")],
        });

        Is("""{"kind":"parked","id":"s1a2b3c4","since":"2026-09-21T10:00:00Z","workspace":"default","where":"engine","note":{"note":"two ways forward."}}""", Assert.Single(rows));
    }

    /// <summary>LANG1b: the record's note travels whole, its parts beside its English, for the row to word in the reader's language.</summary>
    [Fact]
    public void Hands_the_row_a_parked_session_s_note_with_its_parts_never_a_sentence_made_of_them()
    {
        var parts = """[{"code":"ended.parked-asked","values":{},"text":"It stopped with its quest still taken, to ask you:"}]""";
        var row = Assert.Single(Rows(new()
        {
            Sessions = [Session($$"""{"state":"awaiting-person","quest":"7a82cc","note":"It stopped…","noteParts":{{parts}}}""")],
            Quests = [Quest("""{"status":"Taken"}""")],
            Registry = [Registration("engine")],
        }));

        Assert.Equal(Normal(JsonNode.Parse($$"""{"note":"It stopped…","parts":{{parts}}}""")!), Normal(row["note"]!));
        Assert.False(row.ContainsKey("detail"));
    }

    /// <summary>
    /// The publish door refuses a quest addressed to a non-adopter, so one only comes to exist afterwards: the receiver retired, or its
    /// registration never reached this deployment. Either way no agent will ever pull it, and nothing else in the platform says so.
    /// </summary>
    [Fact]
    public void Names_an_open_quest_addressed_to_a_repository_this_deployment_does_not_hold()
    {
        var rows = Rows(new() { Quests = [Quest("""{"to":"retired"}""")], Registry = [Registration("engine")] });

        Is("""{"kind":"unanswerable","id":"7a82cc","since":"2026-09-14T09:00:00Z","workspace":"default","where":"retired"}""", Assert.Single(rows));
    }

    [Fact]
    public void Matches_a_receiver_whatever_its_case() =>
        Assert.Empty(Rows(new() { Quests = [Quest("""{"to":"Engine"}""")], Registry = [Registration("engine")] }));

    [Fact]
    public void Leaves_a_quest_somebody_already_took_out_of_it() =>
        Assert.Empty(Rows(new() { Quests = [Quest("""{"to":"retired","status":"Taken"}""")] }));

    /// <summary>A parked session holds a working tree while it waits; a sitting quest holds nothing. Each says since when, which the page orders by.</summary>
    [Fact]
    public void Lists_parked_sessions_and_quests_nobody_can_take_each_with_since_when()
    {
        var rows = Rows(new()
        {
            Sessions =
            [
                Session("""{"id":"newer","state":"awaiting-person","updated":"2026-09-21T11:00:00Z"}"""),
                Session("""{"id":"older","state":"awaiting-person","updated":"2026-09-21T08:00:00Z"}"""),
            ],
            Quests =
            [
                Quest("""{"id":"q-new","to":"gone","filed":"2026-09-20T00:00:00Z"}"""),
                Quest("""{"id":"q-old","to":"gone","filed":"2026-09-01T00:00:00Z"}"""),
            ],
            Registry = [Registration("engine")],
        });

        Assert.Equal(["parked:newer", "parked:older", "unanswerable:q-new", "unanswerable:q-old"], Keys(rows));
        Assert.Equal("2026-09-01T00:00:00Z", rows.Single(row => row["id"]!.GetValue<string>() == "q-old")["since"]!.GetValue<string>());
    }

    // ---- an ask that waits on a person (INT4d) ----

    [Fact]
    public void An_ask_is_a_proposal_while_nothing_serves_it_with_its_circle_since_it_was_asked_and_what_was_proposed()
    {
        var rows = Rows(new() { Registry = [Registration("engine")], Asks = [Ask()] });

        Is("""
           {"kind":"proposal","id":"7c1e9a04b2d5","since":"2026-09-21T08:30:00Z","workspace":"aurora","where":"aurora",
            "publishTo":["engine","game"],"choices":[]}
           """, Assert.Single(rows));
    }

    [Fact]
    public void A_proposal_carries_the_refused_receiver_s_sentence_verbatim()
    {
        const string note = "`newcomer` is not an adopted repository in `aurora` — only an adopter can be asked.";
        var row = Assert.Single(Rows(new() { Asks = [Ask(JsonSerializer.Serialize(new { note }))] }));
        Assert.Equal(note, row["detail"]!.GetValue<string>());
    }

    [Fact]
    public void A_proposal_the_declarations_proposed_nobody_for_carries_no_receiver_and_no_sentence()
    {
        var row = Assert.Single(Rows(new() { Asks = [Ask("""{"proposal":[]}""")] }));
        Assert.Equal("[]", row["publishTo"]!.ToJsonString());
        Assert.False(row.ContainsKey("detail"));
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("starting")]
    [InlineData("working")]
    public void An_ask_is_the_harness_s_while_its_intake_is_busy(string state) =>
        Assert.Empty(Rows(new() { Sessions = [Intake($$"""{"state":"{{state}}"}""")], Asks = [Ask("""{"intake":"i9n8t7k6"}""")] }));

    /// <summary>
    /// The parked intake is a session awaiting a person AND the ask is waiting: one thing, counted once. The answer is on the ask
    /// (publish or close), so the ask's row stands for both, with the intake's own note.
    /// </summary>
    [Fact]
    public void An_ask_whose_intake_parked_asking_is_one_row_the_ask_s_with_the_intake_s_note()
    {
        const string note = "published nothing: the declarations did not settle ask `#7c1e9a04b2d5`, so it asks you rather than guess.";
        var rows = Rows(new()
        {
            Sessions = [Intake(JsonSerializer.Serialize(new { state = "awaiting-person", updated = "2026-09-21T09:45:00Z", note }))],
            Asks = [Ask("""{"intake":"i9n8t7k6"}""")],
        });

        var expected = JsonNode.Parse("""
            {"kind":"intake","id":"7c1e9a04b2d5","since":"2026-09-21T09:45:00Z","workspace":"aurora","where":"aurora","note":{}}
            """)!.AsObject();
        expected["note"]!["note"] = note;
        Is(expected.ToJsonString(), Assert.Single(rows));
    }

    [Fact]
    public void A_parked_intake_whose_ask_is_not_in_hand_stays_a_parked_session() =>
        Assert.Equal(["parked:i9n8t7k6"], Keys(Rows(new() { Sessions = [Intake("""{"state":"awaiting-person"}""")] })));

    [Fact]
    public void An_ask_is_a_proposal_again_once_its_intake_ended_without_publishing() =>
        Assert.Equal(["proposal:7c1e9a04b2d5"], Keys(Rows(new() { Asks = [Ask("""{"intake":"i9n8t7k6"}""")] })));

    [Fact]
    public void An_ask_that_became_quests_or_was_closed_is_left_out() =>
        Assert.Empty(Rows(new()
        {
            Asks = [Ask("""{"state":"Published","quests":["9a8b7c6d5e4f"]}"""), Ask("""{"id":"closed","state":"Closed","note":"Answered elsewhere."}""")],
        }));

    /// <summary>UX6c: an ask waits for the person's word beside a quest nobody can take; each says since when, which the page orders by.</summary>
    [Fact]
    public void Asks_and_a_parked_session_and_a_quest_nobody_can_take_are_each_a_row()
    {
        var rows = Rows(new()
        {
            Sessions =
            [
                Session("""{"id":"parked","state":"awaiting-person","updated":"2026-09-21T11:00:00Z"}"""),
                Intake("""{"state":"awaiting-person","updated":"2026-09-21T07:00:00Z"}"""),
            ],
            Quests = [Quest("""{"id":"sitting","to":"gone","filed":"2026-09-01T00:00:00Z"}""")],
            Registry = [Registration("engine")],
            Asks = [Ask("""{"id":"later","asked":"2026-09-21T10:00:00Z"}"""), Ask("""{"intake":"i9n8t7k6"}""")],
        });

        Assert.Equal(["intake:7c1e9a04b2d5", "parked:parked", "proposal:later", "unanswerable:sitting"], Keys(rows));
    }

    /// <summary>UX6c: a proposal publishes where its declarations named receivers, and chooses among whoever its workspace can ask.</summary>
    [Fact]
    public void A_proposal_names_whom_its_publish_goes_to_and_whom_it_could_choose_from_the_ask_and_the_registry()
    {
        JsonObject InAurora(string name, string over = "{}") => With(Registration(name, """{"workspace":"aurora"}"""), over);
        var item = Assert.Single(Rows(new()
        {
            Registry = [InAurora("game"), InAurora("engine"), InAurora("tools", """{"adopted":false}"""), Registration("elsewhere")],
            Asks = [Ask()],
        }));

        Assert.Equal("""["engine","game"]""", item["publishTo"]!.ToJsonString());
        Assert.Equal("""["engine","game"]""", item["choices"]!.ToJsonString());
        var nobody = Assert.Single(Rows(new() { Registry = [InAurora("engine")], Asks = [Ask("""{"proposal":[]}""")] }));
        Assert.Equal("[]", nobody["publishTo"]!.ToJsonString());
        Assert.Equal("""["engine"]""", nobody["choices"]!.ToJsonString());
    }

    /// <summary>D70: the host's own judgement of who can be asked, where it answers one; adoption where it answers none.</summary>
    [Fact]
    public void A_proposal_chooses_among_what_the_host_says_can_be_asked()
    {
        var item = Assert.Single(Rows(new()
        {
            Registry =
            [
                Registration("rooted", """{"workspace":"aurora","adopted":false,"addressable":true}"""),
                Registration("refused", """{"workspace":"aurora","adopted":true,"addressable":false}"""),
            ],
            Asks = [Ask()],
        }));

        Assert.Equal("""["rooted"]""", item["choices"]!.ToJsonString());
    }

    // ---- a folder waiting on the person's trust (D73) ----

    private const string Hold = """{"folder":"C:/somewhere/engine","trustFile":"C:/somewhere/data/harnesses/claude-code/work/.claude.json"}""";

    [Fact]
    public void A_folder_is_one_row_naming_the_folder_and_what_it_holds_since_the_oldest_thing_it_holds()
    {
        var rows = Rows(new()
        {
            Quests = [Quest("""{"id":"q1","filed":"2026-09-14T09:00:00Z"}"""), Quest("""{"id":"q2","filed":"2026-09-10T09:00:00Z"}""")],
            Registry = [Registration("engine")],
            Untrusted = [With(Hold, """{"quest":"q1"}"""), With(Hold, """{"quest":"q2"}""")],
        });

        Is("""
           {"kind":"trust","id":"C:/somewhere/data/harnesses/claude-code/work/.claude.json\nC:/somewhere/engine","since":"2026-09-10T09:00:00Z",
            "workspace":"default","where":"engine","trust":{"folder":"C:/somewhere/engine","trustFile":"C:/somewhere/data/harnesses/claude-code/work/.claude.json"},
            "quest":"q1"}
           """, Assert.Single(rows));
    }

    /// <summary>REV3 web-work F12: one folder held in two accounts' files is two grants, and two rows, each with an id of its own.</summary>
    [Fact]
    public void One_folder_held_in_two_files_is_two_rows_with_ids_of_their_own()
    {
        var other = With(Hold, """{"trustFile":"C:/somewhere/data/harnesses/claude-code/personal/.claude.json"}""");
        var rows = Rows(new() { Quests = [Quest()], Registry = [Registration("engine")], Untrusted = [JsonNode.Parse(Hold)!.AsObject(), other] })
            .Where(row => row["kind"]!.GetValue<string>() == "trust").ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(row => row["id"]!.GetValue<string>()).Distinct().Count());
    }

    /// <summary>
    /// POOL1a3 (D197 point 3; the account-pool design §3 point 4): a hold whose file is the own sign-in's carries <c>own</c> on its
    /// row's trust, so the page draws its sentence with no press; any other hold carries none.
    /// </summary>
    [Fact]
    public void A_hold_on_the_own_sign_in_s_file_carries_own_and_another_carries_none()
    {
        var own = With(Hold, """{"quest":"q1","trustFile":"X:/person/.claude.json","own":true}""");
        var rows = Rows(new() { Quests = [Quest("""{"id":"q1"}""")], Registry = [Registration("engine")], Untrusted = [own, With(Hold, """{"quest":"q1","own":false}""")] })
            .Where(row => row["kind"]!.GetValue<string>() == "trust").ToList();

        Assert.Equal(2, rows.Count);
        Assert.True(rows.Single(row => row["trust"]!["trustFile"]!.GetValue<string>() == "X:/person/.claude.json")["trust"]!["own"]!.GetValue<bool>());
        Assert.False(rows.Single(row => row["trust"]!["trustFile"]!.GetValue<string>() != "X:/person/.claude.json")["trust"]!.AsObject().ContainsKey("own"));
    }

    /// <summary>An intake's room is named by its ask, which the page words; it waits since the ask was asked.</summary>
    [Fact]
    public void An_intake_s_room_carries_its_ask_since_the_ask_was_asked()
    {
        var room = """{"folder":"C:/somewhere/data/intake/aurora","trustFile":"C:/somewhere/data/harnesses/claude-code/work/.claude.json","ask":"7c1e9a04b2d5"}""";
        var item = Only(Rows(new() { Asks = [Ask()], Untrusted = [JsonNode.Parse(room)!.AsObject()] }), "trust");

        Assert.Equal(("7c1e9a04b2d5", "2026-09-21T08:30:00Z"), (item["ask"]!.GetValue<string>(), item["since"]!.GetValue<string>()));
        Assert.Null(item["where"]);
        Assert.False(item.ContainsKey("quest"));
    }

    [Fact]
    public void A_folder_s_row_holds_work_beside_a_parked_session_and_an_ask() =>
        Assert.Equal(
            ["parked:parked", "proposal:7c1e9a04b2d5", "trust:C:/somewhere/data/harnesses/claude-code/work/.claude.json\nC:/somewhere/engine"],
            Keys(Rows(new()
            {
                Sessions = [Session("""{"id":"parked","state":"awaiting-person","updated":"2026-09-21T09:00:00Z"}""")],
                Quests = [Quest("""{"id":"q1"}""")],
                Registry = [Registration("engine")],
                Asks = [Ask()],
                Untrusted = [With(Hold, """{"quest":"q1"}""")],
            })));

    /// <summary>Its door is what it holds: the quest's page, or the ask's for an intake's room.</summary>
    [Fact]
    public void A_folder_s_row_names_the_quest_or_the_ask_it_holds()
    {
        var onQuest = Assert.Single(Rows(new() { Quests = [Quest("""{"id":"q1"}""")], Registry = [Registration("engine")], Untrusted = [With(Hold, """{"quest":"q1"}""")] }));
        Assert.Equal("q1", onQuest["quest"]!.GetValue<string>());
        var onAsk = Only(Rows(new() { Asks = [Ask()], Untrusted = [With(Hold, """{"ask":"7c1e9a04b2d5"}""")] }), "trust");
        Assert.Equal("7c1e9a04b2d5", onAsk["ask"]!.GetValue<string>());
    }

    [Fact]
    public void Nothing_held_for_trust_adds_nothing() =>
        Assert.Empty(Rows(new() { Quests = [Quest()], Registry = [Registration("engine")], Untrusted = [] }));

    // ---- a proposal to widen the rules (PERM2, D74) ----

    private static JsonObject Proposal(string over = "{}") => With(
        """
        {"id":"p0000002","state":"waiting","action":"add","scope":"machine","list":"allow","rule":"WebFetch",
         "why":"The docs it needs are on the web.","session":"i9n8t7k6","ask":"a1b2c3","proposed":"2026-09-24T11:00:00Z"}
        """,
        over);

    /// <summary>What it would change and who proposed it are the page's words from the proposal it carries; why is the session's own.</summary>
    [Fact]
    public void A_widening_is_a_row_carrying_the_proposal_since_it_was_proposed_and_why_in_the_session_s_words()
    {
        var item = Assert.Single(Rows(new() { Proposals = [Proposal()] }));

        Is($$"""
           {"kind":"rule","id":"p0000002","since":"2026-09-24T11:00:00Z","detail":"The docs it needs are on the web.","proposal":{{Proposal().ToJsonString()}}}
           """, item);
    }

    [Fact]
    public void What_applied_itself_what_the_driver_has_not_judged_and_what_was_settled_are_left_out() =>
        Assert.Empty(Rows(new()
        {
            Proposals = [.. new[] { "applied", "proposed", "accepted", "declined", "refused", "unchanged" }.Select(state => Proposal($$"""{"id":"{{state}}","state":"{{state}}"}"""))],
        }));

    // ---- a quest parked on its failed sessions (SESSUX1i, D126 §4.6) ----

    private static JsonObject Parked(string over = "{}") => With(
        """
        {"quest":"7a82cc","repository":"engine","verdict":"Exhausted","strikes":3,"since":"2026-10-01T09:21:00+00:00",
         "reason":"3 session(s) have failed on `#7a82cc` without landing anything — parked, because trying again spends an account rather than making progress. `daoris driver retry 7a82cc` starts it again once you know why."}
        """,
        over);

    /// <summary>The quest, where it is addressed, since its last session ended, and its verdict for the page to say why it sits.</summary>
    [Fact]
    public void A_parked_quest_is_a_row_since_its_last_session_ended_with_its_verdict()
    {
        var item = Assert.Single(Rows(new() { Quests = [Quest("""{"status":"Taken"}""")], Registry = [Registration("engine")], Considered = [Parked()] }));

        Is($$"""{"kind":"parked-quest","id":"7a82cc","since":"2026-10-01T09:21:00+00:00","workspace":"default","where":"engine","sitting":{{Parked().ToJsonString()}}}""", item);
    }

    [Fact]
    public void A_parked_quest_holds_work_beside_a_parked_session_and_a_folder() =>
        Assert.Equal(
            ["parked-quest:q-new", "parked-quest:q-old", "parked:asking", "trust:C:/somewhere/.claude.json\nC:/somewhere/engine"],
            Keys(Rows(new()
            {
                Sessions = [Session("""{"id":"asking","state":"awaiting-person","updated":"2026-10-01T12:00:00Z"}""")],
                Quests = [Quest("""{"id":"q-new","status":"Taken"}"""), Quest("""{"id":"q-old"}""")],
                Registry = [Registration("engine")],
                Untrusted = [JsonNode.Parse("""{"folder":"C:/somewhere/engine","trustFile":"C:/somewhere/.claude.json","quest":"q-new"}""")!.AsObject()],
                Considered = [Parked("""{"quest":"q-new","since":"2026-10-01T10:00:00+00:00"}"""), Parked("""{"quest":"q-old","since":"2026-09-30T10:00:00+00:00"}""")],
            })));

    /// <summary>Only the planner's park is the person's to press: a stop, a busy repository and a start are not.</summary>
    [Fact]
    public void A_parked_quest_is_only_the_planner_s_park() =>
        Assert.Empty(Rows(new()
        {
            Quests = [Quest()],
            Registry = [Registration("engine")],
            Considered = [Parked("""{"verdict":"Stopped","heldBy":"s1a2b3c4"}"""), Parked("""{"verdict":"RepositoryBusy"}"""), Parked("""{"verdict":"Start"}""")],
        }));

    /// <summary>A shell older than the fact names no time: the row waits since the quest's last move; with no quest in hand, since nothing known.</summary>
    [Fact]
    public void A_parked_quest_with_no_time_waits_since_the_quest_s_last_move()
    {
        var unparked = Parked();
        unparked.Remove("since");
        unparked.Remove("strikes");
        var named = Assert.Single(Rows(new() { Quests = [Quest("""{"updated":"2026-09-30T08:00:00Z"}""")], Registry = [Registration("engine")], Considered = [unparked] }));
        Assert.Equal("2026-09-30T08:00:00Z", named["since"]!.GetValue<string>());

        var unnamed = Assert.Single(Rows(new() { Considered = [unparked] }));
        Assert.Null(unnamed["since"]);
    }

    // ---- what the Sessions badge counts (UX5 U20, D126 §2.5) ----

    private static int InSessions(AttentionFacts facts) => NeedsAPerson.WaitingInSessions(facts).Count;

    [Fact]
    public void The_sessions_badge_counts_the_sessions_waiting_on_the_person_a_parked_intake_among_them_and_nothing_else_the_band_holds()
    {
        var sessions = new[]
        {
            Session("""{"id":"c4a7c4a7","kind":"chat","state":"awaiting-person"}"""),
            Session("""{"id":"1n7a4e00","ask":"7c1e9a04b2d5","state":"awaiting-person"}"""),
            Session("""{"id":"b05y0000","state":"working"}"""),
            Session("""{"id":"d0ne0000","state":"completed"}"""),
        };

        Assert.Equal(2, InSessions(new() { Sessions = sessions }));
        var band = Rows(new() { Sessions = sessions, Quests = [Quest("""{"to":"nobody"}""")], Registry = [Registration("engine")], Asks = [Ask("""{"intake":"1n7a4e00"}""")] });
        Assert.True(band.Count > InSessions(new() { Sessions = sessions }));
    }

    [Fact]
    public void The_sessions_badge_counts_a_quest_parked_on_its_failed_sessions_and_nothing_else_the_driver_holds()
    {
        var sessions = new[] { Session("""{"id":"c4a7c4a7","kind":"chat","state":"awaiting-person"}"""), Session("""{"id":"f41led00","quest":"q1","state":"failed"}""") };
        var considered = new[]
        {
            JsonNode.Parse("""{"quest":"q1","repository":"engine","verdict":"Exhausted","reason":"3 failed sessions"}""")!.AsObject(),
            JsonNode.Parse("""{"quest":"q2","repository":"engine","verdict":"Busy","reason":"engine is busy"}""")!.AsObject(),
            JsonNode.Parse("""{"quest":"q3","repository":"game","verdict":"Start","reason":""}""")!.AsObject(),
        };

        Assert.Equal(2, InSessions(new() { Sessions = sessions, Considered = considered }));
        Assert.Equal(1, InSessions(new() { Sessions = sessions }));
    }

    /// <summary>A teammate's session parked to ask waits on them (SESSUX1a): not the person's to count.</summary>
    [Fact]
    public void The_sessions_badge_leaves_a_teammate_s_parked_session_to_its_own_machine() =>
        Assert.Equal(0, InSessions(new() { Sessions = [Session("""{"id":"laptop/c4a7c4a7","kind":"chat","state":"awaiting-person"}""")] }));

    /// <summary>ANSWER1c (D131): a park the person answered goes on at the driver's next look: neither the badge nor the band counts it.</summary>
    [Fact]
    public void No_park_the_person_answered_is_counted_or_listed()
    {
        var sessions = new[]
        {
            Session("""{"id":"p4rk3d00","quest":"7a82cc","state":"awaiting-person"}"""),
            Session("""{"id":"an5wered","quest":"7a82cd","state":"awaiting-person","answer":"Use the second."}"""),
        };

        Assert.Equal(1, InSessions(new() { Sessions = sessions }));
        Assert.Equal(["parked:p4rk3d00"], Keys(Rows(new() { Sessions = sessions })));
    }

    /// <summary>
    /// ASKRAIL1: Ask Daoris's conversations (<c>daoris:help</c>) are reached from its own panel and history, never from Sessions: a
    /// parked one and one placed to review are no session rows here, and the Sessions badge counts none, beside a session's that is.
    /// </summary>
    [Fact]
    public void An_Ask_Daoris_record_is_no_session_row_and_the_sessions_badge_counts_none()
    {
        var facts = new AttentionFacts
        {
            Sessions =
            [
                Session("""{"id":"p4rk3d00","state":"awaiting-person"}"""),
                Session("""{"id":"h3lp0001","repository":"daoris:help","kind":"chat","state":"awaiting-person"}"""),
                Session("""{"id":"h3lp0002","repository":"daoris:help","kind":"chat","state":"completed"}"""),
                Session("""{"id":"r3v13w00","state":"completed"}"""),
            ],
            Groups = [Placed(), Placed("""{"session":"h3lp0002"}""")],
        };

        Assert.Equal(["parked:p4rk3d00", "review:r3v13w00"], Keys(Rows(facts)));
        Assert.Equal(["session:p4rk3d00"], NeedsAPerson.WaitingInSessions(facts).Select(each => each.Key));
    }

    /// <summary>
    /// The badge's partition says each counted thing's workspace where the window's scope filters it: a session's, and a parked quest's
    /// where its record is in hand (NEEDSSCOPE1); one whose quest is not in hand says none, and every window counts it.
    /// </summary>
    [Fact]
    public void The_sessions_badge_says_a_session_s_workspace_for_the_window_s_scope()
    {
        var counted = NeedsAPerson.WaitingInSessions(new()
        {
            Sessions = [Session("""{"id":"c4a7c4a7","state":"awaiting-person","workspace":"aurora"}""")],
            Quests = [Quest("""{"id":"q1","status":"Taken","workspace":"aurora"}""")],
            Considered = [Parked("""{"quest":"q1"}"""), Parked("""{"quest":"q9"}""")],
        });

        Assert.Equal(
            [
                "quest:q1={\"kind\":\"parked-quest\",\"workspace\":\"aurora\"}", "quest:q9={\"kind\":\"parked-quest\"}",
                "session:c4a7c4a7={\"kind\":\"parked\",\"workspace\":\"aurora\"}",
            ],
            counted.Select(each => $"{each.Key}={each.Value.ToJsonString()}").Order(StringComparer.Ordinal));
    }

    // ---- the sessions the driver runs, which the Sessions badge and the status bar count (SESSBADGE1, D76's note) ----

    /// <summary>
    /// One count, the cap's (CAPCOUNT1, <see cref="ActiveSessions.HoldsSlot(SessionView)"/>): this machine's active sessions the driver
    /// started, a driven quest's, an intake's, a helper's and a reviewer's, a parked one and one whose host answers no kind among them;
    /// never a conversation the person holds (Ask Daoris's, a chat), a teammate's or one that ended. The records are the ones
    /// <c>CapCountTests</c> reads as the planner does, and the count is the slots the planner holds for them.
    /// </summary>
    [Fact]
    public void The_sessions_the_driver_runs_are_counted_by_the_cap_s_rule_never_a_conversation_the_person_holds()
    {
        JsonObject[] sessions =
        [
            Session("""{"id":"s1","repository":"A","kind":"driven","quest":"q1"}"""),
            Session("""{"id":"h1","repository":"daoris:help","kind":"chat"}"""),
            Session("""{"id":"c1","repository":"B","kind":"chat","state":"awaiting-person"}"""),
            Session("""{"id":"i1","repository":"ask #a1","kind":"chat","ask":"a1"}"""),
            Session("""{"id":"w1","repository":"C","kind":"chat","serves":"s1"}"""),
            Session("""{"id":"r1","repository":"D","kind":"chat","opinion":"o1"}"""),
            JsonNode.Parse("""{"id":"old","repository":"A","state":"working"}""")!.AsObject(),
            Session("""{"id":"p4rk3d00","state":"awaiting-person"}"""),
            Session("""{"id":"st4rt000","state":"queued"}"""),
            Session("""{"id":"d0ne0000","state":"completed"}"""),
            Session("""{"id":"f41led00","state":"failed"}"""),
            Session("""{"id":"laptop/b05y0000"}"""),
        ];

        var working = NeedsAPerson.Working(new() { Sessions = sessions });

        Assert.Equal(
            ["session:i1", "session:old", "session:p4rk3d00", "session:r1", "session:s1", "session:st4rt000", "session:w1"],
            working.Select(each => each.Key).Order(StringComparer.Ordinal));
        // The slots the planner holds for the same records, read as it reads the active ones of this machine.
        SessionView[] active =
        [
            .. sessions
                .Where(record => record["state"]!.GetValue<string>() is "queued" or "starting" or "working" or "awaiting-person")
                .Where(record => !record["id"]!.GetValue<string>().Contains('/'))
                .Select(record => new SessionView(record["id"]!.GetValue<string>(), record["repository"]!.GetValue<string>(), record["state"]!.GetValue<string>(), null)
                {
                    Kind = record["kind"]?.GetValue<string>() ?? ActiveSessions.DrivenKind,
                    Ask = record["ask"]?.GetValue<string>(),
                    Serves = record["serves"]?.GetValue<string>(),
                    Opinion = record["opinion"]?.GetValue<string>(),
                }),
        ];
        Assert.Equal(new Snapshot([], [], active).SlotsHeld, working.Count);
    }

    /// <summary>Each counted session says its workspace, for the window's scope, as the badge's waiting ones do.</summary>
    [Fact]
    public void Each_session_the_driver_runs_says_its_workspace_for_the_window_s_scope()
    {
        var working = NeedsAPerson.Working(new()
        {
            Sessions = [Session("""{"id":"s1","workspace":"aurora"}"""), JsonNode.Parse("""{"id":"s2","repository":"A","state":"working"}""")!.AsObject()],
        });

        Assert.Equal(
            ["session:s1={\"workspace\":\"aurora\"}", "session:s2={\"workspace\":\"default\"}"],
            working.Select(each => $"{each.Key}={each.Value.ToJsonString()}").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// What the count reads of a record moves the sessions' read, so the slice is woken where a record's kind or mark moves, as where
    /// its state does.
    /// </summary>
    [Theory]
    [InlineData("""{"kind":"chat"}""")]
    [InlineData("""{"ask":"a1"}""")]
    [InlineData("""{"serves":"s9"}""")]
    [InlineData("""{"opinion":"o1"}""")]
    public void A_record_s_kind_or_mark_moving_moves_what_the_judge_reads(string moved)
    {
        var working = Session();

        Assert.NotEqual(AttentionStateSlice.SessionsRead([working]), AttentionStateSlice.SessionsRead([With(working, moved)]));
    }

    // ---- a go-ahead asked on an ask (KNOWUSE1a) ----

    private static JsonObject GoAhead(string over = "{}") => With(
        """
        {"number":1,"kind":"push","on":"prod","act":"the report’s menu entries","state":"asked",
         "asked":[{"session":"s1a2b3c4","quest":"7a82cc","at":"2026-09-21T09:10:00Z","why":"The menu ships with the release."}]}
        """,
        over);

    [Fact]
    public void A_go_ahead_is_a_row_per_waiting_one_with_its_act_its_ask_since_first_asked_and_why_in_the_session_s_words()
    {
        // Its asker still at work, so it is no stale one (GOAHEADSTALE1).
        var rows = Rows(new() { Sessions = [Session()], Asks = [Ask($$"""{"state":"Published","goAheads":[{{GoAhead().ToJsonString()}}]}""")] });

        Is("""
           {"kind":"go-ahead","id":"7c1e9a04b2d5#1","since":"2026-09-21T09:10:00Z","workspace":"aurora","ask":"7c1e9a04b2d5","number":1,
            "goAhead":{"kind":"push","on":"prod","act":"the report’s menu entries"},"detail":"The menu ships with the release.","session":"s1a2b3c4"}
           """, Assert.Single(rows));
    }

    // ---- a go-ahead nothing on its ask waits for any more (GOAHEADSTALE1) ----

    /// <summary>The ask's quest, as the service files one an ask asked (D65 §4): its sender names the ask.</summary>
    private static JsonObject AskedQuest(string over = "{}") => With(Quest("""{"from":"ask #7c1e9a04b2d5","workspace":"aurora"}"""), over);

    /// <summary>
    /// 🔴 The owner's case (2026-10-21): go-aheads to write production configuration sat in What needs you for days after their asks'
    /// work was done another way. Its asker ended and its ask's one quest is done, so nothing on the ask can use its answer: the row says
    /// it is stale, since the last thing on its ask moved, and keeps its workspace (NEEDSSCOPE1).
    /// </summary>
    [Fact]
    public void A_go_ahead_nothing_on_its_ask_waits_for_is_stale_since_the_last_thing_on_its_ask_moved()
    {
        var row = Assert.Single(Rows(new()
        {
            Sessions = [Session("""{"quest":"7a82cc","state":"completed","updated":"2026-09-23T10:00:00Z"}""")],
            Quests = [AskedQuest("""{"status":"Done","updated":"2026-09-23T11:00:00Z"}""")],
            Asks = [Ask($$"""{"state":"Published","goAheads":[{{GoAhead().ToJsonString()}}]}""")],
        }));

        Is("""{"since":"2026-09-23T11:00:00Z"}""", Assert.IsType<JsonObject>(row["stale"]));
        Assert.Equal("aurora", row["workspace"]!.GetValue<string>());
        Assert.Equal("2026-09-21T09:10:00Z", row["since"]!.GetValue<string>());
    }

    /// <summary>
    /// Nothing on its ask in hand at all (its asker's record forgotten, no quest of it held): stale since it was last asked, the one moment
    /// left that says something needed it.
    /// </summary>
    [Fact]
    public void A_go_ahead_whose_ask_holds_nothing_in_hand_is_stale_since_it_was_last_asked()
    {
        var again = GoAhead("""
            {"asked":[{"session":"s1a2b3c4","quest":"7a82cc","at":"2026-09-21T09:10:00Z","why":"first"},
                      {"session":"c4rry0n0","quest":"7a82cc","at":"2026-09-22T08:00:00+00:00","why":"again"}]}
            """);
        var row = Assert.Single(Rows(new() { Asks = [Ask($$"""{"state":"Published","goAheads":[{{again.ToJsonString()}}]}""")] }));

        Is("""{"since":"2026-09-22T08:00:00+00:00"}""", Assert.IsType<JsonObject>(row["stale"]));
    }

    /// <summary>
    /// Anything on its ask that could still use its answer keeps a go-ahead current: a session that asked it, the ask's intake, or a
    /// session of a quest the ask asked, queued, starting, working or parked; or a quest the ask asked still open, taken, or held for the
    /// person's yes, whose next session would be handed it.
    /// </summary>
    [Theory]
    [InlineData("asker queued")]
    [InlineData("asker starting")]
    [InlineData("asker working")]
    [InlineData("asker parked")]
    [InlineData("another session of its quest")]
    [InlineData("its intake")]
    [InlineData("its quest open")]
    [InlineData("its quest taken")]
    [InlineData("its quest held")]
    public void A_go_ahead_is_not_stale_while_anything_on_its_ask_could_still_use_its_answer(string what)
    {
        var ended = Session("""{"quest":"7a82cc","state":"completed"}""");
        var done = AskedQuest("""{"status":"Done"}""");
        var facts = what switch
        {
            "asker queued" => new AttentionFacts { Sessions = [With(ended, """{"state":"queued"}""")], Quests = [done] },
            "asker starting" => new AttentionFacts { Sessions = [With(ended, """{"state":"starting"}""")], Quests = [done] },
            "asker working" => new AttentionFacts { Sessions = [With(ended, """{"state":"working"}""")], Quests = [done] },
            "asker parked" => new AttentionFacts { Sessions = [With(ended, """{"state":"awaiting-person"}""")], Quests = [done] },
            "another session of its quest" => new AttentionFacts
            {
                Sessions = [ended, Session("""{"id":"c4rry0n0","quest":"q2","state":"working"}""")],
                Quests = [done, AskedQuest("""{"id":"q2","status":"Taken"}""")],
            },
            "its intake" => new AttentionFacts { Sessions = [ended, Intake("""{"state":"working"}""")], Quests = [done] },
            "its quest open" => new AttentionFacts { Sessions = [ended], Quests = [AskedQuest()] },
            "its quest taken" => new AttentionFacts { Sessions = [ended], Quests = [AskedQuest("""{"status":"Taken"}""")] },
            "its quest held" => new AttentionFacts { Sessions = [ended], Quests = [AskedQuest("""{"status":"Done","held":true}""")] },
            _ => throw new ArgumentOutOfRangeException(nameof(what)),
        };

        var rows = Rows(facts with { Asks = [Ask($$"""{"state":"Published","goAheads":[{{GoAhead().ToJsonString()}}]}""")] });

        Assert.False(Only(rows, "go-ahead").ContainsKey("stale"), what);
    }

    /// <summary>
    /// What is on another ask never keeps it current, and a quest of its ask held and then accepted is one whose next step went on, so it
    /// waits on nobody.
    /// </summary>
    [Fact]
    public void A_go_ahead_is_stale_whatever_runs_on_another_ask_or_once_its_held_quest_was_accepted()
    {
        var rows = Rows(new()
        {
            Sessions =
            [
                Session("""{"quest":"7a82cc","state":"completed"}"""),
                Session("""{"id":"0th3r000","quest":"q9","state":"working"}"""),
                Intake("""{"id":"0th3r1nt","ask":"another","state":"working"}"""),
            ],
            Quests =
            [
                AskedQuest("""{"status":"Done","held":true,"accepted":"2026-09-23T09:00:00Z"}"""),
                Quest("""{"id":"q9","from":"ask #another","status":"Taken"}"""),
                Quest("""{"id":"q8","from":"platform","status":"Open"}"""),
            ],
            Asks = [Ask($$"""{"state":"Published","goAheads":[{{GoAhead().ToJsonString()}}]}""")],
        });

        Assert.True(Only(rows, "go-ahead").ContainsKey("stale"));
    }

    [Fact]
    public void A_go_ahead_answered_or_on_a_closed_ask_is_left_out()
    {
        var answered = GoAhead("""{"state":"approved","answer":{"approved":true,"at":"2026-09-21T10:00:00Z"}}""").ToJsonString();
        Assert.Empty(Rows(new()
        {
            Asks =
            [
                Ask($$"""{"state":"Published","goAheads":[{{answered}}]}"""),
                Ask($$"""{"id":"closed","state":"Closed","goAheads":[{{GoAhead().ToJsonString()}}]}"""),
            ],
        }));
    }

    [Fact]
    public void A_go_ahead_holds_work_beside_a_parked_session_and_an_ask() =>
        Assert.Equal(
            ["go-ahead:7c1e9a04b2d5#1", "parked:parked", "proposal:proposed"],
            Keys(Rows(new()
            {
                Sessions = [Session("""{"id":"parked","state":"awaiting-person","updated":"2026-09-21T09:00:00Z"}""")],
                Asks = [Ask("""{"id":"proposed","asked":"2026-09-20T09:00:00Z"}"""), Ask($$"""{"state":"Published","goAheads":[{{GoAhead().ToJsonString()}}]}""")],
            })));

    // ---- a departure that waits for your yes (DRIFT1d2) ----

    private static JsonObject Held(string over = "{}") => With(
        Quest("""
              {"status":"Done","held":true,"updated":"2026-09-22T09:00:00Z",
               "requirements":[{"quote":"cap it per frame","check":"a test"},{"quote":"on the chunk API","check":"the API"}],
               "answers":[{"requirement":1,"met":"tested"},{"requirement":2,"departed":"the scheduler holds the budget instead","quote":"on the chunk API"}]}
              """),
        over);

    [Fact]
    public void A_departure_is_a_row_since_its_done_carrying_the_departed_requirement_and_its_reason()
    {
        var item = Assert.Single(Rows(new() { Quests = [Held()], Registry = [Registration("engine")] }));

        Is("""
           {"kind":"departure","id":"7a82cc","since":"2026-09-22T09:00:00Z","workspace":"default","where":"engine",
            "departed":{"count":1,"number":2,"reason":"the scheduler holds the budget instead"}}
           """, item);
    }

    [Fact]
    public void A_departure_counts_the_requirements_its_done_departed_from()
    {
        var item = Assert.Single(Rows(new()
        {
            Quests =
            [
                Held("""
                     {"answers":[{"requirement":1,"departed":"no per-frame cap","quote":"cap it per frame"},
                                 {"requirement":2,"departed":"the scheduler holds it","quote":"on the chunk API"}]}
                     """),
            ],
            Registry = [Registration("engine")],
        }));

        Is("""{"count":2,"number":1,"reason":"no per-frame cap"}""", item["departed"]!.AsObject());
    }

    [Fact]
    public void A_departure_is_gone_once_accepted_and_absent_for_a_quest_no_departure_holds() =>
        Assert.Empty(Rows(new() { Quests = [Held("""{"accepted":"2026-09-22T10:00:00Z"}"""), Quest("""{"status":"Done"}""")], Registry = [Registration("engine")] }));

    // ---- work to review (D126) ----

    private static JsonObject Placed(string over = "{}") => With(
        """{"session":"r3v13w00","group":"review","shown":"completed","archived":false,"teammate":false,"work":{"commits":3,"uncommitted":0}}""", over);

    [Fact]
    public void Work_to_review_is_a_row_per_session_the_list_places_to_review_where_since_it_ended_and_its_place()
    {
        var item = Assert.Single(Rows(new()
        {
            Sessions = [Session("""{"id":"r3v13w00","quest":"7a82cc","state":"completed","updated":"2026-09-21T11:00:00Z"}""")],
            Quests = [Quest("""{"status":"Done"}""")],
            Registry = [Registration("engine")],
            Groups = [Placed()],
        }));

        Is($$"""{"kind":"review","id":"r3v13w00","since":"2026-09-21T11:00:00Z","workspace":"default","where":"engine","placed":{{Placed().ToJsonString()}}}""", item);
    }

    /// <summary>
    /// A session whose record is not in hand still says what waits: no repository, and no known wait; and no workspace, so every window
    /// shows it rather than none (NEEDSSCOPE1).
    /// </summary>
    [Fact]
    public void Work_to_review_whose_record_is_not_in_hand_still_says_what_waits()
    {
        var item = Assert.Single(Rows(new() { Groups = [Placed()] }));
        Assert.Equal(("review", "", null), (item["kind"]!.GetValue<string>(), item["where"]!.GetValue<string>(), item["since"]));
        Assert.False(item.ContainsKey("workspace"));
    }

    [Fact]
    public void Only_the_list_s_to_review_is_ready_for_the_person() =>
        Assert.Equal(
            ["parked:parked", "review:r3v13w00"],
            Keys(Rows(new()
            {
                Sessions = [Session("""{"id":"parked","state":"awaiting-person","updated":"2026-09-21T12:00:00Z"}""")],
                Groups = [Placed(), Placed("""{"session":"w0rk1ng0","group":"working"}"""), Placed("""{"session":"end3d000","group":"ended"}""")],
            })));

    // ---- a unit's milestone, an agent's ask, a held call, a second opinion ----

    private static JsonObject Entry(string over = "{}") => With(
        """{"unit":"u1","kind":"tree","resource":"s1a2b3c4","role":"temporary","disposition":"blocked","detail":"a session still holds this tree"}""", over);

    private static JsonObject Unit(string over = "{}") => With(
        $$"""
        {"unit":"u1","repository":"engine","branch":"daoris/s1a2b3c4","session":"s1a2b3c4",
         "end":{"state":"ended","by":"merged","on":"main","at":"2026-09-21T09:00:00Z","said":"on main"},
         "result":[],"cleanup":{"state":"pending","waits":[{{Entry().ToJsonString()}}],"gone":[]},"recovery":[],"entries":[]}
        """,
        over);

    /// <summary>
    /// FABRIC-MILESTONE1b, OVERVIEWNOISE1b: a unit is a row only where the person has an act: a tree its session recorded and no branch
    /// record follows waits on the clean-up's press, named by that session and its tree's folder, never its path. A guard the tidy keeps
    /// a branch by, coded or not, is said on its session's page and its branch's line, not here; a wait that ends by itself is no row.
    /// </summary>
    [Fact]
    public void A_unit_is_a_row_only_where_the_clean_up_s_press_settles_it_and_never_for_a_guard_or_a_wait_that_ends_by_itself()
    {
        var pending = Entry("""{"unit":"u2","disposition":"pending","kind":"end","resource":"the end"}""").ToJsonString();
        var kept = Entry("""
            {"unit":"u4","kind":"session-branch","resource":"daoris/s-4","code":"kept","facts":{"why":"nested","at":"2026-09-21T09:00:00Z"},
             "detail":"the look's tidy kept it at 2026-09-21 09:00 UTC, since its tree holds another repository at docs/inner"}
            """).ToJsonString();
        var unfollowed = Entry("""
            {"unit":"session:c0ffee01","resource":"X:/daoris/trees/default/engine/s-e0a8e0fd","code":"unfollowed",
             "facts":{"branch":"daoris/s-e0a8e0fd"},"detail":"its session recorded it, but no branch record follows `daoris/s-e0a8e0fd`"}
            """).ToJsonString();
        var rows = Rows(new()
        {
            Works =
            [
                Unit(),
                Unit($$$"""{"unit":"u2","session":null,"cleanup":{"state":"pending","waits":[{{{pending}}}],"gone":[]}}"""),
                Unit("""{"unit":"u3","cleanup":{"state":"completed","waits":[],"gone":[]}}"""),
                Unit($$$"""{"unit":"u4","session":"s4","branch":"daoris/s-4","cleanup":{"state":"pending","waits":[{{{kept}}}],"gone":[]}}"""),
                Unit($$$"""
                    {"unit":"session:c0ffee01","session":"c0ffee01","branch":null,"end":{"state":"not-ended","said":"no branch record follows its tree"},
                     "cleanup":{"state":"pending","waits":[{{{unfollowed}}}],"gone":[]}}
                    """),
            ],
        });

        Is("""
           {"kind":"work","id":"c0ffee01","since":null,"where":"engine","session":"c0ffee01","title":"s-e0a8e0fd",
            "blocked":{"unknown":false,"entries":[{"kind":"tree","resource":"X:/daoris/trees/default/engine/s-e0a8e0fd",
              "detail":"its session recorded it, but no branch record follows `daoris/s-e0a8e0fd`","code":"unfollowed","facts":{"branch":"daoris/s-e0a8e0fd"}}]}}
           """, Assert.Single(rows));
    }

    /// <summary>OVERVIEWNOISE1b: a tree no record and no session names is titled by its folder's name, never the unit id its path makes.</summary>
    [Fact]
    public void An_unknown_owner_s_tree_is_titled_by_its_folder_s_name_with_its_code()
    {
        var entry = Entry("""
            {"unit":"tree:X:/daoris/trees/default/engine/stray","resource":"X:/daoris/trees/default/engine/stray","code":"unknown-branch",
             "facts":{"branch":"daoris/stray"},"detail":"its owner is unknown: no record names `daoris/stray`, so nothing here is cleaned"}
            """).ToJsonString();
        var item = Assert.Single(Rows(new()
        {
            Works =
            [
                Unit($$"""
                    {"unit":"tree:X:/daoris/trees/default/engine/stray","session":null,"branch":null,"end":{"state":"unknown","said":"no record"},
                     "cleanup":{"state":"pending","waits":[{{entry}}],"gone":[]},"entries":[{{entry}}]}
                    """),
            ],
        }));

        Assert.Equal(("tree:X:/daoris/trees/default/engine/stray", "stray"), (item["id"]!.GetValue<string>(), item["title"]!.GetValue<string>()));
        Is("""
           {"unknown":true,"entries":[{"kind":"tree","resource":"X:/daoris/trees/default/engine/stray",
             "detail":"its owner is unknown: no record names `daoris/stray`, so nothing here is cleaned","code":"unknown-branch","facts":{"branch":"daoris/stray"}}]}
           """, item["blocked"]!.AsObject());
    }

    /// <summary>FABRIC-UNKNOWN1: a daoris/* branch no record names is a unit with an unknown owner, its blocked entry and no session.</summary>
    [Fact]
    public void An_unrecorded_daoris_branch_is_an_unknown_owner_with_its_reason_and_no_session()
    {
        const string why = "its owner is unknown: Daoris did not record `daoris/orphan`, so nothing here is cleaned; only your Discard branch press removes it";
        var entry = JsonSerializer.Serialize(new
        {
            unit = "unknown-branch:engine/daoris/orphan", kind = "session-branch", resource = "daoris/orphan", role = "temporary", disposition = "blocked", detail = why,
        });
        var rows = Rows(new()
        {
            Works =
            [
                JsonNode.Parse($$"""
                    {"unit":"unknown-branch:engine/daoris/orphan","repository":"engine","branch":"daoris/orphan","session":null,
                     "end":{"state":"unknown","said":"no record names its unit, so its end is not said"},
                     "result":[],"cleanup":{"state":"pending","waits":[{{entry}}],"gone":[]},"recovery":[],"entries":[{{entry}}]}
                    """)!.AsObject(),
            ],
        });

        var item = Assert.Single(rows);
        Assert.Equal(["work:unknown-branch:engine/daoris/orphan"], Keys(rows));
        Assert.Equal(("daoris/orphan", "engine", null), (item["title"]!.GetValue<string>(), item["where"]!.GetValue<string>(), item["session"]));
        Is(JsonSerializer.Serialize(new { unknown = true, entries = new[] { new { kind = "session-branch", resource = "daoris/orphan", detail = why } } }), item["blocked"]!.AsObject());
    }

    /// <summary>A unit whose owner is unknown and whose clean-up names nothing blocked says its first entry.</summary>
    [Fact]
    public void A_unit_with_an_unknown_owner_and_nothing_blocked_says_its_first_entry()
    {
        var first = Entry("""{"disposition":"kept","detail":"standing"}""");
        var item = Assert.Single(Rows(new()
        {
            Works = [Unit($$"""{"end":{"state":"unknown","said":"no record"},"cleanup":{"state":"pending","waits":[],"gone":[]},"entries":[{{first.ToJsonString()}}]}""")],
        }));

        Is("""{"unknown":true,"entries":[]}""", item["blocked"]!.AsObject());
        Assert.Null(item["since"]);
    }

    /// <summary>MIXAGENT2c (D168): an agent's ask for a helper waiting on the person's yes: its question's first line, the session that asked.</summary>
    [Fact]
    public void An_agent_s_ask_for_a_helper_is_a_row_with_its_question_s_first_line_and_the_session_that_asked()
    {
        var rows = Rows(new()
        {
            Sessions = [Session("""{"id":"c0nv0001","quest":"7a82cc"}""")],
            Quests = [Quest()],
            Registry = [Registration("engine")],
            Helpers =
            [
                JsonNode.Parse("""{"id":"a1","session":"c0nv0001","question":"Which test covers the loader?\nIt drops the last day.","agent":"codex-acp","at":"2026-09-21T09:30:00Z"}""")!.AsObject(),
                JsonNode.Parse("""{"id":"a2","session":"g0n3","question":"Is the cap right?","agent":null,"at":"2026-09-21T09:10:00Z"}""")!.AsObject(),
            ],
        });

        Assert.Equal(["helper:a1", "helper:a2"], Keys(rows));
        Is("""{"kind":"helper","id":"a1","since":"2026-09-21T09:30:00Z","workspace":"default","where":"engine","session":"c0nv0001","title":"Which test covers the loader?"}""", rows.Single(row => row["id"]!.GetValue<string>() == "a1"));
        // A session the page does not hold: no repository is guessed.
        Is("""{"kind":"helper","id":"a2","since":"2026-09-21T09:10:00Z","where":"","session":"g0n3","title":"Is the cap right?"}""", rows.Single(row => row["id"]!.GetValue<string>() == "a2"));
    }

    /// <summary>
    /// NOTIFY2e: an agent's ask to hand its session over waiting on the person's yes or no is a row by the ask's own id: its note's first
    /// line, the session that asked and the agent it goes to; it stands still on it once that session's turn no longer runs.
    /// </summary>
    [Fact]
    public void An_agent_s_ask_to_hand_its_session_over_is_a_row_by_its_own_id_standing_still_once_its_turn_ended()
    {
        var rows = Rows(new()
        {
            Sessions =
            [
                Session("""{"id":"c0nv0001","quest":"7a82cc","state":"completed"}"""),
                Session("""{"id":"w0rk1ng0","quest":"7a82cc","state":"working"}"""),
            ],
            Quests = [Quest()],
            Registry = [Registration("engine")],
            HandOvers =
            [
                JsonNode.Parse("""{"id":"h1","session":"c0nv0001","note":"Near my limit.\nParserTests are left.","agent":"codex-acp","at":"2026-10-21T09:30:00Z"}""")!.AsObject(),
                JsonNode.Parse("""{"id":"h2","session":"w0rk1ng0","note":"Codex suits the rest.","agent":"codex-acp","at":"2026-10-21T09:10:00Z"}""")!.AsObject(),
                JsonNode.Parse("""{"id":"h3","session":"g0n3","note":"Gone.","agent":"codex-acp","at":"2026-10-21T09:00:00Z"}""")!.AsObject(),
            ],
        });

        Assert.Equal(["hand-over:h1", "hand-over:h2", "hand-over:h3"], Keys(rows));
        Is("""{"kind":"hand-over","id":"h1","since":"2026-10-21T09:30:00Z","workspace":"default","where":"engine","session":"c0nv0001","title":"Near my limit.","agent":"codex-acp","stands":true}""",
            rows.Single(row => row["id"]!.GetValue<string>() == "h1"));
        // Its turn still runs: it holds nothing up yet.
        Assert.False(rows.Single(row => row["id"]!.GetValue<string>() == "h2")["stands"]!.GetValue<bool>());
        // A session the page does not hold: no repository is guessed, and nothing says it stands still.
        Is("""{"kind":"hand-over","id":"h3","since":"2026-10-21T09:00:00Z","where":"","session":"g0n3","title":"Gone.","agent":"codex-acp","stands":false}""",
            rows.Single(row => row["id"]!.GetValue<string>() == "h3"));
    }

    /// <summary>
    /// NOTIFY2e: the hand-over waits are read off the home's sessions folder: an ask the look holds <c>waiting</c>, by its own id, with its
    /// session, note and agent; one not read yet, approved, taken or refused, and a state naming an earlier ask, wait on nothing of theirs.
    /// </summary>
    [Fact]
    public void The_hand_over_waits_are_the_asks_the_look_holds_waiting()
    {
        var files = new HandOverAskFiles(Home);
        Directory.CreateDirectory(DaorisHome.Entry(Home, HomeEntry.Sessions));
        void Ask(string session, string id, string at, string? state, string? code = null, string? stateId = null)
        {
            File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Sessions, session + HandOverAskFiles.AskSuffix),
                $$"""{"id":"{{id}}","session":"{{session}}","note":"Near my limit.","askedBy":"agent","at":"{{at}}"}""");
            if (state is not null) files.WriteState(session, new HandOverAskState(stateId ?? id, state, code, "codex-acp", DateTimeOffset.Parse(at)));
        }

        Ask("s1", "h1", "2026-10-21T09:10:00Z", HandOverAskStates.Waiting);
        Ask("s2", "h2", "2026-10-21T09:00:00Z", HandOverAskStates.Waiting);
        Ask("s3", "h3", "2026-10-21T08:00:00Z", null);
        Ask("s4", "h4", "2026-10-21T08:00:00Z", HandOverAskStates.Approved);
        Ask("s5", "h5", "2026-10-21T08:00:00Z", HandOverAskStates.Taken);
        Ask("s6", "h6", "2026-10-21T08:00:00Z", HandOverAskStates.Refused, HandOverAskWhy.Declined);
        Ask("s7", "h7", "2026-10-21T08:00:00Z", HandOverAskStates.Waiting, stateId: "h0");

        var waits = JsonSerializer.SerializeToNode(AttentionStateSlice.HandOverWaits(Home), IpcJson.Options)!.AsArray();

        Assert.Equal([("h2", "s2"), ("h1", "s1")], waits.Select(wait => (wait!["id"]!.GetValue<string>(), wait["session"]!.GetValue<string>())));
        Assert.Equal(["id", "session", "note", "agent", "at"], waits[0]!.AsObject().Select(field => field.Key));
        Assert.Equal(("Near my limit.", "codex-acp"), (waits[0]!["note"]!.GetValue<string>(), waits[0]!["agent"]!.GetValue<string>()));
        Assert.Equal(DateTimeOffset.Parse("2026-10-21T09:00:00Z"), DateTimeOffset.Parse(waits[0]!["at"]!.GetValue<string>()));
    }

    /// <summary>
    /// CONFIRMSEEN1: a terminal's press waiting for the window is a row of <i>What needs you</i>, which the badge counts: by its route and
    /// the terminal as the system names it, since it was asked, never a value it carries; and it is gone once it no longer waits.
    /// </summary>
    [Fact]
    public void A_terminal_s_press_waiting_for_the_window_is_a_row_by_its_route_and_terminal_never_its_values()
    {
        var clock = new HeldClock();
        using var log = new MachineLog(Home, "desktop");
        using var confirmations = new PipeConfirmations(null, log, clock);
        var asked = confirmations.Ask(DriverModule.Name, "SESSION_ARCHIVE",
            [new PipeConfirmationField("/ids/0", "string", "s1a2b3c4")], new PipeAsker("daoris-driver", 4242))!.Value;

        var rows = Rows(new() { Confirmations = Waits(confirmations) });
        var at = clock.GetUtcNow();

        Is($$$"""
             {"kind":"confirmation","id":"{{{asked.Shown.Id}}}","since":"{{{Moments.Write(at)}}}","confirmation":{"module":"DAORIS.DRIVER",
              "route":"SESSION_ARCHIVE","asker":{"process":"daoris-driver","pid":4242},"expires":"{{{Moments.Write(at.AddMinutes(10))}}}","shownAt":null}}
             """, Assert.Single(rows));
        Assert.DoesNotContain("s1a2b3c4", JsonSerializer.Serialize(AttentionStateSlice.Judge(new() { Confirmations = Waits(confirmations) })!
            .Select(each => each.Data), IpcJson.Options));

        // Shown, its two minutes are said; answered, it is no row.
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(confirmations.Show(asked.Shown.Id));
        var shown = Assert.Single(Rows(new() { Confirmations = Waits(confirmations) }))["confirmation"]!;
        Assert.Equal(Moments.Write(at.AddMinutes(1)), shown["shownAt"]!.GetValue<string>());
        Assert.Equal(Moments.Write(at.AddMinutes(3)), shown["expires"]!.GetValue<string>());
        Assert.True(confirmations.Answer(asked.Shown.Id, confirm: false));
        Assert.Empty(Rows(new() { Confirmations = Waits(confirmations) }));
        Assert.Empty(AttentionStateSlice.ConfirmationWaits(null));
    }

    private static List<JsonObject> Waits(PipeConfirmations confirmations) =>
        [.. AttentionStateSlice.ConfirmationWaits(confirmations).Select(each => JsonSerializer.SerializeToNode(each, IpcJson.Options)).OfType<JsonObject>()];

    /// <summary>
    /// KEEP1f (the cleanup design §3.2): a volume still below its free-space floor once the floor's acts ran is a row of <i>What needs
    /// you</i>, by its volume since it was said, with its free space, size and floor, what the floor freed and what the press could; the
    /// slice is woken as it is said and as it ends, and the row is gone once the volume is above its floor by the margin. A stand-in read.
    /// </summary>
    [Fact]
    public async Task A_volume_still_below_its_floor_is_a_row_until_it_is_above_it_by_the_margin()
    {
        const long gb = 1L << 30;
        var free = 4 * gb;
        var now = new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
        var floors = new SpaceFloors();
        var woken = 0;
        using var hearing = AttentionStateSlice.HearFloors(floors, () => woken++);
        var world = new FloorWorld(Home)
        {
            Volume = _ => new VolumeReading(@"\\?\Volume{A}\", @"C:\", free, 100 * gb),
            Now = () => now,
            Policy = _ => KeepPolicy.Default,
            Floors = floors,
            Could = () => 3 * gb,
        };
        List<JsonObject> Space() =>
            [.. AttentionStateSlice.SpaceWaits(floors, Home).Select(each => JsonSerializer.SerializeToNode(each, IpcJson.Options)).OfType<JsonObject>()];

        await SpaceFloor.PassAsync(world);

        Assert.Equal(1, woken);
        Is($$$"""
             {"kind":"space","id":"\\\\?\\Volume{A}\\","since":"{{{Moments.Write(now)}}}","space":{"root":"C:\\","free":{{{4 * gb}}},
              "total":{{{100 * gb}}},"floor":{{{10 * gb}}},"freed":0,"could":{{{3 * gb}}}}}
             """, Assert.Single(Rows(new() { Space = Space() })));
        var pushed = AttentionStateSlice.Judge(new() { Space = Space() })!.Single(each => each.Partition == AttentionStateSlice.Rows);
        Assert.Contains("\"could\"", JsonSerializer.Serialize(pushed.Data, IpcJson.Options));

        // An hour later, still below: said once, never again.
        now = now.AddHours(1);
        await SpaceFloor.PassAsync(world);
        Assert.Equal(Moments.Write(now.AddHours(-1)), Assert.Single(Rows(new() { Space = Space() }))["since"]!.GetValue<string>());

        // Above its floor but within the margin, it stands; above it by the margin, it is gone.
        free = 12 * gb;
        await SpaceFloor.PassAsync(world);
        Assert.Single(Rows(new() { Space = Space() }));
        free = 40 * gb;
        await SpaceFloor.PassAsync(world);
        Assert.Empty(Rows(new() { Space = Space() }));
        Assert.True(woken >= 2);
    }

    /// <summary>AUTONOMY1b3c (D184 point 3): a tool call a session holds for the person holds work: its wait whole, the session that holds it.</summary>
    [Fact]
    public void A_tool_call_a_session_holds_for_the_person_is_a_row_with_the_wait_whole()
    {
        var held = """
                   {"session":"c0nv0001","request":"900","call":"tc-1","title":"npm test","kind":"execute","since":"2026-09-21T09:30:00Z",
                    "until":"2026-09-21T09:45:00Z",
                    "choices":[{"id":"allow","name":"Allow","kind":"allow_once"},{"id":"reject","name":"Reject","kind":"reject_once"}]}
                   """;
        var untitled = """
                       {"session":"g0n3","request":"7","call":null,"title":null,"kind":"edit","since":"2026-09-21T09:10:00Z",
                        "choices":[{"id":"no","name":"Reject","kind":"reject_once"},{"id":"later","name":"Later","kind":"snooze"}]}
                       """;
        var rows = Rows(new()
        {
            Sessions = [Session("""{"id":"c0nv0001","quest":"7a82cc"}""")],
            Quests = [Quest()],
            Registry = [Registration("engine")],
            Permissions = [JsonNode.Parse(held)!.AsObject(), JsonNode.Parse(untitled)!.AsObject()],
        });

        Assert.Equal(["permission:c0nv0001#900", "permission:g0n3#7"], Keys(rows));
        Is($$"""{"kind":"permission","id":"c0nv0001#900","since":"2026-09-21T09:30:00Z","workspace":"default","where":"engine","session":"c0nv0001","permission":{{held}}}""", rows.Single(row => row["id"]!.GetValue<string>() == "c0nv0001#900"));
        Assert.Equal("", rows.Single(row => row["id"]!.GetValue<string>() == "g0n3#7")["where"]!.GetValue<string>());
    }

    /// <summary>
    /// COWORK1e (D188 point 4; the message-urgency design §8.3): a third answer in a row with nobody between holds its session's work for
    /// the person: a row by its session, its wait whole (the agent that answered, the answers' ids, since when), its repository where the
    /// record is in hand.
    /// </summary>
    [Fact]
    public void A_third_answer_alone_held_for_the_person_is_a_row_with_its_wait_whole()
    {
        var held = """{"session":"c0nv0001","agent":"codex-acp","words":["w3","w4"],"since":"2026-09-21T09:30:00Z"}""";
        var rows = Rows(new()
        {
            Sessions = [Session("""{"id":"c0nv0001","quest":"7a82cc"}""")],
            Quests = [Quest()],
            Registry = [Registration("engine")],
            Answers = [JsonNode.Parse(held)!.AsObject(), JsonNode.Parse("""{"session":"g0n3","agent":"codex-acp","words":["w1"],"since":"2026-09-21T09:10:00Z"}""")!.AsObject()],
        });

        Assert.Equal(["answer:c0nv0001", "answer:g0n3"], Keys(rows));
        Is($$"""{"kind":"answer","id":"c0nv0001","since":"2026-09-21T09:30:00Z","workspace":"default","where":"engine","session":"c0nv0001","answer":{{held}}}""", rows.Single(row => row["id"]!.GetValue<string>() == "c0nv0001"));
        Assert.Equal("", rows.Single(row => row["id"]!.GetValue<string>() == "g0n3")["where"]!.GetValue<string>());
    }

    /// <summary>
    /// COWORK1e: the answers-alone waits are read off the home's marks: each held for the person with the agent, the answers and since
    /// when, oldest first; a mark of any other reason, and answers the person kept with *Not now*, wait on nothing of theirs.
    /// </summary>
    [Fact]
    public void The_answers_alone_waits_are_the_marks_held_for_the_person()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-answer-waits-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var marks = new GoOnMarks(home);
            marks.Mark("s2", ["w5"], GoOnMarks.AnswersAlone, DateTimeOffset.Parse("2026-10-21T09:10:00Z"), agent: "claude-code-acp");
            marks.Mark("s1", ["w3", "w4"], GoOnMarks.AnswersAlone, DateTimeOffset.Parse("2026-10-21T09:00:00Z"), agent: "codex-acp");
            marks.Mark("s3", ["w9"], GoOnMarks.NotNow, DateTimeOffset.Parse("2026-10-21T08:00:00Z"), agent: "codex-acp");
            marks.Mark("s4", ["w8"], ContinueWhy.Of(ContinueWhy.Tree), DateTimeOffset.Parse("2026-10-21T08:00:00Z"));

            var waits = JsonSerializer.SerializeToNode(AttentionStateSlice.AnswerWaits(home), IpcJson.Options)!.AsArray();

            Assert.Equal(
                [("s1", "codex-acp", "w3,w4"), ("s2", "claude-code-acp", "w5")],
                waits.Select(wait => (wait!["session"]!.GetValue<string>(), wait["agent"]!.GetValue<string>(),
                    string.Join(",", wait["words"]!.AsArray().Select(id => id!.GetValue<string>())))));
            Assert.Equal(DateTimeOffset.Parse("2026-10-21T09:00:00Z"), DateTimeOffset.Parse(waits[0]!["since"]!.GetValue<string>()));
            Assert.Equal(["session", "agent", "words", "since"], waits[0]!.AsObject().Select(field => field.Key));
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>XAGENT1g: a second opinion that waits on the person, its wait whole, which the page says and offers by.</summary>
    [Fact]
    public void A_second_opinion_that_waits_on_the_person_is_a_row_with_its_wait_whole()
    {
        var disputed = """
                       {"session":"s42","quest":"q1","repository":"engine","since":"2026-09-21T09:00:00Z","auto":false,
                        "opinion":{"state":"disputed","holds":true,"product":"Codex","maker":"OpenAI","disputes":2,"answers":"o1:t:disputed:2:0"}}
                       """;
        var none = """
                   {"session":"s43","quest":"q1","repository":"engine","since":"2026-09-21T08:00:00Z","auto":false,
                    "opinion":{"state":"unavailable","holds":true,"required":true,"code":"no-reviewer"}}
                   """;
        var rows = Rows(new()
        {
            Quests = [Quest("""{"id":"q1","to":"engine","title":"Cap hydration per frame"}""")],
            Registry = [Registration("engine")],
            Opinions = [JsonNode.Parse(disputed)!.AsObject(), JsonNode.Parse(none)!.AsObject(), With(disputed, """{"session":"s44","auto":true}""")],
        });

        Assert.Equal(["opinion:s42", "opinion:s43", "opinion:s44"], Keys(rows));
        Is($$"""{"kind":"opinion","id":"s42","since":"2026-09-21T09:00:00Z","where":"engine","opinion":{{disputed}}}""", rows.Single(row => row["id"]!.GetValue<string>() == "s42"));
    }

    // ---- a set-up in what needs you (REVIEWENV1g, D154 point 8) ----

    private const string Work = """
        {"id":"q1","from":"ask:a1","to":"reports","title":"Add the compare setting to the report","body":"Add the compare setting.",
         "status":"Done","filed":"2026-10-09T08:00:00Z","updated":"2026-10-09T09:00:00Z","workspace":"aurora"}
        """;

    private const string SetUp = """
        {"commit":"a1b2c3d4e5f60718293a4b5c6d7e8f9012345678","look":"http://localhost:4200/v3/reports/42",
         "shows":"The report with the compare setting turned on, showing last month beside this one.",
         "again":"Open http://localhost:4200/v3/reports/42, then turn on Compare in the toolbar.","served":"dist","session":"s9","local":true,
         "at":"2026-10-09T09:40:00Z","machine":"desk","sequence":7}
        """;

    private static JsonObject SetUpAgain() => With(SetUp, """
        {"commit":"f0e1d2c3b4a5968778695a4b3c2d1e0f98765432","shows":"The same report, with the label renamed as you said.","at":"2026-10-09T10:20:00Z","sequence":9}
        """);

    private static JsonObject StepShown(string over = "{}") => With(With($$"""
        {"id":"q2","from":"ask:a1","to":"reports","title":"Show #q1 in `local` for review","body":"Set the work of #q1 up in `local` for the person's review.",
         "status":"Done","filed":"2026-10-09T09:05:00Z","updated":"2026-10-09T09:41:00Z","workspace":"aurora","parent":"q1",
         "held":true,"hold":"unreviewed","setUpIn":"local","setUps":[{{SetUp}}]}
        """, "{}"), over);

    private const string NotYet = """
        {"said":"not-yet","setUp":{"machine":"desk","sequence":7},"commit":"a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
         "words":"The label still reads the old name.","at":"2026-10-09T09:50:00Z","machine":"desk"}
        """;

    private static JsonObject StepNotYet() => StepShown($$"""{"verdicts":[{{NotYet}}]}""");

    private static JsonObject StepShownAgain() => With(StepNotYet(), $$"""{"setUps":[{{SetUp}},{{SetUpAgain().ToJsonString()}}],"updated":"2026-10-09T10:21:00Z"}""");

    [Fact]
    public void A_set_up_shown_and_waiting_is_a_row_with_what_it_showed_and_the_set_up_its_verdict_names()
    {
        var rows = Rows(new() { Quests = [JsonNode.Parse(Work)!.AsObject(), StepShown()] });

        Is("""
           {"kind":"set-up","id":"q2","since":"2026-10-09T09:40:00Z","workspace":"aurora","where":"reports","setUp":{"machine":"desk","sequence":7},
            "local":true,"session":"s9","environment":"local","shows":"The report with the compare setting turned on, showing last month beside this one."}
           """, Assert.Single(rows));
    }

    [Fact]
    public void A_set_up_shown_again_names_the_newest() =>
        Is("""{"machine":"desk","sequence":9}""", Assert.Single(Rows(new() { Quests = [StepShownAgain()] }))["setUp"]!.AsObject());

    [Fact]
    public void A_set_up_lists_nothing_while_it_is_set_up_once_said_not_yet_or_once_reviewed()
    {
        var settingUp = StepShown("""{"status":"Taken","held":false,"hold":null,"setUps":[],"updated":"2026-10-09T09:06:00Z"}""");
        var reviewed = With(StepShownAgain(), $$"""
            {"held":false,"hold":null,"verdicts":[{{NotYet}},
             {"said":"reviewed","setUp":{"machine":"desk","sequence":9},"commit":"f0e1d2c3b4a5968778695a4b3c2d1e0f98765432","at":"2026-10-09T10:30:00Z","machine":"desk"}]}
            """);

        foreach (var quest in new[] { settingUp, StepNotYet(), reviewed }) Assert.Empty(Rows(new() { Quests = [quest] }));
    }

    [Fact]
    public void A_deployed_set_up_is_not_local()
    {
        var served = With(SetUp, """{"look":"https://dev.example.test/reports/42","served":null,"local":false}""").ToJsonString();
        var deployed = StepShown($$"""{"title":"Show #q1 in `dev` for review","setUpIn":"dev","setUps":[{{served}}]}""");
        Assert.False(Assert.Single(Rows(new() { Quests = [deployed] }))["local"]!.GetValue<bool>());
    }

    // ---- an attempt held for the person (FABRIC-PERFORM1g, FABRIC-CLOSE1q) ----

    private static JsonObject HeldAttempt(string over = "{}") => With(
        """
        {"unit":"u1","node":"deploy","attempt":2,"plugin":"acme.deploy","step":"acme.deploy/deploy","nodeState":"unknown","why":"unknown",
         "at":"2026-10-17T09:00:00Z","said":"its plugin was asked and cannot say what became of it"}
        """,
        over);

    [Fact]
    public void An_attempt_held_for_the_person_is_a_row_naming_its_unit_with_the_attempt_whole()
    {
        var row = Assert.Single(Rows(new() { Held = [HeldAttempt()] }));
        Is($$"""{"kind":"work","id":"u1#deploy#2","since":"2026-10-17T09:00:00Z","where":"u1","held":{{HeldAttempt().ToJsonString()}}}""", row);

        var worded = Assert.Single(Rows(new() { Held = [HeldAttempt("""{"resolution":"did-not","resolvedAt":"2026-10-17T10:00:00Z"}""")] }));
        Assert.Equal("2026-10-17T10:00:00Z", worded["since"]!.GetValue<string>());
    }

    [Fact]
    public void A_held_close_is_a_row_with_its_words_as_the_driver_offers_them()
    {
        var close = HeldAttempt("""
            {"unit":"quest:q7","node":"close","attempt":1,"plugin":"quest.close","step":"quest.close","why":"no-acceptance","outcome":"reached",
             "words":["record","resend","stop"],"said":"the quest was closed"}
            """);
        var row = Assert.Single(Rows(new() { Held = [close] }));

        Assert.Equal(("work:quest:q7#close#1", "quest:q7"), (Keys([row])[0], row["where"]!.GetValue<string>()));
        Assert.Equal("""["record","resend","stop"]""", row["held"]!["words"]!.ToJsonString());
    }

    [Fact]
    public void Nothing_is_held_where_the_driver_lists_none() => Assert.Empty(Rows(new() { Held = [] }));

    // ---- a node record that does not read (FABRIC-PERFORM1j) ----

    private static JsonObject Unreadable(string over = "{}") => With(
        """
        {"file":"0a1b2c3d4e5f60718293a4b5c6d7e8f9.json","unit":"quest:q7","at":"2026-10-17T09:00:00Z",
         "said":"the node record 0a1b2c3d4e5f60718293a4b5c6d7e8f9.json of quest:q7 under the home's workflows/units does not read"}
        """,
        over);

    /// <summary>
    /// A node record that does not read is a row of its own, by its file, naming the unit it names where it names one, since it was last
    /// written, with the record whole for the page to word; one naming no unit is a row too, said where nothing names it.
    /// </summary>
    [Fact]
    public void A_node_record_that_does_not_read_is_a_row_by_its_file_naming_its_unit()
    {
        var row = Assert.Single(Rows(new() { Unreadable = [Unreadable()] }));
        Is($$"""
            {"kind":"work","id":"record#0a1b2c3d4e5f60718293a4b5c6d7e8f9.json","since":"2026-10-17T09:00:00Z","where":"quest:q7",
             "unreadable":{{Unreadable().ToJsonString()}}}
            """, row);

        var unnamed = Assert.Single(Rows(new() { Unreadable = [Unreadable("""{"unit":null}""")] }));
        Assert.Null(unnamed["where"]);
        Assert.Empty(Rows(new() { Unreadable = [] }));
    }

    // ---- a closure graph's end (FABRIC-CLOSE1j) ----

    private static JsonObject Ended(string over = "{}") => With(
        """
        {"unit":"quest:q7","kind":"partially-closed","at":"2026-10-17T09:00:00Z","nodes":[],"quest":"q6","node":"close","attempt":2,
         "nodeState":"ready","words":["stop"]}
        """,
        over);

    [Fact]
    public void A_close_found_closed_only_in_part_is_a_row_while_its_stop_is_offered_or_waits_for_the_next_look()
    {
        var row = Assert.Single(Rows(new() { Ends = [Ended()] }));
        Is($$"""{"kind":"work","id":"quest:q7#close#q6","since":"2026-10-17T09:00:00Z","where":"quest:q7","ended":{{Ended().ToJsonString()}}}""", row);
        Assert.Single(Rows(new() { Ends = [Ended("""{"resolution":"stop","words":[]}""")] }));

        // Its node stopped, or the person's own done ending the work early: nothing waits on them.
        Assert.Empty(Rows(new() { Ends = [Ended("""{"nodeState":"stopped","words":[]}""")] }));
        Assert.Empty(Rows(new() { Ends = [Ended("""{"kind":"overridden","nodes":["qa","close"],"words":[]}""")] }));
    }

    // ---- the scope a window reads (NEEDSSCOPE1) ----

    /// <summary>
    /// NEEDSSCOPE1: two windows on one home, each on its own workspace, and one counted three sessions the other's list placed to review.
    /// Every row that is a session's, a quest's or an ask's carries that record's workspace, however its wait reached the judge (the
    /// service's records or the driver's own lists), so a window on another workspace neither counts nor lists it.
    /// </summary>
    [Fact]
    public void Each_row_of_a_session_a_quest_or_an_ask_carries_its_record_s_workspace()
    {
        const string unfollowed = """
            {"unit":"session:c0nv0001","kind":"tree","resource":"X:/daoris/trees/aurora/engine/s-c0nv0001","role":"temporary","disposition":"blocked",
             "code":"unfollowed","facts":{"branch":"daoris/s-c0nv0001"},"detail":"its session recorded it, but no branch record follows it"}
            """;
        var rows = Rows(new()
        {
            Sessions =
            [
                Session("""{"id":"c0nv0001","quest":"q1","state":"completed","workspace":"aurora"}"""),
                Session("""{"id":"p4rk3d00","state":"awaiting-person","workspace":"aurora"}"""),
            ],
            Quests = [Quest("""{"id":"q1","status":"Taken","workspace":"aurora"}"""), Quest("""{"id":"q2","to":"retired","workspace":"aurora"}""")],
            Asks = [Ask()],
            Registry = [Registration("engine", """{"workspace":"aurora"}""")],
            Groups = [Placed("""{"session":"c0nv0001"}""")],
            Opinions =
            [
                JsonNode.Parse("""{"session":"s42","quest":"q1","repository":"engine","workspace":"aurora","since":"2026-09-21T09:00:00Z","auto":false,"opinion":{"state":"disputed","holds":true}}""")!.AsObject(),
                JsonNode.Parse("""{"session":"c0nv0001","quest":"q1","repository":"engine","since":"2026-09-21T09:00:00Z","auto":false,"opinion":{"state":"disputed","holds":true}}""")!.AsObject(),
            ],
            Considered = [Parked("""{"quest":"q1"}""")],
            Helpers = [JsonNode.Parse("""{"id":"a1","session":"c0nv0001","question":"Which test covers the loader?","at":"2026-09-21T09:30:00Z"}""")!.AsObject()],
            HandOvers = [JsonNode.Parse("""{"id":"h1","session":"c0nv0001","note":"Near my limit.","agent":"codex-acp","at":"2026-09-21T09:30:00Z"}""")!.AsObject()],
            Permissions = [JsonNode.Parse("""{"session":"c0nv0001","request":"900","kind":"execute","since":"2026-09-21T09:30:00Z","choices":[]}""")!.AsObject()],
            Answers = [JsonNode.Parse("""{"session":"c0nv0001","agent":"codex-acp","words":["w3"],"since":"2026-09-21T09:30:00Z"}""")!.AsObject()],
            Works =
            [
                Unit($$$"""
                    {"unit":"session:c0nv0001","session":"c0nv0001","branch":null,"end":{"state":"not-ended","said":"no branch record follows its tree"},
                     "cleanup":{"state":"pending","waits":[{{{unfollowed}}}],"gone":[]}}
                    """),
            ],
            Held = [HeldAttempt("""{"unit":"session:c0nv0001"}"""), HeldAttempt("""{"unit":"quest:q1","node":"close","attempt":1}""")],
            Unreadable = [Unreadable("""{"unit":"quest:q1"}""")],
            Ends = [Ended("""{"unit":"quest:q1"}""")],
            Proposals =
            [
                Proposal("""{"id":"p-workspace","scope":"workspace","name":"aurora"}"""),
                Proposal("""{"id":"p-repository","scope":"repository","name":"engine","session":"c0nv0001"}"""),
            ],
            Untrusted = [JsonNode.Parse("""{"folder":"C:/somewhere/engine","trustFile":"C:/somewhere/.claude.json","quest":"q1"}""")!.AsObject()],
            Waits =
            [
                JsonNode.Parse("""
                    {"agent":"claude-code","account":"account-2","name":"home","workspace":"aurora","until":"2026-10-06T04:42:00Z","stated":true,
                     "quests":["q1"],"asks":[],"signedOut":[]}
                    """)!.AsObject(),
            ],
        });

        Assert.Equal(
            [
                "account-wait:wait:claude-code/account-2", "answer:c0nv0001", "hand-over:h1", "helper:a1", "opinion:c0nv0001", "opinion:s42", "parked-quest:q1",
                "parked:p4rk3d00", "permission:c0nv0001#900", "proposal:7c1e9a04b2d5", "review:c0nv0001", "rule:p-repository", "rule:p-workspace",
                "trust:C:/somewhere/.claude.json\nC:/somewhere/engine", "unanswerable:q2", "work:c0nv0001", "work:quest:q1#close#1",
                "work:quest:q1#close#q6", "work:record#0a1b2c3d4e5f60718293a4b5c6d7e8f9.json", "work:session:c0nv0001#deploy#2",
            ],
            Keys(rows));
        Assert.Equal(
            [.. Keys(rows).Select(key => $"{key}=aurora")],
            [.. rows.Select(row => $"{Keys([row])[0]}={row["workspace"]?.GetValue<string>()}").Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// NEEDSSCOPE1: what is the machine's carries no workspace, so every window counts and lists it: a terminal's press (the pipe is this
    /// machine's), a rule for the whole machine, a folder whose holds span two workspaces, starts held on an account across workspaces, a
    /// unit no record owns. A row whose record is not in hand carries none either, so no window loses it.
    /// </summary>
    [Fact]
    public void What_is_the_machine_s_or_whose_record_is_not_in_hand_carries_no_workspace()
    {
        var orphan = JsonSerializer.Serialize(new
        {
            unit = "unknown-branch:engine/daoris/orphan", kind = "session-branch", resource = "daoris/orphan", role = "temporary", disposition = "blocked",
            detail = "its owner is unknown",
        });
        var rows = Rows(new()
        {
            Quests = [Quest("""{"id":"q1","status":"Taken","workspace":"aurora"}"""), Quest("""{"id":"q2","status":"Taken","workspace":"borealis"}""")],
            Registry = [Registration("engine")],
            Confirmations =
            [
                JsonNode.Parse("""
                    {"id":"c1","module":"DAORIS.DRIVER","route":"SESSION_ARCHIVE","asker":{"process":"daoris-driver","pid":4242},
                     "asked":"2026-09-21T09:00:00Z","expires":"2026-09-21T09:10:00Z"}
                    """)!.AsObject(),
            ],
            Proposals = [Proposal(), Proposal("""{"id":"p-unheld","scope":"repository","name":"engine","session":"g0n3"}""")],
            Untrusted =
            [
                JsonNode.Parse("""{"folder":"C:/somewhere/engine","trustFile":"C:/somewhere/.claude.json","quest":"q1"}""")!.AsObject(),
                JsonNode.Parse("""{"folder":"C:/somewhere/engine","trustFile":"C:/somewhere/.claude.json","quest":"q2"}""")!.AsObject(),
            ],
            Waits =
            [
                JsonNode.Parse("""
                    {"agent":"claude-code","account":"account-2","name":"home","workspace":null,"until":"2026-10-06T04:42:00Z","stated":true,
                     "quests":["q1","q2"],"asks":[],"signedOut":[]}
                    """)!.AsObject(),
            ],
            Groups = [Placed()],
            Helpers = [JsonNode.Parse("""{"id":"a2","session":"g0n3","question":"Is the cap right?","at":"2026-09-21T09:10:00Z"}""")!.AsObject()],
            Works =
            [
                JsonNode.Parse($$"""
                    {"unit":"unknown-branch:engine/daoris/orphan","repository":"engine","branch":"daoris/orphan","session":null,
                     "end":{"state":"unknown","said":"no record names its unit"},"result":[],"cleanup":{"state":"pending","waits":[{{orphan}}],"gone":[]},
                     "recovery":[],"entries":[{{orphan}}]}
                    """)!.AsObject(),
            ],
            Held = [HeldAttempt()],
        });

        Assert.Equal(
            [
                "account-wait:wait:claude-code/account-2", "confirmation:c1", "helper:a2", "review:r3v13w00", "rule:p-unheld", "rule:p0000002",
                "trust:C:/somewhere/.claude.json\nC:/somewhere/engine", "work:u1#deploy#2", "work:unknown-branch:engine/daoris/orphan",
            ],
            Keys(rows));
        Assert.Empty(rows.Where(row => row.ContainsKey("workspace")).Select(row => Keys([row])[0]));
    }

    // ---- the slice ----

    /// <summary>
    /// The allow-list holds every field the judge writes: each row of every case above, through the slice's own pick, keeps every field
    /// it had, so a field the judge comes to write is pushed only once it is named.
    /// </summary>
    [Fact]
    public void Every_field_the_judge_writes_is_named_in_the_allow_list()
    {
        var facts = new AttentionFacts
        {
            Sessions =
            [
                Session("""{"id":"parked","state":"awaiting-person","note":"why","noteParts":[]}"""),
                Intake("""{"state":"awaiting-person"}"""),
                Session("""{"id":"r3v13w00","state":"completed"}"""),
            ],
            Quests = [Quest("""{"to":"gone"}"""), Held("""{"id":"held"}"""), StepShown()],
            Asks = [Ask("""{"intake":"i9n8t7k6"}"""), Ask($$"""{"id":"asked","note":"refused","goAheads":[{{GoAhead().ToJsonString()}}]}""")],
            Untrusted = [With(Hold, """{"quest":"7a82cc","ask":"asked"}""")],
            Proposals = [Proposal()],
            Considered = [Parked()],
            Groups = [Placed("""{"pausedBy":{"scope":"ask","id":"a1"},"holds":{"why":"cap","reason":"r","repository":"engine","until":"2026-10-20T10:00:00Z"},"strikes":2,"awaits":"q","awaitsOf":"engine","holdsQuest":true}""")],
            Opinions = [JsonNode.Parse("""{"session":"s42","quest":"q1","repository":"engine","workspace":"aurora","since":"2026-09-21T09:00:00Z","auto":false,"opinion":{"state":"disputed","holds":true,"required":true,"opinion":"o1","reviewer":"codex","product":"Codex","maker":"OpenAI","label":"Codex","reviewing":"s42","findings":2,"disputes":2,"since":"2026-09-21T09:00:00Z","code":"c","until":"2026-09-22T09:00:00Z","later":1,"person":"p","answers":"o1","says":"s"}}""")!.AsObject()],
            Helpers = [JsonNode.Parse("""{"id":"a1","session":"parked","question":"Which?","agent":null,"at":"2026-09-21T09:30:00Z"}""")!.AsObject()],
            // NOTIFY2e: an agent's ask to hand its session over, waiting on the person.
            HandOvers = [JsonNode.Parse("""{"id":"h1","session":"parked","note":"Near my limit.","agent":"codex-acp","at":"2026-09-21T09:30:00Z"}""")!.AsObject()],
            Permissions = [JsonNode.Parse("""{"session":"parked","request":"9","call":"c","title":"t","kind":"edit","since":"2026-09-21T09:10:00Z","until":"2026-09-21T09:25:00Z","choices":[{"id":"a","name":"A","kind":"allow_once"}]}""")!.AsObject()],
            // COWORK1e: a third answer alone held for the person.
            Answers = [JsonNode.Parse("""{"session":"parked","agent":"codex-acp","words":["w1"],"since":"2026-09-21T09:20:00Z"}""")!.AsObject()],
            // OVERVIEWNOISE1b: a unit's row, its blocked entry with its code and facts.
            Works = [Unit($$$"""{"cleanup":{"state":"pending","waits":[{{{Entry("""{"code":"unfollowed","facts":{"branch":"daoris/s1a2b3c4"}}""").ToJsonString()}}}],"gone":[]}}""")],
            Held = [HeldAttempt("""{"outcome":"reached","revision":"r","resolution":"stop","resolvedAt":"2026-10-17T10:00:00Z","words":["stop"]}""")],
            // FABRIC-PERFORM1j: a node record that does not read.
            Unreadable = [Unreadable()],
            // FABRIC-CLOSE1j: a close found closed only in part, every field set.
            Ends = [Ended("""{"nodes":["qa"],"resolution":"stop"}""")],
            // CONFIRMSEEN1: a terminal's press waiting for the window, shown.
            Confirmations =
            [
                JsonNode.Parse("""{"id":"c1","module":"DAORIS.DRIVER","route":"SESSION_ARCHIVE","asker":{"process":"daoris-driver","pid":4242},"asked":"2026-10-20T09:00:00Z","expires":"2026-10-20T09:03:00Z","shownAt":"2026-10-20T09:01:00Z"}""")!.AsObject(),
            ],
            // KEEP1f: a volume still below its floor, every field set.
            Space =
            [
                JsonNode.Parse("""{"volume":"v1","said":"2026-10-22T09:00:00Z","root":"C:\\","free":4,"total":100,"floor":10,"freed":2,"could":3}""")!.AsObject(),
            ],
        };
        var rows = Rows(facts);
        Assert.Contains(rows, row => row["ended"] is JsonObject);
        Assert.Contains(rows, row => row["blocked"] is JsonObject blocked && blocked["entries"]![0]!["facts"] is JsonObject);

        // Every kind but the accounts': nineteen.
        Assert.Equal(19, rows.Select(row => row["kind"]!.GetValue<string>()).Distinct().Count());
        foreach (var row in rows) Assert.Equal(Normal(row), Normal(AttentionStateSlice.Picked(row)));
    }

    /// <summary>
    /// The allow-list's twin on the page: every field of a row the page's <c>PushedAttention</c> reads is pushed, and every field pushed
    /// is one it declares; the same for an account row's facts, as <c>AccountRowFacts</c> declares them. Read from the page's sources,
    /// so a field one side comes to read or write fails here until the other names it.
    /// </summary>
    [Fact]
    public void Every_field_the_page_reads_of_a_row_is_pushed_and_every_field_pushed_the_page_declares()
    {
        var attention = PageSource("work", "attention", "attention.ts");
        var accounts = PageSource("work", "attention", "accountWords.ts");

        Assert.Equal(AttentionStateSlice.Fields["row"].Order(StringComparer.Ordinal), TypeFields(attention, "PushedAttention").Order(StringComparer.Ordinal));
        Assert.Equal(AttentionStateSlice.Fields["account"].Order(StringComparer.Ordinal), TypeFields(accounts, "AccountRowFacts").Order(StringComparer.Ordinal));
    }

    private static string PageSource(params string[] path)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        Assert.NotNull(at);
        return File.ReadAllText(Path.Combine([at.FullName, "src", "Daoris.Web", "src", .. path]));
    }

    /// <summary>The top-level fields of a type the page declares as <c>export type Name = { … };</c>, comments apart.</summary>
    private static IReadOnlyList<string> TypeFields(string source, string name)
    {
        var block = System.Text.RegularExpressions.Regex.Match(
            source, $@"export type {name} = \{{(?<body>.*?)\n\}};", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(block.Success, $"the page declares no `{name}`");
        var body = System.Text.RegularExpressions.Regex.Replace(block.Groups["body"].Value, @"/\*.*?\*/|//[^\n]*", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        return [.. System.Text.RegularExpressions.Regex.Matches(body, @"^\s{2}(?<name>\w+)\??:", System.Text.RegularExpressions.RegexOptions.Multiline).Select(each => each.Groups["name"].Value)];
    }

    /// <summary>A hostile field in an input never reaches a partition: each row and what it holds is picked to its allow-list.</summary>
    [Fact]
    public void A_hostile_field_in_an_input_never_reaches_a_partition()
    {
        var judged = AttentionStateSlice.Judge(new AttentionFacts
        {
            Sessions = [Session("""{"state":"awaiting-person","token":"sk-hostile-session"}""")],
            Groups = [Placed("""{"token":"sk-hostile-placed","work":{"commits":1,"token":"sk-hostile-work"}}""")],
            Held = [HeldAttempt("""{"token":"sk-hostile-attempt"}""")],
            Permissions = [JsonNode.Parse("""{"session":"s","request":"1","since":"2026-09-21T09:10:00Z","token":"sk-hostile-wait","choices":[{"id":"a","name":"A","kind":"allow_once","token":"sk-hostile-choice"}]}""")!.AsObject()],
            Answers = [JsonNode.Parse("""{"session":"s","agent":"codex-acp","words":["w1"],"since":"2026-09-21T09:10:00Z","token":"sk-hostile-answer"}""")!.AsObject()],
            Opinions = [JsonNode.Parse("""{"session":"s42","quest":"q1","repository":"engine","since":"2026-09-21T09:00:00Z","auto":false,"token":"sk-hostile-opinion","opinion":{"state":"disputed","token":"sk-hostile-gate"}}""")!.AsObject()],
            Proposals = [Proposal("""{"token":"sk-hostile-proposal"}""")],
        })!;

        var pushed = JsonSerializer.Serialize(judged.Select(each => each.Data), IpcJson.Options);
        Assert.Contains("s1a2b3c4", pushed);
        Assert.DoesNotContain("sk-hostile", pushed);
    }

    /// <summary>
    /// Three partitions, each keyed: <c>rows</c>, each row under its kind and id as the band keys it; <c>sessions</c>, what waits on the
    /// person in Sessions, which the badge marks; and <c>working</c>, the sessions the driver runs, which the badge and the status bar
    /// count (SESSBADGE1). Judged empty, each is pushed empty, never gone. Not judged before every input it reads was judged (PUSHUX1).
    /// </summary>
    [Fact]
    public void The_slice_is_three_keyed_partitions_judged_empty_as_empty_and_not_judged_before_its_inputs()
    {
        Assert.Null(AttentionStateSlice.Judge(null));

        var judged = AttentionStateSlice.Judge(new AttentionFacts
        {
            Sessions = [Session("""{"state":"awaiting-person"}"""), Session("""{"id":"w0rk1ng0"}"""), Session("""{"id":"c4a7c4a7","kind":"chat"}""")],
        })!;
        Assert.Equal([AttentionStateSlice.Rows, AttentionStateSlice.Sessions, AttentionStateSlice.Working], judged.Select(each => each.Partition));
        var rows = JsonSerializer.SerializeToNode(judged[0].Data, IpcJson.Options)!.AsObject();
        Assert.Equal(["parked:s1a2b3c4"], rows.Select(each => each.Key));
        Assert.Equal(["session:s1a2b3c4"], JsonSerializer.SerializeToNode(judged[1].Data, IpcJson.Options)!.AsObject().Select(each => each.Key));
        Assert.Equal(
            """{"session:s1a2b3c4":{"workspace":"default"},"session:w0rk1ng0":{"workspace":"default"}}""",
            JsonSerializer.Serialize(judged[2].Data, IpcJson.Options));

        var empty = AttentionStateSlice.Judge(new AttentionFacts())!;
        Assert.All(empty, each => Assert.Equal("{}", JsonSerializer.Serialize(each.Data, IpcJson.Options)));
    }

    /// <summary>
    /// The inputs are the other slices' snapshots in memory and the registry the machine's facts hold: none read until every one of
    /// them has judged, and then each read from memory, with no service.
    /// </summary>
    [Fact]
    public async Task Its_inputs_are_the_other_slices_snapshots_in_memory_once_each_has_judged()
    {
        var sessions = new StateSlice(SessionsStateSlice.Name, Bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(
            SessionsStateSlice.Judge(new JsonArray(Session("""{"state":"awaiting-person"}""")).ToJsonString()))) { Keyed = true };
        var quests = new StateSlice(QuestsStateSlice.Name, Bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(
            QuestsStateSlice.Judge(new JsonArray(Quest("""{"to":"gone"}""")).ToJsonString()))) { Keyed = true };
        var asks = new StateSlice(AsksStateSlice.Name, Bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(AsksStateSlice.Judge("[]"))) { Keyed = true };
        var groups = new StateSlice(GroupsStateSlice.Name, Bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(
            [new SliceJudged(GroupsStateSlice.Rows, new JsonObject { ["r3v13w00"] = Placed() }), new SliceJudged(GroupsStateSlice.Entries, new JsonObject())])) { Keyed = true };
        // OVERVIEWNOISE1b: a unit with an act of the person's, a tree its session recorded that no branch record follows.
        var unfollowed = Entry("""{"code":"unfollowed","facts":{"branch":"daoris/s1a2b3c4"}}""").ToJsonString();
        var unit = Unit($$$"""{"cleanup":{"state":"pending","waits":[{{{unfollowed}}}],"gone":[]}}""");
        var work = new StateSlice(WorkStateSlice.Name, Bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(
            [new SliceJudged("engine", new JsonObject { ["units"] = new JsonArray(unit.DeepClone()), ["recovery"] = new JsonArray(), ["unread"] = new JsonArray() }),
             new SliceJudged(WorkStateSlice.Held, new JsonObject { ["held"] = new JsonArray(HeldAttempt()) })]));
        var slices = new StateSlices(sessions, quests, asks, groups, work);
        RepoView[] registry = [new("engine", true, null)];

        Assert.Null(AttentionStateSlice.Gather(slices, registry));
        foreach (var slice in new[] { sessions, quests, asks, groups }) await slice.JudgeAsync();
        Assert.Null(AttentionStateSlice.Gather(slices, registry));
        await work.JudgeAsync();
        Assert.Null(AttentionStateSlice.Gather(slices, null));

        var facts = AttentionStateSlice.Gather(slices, registry)!;
        Assert.Equal(
            ["parked:s1a2b3c4", "review:r3v13w00", "unanswerable:7a82cc", "work:s1a2b3c4", "work:u1#deploy#2"],
            Keys(NeedsAPerson.Rows(facts)));
        Assert.Equal("""{"repository":"engine","adopted":true,"addressable":true,"workspace":"default"}""", Assert.Single(facts.Registry).ToJsonString());
    }

    /// <summary>A session's park pushes its row alone as a delta; a judgement with nothing moved pushes nothing.</summary>
    [Fact]
    public async Task A_park_pushes_its_row_alone_and_nothing_moved_pushes_nothing()
    {
        var facts = new AttentionFacts
        {
            Sessions = [.. Enumerable.Range(0, 4).Select(at => Session($$"""{"id":"p{{at}}","state":"awaiting-person"}"""))],
        };
        var slice = AttentionStateSlice.Of(() => facts, Bus, []);

        await slice.JudgeAsync();
        var first = Pushes().Count;
        await slice.JudgeAsync();
        Assert.Equal(first, Pushes().Count);

        facts = facts with { Sessions = [.. facts.Sessions, Session("""{"id":"p9","state":"awaiting-person"}""")] };
        await slice.JudgeAsync();

        var moved = Pushes().Where(push => push.Partition == AttentionStateSlice.Rows).Last();
        Assert.NotNull(moved.BaseSeq);
        Assert.Equal(["parked:p9"], JsonSerializer.SerializeToNode(moved.Data, IpcJson.Options)!.AsObject().Select(each => each.Key));
    }

    private IReadOnlyList<SlicePush> Pushes() =>
    [
        .. Raised.Where(each => each.Type == StateSlice.PushType).Select(each => (SlicePush)each.Payload!)
            .Where(push => push.Slice == AttentionStateSlice.Name),
    ];

    [Theory]
    [InlineData("STATE_CHANGED", """{"scopes":["rules"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sessions","driver"]}""", true)]
    [InlineData("STATE_CHANGED", """{"scopes":["sync"]}""", false)]
    [InlineData("STATE_CHANGED", """{"scopes":["harnesses"]}""", true)]
    [InlineData("HARNESS_ENDED", "{}", true)]
    [InlineData("DRIVER_READY", "{}", true)]
    [InlineData("DRIVER_TICK", "{}", false)]
    public void What_wakes_the_attention_slice_on_the_bus(string type, string payload, bool wakes)
    {
        var wake = AttentionStateSlice.WakesOn.FirstOrDefault(each => each.Type == type);
        object told = type == StatePublisher.Type ? JsonSerializer.Deserialize<StateChanged>(payload, IpcJson.Options)! : new { };

        Assert.Equal(wakes, wake is not null && (wake.When is null || wake.When(told)));
    }

    /// <summary>
    /// Each input slice's push wakes it, but the sessions' only where what the judge reads of them moved: a working session's every
    /// word moves its record's time and nothing the band says, and wakes nothing.
    /// </summary>
    [Fact]
    public void A_working_session_s_word_wakes_nothing_and_a_park_wakes_it()
    {
        var working = Session("""{"state":"working"}""");
        var read = AttentionStateSlice.SessionsRead([working]);

        Assert.Equal(read, AttentionStateSlice.SessionsRead([With(working, """{"updated":"2026-09-21T10:05:00Z"}""")]));
        Assert.NotEqual(read, AttentionStateSlice.SessionsRead([With(working, """{"state":"awaiting-person"}""")]));
        var parked = Session("""{"state":"awaiting-person"}""");
        Assert.NotEqual(AttentionStateSlice.SessionsRead([parked]), AttentionStateSlice.SessionsRead([With(parked, """{"answer":"go"}""")]));
    }

    /// <summary>
    /// How each agent's accounts are used is read from the accounts slice's memory where it has judged, as <c>ACCOUNTS</c> answers it;
    /// before it has, the look's waits alone say the account rows.
    /// </summary>
    [Fact]
    public async Task How_the_accounts_are_used_is_read_from_the_accounts_slice_where_it_has_judged()
    {
        var agent = new JsonObject { ["agent"] = "claude-code", ["scopes"] = new JsonArray(), ["accounts"] = new JsonArray() };
        var accounts = new StateSlice(AccountsStateSlice.Name, Bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>([new SliceJudged("claude-code", agent)]));
        var slices = new StateSlices(accounts);

        Assert.Null(AttentionStateSlice.Use(slices));
        await accounts.JudgeAsync();
        Assert.Equal("""{"agents":[{"agent":"claude-code","scopes":[],"accounts":[]}]}""", AttentionStateSlice.Use(slices)!.ToJsonString());
    }

    /// <summary>The route answers the slice's snapshot from memory, `notJudged` with no partition before its inputs, on a loop with no service.</summary>
    [Fact]
    public async Task The_snapshot_route_answers_it_not_judged_before_its_inputs()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.Slices.Find(AttentionStateSlice.Name)!.JudgeAsync();

        var answered = await AnswerAsync(module, "STATE_SLICE", new { slice = AttentionStateSlice.Name });

        Assert.Equal((AttentionStateSlice.Name, StateSlice.NotJudged), (answered.GetProperty("slice").GetString(), answered.GetProperty("status").GetString()));
        Assert.Equal(0, answered.GetProperty("partitions").GetArrayLength());
    }
}
