using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DRIFT1d (D133 §4): the session working a quest is handed its requirements, each the person's own words quoted
/// verbatim with the check that proves it (DRIFT1c), beneath the quest and beside the ask's words (DRIFT1b), on a first
/// start, a resume and a carry-on alike — and told that its done answers each, met or departed, and that a departure
/// holds what follows for the person's yes. The drift's build closed "per your answer" on its own reading; the
/// requirement it departed from was never in front of it as the person's words.
/// </summary>
public sealed class RequirementsHandedTests
{
    private const string Sentence = "complete the ticket I logged, and this will need the v3 bridge";

    private static readonly QuestRequirementView Bridge =
        new("this will need the v3 bridge", "The report opens in the older shell through the bridge's route.");

    private static readonly QuestRequirementView Common =
        new("we should be using the v3 common-report", "The report is a common-report configuration,\nnot a report type of its own.");

    private static QuestView Quest(params QuestRequirementView[] requirements) =>
        new("q1", "ask #a1", "report", "Build the daily report", "Reached through the bridge; the figures are in the ticket.", "Taken")
        {
            Requirements = requirements,
        };

    private static SessionTarget Target(params QuestRequirementView[] requirements) =>
        SessionTarget.ForQuest(Quest(requirements), "C:/somewhere/report", "http://stand-in");

    private static readonly AskWords Words = new("a1", [new AskWordView(AskWordView.Asked, Sentence, new DateTimeOffset(2026, 10, 1, 2, 26, 0, TimeSpan.Zero))]);

    /// <summary>
    /// 🔴 Every kind of start carries the requirements beneath the quest, before the ask's words and the boundary: a first
    /// start, a resume after a question, and a carry-on after a cut-off.
    /// </summary>
    [Fact]
    public void A_first_start_a_resume_and_a_carry_on_are_each_handed_the_requirements_beneath_the_quest()
    {
        var question = new QuestView("q9", "report", "backend", "What does the endpoint take?", "We need the contract.", "Done") { Note = "{ text }" };
        var target = Target(Bridge, Common) with { Words = Words };

        foreach (var prompt in new[]
                 {
                     TargetPrompt.Compose(target),
                     TargetPrompt.Compose(target with { Answered = question }),
                     TargetPrompt.Compose(target with { CutOff = "the agent's turn failed." }),
                 })
        {
            var body = prompt.IndexOf("the figures are in the ticket", StringComparison.Ordinal);
            var first = prompt.IndexOf("  > " + Bridge.Quote, StringComparison.Ordinal);
            var second = prompt.IndexOf("  > " + Common.Quote, StringComparison.Ordinal);
            var words = prompt.IndexOf("The person's own words on ask `#a1`", StringComparison.Ordinal);
            var boundary = prompt.IndexOf("Never write outside this repository", StringComparison.Ordinal);
            Assert.True(body >= 0 && body < first && first < second && second < words && words < boundary, prompt);
        }
    }

    /// <summary>Each is numbered as the service numbers it, quoted verbatim line by line, with its check; the close says how to answer each.</summary>
    [Fact]
    public void Each_requirement_is_numbered_quoted_with_its_check_and_the_close_says_how_to_answer_it()
    {
        var prompt = TargetPrompt.Compose(Target(Bridge, Common));

        Assert.Contains("What the person requires of this quest, in their own words", prompt);
        Assert.Contains("- Requirement 1:\n\n  > " + Bridge.Quote + "\n\n  Check: " + Bridge.Check, prompt);
        Assert.Contains("- Requirement 2:\n\n  > " + Common.Quote + "\n\n  Check: The report is a common-report configuration,\n  not a report type of its own.", prompt);
        var close = prompt.ReplaceLineEndings(" ");
        Assert.Contains("answer each requirement by its number", close);
        Assert.Contains("`met`, with how its check was met", close);
        Assert.Contains("`departed`, with the reason and the person's own words it turns on, quoted exactly (`quote`)", close);
        Assert.Contains("A `done` that leaves one unanswered is refused", close);
        Assert.Contains("waits until the person accepts it", close);
    }

    /// <summary>
    /// EVID1b (D144 §2): beneath a requirement naming evidence the session is told what Daoris reads in its branch's last commit
    /// when it ends, and that a met answer without it holds the quest, so it can commit a missing file itself, which costs
    /// nothing. A gate is said to be read from the landing queue (EVID1d). A requirement naming none reads as it did.
    /// </summary>
    [Fact]
    public void A_requirement_naming_evidence_tells_the_session_what_Daoris_reads_in_its_last_commit()
    {
        var one = Bridge with { Evidence = [new QuestEvidenceItem("docs/bridge.md")] };
        var two = Common with { Evidence = [new QuestEvidenceItem("docs/report.md"), new QuestEvidenceItem("src/report.json"), new QuestEvidenceItem(null, "web")] };

        var prompt = TargetPrompt.Compose(Target(one, two, Bridge with { Quote = "no evidence here" }));

        Assert.Contains(
            "  Check: " + Bridge.Check + "\n\n  Evidence: Daoris reads `docs/bridge.md` in your branch's last commit when you end. "
            + "A met answer without it holds the quest for the person, so commit it first.\n",
            prompt);
        Assert.Contains(
            "\n\n  Evidence: Daoris reads `docs/report.md` and `src/report.json` in your branch's last commit when you end. A met "
            + "answer without them holds the quest for the person, so commit them first. Gate `web` is read from the landing "
            + "queue, which this machine does not run for it, so a met answer on it waits for the person.\n",
            prompt);
        // The third names none, and reads as a requirement always did.
        var third = prompt[prompt.IndexOf("  > no evidence here", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Evidence:", third[..third.IndexOf("When you close it", StringComparison.Ordinal)]);
    }

    /// <summary>The instruction's bound counts a requirement's evidence beside its words and check (CONTEXT1's one rule).</summary>
    [Fact]
    public void The_bound_counts_each_requirements_evidence()
    {
        var heavy = Enumerable.Range(1, 6)
            .Select(number => new QuestRequirementView($"words {number} " + new string('w', 500), $"check {number}")
            {
                Evidence = [.. Enumerable.Range(1, 5).Select(item => new QuestEvidenceItem($"docs/{number}/{item}/" + new string('p', 280)))],
            })
            .ToArray();

        Assert.True(TargetPrompt.RequirementsShown(heavy) < heavy.Length);
        Assert.Equal(heavy.Length, TargetPrompt.RequirementsShown([.. heavy.Select(each => each with { Evidence = [] })]));
    }

    /// <summary>A quest with no requirements reads exactly as it did: nothing is said of requirements or answers.</summary>
    [Fact]
    public void A_quest_with_no_requirements_reads_as_it_did()
    {
        var prompt = TargetPrompt.Compose(Target());

        Assert.DoesNotContain("requires", prompt);
        Assert.DoesNotContain("Requirement 1", prompt);
        Assert.DoesNotContain("`departed`", prompt);
    }

    /// <summary>
    /// The requirements are bounded, as the person's words are, so the instruction still fits one command line: past the
    /// bound each one left out is named by its number, with where it is read whole, and each still needs its answer.
    /// </summary>
    [Fact]
    public void The_requirements_are_bounded_and_say_which_were_left_out_and_where_they_are_whole()
    {
        var many = Enumerable.Range(1, 20)
            .Select(number => new QuestRequirementView($"words {number} " + new string('w', 600), $"check {number} " + new string('c', 600)))
            .ToArray();

        var prompt = TargetPrompt.Compose(Target(many));

        Assert.Contains("- Requirement 1:", prompt);
        Assert.DoesNotContain("- Requirement 20:", prompt);
        Assert.Contains("are left out here to keep this instruction bounded", prompt);
        Assert.Contains("`quest_list`", prompt);
        Assert.True(prompt.Length < 20_000, $"{prompt.Length} characters");
    }

    /// <summary>The service's quest answers its requirements and whether a departure holds it; the driver reads both, and absent is none.</summary>
    [Fact]
    public async Task The_clients_read_of_a_quest_carries_its_requirements_and_whether_it_is_held()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath == "/api/quests"
            ? (HttpStatusCode.OK, """
                [{"id":"q1","from":"ask #a1","to":"report","title":"t","body":"b","status":"Done","held":true,
                  "requirements":[{"quote":"this will need the v3 bridge","check":"It opens through the route."}]},
                 {"id":"q2","from":"report","to":"backend","title":"t","body":"b","status":"Open"}]
                """)
            : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var held = (await service.FindQuestAsync("q1"))!;
        var plain = (await service.FindQuestAsync("q2"))!;

        Assert.Equal([new QuestRequirementView("this will need the v3 bridge", "It opens through the route.")], held.Requirements);
        Assert.True(held.Held);
        Assert.Empty(plain.Requirements);
        Assert.False(plain.Held);
    }

    /// <summary>
    /// The terminal's yes (`daoris-driver quest accept`) posts to the accept door and says the service's sentence, a
    /// refusal included; a host from before the door is said to be older, never read as nothing.
    /// </summary>
    [Fact]
    public async Task The_terminals_yes_goes_through_the_accept_door_and_says_the_services_words()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/quests/q1/accept" when request.Method == HttpMethod.Post =>
                (HttpStatusCode.OK, """{"quest":{"id":"q1"},"message":"Accepted the departure on quest `#q1`: what it held goes on."}"""),
            "/api/quests/q2/accept" => (HttpStatusCode.Conflict, """{"error":"Quest `#q2` is Taken: nothing waits."}"""),
            _ => null,
        });
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        Assert.Equal((true, "Accepted the departure on quest `#q1`: what it held goes on."), await service.AcceptDepartureAsync("#q1"));
        Assert.Equal((false, "Quest `#q2` is Taken: nothing waits."), await service.AcceptDepartureAsync("q2"));
        var older = await service.AcceptDepartureAsync("q3");
        Assert.False(older.Ok);
        Assert.Contains("older than this driver", older.Message);
    }

    /// <summary>The headless host's usage names the yes, as every verb it answers is named.</summary>
    [Fact]
    public void The_usage_names_the_terminals_yes()
    {
        Assert.Contains("quest delete <id>  ·  quest accept <id>", DriverCommand.Usage);
    }

    /// <summary>A service standing in: each request recorded by its path, then answered, or 404 with no JSON where the answer is null.</summary>
    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
