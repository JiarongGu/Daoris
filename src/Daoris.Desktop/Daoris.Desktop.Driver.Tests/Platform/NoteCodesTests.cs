using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's note codes and the page's catalogues are two lists that must agree, in two languages and two artefacts, with
/// no compiler between them (LANG1a, D142 point 4; the language design §5). Read from this side, as the modules'
/// <c>RefusalCatalogueTests</c> reads its codes, so a new line the driver writes fails here until both languages word it.
/// </summary>
public sealed class NoteCodesTests
{
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>
    /// A language's whole catalogue: one file per area under <c>locales/&lt;language&gt;/</c>, merged as the page merges them.
    /// Shared with <c>ChatTurnsTests</c>, which holds a conversation note's codes to the same catalogues (MSG1c3).
    /// </summary>
    internal static JsonElement Catalogue(string language)
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src", "locales", language);
        var merged = new JsonObject();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            foreach (var (key, value) in JsonNode.Parse(File.ReadAllText(file))!.AsObject()) merged[key] = value?.DeepClone();
        }

        return JsonDocument.Parse(merged.ToJsonString()).RootElement.Clone();
    }

    internal static IReadOnlySet<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\{\s*([\w.]+)[^}]*\}\}").Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    internal static string Entry(JsonElement catalogue, string key, string language)
    {
        Assert.True(
            catalogue.TryGetProperty(key, out var text) && !string.IsNullOrWhiteSpace(text.GetString()),
            $"`{key}` has no entry in {language}: the line would reach the person as its English.");
        return text.GetString()!;
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_code_has_an_entry_that_says_each_of_its_values_and_no_other(string language)
    {
        var catalogue = Catalogue(language);
        Assert.NotEmpty(NoteCodes.All);
        foreach (var code in NoteCodes.All)
        {
            var said = Placeholders(Entry(catalogue, code.CatalogueKey, language));
            // A dropped value renders the fixed part and silently loses the fact (translation-parity).
            Assert.True(said.SetEquals(code.Values), $"{language}: `{code.CatalogueKey}` says {{{string.Join(", ", said)}}}, and `{code.Code}` carries {{{string.Join(", ", code.Values)}}}.");
        }
    }

    /// <summary>A <c>why</c> is a reason's code: each reason it may name has its entry, saying exactly the reason's own values.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("zh")]
    public void Every_reason_a_why_may_name_has_an_entry_that_says_its_own_values(string language)
    {
        var catalogue = Catalogue(language);
        foreach (var reasons in new[] { NoteCodes.Continue, NoteCodes.Cooling, NoteCodes.Opening, NoteCodes.Resumed, NoteCodes.Unreached, NoteCodes.Handed, NoteCodes.Helper })
        {
            foreach (var reason in reasons.All)
            {
                var said = Placeholders(Entry(catalogue, reason.Key, language));
                Assert.True(said.SetEquals(reason.Values), $"{language}: `{reason.Key}` says {{{string.Join(", ", said)}}}, and `{reason.Code}` carries {{{string.Join(", ", reason.Values)}}}.");
            }
        }
    }

    /// <summary>
    /// Every reason no helper answered (<see cref="HelperWhy"/>, the person's no among them since HELPERNO1) is one the helper's
    /// <c>why</c> declares, once, so the page words each one.
    /// </summary>
    [Fact]
    public void Every_helper_reason_is_declared_once()
    {
        var reasons = typeof(HelperWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Contains(HelperWhy.Declined, reasons);
        Assert.Equal(reasons, NoteCodes.Helper.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));
    }

    /// <summary>Every reason the driver can give (MSG1f's <see cref="ContinueWhy"/>) is one a <c>why</c> declares, and only those.</summary>
    [Fact]
    public void Every_continue_reason_is_declared_once()
    {
        var reasons = typeof(ContinueWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(reasons, NoteCodes.Continue.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));
        Assert.Equal(NoteCodes.Cooling.All.Count, NoteCodes.Cooling.All.DistinctBy(reason => reason.Code).Count());
    }

    /// <summary>
    /// UX7d-1: every step the walk can name (<see cref="OpeningWhy"/>) is a reason the opening's <c>why</c> declares, once, and
    /// the codes that name it carry no value a reason does, so a reason's values never shadow the line's.
    /// </summary>
    [Fact]
    public void Every_opening_reason_is_declared_once_and_none_shadows_its_line_s_values()
    {
        var reasons = typeof(OpeningWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(reasons, NoteCodes.Opening.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));
        foreach (var code in NoteCodes.All.Where(code => code.Why == NoteCodes.Opening))
        {
            Assert.All(code.Values.Where(value => value != "why"), value => Assert.False(NoteCodes.Opening.Carries(value), $"`{code.Code}`'s `{value}` is a reason's too"));
        }
    }

    /// <summary>CONVNOTE2a: every way a resumed line says whose words (<see cref="ResumedWhy"/>) is a reason, once, and none shadows the line's values.</summary>
    [Fact]
    public void Every_resumed_reason_is_declared_once_and_none_shadows_its_line_s_values()
    {
        var reasons = typeof(ResumedWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(reasons, NoteCodes.Resumed.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));
        foreach (var code in NoteCodes.All.Where(code => code.Why == NoteCodes.Resumed))
        {
            Assert.All(code.Values.Where(value => value != "why"), value => Assert.False(NoteCodes.Resumed.Carries(value), $"`{code.Code}`'s `{value}` is a reason's too"));
        }
    }

    [Fact]
    public void Every_code_is_declared_once_and_spelled_as_the_page_keys_it()
    {
        Assert.Equal(NoteCodes.All.Count, NoteCodes.All.Select(code => code.Code).Distinct(StringComparer.Ordinal).Count());
        foreach (var code in NoteCodes.All)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+(-[a-z]+)*$", code.Code);
            Assert.Equal(code.Values.Count, code.Values.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    /// The web's twin test parses this file, one declaration per line (<c>locales/note.test.ts</c>): every code is declared in
    /// the form it reads, so a code written another way is never one the page's test misses.
    /// </summary>
    [Fact]
    public void Every_code_is_declared_in_the_form_the_page_s_test_parses()
    {
        var source = File.ReadAllText(SourceFiles.Driver("NoteCodes.cs"));
        var parsed = Regex.Matches(source, @"new\(""(?<code>[a-z][a-z.-]*)"", \[(?<values>[^\]]*)\](?:, Key: ""(?<key>[^""]+)"")?\)")
            .Select(match => match.Groups["code"].Value)
            .ToList();

        Assert.Equal(NoteCodes.All.Select(code => code.Code).Order(StringComparer.Ordinal), parsed.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AGT3d (D125's AGT3c note): a limit's cooling line says whose account it was and, where the agent named one, the kind
    /// of limit, so a reader on another machine learns both, by codes of their own. <c>account.cooling</c> keeps what it
    /// declared, since notes already on machines carry it and the page words a line only with every value its entry says
    /// (D142 point 4); a limit whose reset named no window has its own line for the same reason.
    /// </summary>
    [Fact]
    public void A_cooling_line_carries_its_owner_and_the_window_it_named_and_the_old_code_keeps_its_values()
    {
        Assert.Equal(["until", "why"], NoteCodes.AccountCooling.Values);
        Assert.Equal(["until", "why", "owner", "window"], NoteCodes.AccountCoolingWindow.Values);
        Assert.Equal(["until", "why", "owner"], NoteCodes.AccountCoolingNoWindow.Values);
        Assert.All(
            [NoteCodes.AccountCooling, NoteCodes.AccountCoolingWindow, NoteCodes.AccountCoolingNoWindow],
            code => Assert.Same(NoteCodes.Cooling, code.Why));
        // Its owner says whose account, never which: the fault is the account's limit, and no strike (AccountsOwn) reads it.
        Assert.DoesNotContain(NoteCodes.AccountCoolingWindow, NoteCodes.AccountsOwn);
        Assert.DoesNotContain(NoteCodes.AccountCoolingNoWindow, NoteCodes.AccountsOwn);
    }

    /// <summary>
    /// XAGENT1g: the look held for a second opinion says so under a code of its own, never a refused landing's, now that the page
    /// words it; a note written before it keeps <c>landing.refused</c>.
    /// </summary>
    [Fact]
    public void A_look_held_for_a_second_opinion_is_noted_under_its_own_code()
    {
        var note = AutoLandingNotes.Of(AutoLandingCode.Opinion, new TreeLanding(false, "Waits for a second opinion.") { Refusal = AutoLandingCode.Opinion });

        Assert.Equal(["landing.opinion", null], NoteAssert.Codes(note.Parts));
        Assert.Contains(NoteCodes.LandingRefused, NoteCodes.All);
    }

    [Fact]
    public void A_part_carries_only_the_values_its_code_declares()
    {
        var part = NoteCodes.EndedTimeout.Part("timed out after 30 minutes and was killed.", ("minutes", 30));
        Assert.Equal(30, part.Value("minutes"));
        Assert.Throws<ArgumentException>(() => NoteCodes.EndedDone.Part("the quest reached done.", ("exit", 1)));
        // A null value is not written: the page then shows the part's text.
        Assert.Empty(NoteCodes.EndedTimeout.Part("timed out.", ("minutes", null)).Values);
    }

    [Fact]
    public void A_reason_brings_its_own_values_and_elsewhere_names_the_agent_it_ran_on()
    {
        var adapter = NoteCodes.WentNewSession.Part("x", NoteCodes.Reason(ContinueWhy.AdapterChanged("claude-code", "codex-acp")));
        Assert.Equal("adapter", adapter.Value("why"));
        Assert.Equal("claude-code", adapter.Value("from"));
        Assert.Equal("codex-acp", adapter.Value("to"));

        var elsewhere = NoteCodes.WentCannot.Part("x", NoteCodes.Reason(ContinueWhy.Of(ContinueWhy.Elsewhere), "codex-acp"));
        Assert.Equal("codex-acp", elsewhere.Value("agent"));
    }

    [Fact]
    public void Parts_read_back_as_written()
    {
        IReadOnlyList<NotePart> parts =
        [
            NoteCodes.IntakePublished.Part("published `#q1`, `#q2` onto ask `#a1`", ("quests", new[] { "q1", "q2" }), ("ask", "a1")),
            NoteCodes.EndedExit.Part("exit 3", ("exit", 3)),
            NotePart.Said("Which branch?", NoteBy.Agent),
        ];

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("noteParts");
            NotePart.Write(writer, parts);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        var read = NotePart.Read(document.RootElement)!;

        Assert.Equal(3, read.Count);
        Assert.Equal("intake.published", read[0].Code);
        Assert.Equal(["q1", "q2"], (IReadOnlyList<string>)read[0].Value("quests")!);
        Assert.Equal("a1", read[0].Value("ask"));
        Assert.Equal(3L, read[1].Value("exit"));
        Assert.Equal(("Which branch?", NoteBy.Agent), (read[2].Words, read[2].By));
        Assert.Null(NotePart.Read(JsonDocument.Parse("""{"note":"x"}""").RootElement));
    }

    /// <summary>Any serializer the modules hand a part to writes the record's shape, so the page reads it as the service answers it.</summary>
    [Fact]
    public void A_part_serializes_in_the_record_s_shape_wherever_it_goes()
    {
        var json = JsonSerializer.Serialize(new { Parts = new[] { NoteCodes.EndedTimeout.Part("t", ("minutes", 5)), NotePart.Said("w", NoteBy.Person) } });

        Assert.Equal("""{"Parts":[{"code":"ended.timeout","values":{"minutes":5},"text":"t"},{"words":"w","by":"person"}]}""", json);
    }

    [Fact]
    public void A_record_with_a_note_and_no_parts_is_carried_as_one_before_part()
    {
        var before = Noted.From("the quest reached done.", null);
        Assert.Equal("the quest reached done.", before.Note);
        Assert.Equal((NoteBy.Before, "the quest reached done."), (before.Parts.Single().By, before.Parts.Single().Words));

        Assert.Empty(Noted.From(null, null).Parts);
        var kept = Noted.From("x", [NotePart.Said("x", NoteBy.Agent)]);
        Assert.Equal(NoteBy.Agent, kept.Parts.Single().By);
    }

    [Fact]
    public void A_composed_note_is_its_lines_and_glue_and_each_coded_text_is_inside_it()
    {
        var noted = Noted.Of(NoteCodes.EndedParkedAsked, "It stopped with its quest still taken, to ask you:")
            .Then("\n\n", Noted.Said("Which branch?", NoteBy.Agent));

        Assert.Equal("It stopped with its quest still taken, to ask you:\n\nWhich branch?", noted.Note);
        Assert.Equal(["ended.parked-asked", null], noted.Parts.Select(part => part.Code));
        Assert.Empty(Noted.Said("", NoteBy.Person).Parts);

        // Where the English interleaves two lines, the second rides beside the first, its text already inside the note.
        var declined = Noted.Of(NoteCodes.EndedDeclined, "the session declined (exit 1); the reason is on the quest.")
            .Also(NoteCodes.EndedExit.Part("exit 1", ("exit", 1)));
        NoteAssert.Holds(declined);
        Assert.Equal(["ended.declined", "ended.exit"], declined.Parts.Select(part => part.Code));
    }
}

/// <summary>CONVNOTE2d1: words that did not reach an agent, or were not taken, by code; the English as it was written.</summary>
public sealed class WordsNotesTests
{
    [Fact]
    public void Unreached_words_say_their_count_and_why_by_code_with_the_english_unchanged()
    {
        var ended = WordsNotes.Unreached(2, "while it worked never reached it", UnreachedWhy.EndedFirst);
        Assert.Equal("— 2 message(s) the person sent while it worked never reached it: it ended first.", ended.Note);
        var part = Assert.Single(ended.Parts);
        Assert.Equal("words.unreached", part.Code);
        Assert.Equal(2, part.Value("count"));
        Assert.Equal(UnreachedWhy.EndedFirst, part.Value("why"));
        NoteAssert.Holds(ended);

        var unkept = WordsNotes.Unreached(1, "while it worked never reached it", UnreachedWhy.Unkept);
        Assert.Equal("— 1 message(s) the person sent while it worked never reached it: Daoris kept no id for its conversation.", unkept.Note);

        var session = WordsNotes.Unreached(3, "never reached the session", UnreachedWhy.EndedFirst);
        Assert.Equal("— 3 message(s) the person sent never reached the session: it ended first.", session.Note);
        NoteAssert.Holds(session);
    }

    [Fact]
    public void Words_refused_and_words_not_taken_carry_the_others_words_as_theirs()
    {
        var refused = WordsNotes.Refused("Internal error: busy");
        Assert.Equal("— the session would not take what the person added: Internal error: busy", refused.Note);
        Assert.Equal(["words.refused", null], NoteAssert.Codes(refused.Parts));
        Assert.Equal((NoteBy.Agent, "Internal error: busy"), (refused.Parts[1].By, refused.Parts[1].Words));
        NoteAssert.Holds(refused);

        var kept = WordsNotes.NotTaken("the service did not answer");
        Assert.Equal("— your words could not be taken off its record: the service did not answer", kept.Note);
        Assert.Equal(["words.not-taken", null], NoteAssert.Codes(kept.Parts));
        Assert.Equal(NoteBy.Program, kept.Parts[1].By);
        NoteAssert.Holds(kept);
    }

    /// <summary>CONVNOTE2d2: the chat runner's and the modules' sites, each English as written, the others' words as theirs.</summary>
    [Fact]
    public void The_chat_runner_s_and_the_modules_words_notes_code_their_lead_and_keep_the_english()
    {
        var refused = WordsNotes.Refused("busy", "conversation");
        Assert.Equal("— the conversation would not take what the person added: busy", refused.Note);
        Assert.Equal(["words.refused", null], NoteAssert.Codes(refused.Parts));
        NoteAssert.Holds(refused);

        var never = WordsNotes.NeverReached("pipe closed");
        Assert.Equal("— what the person added never reached it: pipe closed", never.Note);
        Assert.Equal(["words.not-reached", null], NoteAssert.Codes(never.Parts));
        Assert.Equal(NoteBy.Program, never.Parts[1].By);
        NoteAssert.Holds(never);

        var gone = WordsNotes.NotSent();
        Assert.Equal("the message could not be sent: the agent had gone.", gone.Note);
        Assert.Equal(["words.not-sent"], NoteAssert.Codes(gone.Parts));
        NoteAssert.Holds(gone);

        var start = WordsNotes.StartNotTaken("s-1", "no record");
        Assert.Equal("— the words this conversation started with could not be taken off session `s-1`: no record", start.Note);
        Assert.Equal(["words.start-not-taken", null], NoteAssert.Codes(start.Parts));
        Assert.Equal("s-1", start.Parts[0].Value("session"));
        Assert.Equal(NoteBy.Program, start.Parts[1].By);
        NoteAssert.Holds(start);

        var kept = WordsNotes.NotKept("the record ended");
        Assert.Equal("— what you said as it wound up was not kept for it to go on with: the record ended", kept.Note);
        Assert.Equal(["words.not-kept", null], NoteAssert.Codes(kept.Parts));
        NoteAssert.Holds(kept);
    }

    [Fact]
    public void Every_unreached_reason_is_declared_once_and_none_shadows_its_line_s_values()
    {
        var reasons = typeof(UnreachedWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(reasons, NoteCodes.Unreached.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));
        foreach (var code in NoteCodes.All.Where(code => code.Why == NoteCodes.Unreached))
        {
            Assert.All(code.Values.Where(value => value != "why"), value => Assert.False(NoteCodes.Unreached.Carries(value), $"`{code.Code}`'s `{value}` is a reason's too"));
        }
    }

    /// <summary>
    /// MIXAGENT1a-i: why a conversation was handed over is a family of its own, each reason declared once; the two lines carry facts
    /// only. `curve` (MIXAGENT1a-iii, D165) is a reason a record continues another by, and since SWITCH1c-i one the box takes.
    /// SESSCTX1b (D189 point 4, the session-context design §7): every continuation names its link, so `resume`, `carry-on` and
    /// `retry` are reasons a record continues another by, worded as every reason is, and no box takes them: the box's check stays on
    /// the five a hand-over is asked for.
    /// </summary>
    [Fact]
    public void A_conversation_handed_over_says_where_it_went_and_why_by_code_with_facts_only()
    {
        var reasons = typeof(HandOverWhy).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["agent", "carry-on", "curve", "limit", "near", "press", "resume", "retry"], reasons);
        Assert.Equal(reasons, NoteCodes.Handed.All.Select(reason => reason.Code).Order(StringComparer.Ordinal));
        Assert.Equal(["agent", "curve", "limit", "near", "press"], reasons.Where(HandOverWhy.Known));
        Assert.All(reasons, why => Assert.True(HandOverWhy.Continues(why), $"`{why}` is no reason a record continues another by"));
        Assert.All(reasons, why => Assert.NotEqual(why, HandOverWhy.Noted(why)));
        Assert.All(reasons, why => Assert.NotEqual(why, HandOverWhy.Briefed(why)));
        Assert.False(HandOverWhy.Known("moved-on"));
        Assert.False(HandOverWhy.Continues("moved-on"));

        var went = HandOverNotes.HandedOver("codex-acp", "ab12cd34");
        Assert.Equal("The conversation went on with codex-acp, in conversation ab12cd34.", went.Note);
        Assert.Equal(["chat.handed-over"], NoteAssert.Codes(went.Parts));
        Assert.Equal(("codex-acp", "ab12cd34"), (went.Parts[0].Value("agent"), went.Parts[0].Value("session")));
        NoteAssert.Holds(went);

        var took = HandOverNotes.TakenOver("claude-code", "ab12cd34", HandOverWhy.Limit);
        Assert.Equal("The conversation went on from claude-code conversation ab12cd34, handed over because its account reached its limit.", took.Note);
        Assert.Equal(("claude-code", "ab12cd34", "limit"), (took.Parts[0].Value("agent"), took.Parts[0].Value("session"), took.Parts[0].Value("why")));
        NoteAssert.Holds(took);

        var moved = HandOverNotes.TakenOver("claude-code", "ab12cd34", HandOverWhy.Curve);
        Assert.Equal("The conversation went on from claude-code conversation ab12cd34, handed over because its account was over its usage curve.", moved.Note);
        NoteAssert.Holds(moved);
    }

    /// <summary>
    /// SWITCH1c-i (the switch design §4): a conversation moved by the usage curve says so on both sides, naming the accounts, as a
    /// start's `opening.*` lines do, since a conversation's record is this machine's: the window over its curve, the use there as a
    /// whole percent and the curve's value there now, in whole points, as the walk's `over-curve` says them.
    /// </summary>
    [Fact]
    public void A_conversation_moved_by_the_curve_says_where_from_where_to_and_how_far_over()
    {
        var move = new SwitchChoice.MoveTo("account-3", "weekly", 0.615, 44.6);

        var went = HandOverNotes.Moved("account-1", move);
        Assert.Equal(
            "Moved to `account-3` at the end of its turn: `account-1` had used 62% of its weekly limit, over its curve's 45%; `account-3` has room.",
            went.Note);
        Assert.Equal(["chat.moved"], NoteAssert.Codes(went.Parts));
        Assert.Equal(
            ("account-3", "account-1", "weekly", 62, 45),
            (went.Parts[0].Value("to"), went.Parts[0].Value("from"), went.Parts[0].Value("window"), went.Parts[0].Value("used"), went.Parts[0].Value("curve")));
        NoteAssert.Holds(went);

        var came = HandOverNotes.MovedFrom("account-1", move);
        Assert.Equal("Continues from `account-1`, which had used 62% of its weekly limit, over its curve's 45%.", came.Note);
        Assert.Equal(["chat.moved-from"], NoteAssert.Codes(came.Parts));
        Assert.Equal(
            ("account-1", "weekly", 62, 45),
            (came.Parts[0].Value("from"), came.Parts[0].Value("window"), came.Parts[0].Value("used"), came.Parts[0].Value("curve")));
        NoteAssert.Holds(came);
    }

    /// <summary>MIXAGENT1a-ii: a quest's session handed over ends with a line of its own, the agent it went to, and no account or path.</summary>
    [Fact]
    public void A_quest_s_session_handed_over_says_where_it_went_by_code_with_facts_only()
    {
        var went = HandOverNotes.QuestHandedOver("codex-acp");

        Assert.Equal("Handed over to codex-acp: its quest goes on with it, in the same tree.", went.Note);
        Assert.Equal(["ended.handed-over"], NoteAssert.Codes(went.Parts));
        Assert.Equal("codex-acp", went.Parts[0].Value("agent"));
        NoteAssert.Holds(went);
    }
}

/// <summary>CONVNOTE2f: a session's own background work (BGWAIT1), by code; the English as it was written.</summary>
public sealed class BackgroundNotesTests
{
    [Fact]
    public void A_wait_names_its_work_as_a_list_and_keeps_the_english()
    {
        var waits = BackgroundNotes.Waits(["Run the gates", "Build"]);
        Assert.Equal("— its turn ended while its own background work runs: `Run the gates`, `Build`. The session waits for that work, and goes on when it ends.", waits.Note);
        var part = Assert.Single(waits.Parts);
        Assert.Equal("background.waits", part.Code);
        Assert.Equal(["Run the gates", "Build"], Assert.IsAssignableFrom<IReadOnlyList<string>>(part.Value("works")));
        NoteAssert.Holds(waits);
    }

    [Fact]
    public void Unread_leaves_the_programs_words_as_theirs()
    {
        var unread = BackgroundNotes.Unread("the agent said no");
        Assert.Equal("— its turn ended while its own background work runs, and whether its quest waits on that work could not be read: the agent said no", unread.Note);
        Assert.Equal(["background.unread", null], NoteAssert.Codes(unread.Parts));
        Assert.Equal((NoteBy.Program, "the agent said no"), (unread.Parts[1].By, unread.Parts[1].Words));
        NoteAssert.Holds(unread);
    }

    [Fact]
    public void An_end_codes_each_work_and_its_state_and_the_going_on_and_keeps_the_english()
    {
        var ended = BackgroundNotes.Ended([("Run the gates", "completed"), ("Build", "failed")]);
        Assert.Equal("— its background work ended: `Run the gates` (completed); `Build` (failed). The session goes on.", ended.Note);
        Assert.Equal(["background.work-ended", "background.work-ended", "background.goes-on"], NoteAssert.Codes(ended.Parts));
        Assert.Equal(("Build", "failed"), (ended.Parts[1].Value("work"), ended.Parts[1].Value("state")));
        NoteAssert.Holds(ended);
        Assert.Equal("`Run the gates` (completed); `Build` (failed)", BackgroundNotes.EndedSaid([("Run the gates", "completed"), ("Build", "failed")]));
    }

    [Fact]
    public void The_known_states_are_declared_once_and_the_record_keeps_any_state_as_sent()
    {
        var states = typeof(BackgroundState).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["completed", "ended", "failed", "stopped"], states);
        Assert.Equal(states, BackgroundState.Known.Order(StringComparer.Ordinal));
        var odd = BackgroundNotes.Ended([("Build", "paused")]);
        Assert.Equal("paused", odd.Parts[0].Value("state"));
    }
}

/// <summary>CONVNOTE2h: a review set-up's record and a second opinion's answers, by code; the English as it was written.</summary>
public sealed class ReviewAndOpinionNotesTests
{
    [Fact]
    public void A_set_up_not_posted_codes_each_reason_and_leaves_the_programs_words_as_theirs()
    {
        var noRule = ReviewNotes.NoRule("browser", "web");
        Assert.Equal(
            "its set-up was not posted: no review rule here names `browser` for `web` any more, so its kind is not known. Declare it, and its next end posts it.",
            noRule.Note);
        Assert.Equal(["review.no-rule"], NoteAssert.Codes(noRule.Parts));
        Assert.Equal(("browser", "web"), (noRule.Parts[0].Value("named"), noRule.Parts[0].Value("to")));
        NoteAssert.Holds(noRule);

        var none = ReviewNotes.NoCommit();
        Assert.Equal("its set-up was not posted: its tree's commit could not be read.", none.Note);
        Assert.Equal(["review.no-commit"], NoteAssert.Codes(none.Parts));

        var refused = ReviewNotes.NotPosted("the service said no at D:/x", next: false);
        Assert.Equal("its set-up was not posted: the service said no at D:/x", refused.Note);
        Assert.Equal(["review.not-posted", null], NoteAssert.Codes(refused.Parts));
        Assert.Equal((NoteBy.Program, "the service said no at D:/x"), (refused.Parts[1].By, refused.Parts[1].Words));
        NoteAssert.Holds(refused);

        var failed = ReviewNotes.NotPosted("connection lost", next: true);
        Assert.Equal("its set-up was not posted: connection lost. Its next end posts it.", failed.Note);
        Assert.Equal(["review.not-posted", null, "review.posts-next"], NoteAssert.Codes(failed.Parts));
        NoteAssert.Holds(failed);
    }

    [Fact]
    public void A_set_up_posted_codes_its_commit_and_leaves_the_services_and_the_served_words_as_theirs()
    {
        var posted = ReviewNotes.Posted("0123456789abcdef", "browser", "Kept as set-up 1.", "served `D:/x`.");
        Assert.Equal("Daoris read `01234567` from its tree and posted what it showed in `browser`: Kept as set-up 1. Served `D:/x`.", posted.Note);
        Assert.Equal(["review.posted", null, null], NoteAssert.Codes(posted.Parts));
        Assert.Equal(("01234567", "browser"), (posted.Parts[0].Value("commit"), posted.Parts[0].Value("named")));
        Assert.Equal([NoteBy.Program, NoteBy.Program], [posted.Parts[1].By, posted.Parts[2].By]);
        NoteAssert.Holds(posted);

        Assert.Equal("Daoris read `01234567` from its tree and posted what it showed in `browser`: Kept.", ReviewNotes.Posted("0123456789abcdef", "browser", "Kept.", null).Note);
    }

    [Fact]
    public void A_set_up_posted_at_a_commit_shorter_than_eight_names_it_whole()
    {
        // DEDUP7c: the cut was commit[..8], which threw on a short id.
        var posted = ReviewNotes.Posted("abc12", "browser", "Kept.", null);
        Assert.Equal("Daoris read `abc12` from its tree and posted what it showed in `browser`: Kept.", posted.Note);
        Assert.Equal("abc12", posted.Parts[0].Value("commit"));
    }

    private static OpinionView View() =>
        new("op1", "occ", "first", "w", "s1", "repo", "b", "t", "codex-acp", "Label", "given") { Product = "Codex", Maker = "OpenAI" };

    private static OpinionReading Reading(params (string Weight, string Counts, string? Said, string? Fix, string? Why)[] rows) =>
        new("s1", null, DateTimeOffset.UnixEpoch,
            [.. rows.Select((row, each) => new OpinionAnswerRead(each + 1, row.Weight, row.Counts) { Said = row.Said, Fix = row.Fix, Why = row.Why })]);

    [Fact]
    public void Answers_code_the_lead_and_each_count_above_nought_in_the_englishs_order()
    {
        var reading = Reading(
            ("must", "fixed", "fixed", "a".PadRight(40, 'a'), null),
            ("must", "unresolved", "fixed", null, OpinionAnswerWhy.FixUnknown),
            ("should", "rejected", "rejected", null, null),
            ("must", "unresolved", "unresolved", null, null),
            ("note", "unresolved", null, null, OpinionAnswerWhy.NotAnswered));
        var noted = OpinionAnswers.Told(View(), reading);
        Assert.Equal(OpinionAnswers.Line(View(), reading), noted.Note);
        Assert.Equal(
            ["opinion.answers", "opinion.fixed", "opinion.unproven", "opinion.rejected", "opinion.unresolved", "opinion.unanswered", "opinion.disputed"],
            NoteAssert.Codes(noted.Parts));
        Assert.Equal(("Codex (OpenAI)", 5), (noted.Parts[0].Value("who"), noted.Parts[0].Value("count")));
        Assert.Equal(1, noted.Parts[1].Value("count"));
        Assert.Equal(2, noted.Parts[6].Value("count"));
        NoteAssert.Holds(noted);
    }

    /// <summary>XAGENT1g4a: the reviewer's own conclusion, by code; the English as it was written.</summary>
    [Fact]
    public void A_reviewers_conclusion_codes_each_ending_and_leaves_the_failure_as_the_programs()
    {
        var failed = Daoris.Driver.DriverOpinions.Read("op1", 0, "no route to D:/x");
        Assert.Equal("failed", failed.State);
        Assert.Equal("the reviewer's turn failed as it read the work for second opinion `op1`: no route to D:/x", failed.Note);
        var failedNote = failed.AsNoted();
        Assert.Equal(["opinion.reviewer-failed", null], NoteAssert.Codes(failedNote.Parts));
        Assert.Equal("op1", failedNote.Parts[0].Value("opinion"));
        Assert.Equal((NoteBy.Program, "no route to D:/x"), (failedNote.Parts[1].By, failedNote.Parts[1].Words));
        NoteAssert.Holds(failedNote);

        var read = Daoris.Driver.DriverOpinions.Read("op1", 0, null);
        Assert.Equal(("completed", "it read the work and ended; what it said is second opinion `op1`'s."), (read.State, read.Note));
        Assert.Equal(["opinion.reviewer-read"], NoteAssert.Codes(read.AsNoted().Parts));
        NoteAssert.Holds(read.AsNoted());

        var exited = Daoris.Driver.DriverOpinions.Read("op1", 3, null);
        Assert.Equal(
            ("failed", "it ended with exit 3 as it read the work; what it said, if anything, is second opinion `op1`'s."),
            (exited.State, exited.Note));
        Assert.Equal(["opinion.reviewer-exited"], NoteAssert.Codes(exited.AsNoted().Parts));
        Assert.Equal(("op1", 3), (exited.AsNoted().Parts[0].Value("opinion"), exited.AsNoted().Parts[0].Value("exit")));
        NoteAssert.Holds(exited.AsNoted());
    }

    [Fact]
    public void Answers_with_one_kind_code_that_kind_alone()
    {
        var noted = OpinionAnswers.Told(View(), Reading(("should", "rejected", "rejected", null, null)));
        Assert.Equal(["opinion.answers", "opinion.rejected"], NoteAssert.Codes(noted.Parts));
        Assert.Equal("— the answers to Codex (OpenAI)'s 1 finding, as its turn ended: 1 rejected with its evidence.", noted.Note);
        NoteAssert.Holds(noted);
    }
}

/// <summary>CONVNOTE2e: a conversation's door that failed, by code; the English as it was written.</summary>
public sealed class DoorNotesTests
{
    [Fact]
    public void A_door_or_turn_that_failed_codes_its_lead_and_leaves_the_programs_words_as_theirs()
    {
        var open = DoorNotes.NotOpen("could not read D:/x/session.json");
        Assert.Equal("the ACP session could not open: could not read D:/x/session.json", open.Note);
        Assert.Equal(["door.not-open", null], NoteAssert.Codes(open.Parts));
        Assert.Equal((NoteBy.Program, "could not read D:/x/session.json"), (open.Parts[1].By, open.Parts[1].Words));
        NoteAssert.Holds(open);

        var turn = DoorNotes.TurnNotTaken("the ACP agent refused the call: Authentication required");
        Assert.Equal("the turn could not be taken: the ACP agent refused the call: Authentication required", turn.Note);
        Assert.Equal(["door.turn-not-taken", null], NoteAssert.Codes(turn.Parts));
        Assert.Equal(NoteBy.Program, turn.Parts[1].By);
        NoteAssert.Holds(turn);

        var failed = DoorNotes.Failed("the stream ended");
        Assert.Equal("— the ACP session failed: the stream ended", failed.Note);
        Assert.Equal(["door.failed", null], NoteAssert.Codes(failed.Parts));
        Assert.Equal(NoteBy.Program, failed.Parts[1].By);
        NoteAssert.Holds(failed);

        // An exception with no message still writes the English it always did, and no empty words part.
        Assert.Equal("the turn could not be taken: ", DoorNotes.TurnNotTaken("").Note);
        Assert.Equal(["door.turn-not-taken"], NoteAssert.Codes(DoorNotes.TurnNotTaken("").Parts));
    }

    /// <summary>
    /// The refusal is Daoris's own sentence from its <see cref="ContinueReason"/>: one code whose <c>why</c> is the reason by the
    /// <c>Continue</c> family, the page's <c>work.say.why.*</c>, then the agent's words where it refused in any.
    /// </summary>
    [Theory]
    [InlineData(ContinueWhy.Gone, null)]
    [InlineData(ContinueWhy.Offered, null)]
    [InlineData(ContinueWhy.Refused, "Internal error: thread D:/x busy")]
    [InlineData(ContinueWhy.Elsewhere, "thread held by another client")]
    public void A_refused_resume_says_its_reason_by_the_continue_family_with_the_english_as_its_refusal(string code, string? words)
    {
        var refused = new AcpResumeRefused(ContinueWhy.Of(code), words);

        var chat = DoorNotes.ResumeRefused(refused, ranOn: "codex-acp");
        Assert.Equal($"— {refused.Message}.", chat.Note);
        string?[] said = words is null ? ["door.resume-refused"] : ["door.resume-refused", null];
        Assert.Equal(said, NoteAssert.Codes(chat.Parts));
        Assert.Equal(ContinueWhy.Of(code).Sentence, chat.Parts[0].Text);
        Assert.Equal(code, chat.Parts[0].Value("why"));
        // Elsewhere's line names the agent it ran on, as every line of the family does (NoteCodes.Reason).
        Assert.Equal(code == ContinueWhy.Elsewhere ? "codex-acp" : null, chat.Parts[0].Value("agent"));
        if (words is not null) Assert.Equal((NoteBy.Agent, words), (chat.Parts[1].By, chat.Parts[1].Words));
        NoteAssert.Holds(chat);

        var answer = DoorNotes.AnswerCarriedOn(refused, ranOn: "codex-acp");
        Assert.Equal($"— {refused.Message}; your answer is carried on in a new session.", answer.Note);
        string?[] carried = words is null
            ? ["door.resume-refused", "door.answer-carried-on"]
            : ["door.resume-refused", null, "door.answer-carried-on"];
        Assert.Equal(carried, NoteAssert.Codes(answer.Parts));
        NoteAssert.Holds(answer);
    }
}

/// <summary>AGT3c: a words part a coded line will say as facts leaves the note, its space with it, and nothing else does.</summary>
public sealed class NoteUnsaidTests
{
    [Fact]
    public void Someone_s_words_leave_the_note_with_the_space_before_them()
    {
        var noted = Noted.Of(NoteCodes.EndedTurnFailedTaken, "the agent's turn failed with the quest still taken:")
            .Then(" ", Noted.Said("You've hit your limit · resets 4pm (Europe/London)", NoteBy.Agent))
            .Then(" ", Noted.Of(NoteCodes.EndedStopped, "the person stopped it."));

        var unsaid = noted.Unsaid("You've hit your limit · resets 4pm (Europe/London)", NoteBy.Agent);

        Assert.Equal("the agent's turn failed with the quest still taken: the person stopped it.", unsaid.Note);
        Assert.Equal(["ended.turn-failed-taken", "ended.stopped"], NoteAssert.Codes(unsaid.Parts));
        NoteAssert.Holds(unsaid);
    }

    [Fact]
    public void Words_no_part_holds_or_held_by_another_leave_the_note_as_it_was()
    {
        var noted = Noted.Of(NoteCodes.EndedTurnFailedOpen, "the agent's turn failed before it took its quest:")
            .Then(" ", Noted.Said("refused", NoteBy.Agent));

        Assert.Same(noted, noted.Unsaid("overloaded", NoteBy.Agent));
        Assert.Same(noted, noted.Unsaid("refused", NoteBy.Person));
        Assert.Same(noted, noted.Unsaid("", NoteBy.Agent));

        var before = new SessionConclusion("failed", "refused.");
        Assert.Same(before, before.Unsaid("refused."));
    }
}

/// <summary>What every note site's test asks of what it wrote (LANG1a): each coded part's text inside the note it built.</summary>
internal static class NoteAssert
{
    public static void Holds(Noted noted) => Holds(noted.Note, noted.Parts);

    public static void Holds(string? note, IReadOnlyList<NotePart>? parts)
    {
        Assert.NotNull(parts);
        Assert.NotEmpty(parts);
        foreach (var part in parts)
        {
            var text = part.Code is null ? part.Words : part.Text;
            Assert.False(string.IsNullOrEmpty(text), $"`{part.Code ?? part.By}` has no text");
            Assert.True(note!.Contains(text, StringComparison.Ordinal), $"`{part.Code ?? part.By}`'s text \"{text}\" is not inside the note \"{note}\"");
            if (part.Code is not null) Assert.Contains(NoteCodes.All, code => code.Code == part.Code);
        }
    }

    /// <summary>The codes of the parts, in order; null for a words part.</summary>
    public static IReadOnlyList<string?> Codes(IReadOnlyList<NotePart>? parts) => [.. (parts ?? []).Select(part => part.Code)];
}
