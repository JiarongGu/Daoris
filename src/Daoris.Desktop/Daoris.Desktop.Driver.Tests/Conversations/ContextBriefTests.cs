using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Conversations;

/// <summary>
/// SESSCTX1b (D189 points 4-5, the session-context design §3): the brief a new part is handed is a pure function of its line's
/// records, composed with no model. Every case here is recorded events, as a part's record keeps them: a word counts once by its id,
/// a word taken back is left out, an answered word is a past exchange, a channel's message is told by its mark, the bound keeps what
/// its caps choose and says every cut, a Han character weighs three, a check is a call matching a declared gate, the last failed
/// one carries its kept output, and no thought or parallel part is ever handed.
/// </summary>
public sealed class ContextBriefTests
{
    private const string Tree = "X:/daoris/trees/engine/s-1a2b3c4d";

    private static readonly HeavyGate[] Gates =
    [
        new("driver", "dotnet test src/Engine.Tests --filter Category!=Process", null, false),
        new("web", "npm run test:web:unit", null, true),
    ];

    private static SessionEvent Asked(string text, string origin = "person", string? id = null, string? reaches = null) =>
        new() { Kind = SessionEventKind.User, Origin = origin, Text = text, Id = id, Reaches = reaches };

    private static SessionEvent Said(string text) => new() { Kind = SessionEventKind.Message, Text = text };

    private static SessionEvent Ended() => new() { Kind = SessionEventKind.Turn, StopReason = "end_turn" };

    private static SessionEvent Call(
        string id, string title, string kind = "execute", string? status = "completed", string? input = null, string? output = null,
        IReadOnlyList<string>? locations = null) =>
        new()
        {
            Kind = SessionEventKind.Tool, Id = id, Title = title, ToolKind = kind, Status = status, Input = input, Output = output,
            Locations = locations,
        };

    private static BriefPart Part(string session, params SessionEvent[] events) => new(session, "codex-acp", events);

    private static BriefLine Line(params BriefPart[] parts) => new(HandOverWhy.Press, parts) { Conversation = true, Tree = Tree, Gates = Gates };

    private static string Folded(ComposedBrief brief) => System.Text.RegularExpressions.Regex.Replace(brief.Text, @"\s+", " ");

    // ——— Its opening, and the request.

    [Fact]
    public void It_opens_with_the_session_it_continues_its_agent_why_it_moved_and_that_it_is_Daoris_s_record()
    {
        var brief = ContextBrief.Compose(Line(Part("c0nv0001", Asked("Add a dark mode."), Said("Started."), Ended())));

        var text = Folded(brief);
        Assert.StartsWith("You continue session `c0nv0001`, which ran on codex-acp; it moved because the person asked for it.", text);
        Assert.Contains("Daoris's record", text);
        Assert.Contains("not your own memory", text);
        Assert.Contains("The request, as the person first wrote it: > Add a dark mode.", text);
        // A conversation's request is its first word, never told again among the words it took: its turn points back to it.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(brief.Text, "Add a dark mode"));
        Assert.Contains("- Turn 1: The request, above. It answered, in its last message of the turn: > Started.", text);
    }

    [Fact]
    public void A_driven_line_has_no_request_of_its_own_and_hands_no_instruction_again()
    {
        var instruction = Asked("You are the agent for `engine`… # Add the note field", origin: "target") with
        {
            Account = new InstructionAccount(42, []),
        };
        var brief = ContextBrief.Compose(new BriefLine(HandOverWhy.CarryOn, [Part("s1", instruction, Said("The column is in."), Ended())]) { Tree = Tree });

        Assert.DoesNotContain("The request", brief.Text);
        Assert.DoesNotContain("You are the agent for", brief.Text);
        Assert.Contains("it stopped before its work was done", brief.Text);
        Assert.Contains("> The column is in.", brief.Text);
    }

    // ——— Every word the session took, once each.

    [Fact]
    public void A_word_told_twice_is_counted_once_where_it_was_taken()
    {
        var brief = ContextBrief.Compose(Line(Part(
            "c0nv0001",
            Asked("Add a dark mode."), Said("Started."), Ended(),
            // Shown as said, told again for its level, then taken under the same id.
            Asked("Use the system's colours.", id: "w1", reaches: "turn-end") with { Level = "ordinary" },
            Asked("Use the system's colours.", id: "w1", reaches: "turn-end") with { Level = "urgent" },
            Asked("Use the system's colours.", id: "w1"),
            Said("Done: it follows the system."), Ended())));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(brief.Text, "Use the system's colours"));
        var words = Assert.Single(brief.Sections, section => section.Name == HandedSections.BriefWords);
        Assert.Equal(1, words.Of);
    }

    [Fact]
    public void A_word_taken_back_before_it_was_sent_is_left_out_and_one_still_waiting_follows_the_brief()
    {
        var brief = ContextBrief.Compose(Line(Part(
            "c0nv0001",
            Asked("Add a dark mode."), Said("Started."), Ended(),
            Asked("Make it blue.", id: "w1", reaches: "turn-end"),
            SessionNotes.Withdrawn("w1"),
            Asked("Still waiting.", id: "w2", reaches: "turn-end"))));

        Assert.DoesNotContain("Make it blue", brief.Text);
        Assert.DoesNotContain("Still waiting", brief.Text);
    }

    [Fact]
    public void An_answered_word_is_a_past_exchange_kept_with_the_turn_that_answered_it()
    {
        var brief = ContextBrief.Compose(Line(Part(
            "c0nv0001",
            Asked("Add a dark mode."), Said("Started."), Ended(),
            Asked("Make it blue.", id: "w1"), Said("Blue it is."), Ended(),
            Asked("And the borders?", id: "w2"), Said("Working on the borders"))));

        var text = brief.Text;
        Assert.Contains("past exchange", Folded(brief));
        var word = text.IndexOf("Make it blue.", StringComparison.Ordinal);
        var answer = text.IndexOf("Blue it is.", StringComparison.Ordinal);
        Assert.True(word >= 0 && word < answer, text);
        // The last word, taken by a turn that never ended, is said to be so: the one exchange a new part may still owe an answer.
        var last = text.IndexOf("And the borders?", StringComparison.Ordinal);
        Assert.True(last > answer, text);
        Assert.Contains("did not end", text[last..]);
    }

    [Fact]
    public void Another_agent_s_findings_and_a_channel_s_message_are_that_agent_s_claim_told_by_their_marks()
    {
        var brief = ContextBrief.Compose(Line(Part(
            "c0nv0001",
            Asked("Add a dark mode."), Said("Started."), Ended(),
            Asked("The reviewer found a missing test.", origin: "target") with { Opinion = "o1" }, Said("Added it."), Ended(),
            // By its mark, whatever Daoris's words around it say.
            Asked("Session `s9` wrote: the column is `total`.", origin: "target") with { Channel = "m1" }, Said("Using total."), Ended(),
            // A record from before the mark, by the claim's opening.
            Asked("Another session says, on the ask's channel (db): the table is `orders`.", origin: "target"), Said("Orders."), Ended(),
            // The person's own channel message is theirs.
            Asked("Ship it today.", id: "w9") with { Channel = "m2" }, Said("Shipping."), Ended(),
            // Daoris's own words are not repeated, and nor is any other target it composed.
            Asked("Daoris: the person reopened submission 41.", origin: "target") with { From = "daoris" }, Said("Noted."), Ended())));

        var text = Folded(brief);
        Assert.Contains("Another agent's findings (opinion `o1`), as its claim: > The reviewer found a missing test.", text);
        Assert.Contains("Another session's message on the ask's channel, as that agent's claim: > Session `s9` wrote: the column is `total`.", text);
        Assert.Contains("Another session's message on the ask's channel, as that agent's claim: > Another session says", text);
        Assert.Contains("The person, on the ask's channel: > Ship it today.", text);
        Assert.DoesNotContain("reopened submission 41", text);
    }

    // ——— What each turn did, and the checks.

    [Fact]
    public void Each_turn_says_the_files_it_edited_and_the_commands_that_failed_with_no_machine_path_and_no_thought()
    {
        var brief = ContextBrief.Compose(Line(Part(
            "c0nv0001",
            Asked("Add a dark mode."),
            new SessionEvent { Kind = SessionEventKind.Thought, Text = "I wonder about the palette." },
            Call("t1", $"Edit {Tree}/src/theme.ts", kind: "edit", locations: [$"{Tree}/src/theme.ts"]),
            Call("t2", "Write src/dark.css", kind: "edit", locations: ["X:\\daoris\\trees\\engine\\s-1a2b3c4d\\src\\dark.css"]),
            Call("t3", "npm run lint", input: """{"command":"npm run lint"}""", status: "failed"),
            Said("Themes in."), Ended())));

        var text = Folded(brief);
        Assert.Contains("It edited `src/theme.ts` and `src/dark.css`", text);
        Assert.Contains("a command failed: `npm run lint`", text);
        Assert.DoesNotContain("X:/daoris", brief.Text);
        Assert.DoesNotContain("X:\\daoris", brief.Text);
        Assert.DoesNotContain("palette", brief.Text);
    }

    [Fact]
    public void A_check_is_a_call_matching_a_declared_gate_and_the_last_failed_one_carries_its_kept_output()
    {
        var brief = ContextBrief.Compose(Line(
            Part(
                "c0nv0001",
                Asked("Add a dark mode."),
                // A title that sounds like a check is none: only the command, matched to a declared gate, makes one.
                Call("t0", "Run the tests", input: """{"command":"echo tests"}"""),
                Call("t1", "dotnet test", input: """{"command":"dotnet test src/Engine.Tests --filter Category!=Process"}""", status: "failed",
                    output: "Failed ThemeTests.Dark_mode_reads_the_system [12 ms]\n  Expected: dark\n  Actual: light"),
                Said("A test fails."), Ended()),
            Part(
                "c0nv0002",
                Asked("Fix it.", id: "w1"),
                Call("t5", "web tests", input: """{"command":"npm run   test:web:unit"}"""),
                Said("Web passes."), Ended())));

        var text = Folded(brief);
        Assert.DoesNotContain("Run the tests", text);
        Assert.Contains("The last check session `c0nv0001` ran: `driver` (dotnet test), failed.", text);
        Assert.Contains("The last check session `c0nv0002` ran: `web` (web tests), completed.", text);
        // The driver gate was not run again green, so its failure is the work's state, with what it printed.
        Assert.Contains("The last check that failed, `driver`, printed: > Failed ThemeTests.Dark_mode_reads_the_system [12 ms]", text);
    }

    [Fact]
    public void A_failed_check_run_green_since_carries_no_output()
    {
        var brief = ContextBrief.Compose(Line(Part(
            "c0nv0001",
            Asked("Add a dark mode."),
            Call("t1", "dotnet test", input: """{"command":"dotnet test src/Engine.Tests --filter Category!=Process"}""", status: "failed", output: "Failed once."),
            Call("t2", "dotnet test", input: """{"command":"dotnet test src/Engine.Tests --filter Category!=Process"}""", status: "completed"),
            Said("Green."), Ended())));

        Assert.DoesNotContain("Failed once.", brief.Text);
        Assert.DoesNotContain("The last check that failed", brief.Text);
    }

    // ——— The work's state.

    [Fact]
    public void The_state_says_the_tree_s_head_what_it_holds_uncommitted_its_commits_and_the_plan_with_the_turn_it_was_written_in()
    {
        var plan = new SessionEvent { Kind = SessionEventKind.Plan, Entries = [new("Read the theme", "completed"), new("Add the toggle", "pending")] };
        var line = Line(Part("c0nv0001", Asked("Add a dark mode."), Said("One."), Ended(), Asked("Go on.", id: "w1"), plan, Said("Two."), Ended())) with
        {
            State = new BriefTree("abc1234 Add the theme", [" M src/theme.ts"], ["abc1234 Add the theme"]),
            Open = ["the go-ahead to change the public API"],
        };

        var text = Folded(ContextBrief.Compose(line));

        Assert.Contains("The tree's head, read now: abc1234 Add the theme", text);
        Assert.Contains("Uncommitted in the tree: - M src/theme.ts", text);
        Assert.Contains("Commits on this line since it began: - abc1234 Add the theme", text);
        Assert.Contains("The plan, as last kept, written in turn 2 of session `c0nv0001`:", text);
        Assert.Contains("[pending] Add the toggle", text);
        Assert.Contains("Asked and not answered yet: the go-ahead to change the public API", text);
    }

    [Fact]
    public void The_files_the_person_attached_are_named_with_where_they_lie()
    {
        var part = Part("c0nv0001", Asked("Add a dark mode.") with { Files = ["mock.png"] }, Said("Seen."), Ended()) with
        {
            Kept = "X:/daoris/data/chats/c0nv0001",
        };

        var text = Folded(ContextBrief.Compose(Line(part)));

        Assert.Contains("`mock.png`, in X:/daoris/data/chats/c0nv0001", text);
    }

    [Fact]
    public void The_outgoing_agent_s_note_is_its_claim_and_the_entry_s_other_parts_one_line_each()
    {
        var line = Line(Part("c0nv0001", Asked("Add a dark mode."), Said("One."), Ended())) with
        {
            Note = "I was about to wire the toggle.",
            Others = ["session `q2s00001` for #q2, opened, completed"],
        };

        var brief = ContextBrief.Compose(line);

        // In MIXAGENT1c's one wording for an outgoing agent's note.
        Assert.Contains("its agent left you this note. It is that agent's own claim, not Daoris's record: check it before you rely on it. "
            + "> I was about to wire the toggle.", Folded(brief));
        Assert.Contains("- session `q2s00001` for #q2, opened, completed", brief.Text);
        Assert.Contains(brief.Sections, section => section.Name == HandedSections.BriefNote);
        Assert.DoesNotContain(ContextBrief.Compose(line with { Note = null }).Sections, section => section.Name == HandedSections.BriefNote);
    }

    // ——— The bound.

    [Fact]
    public void The_bound_keeps_the_newest_words_and_turns_and_says_every_cut()
    {
        var events = new List<SessionEvent> { Asked("Add a dark mode."), Said("Started."), Ended() };
        for (var n = 1; n <= 60; n++)
        {
            events.Add(Asked($"Word {n:D2}: " + new string('w', 900), id: $"w{n}"));
            events.Add(Said($"Answer {n:D2}: " + new string('a', 1500)));
            events.Add(Ended());
        }

        var brief = ContextBrief.Compose(Line(new BriefPart("c0nv0001", "codex-acp", events)));

        Assert.True(ContextBrief.Weight(brief.Text) <= ContextBrief.Bound, $"{ContextBrief.Weight(brief.Text)} over the bound");
        // The request stays, and the newest words and answers, the oldest after the request left out first.
        Assert.Contains("Add a dark mode.", brief.Text);
        Assert.Contains("Word 60:", brief.Text);
        Assert.Contains("Answer 60:", brief.Text);
        Assert.DoesNotContain("Word 01:", brief.Text);
        Assert.DoesNotContain("Answer 01:", brief.Text);

        var words = Assert.Single(brief.Sections, section => section.Name == HandedSections.BriefWords);
        var turns = Assert.Single(brief.Sections, section => section.Name == HandedSections.BriefTurns);
        var leftWords = Assert.Single(words.Cuts!, cut => cut.Code == HandedCuts.BriefWordsOlder);
        var leftTurns = Assert.Single(turns.Cuts!, cut => cut.Code == HandedCuts.BriefTurnsOlder);
        Assert.Equal(60, words.Of);
        Assert.Equal(60 - words.Shown, leftWords.Count);
        Assert.True(words.Shown * 900 <= ContextBrief.WordsCap);
        Assert.True(leftTurns.Count > 0);
        Assert.True(turns.Chars >= ContextBrief.TurnsFloor, $"the turns kept only {turns.Chars}");
        // And the brief says what it left out, by count, and that the record holds it.
        Assert.Contains($"{leftWords.Count} of the words the session took", Folded(brief));
        Assert.Contains("Daoris's record of this line holds", Folded(brief));
        Assert.Equal(brief.Text.Length, brief.Sections.Sum(section => section.Chars ?? 0));
    }

    [Fact]
    public void An_item_past_its_own_cap_is_clipped_and_says_so_and_a_last_message_keeps_its_end()
    {
        var brief = ContextBrief.Compose(Line(Part(
            "c0nv0001",
            Asked("Add a dark mode. " + new string('r', 6000)),
            Said(new string('m', 6000) + " The end of it."),
            Ended())));

        var text = brief.Text;
        Assert.Contains("The end of it.", text);
        Assert.Contains("characters left out here", text);
        var opening = Assert.Single(brief.Sections, section => section.Name == HandedSections.BriefOpening);
        Assert.True(opening.Chars <= ContextBrief.OpeningCap);
        Assert.Contains(brief.Sections.SelectMany(section => section.Cuts ?? []), cut => cut.Code == HandedCuts.BriefClipped);
    }

    [Fact]
    public void A_Han_character_weighs_three_toward_the_bound()
    {
        Assert.Equal(3, ContextBrief.Weight("暗"));
        Assert.Equal(6, ContextBrief.Weight("a 暗b"));

        var events = new List<SessionEvent> { Asked("加一个深色模式。"), Said("开始。"), Ended() };
        for (var n = 1; n <= 40; n++)
        {
            events.Add(Asked($"第{n}句：" + new string('词', 700), id: $"w{n}"));
            events.Add(Said($"答{n}：" + new string('答', 700)));
            events.Add(Ended());
        }

        var brief = ContextBrief.Compose(Line(new BriefPart("c0nv0001", "claude-code", events)));

        Assert.True(ContextBrief.Weight(brief.Text) <= ContextBrief.Bound);
        // Counted by characters alone it would fit far more: weighted, the Chinese brief is cut sooner.
        Assert.True(brief.Text.Length < ContextBrief.Bound / 2, $"{brief.Text.Length} characters kept");
    }

    // ——— The line, and only the line.

    [Fact]
    public void The_line_follows_each_part_s_from_link_back_and_never_a_part_beside_it()
    {
        var records = new Dictionary<string, PriorSession>(StringComparer.Ordinal)
        {
            ["s1"] = new("s1", null, "stopped") { Adapter = "claude-code" },
            ["s2"] = new("s2", null, "stopped") { Adapter = "claude-code", From = "s1", FromWhy = HandOverWhy.CarryOn },
            // A part of the same entry running beside the line, continuing nothing on it.
            ["s9"] = new("s9", null, "working") { Adapter = "codex-acp" },
            ["s3"] = new("s3", null, "stopped") { Adapter = "codex-acp", From = "s2", FromWhy = HandOverWhy.Press },
        };

        Assert.Equal(["s1", "s2", "s3"], ContextBriefs.LineOf(records, "s3").Select(part => part.Session));
        Assert.Equal([null, HandOverWhy.CarryOn, HandOverWhy.Press], ContextBriefs.LineOf(records, "s3").Select(part => part.Why));
        Assert.Equal(["s9"], ContextBriefs.LineOf(records, "s9").Select(part => part.Session));
        // A link that comes back on itself ends where it would repeat.
        records["s1"] = records["s1"] with { From = "s3", FromWhy = HandOverWhy.Retry };
        Assert.Equal(["s1", "s2", "s3"], ContextBriefs.LineOf(records, "s3").Select(part => part.Session));
    }

    [Fact]
    public void The_account_s_sections_sum_to_the_brief_and_each_names_its_source()
    {
        var brief = ContextBrief.Compose(Line(Part("c0nv0001", Asked("Add a dark mode."), Said("One."), Ended())) with
        {
            State = new BriefTree("abc1234 Add the theme", [], []),
        });

        Assert.Equal(brief.Text.Length, brief.Account.Chars);
        Assert.Equal(brief.Text.Length, brief.Account.Sections.Sum(section => section.Chars ?? 0));
        Assert.All(brief.Account.Sections, section => Assert.False(string.IsNullOrEmpty(section.Source)));
        // The request is the conversation's first word, so no other word was taken: the words' section is not handed.
        Assert.Equal(
            [HandedSections.BriefOpening, HandedSections.BriefTurns, HandedSections.BriefState],
            brief.Account.Sections.Select(section => section.Name));
        var state = brief.Account.Sections.Single(section => section.Name == HandedSections.BriefState);
        Assert.Equal(HandedSources.Tree, state.Source);
    }
}
