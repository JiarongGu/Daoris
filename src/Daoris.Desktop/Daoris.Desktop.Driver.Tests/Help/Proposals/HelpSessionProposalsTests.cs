using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// Ask Daoris's <c>session</c> proposal (SESSUX1h2b, D126 §5.2, §5.3, §7.3): sessions archived on this machine or unarchived,
/// by id, or every session that ended archived, judged by where Sessions' list places each, as <see cref="SessionArchive"/>
/// judges an archive, and applied through Sessions' own archive. Nothing is archived until the person's press (D89).
/// </summary>
public sealed class HelpSessionProposalsTests : HelpProposalsFixture
{
    private static HelpProposal Session(string door, string? sessions, string? value = null) => Of("session", door, sessions, value);

    [Fact]
    public void One_ended_session_is_archived_and_the_card_says_it_keeps_its_record()
    {
        var plan = HelpProposals.Plan(Session("archive", "s1ended0"), DriverConfig.Empty, SessionsHere);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Archive session `s1ended0` on this machine: it leaves Sessions' list with its record whole, and Show archived, in "
            + "the list's ⋯, brings it back.",
            plan.Describe);
        Assert.Equal("daoris session archive s1ended0", plan.Terminal);
        Assert.Equal(["s1ended0"], plan.Sessions);
    }

    /// <summary>The records' own spelling is what the door is sent and the terminal names, whatever case the helper wrote.</summary>
    [Fact]
    public void Several_are_archived_by_the_records_own_spelling()
    {
        var plan = HelpProposals.Plan(Session("archive", "s1ended0,S2ENDED0,s1ended0"), DriverConfig.Empty, SessionsHere);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Archive 2 sessions on this machine: `s1ended0`, `s2ended0`. Each leaves Sessions' list with its record whole, and "
            + "Show archived, in the list's ⋯, brings them back.",
            plan.Describe);
        Assert.Equal("daoris session archive s1ended0 s2ended0", plan.Terminal);
        Assert.Equal(["s1ended0", "s2ended0"], plan.Sessions);
    }

    /// <summary>
    /// Archive never hides what needs the person (§5.2): a live session, one waiting on you and one to review are refused, as
    /// Sessions' own archive refuses them; so is an id no record here has, and Ask Daoris's own conversation, which its
    /// History keeps.
    /// </summary>
    [Theory]
    [InlineData("s3live00", "`s3live00` is still running: stop it first.")]
    [InlineData("s4parked", "`s4parked` is waiting on you, and archive never hides what needs you.")]
    [InlineData("s5review", "`s5review` has work to review, and archive never hides what needs you.")]
    [InlineData("zz000000", "there is no session `zz000000` on this machine.")]
    [InlineData("h1helper", "`h1helper` is Ask Daoris's own conversation, which Ask Daoris → History keeps.")]
    public void An_archive_the_list_would_keep_is_refused_in_its_words(string session, string refusal)
    {
        var plan = HelpProposals.Plan(Session("archive", session), DriverConfig.Empty, SessionsHere);

        Assert.Equal(refusal, plan.Refusal);
        Assert.Null(plan.Sessions);
    }

    /// <summary>A card never says it archives what its Apply would keep: one kept among several refuses them all, naming each.</summary>
    [Fact]
    public void One_the_list_would_keep_among_several_refuses_the_card_and_names_each()
    {
        var one = HelpProposals.Plan(Session("archive", "s1ended0,s3live00"), DriverConfig.Empty, SessionsHere);
        var two = HelpProposals.Plan(Session("archive", "s1ended0,s3live00,s4parked"), DriverConfig.Empty, SessionsHere);

        Assert.Equal("`s3live00` is still running: stop it first. Propose the others without it.", one.Refusal);
        Assert.Equal(
            "`s3live00` is still running: stop it first; `s4parked` is waiting on you, and archive never hides what needs you. "
            + "Propose the others without them.",
            two.Refusal);
    }

    /// <summary>An archive mark is this machine's, never the record's (§5.2), so a teammate's session may be archived here.</summary>
    [Fact]
    public void A_teammates_ended_session_may_be_archived_here_and_their_record_is_not_changed()
    {
        var plan = HelpProposals.Plan(Session("archive", "north/s7team0"), DriverConfig.Empty, SessionsHere);

        Assert.Null(plan.Refusal);
        Assert.Contains("Archive session `north/s7team0` on this machine", plan.Describe);
        Assert.Contains("It is a teammate's: the mark is this machine's alone, and their record is not changed.", plan.Describe);
        Assert.Equal(["north/s7team0"], plan.Sessions);
    }

    [Fact]
    public void One_archived_already_is_said_and_changes_nothing()
    {
        var alone = HelpProposals.Plan(Session("archive", "s6archiv"), DriverConfig.Empty, SessionsHere);
        var beside = HelpProposals.Plan(Session("archive", "s1ended0,s6archiv"), DriverConfig.Empty, SessionsHere);

        Assert.Null(alone.Refusal);
        Assert.Equal("`s6archiv` is archived already, so applying changes nothing.", alone.Describe);
        Assert.Equal([], alone.Sessions);
        Assert.EndsWith(" `s6archiv` is archived already, so nothing changes for it.", beside.Describe);
        Assert.Equal(["s1ended0"], beside.Sessions);
    }

    /// <summary>
    /// §5.3: archive what ended lists first what it would take and what it keeps, as Sessions' *Archive what ended…* does, and
    /// the press archives what has ended, each judged again as it goes.
    /// </summary>
    [Fact]
    public void Archive_what_ended_says_how_many_go_and_what_stays_in_the_list()
    {
        var plan = HelpProposals.Plan(Session("archive", null, "ended"), DriverConfig.Empty, SessionsHere);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Archives 8 sessions that ended. Kept in the list: 1 waiting on you, 1 to review. Each is judged again as Apply "
            + "archives it, and Show archived, in the list's ⋯, brings them back.",
            plan.Describe);
        Assert.Equal("daoris session archive --ended --yes", plan.Terminal);
        Assert.Equal(["s1ended0", "s2ended0", "h1helper", "north/s7team0", "c1chat00", "c2tree00", "c3named0", "c4remote"], plan.Sessions);
    }

    [Fact]
    public void Archive_what_ended_with_nothing_ended_says_so_and_changes_nothing()
    {
        var facts = SessionsHere with
        {
            Placed = [new SessionGrouping("s4parked", SessionGroup.You, "parked"), new SessionGrouping("s6archiv", SessionGroup.Archived, "completed") { Archived = true }],
        };

        var plan = HelpProposals.Plan(Session("archive", null, "ended"), DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal("Nothing that ended is left to archive, so applying changes nothing. Kept in the list: 1 waiting on you.", plan.Describe);
        Assert.Equal([], plan.Sessions);
    }

    [Fact]
    public void An_archived_session_is_unarchived_and_one_that_is_not_is_said_never_refused()
    {
        var plan = HelpProposals.Plan(Session("unarchive", "s6archiv,s1ended0"), DriverConfig.Empty, SessionsHere);
        var none = HelpProposals.Plan(Session("unarchive", "s1ended0,s2ended0"), DriverConfig.Empty, SessionsHere);

        Assert.Null(plan.Refusal);
        Assert.Equal(
            "Unarchive session `s6archiv` on this machine: it is back in Sessions' list, in the group its state puts it in. "
            + "`s1ended0` is not archived, so nothing changes for it.",
            plan.Describe);
        Assert.Equal("daoris session unarchive s6archiv s1ended0", plan.Terminal);
        Assert.Equal(["s6archiv"], plan.Sessions);
        Assert.Null(none.Refusal);
        Assert.Equal("`s1ended0`, `s2ended0` are not archived, so applying changes nothing.", none.Describe);
        Assert.Equal([], none.Sessions);
    }

    [Theory]
    [InlineData("unarchive", "zz000000", null, "there is no session `zz000000` on this machine.")]
    [InlineData("unarchive", "h1helper", null, "`h1helper` is Ask Daoris's own conversation, which Ask Daoris → History keeps.")]
    [InlineData("unarchive", null, "ended", "`ended` goes only with `archive`: what ended is archived, never unarchived.")]
    [InlineData("archive", null, null, "a session proposal names the sessions by id, or `ended` for every one that ended.")]
    [InlineData("archive", " ", "later", "a session proposal names the sessions by id, or `ended` for every one that ended.")]
    [InlineData("delete", "c1chat00", null, "`delete` is not a session's change — `archive`, `unarchive`, `helper` or `pass`; a delete is `delete_propose`.")]
    public void A_proposal_the_list_would_not_take_is_refused(string door, string? sessions, string? value, string refusal)
    {
        var plan = HelpProposals.Plan(Session(door, sessions, value), DriverConfig.Empty, SessionsHere);

        Assert.Equal(refusal, plan.Refusal);
    }

    /// <summary>SESSUX1h1: a session's stop is its own conversation, so the refusal says where it is pressed.</summary>
    [Fact]
    public void A_stop_is_no_door_and_the_refusal_says_the_person_stops_it_on_its_page()
    {
        var plan = HelpProposals.Plan(Session("stop", "s3live00"), DriverConfig.Empty, SessionsHere);

        Assert.Equal(
            "`stop` is not a session's change — `archive`, `unarchive`, `helper` or `pass`; a stop is the person's own, on the session's page, "
            + "where a go takes them.",
            plan.Refusal);
    }

    [Fact]
    public async Task An_archive_goes_through_sessions_own_door_and_its_refusal_is_said_in_its_words()
    {
        var (applied, doors, _) = await ApplyAsync(Session("archive", "s1ended0,s2ended0"), facts: SessionsHere);
        var refusing = new HelpStandInDoors { Archived = (false, "s2ended0 is still running, so it was not archived. Stop it first.") };
        var (refused, _, _) = await ApplyAsync(Session("archive", "s2ended0") with { Id = "p9" }, refusing, SessionsHere);

        Assert.True(applied.Applied);
        Assert.Equal(["SESSION_ARCHIVE archive s1ended0,s2ended0"], doors.Calls);
        Assert.Equal("Applied: `#p6` (`daoris session archive s1ended0 s2ended0`) — Archived it.", applied.Told);
        Assert.Equal("applied", HelpProposals.Find(_home, "p6")!.State);
        Assert.False(refused.Applied);
        Assert.Equal(
            "Not applied: `#p9` (`daoris session archive s2ended0`) — s2ended0 is still running, so it was not archived. Stop it first.",
            refused.Told);
        Assert.Equal("refused", HelpProposals.Find(_home, "p9")!.State);
    }

    [Fact]
    public async Task An_unarchive_and_archive_what_ended_go_through_the_same_door()
    {
        var (unarchived, back, _) = await ApplyAsync(Session("unarchive", "s6archiv,s1ended0"), facts: SessionsHere);
        var (ended, all, _) = await ApplyAsync(Session("archive", null, "ended") with { Id = "p7" }, facts: SessionsHere);

        Assert.True(unarchived.Applied);
        Assert.Equal(["SESSION_ARCHIVE unarchive s6archiv"], back.Calls);
        Assert.True(ended.Applied);
        Assert.Equal(["SESSION_ARCHIVE archive s1ended0,s2ended0,h1helper,north/s7team0,c1chat00,c2tree00,c3named0,c4remote"], all.Calls);
    }

    // COWORKUX1: the person's helper doors, a card for the page's Ask another agent…, Ask again… and Pass to the lead.

    private static HelpProposal Helping(string question, HelperProposalFields? fields = null, string session = "s1ended0") =>
        Session("helper", session, question) with { Helper = fields ?? new HelperProposalFields(null, null, false) };

    [Fact]
    public void A_helper_card_says_whom_it_asks_and_where_the_answer_goes_and_names_the_terminal_that_does_it()
    {
        var lead = HelpProposals.Plan(Helping("Why does it drop the last day?"), DriverConfig.Empty, SessionsHere);
        var again = HelpProposals.Plan(
            Helping("And on a leap year?", new HelperProposalFields("codex-acp", "a1b2c3d4", true)), DriverConfig.Empty, SessionsHere);

        Assert.Null(lead.Refusal);
        Assert.Contains("Ask another agent Daoris chooses, read-only, a question about session `s1ended0`'s work", lead.Describe);
        Assert.Contains("comes back to that session as its next turn, marked as the helper's", lead.Describe);
        Assert.Equal("daoris session helper s1ended0 \"Why does it drop the last day?\"", lead.Terminal);
        Assert.Null(again.Refusal);
        Assert.Contains("Ask agent `codex-acp`", again.Describe);
        Assert.Contains("asks again after `#a1b2c3d4`", again.Describe);
        Assert.Contains("reaches that session only once you pass it to the lead", again.Describe);
        Assert.Equal("daoris session helper s1ended0 \"And on a leap year?\" --agent codex-acp --after a1b2c3d4 --to-me", again.Terminal);
    }

    [Theory]
    [InlineData("zz000000", "Why?", "there is no session `zz000000` on this machine")]
    [InlineData("h1helper", "Why?", "`h1helper` is Ask Daoris's own conversation, which asks no helper and has none to pass")]
    [InlineData("north/s7team0", "Why?", "`north/s7team0` is a teammate's: its helper is asked on their machine")]
    [InlineData("s1ended0", "  ", "a helper is asked a question, and none was given.")]
    [InlineData("s1ended0,s2ended0", "Why?", "the helper door names exactly one session, by its id.")]
    public void A_helper_card_the_machine_would_refuse_is_refused_in_the_cards_words(string session, string question, string refusal)
    {
        Assert.Equal(refusal, HelpProposals.Plan(Helping(question, session: session), DriverConfig.Empty, SessionsHere).Refusal);
    }

    [Fact]
    public void A_pass_card_names_the_ask_and_the_session_and_nothing_else()
    {
        var pass = Session("pass", "s1ended0") with { Helper = new HelperProposalFields(null, "a1b2c3d4", false) };
        var none = Session("pass", "s1ended0") with { Helper = new HelperProposalFields(null, null, false) };

        var plan = HelpProposals.Plan(pass, DriverConfig.Empty, SessionsHere);

        Assert.Null(plan.Refusal);
        Assert.Contains("Pass the answer to ask `#a1b2c3d4`, kept for you, to session `s1ended0`", plan.Describe);
        Assert.Equal("daoris session pass a1b2c3d4", plan.Terminal);
        Assert.Equal("the pass door names the ask whose kept answer is passed.", HelpProposals.Plan(none, DriverConfig.Empty, SessionsHere).Refusal);
    }

    [Fact]
    public async Task A_helper_card_and_a_pass_card_go_through_the_pages_own_doors_and_their_sentences_are_said()
    {
        var (asked, ask, _) = await ApplyAsync(
            Helping("Why?", new HelperProposalFields("codex-acp", "a1", true)), facts: SessionsHere);
        var refusing = new HelpStandInDoors { Helped = (false, "Session s1ended0 holds no ask #a1 to ask again after. Nothing was asked.") };
        var (passed, pass, _) = await ApplyAsync(
            Session("pass", "s1ended0") with { Id = "p8", Helper = new HelperProposalFields(null, "a1", false) }, refusing, SessionsHere);

        Assert.True(asked.Applied);
        Assert.Equal(["SESSION_HELPER_ASK s1ended0 Why? agent=codex-acp after=a1 toPerson=True"], ask.Calls);
        Assert.Equal("Applied: `#p6` (`daoris session helper s1ended0 \"Why?\" --agent codex-acp --after a1 --to-me`) — Asked #h1.", asked.Told);
        Assert.False(passed.Applied);
        Assert.Equal(["SESSION_HELPER_PASS s1ended0 a1"], pass.Calls);
        Assert.Equal("refused", HelpProposals.Find(_home, "p8")!.State);
    }

    /// <summary>The proposal's file is read back with the helper doors' fields, as the service's writer spells them.</summary>
    [Fact]
    public void The_helper_fields_are_read_from_the_proposals_file()
    {
        var folder = HelpProposals.FolderOf(_home);
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(
            Path.Combine(folder, "p5.json"),
            """{"id":"p5","kind":"session","door":"helper","target":"s1ended0","workspace":null,"value":"Why?","sentence":null,"agent":"codex-acp","ask":"a1","toMe":true,"why":"x","state":"proposed"}""");

        var read = HelpProposals.Find(_home, "p5")!;

        Assert.Equal(new HelperProposalFields("codex-acp", "a1", true), read.Helper);
    }

    /// <summary>A card that changes nothing calls no door when applied, and says so.</summary>
    [Fact]
    public async Task A_card_that_changes_nothing_calls_no_door()
    {
        var (applied, doors, _) = await ApplyAsync(Session("unarchive", "s1ended0"), facts: SessionsHere);

        Assert.True(applied.Applied);
        Assert.Empty(doors.Calls);
        Assert.Equal("Applied: `#p6` — nothing changed: `s1ended0` is not archived, so applying changes nothing.", applied.Told);
    }
}