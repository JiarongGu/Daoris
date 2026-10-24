using System.Text.RegularExpressions;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// SUPERVISOR1a-i (D208; the supervisor design §3.4, §4.5, §6.1, §6.3): the supervisor's word on the record, the service's half. Its
/// release is one conditional say whose text the service composes from the choice, whose key is the release's receipt and whose every
/// check is made in the store's one step; it writes no answered line and is never the person's. Every door that keeps the owner's
/// words takes a waiting supervisor's word back in the same step; a move to working names the supervisor's words it admits, exactly;
/// and a park the driver could not hand is restored with its other words.
/// </summary>
public sealed class SupervisorWordTests : GoAheadFixture
{
    private const string Question = "q-1";
    private const string Decision = "d-1";
    private const string Receipt = "receipt-1";

    private string Home => Path.Combine(_root, "home");

    /// <summary>The ledger as a desktop's host composes it: with the home its parks' questions are filed under.</summary>
    private SessionLedger Ledger => new(_quests, _sessions, null, _asks, Home);

    /// <summary>A session on the ask's quest, parked on a recorded question offering two answers.</summary>
    private async Task<Session> ParkedOnAQuestion(string tree = "first", string question = Question)
    {
        var (_, quest) = await Asked($"{Sentence} for the {tree} report");
        var session = await Working(quest, tree);
        await _ledger.AdvanceAsync(session.Id, "awaiting-person", "Which report?", null, null, Now);
        File(session.Id, question);
        return session;
    }

    private void File(string session, string question, string? owner = null)
    {
        var path = QuestionBox.PathOf(Home, session);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var mark = owner is null ? "null" : $"\"{owner}\"";
        System.IO.File.WriteAllText(path, $$"""
            { "id": "{{question}}", "session": "{{session}}", "question": "Which report?",
              "choices": [ { "label": "The monthly one", "does": "I push to origin now." }, { "label": "The weekly one", "does": "I write it." } ],
              "recommend": 1, "by": "now", "owner": {{mark}} }
            """);
    }

    private Task<SessionSayOutcome> Release(
        string session, string choice = "The monthly one", string key = Receipt, string decision = Decision, string question = Question,
        string? text = null, SessionLedger? ledger = null) =>
        (ledger ?? Ledger).SayAsync(session, text, null, Now.AddMinutes(5), from: SaidFrom.Supervisor, key: key,
            decision: decision, question: question, choice: choice);

    private async Task<Session> Record(string id) => (await _sessions.FindAsync(id))!;

    /// <summary>
    /// The release keeps one word, its text the service's paragraph for the choice, marked the supervisor's with its decision, question
    /// and choice; never the person's, never an answer kept on the ask, and no answered line on the note.
    /// </summary>
    [Fact]
    public async Task The_release_keeps_the_composed_word_as_the_supervisors_with_no_answered_line()
    {
        var parked = await ParkedOnAQuestion();

        var kept = await Release(parked.Id);

        Assert.Equal(SessionSayRefusal.None, kept.Refusal);
        var word = Assert.Single((await Record(parked.Id)).Said);
        Assert.Equal(
            (SaidFrom.Supervisor, Decision, Question, "The monthly one", Receipt, SupervisorWord.Handed("The monthly one")),
            (word.From, word.Decision, word.Question, word.Choice, word.Key, word.Text));
        Assert.Equal(word.Id, kept.Word!.Id);
        Assert.False(word.Persons);
        Assert.True(word.Supervisors);
        Assert.False(kept.Answers);
        Assert.Equal("Which report?", (await Record(parked.Id)).Note);
        Assert.Contains("supervisor's answer", kept.Message);
    }

    /// <summary>
    /// A lost response is recovered under the receipt with the word already kept, and nothing kept twice; the same key with another
    /// choice is refused <c>key-mismatch</c>; an identical canonical word posted first under another key is refused <c>decision-taken</c>.
    /// </summary>
    [Fact]
    public async Task A_retry_is_answered_with_its_word_a_different_one_is_refused_and_a_word_first_under_another_key_is_never_the_releases()
    {
        var parked = await ParkedOnAQuestion();
        var first = await Release(parked.Id);

        var again = await Release(parked.Id);
        Assert.Equal((SessionSayRefusal.None, true, first.Word!.Id), (again.Refusal, again.Again, again.Word!.Id));
        Assert.Single((await Record(parked.Id)).Said);

        Assert.Equal(SessionSayRefusal.KeyMismatch, (await Release(parked.Id, choice: "The weekly one")).Refusal);
        Assert.Equal(SessionSayRefusal.KeyMismatch, (await Release(parked.Id, question: "q-2")).Refusal);

        var other = await ParkedOnAQuestion("second");
        Assert.Equal(SessionSayRefusal.None, (await Release(other.Id, key: "posted-first")).Refusal);
        Assert.Equal(SessionSayRefusal.DecisionTaken, (await Release(other.Id)).Refusal);
        Assert.Single((await Record(other.Id)).Said);
    }

    /// <summary>The release's form: no words of its own, and its decision, question, choice and key, which only it names.</summary>
    [Fact]
    public async Task The_release_takes_no_text_and_only_the_supervisor_names_a_decision()
    {
        var parked = await ParkedOnAQuestion();

        Assert.Equal(SessionSayRefusal.SupervisorText, (await Release(parked.Id, text: "Pick the monthly one and push.")).Refusal);
        Assert.Equal(
            SessionSayRefusal.From,
            (await Ledger.SayAsync(parked.Id, null, null, Now, from: SaidFrom.Supervisor, key: Receipt, question: Question, choice: "x")).Refusal);
        Assert.Equal(
            SessionSayRefusal.From,
            (await Ledger.SayAsync(parked.Id, null, null, Now, from: SaidFrom.Supervisor, decision: Decision, question: Question, choice: "x")).Refusal);
        Assert.Equal(SessionSayRefusal.From, (await Ledger.SayAsync(parked.Id, "mine", null, Now, decision: Decision)).Refusal);
        Assert.Equal(SessionSayRefusal.NoHome, (await Release(parked.Id, ledger: _ledger)).Refusal);
        Assert.Empty((await Record(parked.Id)).Said);
    }

    /// <summary>Each of §3.1's refusals marked 1a-i, made inside the say's step, keeping nothing.</summary>
    [Fact]
    public async Task The_release_is_refused_where_the_park_question_choice_owner_or_go_aheads_say_so()
    {
        var parked = await ParkedOnAQuestion();
        Assert.Equal(SessionSayRefusal.QuestionChanged, (await Release(parked.Id, question: "q-old")).Refusal);
        Assert.Equal(SessionSayRefusal.NotOfferedChoice, (await Release(parked.Id, choice: "the monthly one")).Refusal);
        Assert.Equal(SessionSayRefusal.NotOfferedChoice, (await Release(parked.Id, choice: "The monthly one ")).Refusal);

        await _ledger.AnswerAsync(parked.Id, "the weekly one, please", Now);
        Assert.Equal(SessionSayRefusal.OwnerAnswered, (await Release(parked.Id)).Refusal);

        var asking = await ParkedOnAQuestion("second");
        Assert.Equal(GoAheadRefusal.None, (await _ledger.AskGoAheadAsync(asking.Id, "write", "production", "the report", "It needs it.", Now)).Refusal);
        Assert.Equal(SessionSayRefusal.GoAheadOpen, (await Release(asking.Id)).Refusal);

        var (_, quest) = await Asked($"{Sentence} for the third report");
        var working = await Working(quest, "third");
        File(working.Id, Question);
        Assert.Equal(SessionSayRefusal.NotParked, (await Release(working.Id)).Refusal);

        var unasked = await _quests.PublishAsync("Asker", "dashboards", "Do it", "Why.", Now);
        var onNoAsk = await Working(unasked, "fourth");
        await _ledger.AdvanceAsync(onNoAsk.Id, "awaiting-person", "Which?", null, null, Now);
        File(onNoAsk.Id, Question);
        Assert.Equal(SessionSayRefusal.NoQuest, (await Release(onNoAsk.Id)).Refusal);

        var unread = new SessionLedger(_quests, _sessions, null, asks: null, Home);
        var third = await ParkedOnAQuestion("fifth");
        Assert.Equal(SessionSayRefusal.GoAheadUnread, (await Release(third.Id, ledger: unread)).Refusal);

        foreach (var id in new[] { asking.Id, working.Id, onNoAsk.Id, third.Id }) Assert.Empty((await Record(id)).Said);
    }

    /// <summary>
    /// SUPERVISOR1b (D208; the supervisor design §3.1, §4.1): the question's <c>owner</c> mark is checked again at the release, from the
    /// file the service reads inside the say's step, so a question marked the owner's after a decision was admitted is never answered
    /// by the supervisor: refused <c>owner-question</c>, keeping nothing, its wire word the route's.
    /// </summary>
    [Fact]
    public async Task The_release_is_refused_where_the_question_on_file_is_marked_the_owners()
    {
        var parked = await ParkedOnAQuestion();
        File(parked.Id, Question, owner: "sign-in");

        Assert.Equal(SessionSayRefusal.OwnerQuestion, (await Release(parked.Id)).Refusal);
        Assert.Empty((await Record(parked.Id)).Said);

        // A mark this build does not know is still the agent's word that the question is the owner's: the safe side.
        File(parked.Id, Question, owner: "something-later");
        Assert.Equal(SessionSayRefusal.OwnerQuestion, (await Release(parked.Id)).Refusal);

        File(parked.Id, Question);
        Assert.Equal(SessionSayRefusal.None, (await Release(parked.Id)).Refusal);
    }

    /// <summary>
    /// §6.3: the owner's words win at every door that keeps them, in one step. The say, the answer with words and blank, and a reword
    /// each take a waiting supervisor's word back as they keep the owner's, so no read sees both; another agent's word takes none.
    /// </summary>
    [Fact]
    public async Task The_owners_word_at_every_door_takes_the_supervisors_word_back_in_the_same_step()
    {
        async Task<Session> Released(string tree)
        {
            var parked = await ParkedOnAQuestion(tree);
            Assert.Equal(SessionSayRefusal.None, (await Release(parked.Id, key: "r-" + tree, decision: "d-" + tree)).Refusal);
            return parked;
        }

        var said = await Released("say");
        await Ledger.SayAsync(said.Id, "the weekly one", null, Now);
        Assert.Equal(["the weekly one"], (await Record(said.Id)).Said.Select(word => word.Text));

        var answered = await Released("answer");
        await Ledger.AnswerAsync(answered.Id, "the weekly one", Now);
        Assert.Equal(["the weekly one"], (await Record(answered.Id)).Said.Select(word => word.Text));

        var blank = await Released("blank");
        await Ledger.AnswerAsync(blank.Id, "  ", Now);
        Assert.Equal(["carry on."], (await Record(blank.Id)).Said.Select(word => word.Text));

        var reworded = await Released("reword");
        var daoris = await Ledger.SayAsync(reworded.Id, "Daoris carried it on.", null, Now, from: SaidFrom.Daoris);
        Assert.Equal(2, (await Record(reworded.Id)).Said.Count);
        await Ledger.TakeBackAsync(reworded.Id, daoris.Word!.Id, "my own words", Now);
        Assert.Equal(["my own words"], (await Record(reworded.Id)).Said.Select(word => word.Text));
    }

    /// <summary>
    /// §6.3's sweep: every <c>KeepSaidAsync</c> call in all of <c>Daoris.Service.Core</c>, found by a search of its source rather than a
    /// hand list, keeps a person's word only inside <c>KeepOwnersWordAsync</c>. The stated exemptions keep no owner's word: the store
    /// itself, the supervisor's own release, and the helper, partner and opinion desks. A keeper added later fails here until it is
    /// given its case.
    /// </summary>
    [Fact]
    public void Every_keeping_of_a_persons_word_in_the_service_goes_through_the_one_owner_word_operation()
    {
        var core = Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core");
        HashSet<string> covered = ["SessionLedger.KeepOwnersWordAsync"];
        HashSet<string> exempt = ["Sessions.KeepSaidAsync", "SessionLedger.SuperviseAsync", "HelperDesk.*", "PartnerDesk.*", "OpinionDesk.*"];
        var member = new Regex(@"^\s*(?:public|private|internal|protected)\b[^=;]*?\b(\w+)\s*(?:<[^>]*>)?\s*\(");

        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var lines = System.IO.File.ReadAllLines(file);
            var type = Path.GetFileNameWithoutExtension(file);
            for (var at = 0; at < lines.Length; at++)
            {
                if (!lines[at].Contains("KeepSaidAsync(", StringComparison.Ordinal) || lines[at].TrimStart().StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                var within = Enumerable.Range(0, at + 1).Reverse().Select(back => member.Match(lines[back])).First(match => match.Success);
                found.Add($"{type}.{within.Groups[1].Value}");
            }
        }

        Assert.Contains("SessionLedger.KeepOwnersWordAsync", found);
        Assert.All(found, keeper => Assert.True(
            covered.Contains(keeper) || exempt.Contains(keeper) || exempt.Contains(keeper[..keeper.IndexOf('.')] + ".*"),
            $"`{keeper}` keeps a word outside KeepOwnersWordAsync: give it the owner's supersession, or state why it keeps none of theirs."));
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !System.IO.File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>
    /// §6.1, §6.3: a move to working carries the supervisor's words it admits, exactly. Absent reads as none, so a mover that names none
    /// is refused while one waits; one beside a word Daoris cannot attribute is refused; naming one needs the park.
    /// </summary>
    [Fact]
    public async Task A_move_to_working_admits_exactly_the_supervisors_words_it_names_and_only_from_a_park()
    {
        var parked = await ParkedOnAQuestion();
        var word = (await Release(parked.Id)).Word!;

        Assert.Equal(SessionAdvanceRefusal.WordsChanged, (await Ledger.AdvanceAsync(parked.Id, "working", null, null, null, Now)).Refusal);
        Assert.Equal(
            SessionAdvanceRefusal.WordsChanged,
            (await Ledger.AdvanceAsync(parked.Id, "working", null, null, null, Now, expect: ["someone-else"])).Refusal);

        var forged = new SaidWord("forged", SupervisorWord.Handed("The monthly one"), Now, [], From: SaidFrom.Supervisor, Key: "k", Decision: "d-9");
        await _sessions.KeepSaidAsync(parked.Id, forged, null);
        Assert.Equal(
            SessionAdvanceRefusal.WordsChanged,
            (await Ledger.AdvanceAsync(parked.Id, "working", null, null, null, Now, expected: "awaiting-person", expect: [word.Id])).Refusal);
        await _sessions.TakeSaidAsync(parked.Id, ["forged"]);

        var moved = await Ledger.AdvanceAsync(parked.Id, "working", null, null, null, Now, expected: "awaiting-person", expect: [word.Id]);
        Assert.Equal((SessionAdvanceRefusal.None, SessionState.Working), (moved.Refusal, moved.Session!.State));

        var stopped = await ParkedOnAQuestion("stopped");
        var its = (await Release(stopped.Id, key: "r-2", decision: "d-2")).Word!;
        await Ledger.AdvanceAsync(stopped.Id, "stopped", null, null, null, Now);
        Assert.Equal(
            SessionAdvanceRefusal.InvalidMove,
            (await Ledger.AdvanceAsync(stopped.Id, "working", null, null, null, Now, expect: [its.Id])).Refusal);
    }

    /// <summary>
    /// §6.3: a failure between the admission and the wire puts the park back keeping its queue, less the one admitted word, an owner's
    /// word that arrived after the admission included; a record not working is not restored.
    /// </summary>
    [Fact]
    public async Task Restoring_a_park_keeps_every_word_but_the_admitted_supervisors()
    {
        var parked = await ParkedOnAQuestion();
        await _sessions.KeepSaidAsync(parked.Id, new SaidWord("helped", "the helper's answer", Now, [], Helper: "h-1"), null);
        await Ledger.SayAsync(parked.Id, "Daoris's words.", null, Now, from: SaidFrom.Daoris);
        var word = (await Release(parked.Id)).Word!;
        await Ledger.AdvanceAsync(parked.Id, "working", null, null, null, Now, expected: "awaiting-person", expect: [word.Id]);
        Assert.Equal(SessionSayRefusal.None, (await Ledger.SayAsync(parked.Id, "and the weekly one", null, Now, running: true)).Refusal);

        var restored = await Ledger.RestoreParkAsync(parked.Id, [word.Id, "helped"], "Which report?", null, Now);

        Assert.Equal((SessionAdvanceRefusal.None, SessionState.AwaitingPerson), (restored.Refusal, restored.Session!.State));
        Assert.Equal(
            ["the helper's answer", "Daoris's words.", "and the weekly one"],
            (await Record(parked.Id)).Said.Select(each => each.Text));
        Assert.Equal("Which report?", (await Record(parked.Id)).Note);
        Assert.Equal(SessionAdvanceRefusal.InvalidMove, (await Ledger.RestoreParkAsync(parked.Id, [], null, null, Now)).Refusal);
    }

    /// <summary>The ledger's note of what an ended record went on with gains a fifth: the supervisor's answer, never "your words".</summary>
    [Fact]
    public void A_record_going_on_with_the_supervisors_answer_alone_is_never_said_to_go_on_with_your_words()
    {
        var word = new SaidWord("w", SupervisorWord.Handed("The monthly one"), Now, [], From: SaidFrom.Supervisor, Decision: Decision);
        var session = new Session("s", "q", "dashboards", "stub", SessionState.Completed, null, null, null, Now, Now) { Said = [word] };

        var (note, parts) = SessionLedger.WentOnNote(session, null, null, Now);

        Assert.Contains("Went on with the supervisor's answer", note);
        Assert.DoesNotContain("your words", note);
        Assert.Contains("ledger.went-on-supervisor", parts);
    }

    /// <summary>
    /// §4.5: the handed paragraph names the label and every act it does not grant, says the words are not the owner's, and grants
    /// nothing: no clause carries or mentions the chosen option's <c>does</c>, and nothing grants beyond following the offered answer.
    /// </summary>
    [Fact]
    public void The_handed_text_names_each_act_and_grants_nothing_the_chosen_does_included()
    {
        var text = SupervisorWord.Handed("The monthly one");

        Assert.Contains("chose \"The monthly one\" among the answers you offered, on the owner's behalf", text);
        Assert.Contains("These are not the owner's own words.", text);
        Assert.Contains("it grants nothing", text);
        foreach (var act in new[] { "go-ahead", "sign-in", "access", "spending", "push", "publication", "deletion", "history rewrite" })
        {
            Assert.Contains(act, text);
        }

        Assert.Contains("go_ahead_ask", text);
        Assert.Contains("their words win", text);
        Assert.DoesNotContain("I push to origin now", text);
        Assert.DoesNotMatch(new Regex(@"\b(does|beyond what|you may|you are allowed|authori[sz]e)", RegexOptions.IgnoreCase), text);
    }
}
