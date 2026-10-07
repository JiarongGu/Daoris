using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// EVID1a (D144; the evidence design §2, §3 and §6): a requirement may name evidence Daoris reads itself, and a met
/// answer on one closes the quest done and held until it is read. The service is spawn-free and a step publishes in the
/// done's own transaction, so the done itself sets the hold; the driver reads the commit and posts a verdict, which the
/// exchange judges and keeps as an <c>Evidenced</c> operation. Found, it lets go what the done held, in the same
/// transaction; missing, the quest waits for the person as a departure does, and their yes accepts it as it stands.
/// </summary>
public sealed class QuestEvidenceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-evidence-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T09:00:00Z");

    private const string Sentence = "write the bridge report into docs/report-bridge.md, and this will need the v3 bridge";

    private const string Commit = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private const string Later = "0123456789abcdef0123456789abcdef01234567";
    private const string Blob = "fedcba9876543210fedcba9876543210fedcba98";

    private static readonly QuestRequirement Report =
        new("write the bridge report into docs/report-bridge.md", "The report is in docs/report-bridge.md.")
        {
            Evidence = [new QuestEvidence("docs/report-bridge.md")],
        };

    private static readonly QuestRequirement Bridge =
        new("this will need the v3 bridge", "The report opens through the bridge's route.");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages", "the v3 bridge"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await _service.RegistryAsync())
        {
            await _service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(_service, _quests, files: files, asks: _asks);
        _desk = new AskDesk(_service, _asks, _exchange, files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    private async Task<Ask> Asked() => (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;

    private Task<AskOutcome> Publish(
        Ask ask, IReadOnlyList<QuestRequirement> requirements, IReadOnlyList<QuestStep>? then = null,
        string title = "Write the bridge report") =>
        _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft(title, "Through the bridge.")
        {
            Requirements = requirements,
            Then = then ?? [],
        });

    /// <summary>
    /// The report quest: asked by the ask, naming its file as evidence, with a verifying step to follow, and taken. One ask
    /// holds every quest a test makes, so each is told apart by its title.
    /// </summary>
    private async Task<(Ask Ask, Quest Quest)> Built(
        bool chain = true, IReadOnlyList<QuestRequirement>? requirements = null, string title = "Write the bridge report")
    {
        var ask = await Asked();
        var published = await Publish(
            ask, requirements ?? [Report, Bridge],
            chain ? [new QuestStep("checker", "Verify {parent} in the browser", "Open it and look.")] : null, title);
        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now)).Refusal);
        return (ask, published.Quest);
    }

    private static QuestAnswer Met(int requirement) => new(requirement, Met: "Done as the check says.");

    private Task<QuestRespondOutcome> Done(Quest quest, params QuestAnswer[] answers) =>
        _exchange.RespondAsync(quest.Id, "done", "Built.", Now, answers: answers.Length > 0 ? answers : [Met(1), Met(2)]);

    private static QuestEvidenceVerdict Read(string result = "found", string commit = Commit, string how = "session-end") =>
        new(commit, how, [new QuestEvidenceRead(1, "docs/report-bridge.md", null, result)
        {
            Object = result == "found" ? Blob : null, Changed = result == "found" ? true : null,
        }])
        { Session = result == "found" ? "s1a2b3c4" : null };

    private async Task<IReadOnlyList<Quest>> FollowUps(Ask ask, Quest parent) =>
        [.. (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Where(quest => quest.Parent == parent.Id)];

    // ——— §2: a requirement names its evidence, judged at publish.

    /// <summary>A requirement's evidence is kept on the quest as named, read back from the store and replayed from its history.</summary>
    [Fact]
    public async Task A_requirements_evidence_is_kept_on_the_quest()
    {
        var (_, quest) = await Built(chain: false);

        var held = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal([new QuestEvidence("docs/report-bridge.md")], held.Requirements[0].Evidence);
        Assert.Empty(held.Requirements[1].Evidence);
        Assert.Equal([Report, Bridge], QuestLog.Replay(await _quests.HistoryAsync(quest.Id))!.Requirements);
    }

    /// <summary>
    /// 🔴 Each way an evidence item is not one is refused at publish, naming the requirement and why, and nothing is
    /// published: a path that is not repository-relative, that reaches outside the tree or into git's, or that git would
    /// read as an option; both keys or neither; too many items; a gate's name that is not one.
    /// </summary>
    [Theory]
    [InlineData("/etc/passwd", "starts with `/`")]
    [InlineData("D:/checkouts/reports/report.md", "names a drive")]
    [InlineData("docs/../../secrets", "`..`")]
    [InlineData("docs\\report.md", "backslash")]
    [InlineData("docs/\u0007bell", "control character")]
    [InlineData("-rf", "option")]
    [InlineData(".git/config", "`.git`")]
    [InlineData("docs/.GIT/hooks", "`.git`")]
    [InlineData("docs//report.md", "empty segment")]
    [InlineData("./docs/report.md", "`.` segment")]
    [InlineData("LONG", "longer than 300 characters")]
    [InlineData("BOTH", "exactly one of `path` or `gate`")]
    [InlineData("NEITHER", "exactly one of `path` or `gate`")]
    [InlineData("SIX", "at most 5")]
    [InlineData("GATE-NAME", "a gate's name is letters, digits")]
    public async Task An_evidence_item_that_is_not_one_is_refused_naming_its_requirement(string path, string said)
    {
        var ask = await Asked();
        IReadOnlyList<QuestEvidence> evidence = path switch
        {
            "LONG" => [new QuestEvidence(new string('a', QuestEvidence.MaxPathLength + 1))],
            "BOTH" => [new QuestEvidence("docs/report-bridge.md", "web")],
            "NEITHER" => [new QuestEvidence()],
            "SIX" => [.. Enumerable.Range(1, 6).Select(n => new QuestEvidence($"docs/{n}.md"))],
            "GATE-NAME" => [new QuestEvidence(Gate: "web gate")],
            _ => [new QuestEvidence(path)],
        };

        var refused = await Publish(ask, [Bridge, Report with { Evidence = evidence }]);

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains("Requirement 2", refused.Message);
        Assert.Contains(said, refused.Message);
        Assert.Empty((await _desk.FindAsync(ask.Id))!.Quests);
    }

    /// <summary>
    /// 🔴 A gate is refused until the landing queue reads gates (D144 point 5, EVID1d), naming the queue it waits for and the
    /// path evidence that is read today — the same refusal at every door, a remote's judgement of a pushed quest included.
    /// </summary>
    [Fact]
    public async Task A_gate_is_refused_naming_the_queue_it_waits_for()
    {
        var ask = await Asked();
        var gated = Report with { Evidence = [new QuestEvidence(Gate: "web")] };

        var refused = await Publish(ask, [gated]);

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains("Requirement 1", refused.Message);
        Assert.Contains("`web`", refused.Message);
        Assert.Contains("queue", refused.Message);
        var asked = new Quest("abcdefabcdef", "ask #a1b2c3", "reports", "t", "b", QuestStatus.Open, null, Now, Now, "work");
        Assert.Contains("queue", _exchange.JudgeReceived(asked with { Requirements = [gated] }, await _service.RegistryAsync()));
        Assert.Null(_exchange.JudgeReceived(asked with { Requirements = [Report] }, await _service.RegistryAsync()));
    }

    // ——— §3, §6: a met answer on a requirement naming evidence holds the done until it is read.

    /// <summary>
    /// 🔴 A met answer on a requirement that names evidence closes the quest done and held, with the cause
    /// <c>evidence-unread</c>: its step is not published, it stays outstanding, its ask is not done, and the answer tells
    /// the session what Daoris reads and when, so it can commit it first.
    /// </summary>
    [Fact]
    public async Task A_met_answer_naming_evidence_closes_done_and_held_until_it_is_read()
    {
        var (ask, quest) = await Built();

        var closed = await Done(quest);

        Assert.Equal(QuestRespondRefusal.None, closed.Refusal);
        var held = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Done, held.Status);
        Assert.True(held.Held);
        Assert.True(held.AwaitsEvidence);
        Assert.Equal(QuestHold.EvidenceUnread, held.Hold);
        Assert.Null(held.Evidence);
        Assert.Empty(await FollowUps(ask, quest));
        Assert.Contains(quest.Id, (await _quests.ListAsync()).Select(listed => listed.Id));
        Assert.Equal(AskState.Published, (await _desk.FindAsync(ask.Id))!.State);
        Assert.Contains("`docs/report-bridge.md`", closed.Message);
        Assert.Contains("last commit", closed.Message);
        Assert.Contains("commit it", closed.Message);
    }

    /// <summary>A requirement with no evidence closes on the session's word, as before; so does a departure from one that names some.</summary>
    [Fact]
    public async Task Only_a_met_answer_waits_on_evidence()
    {
        var (_, plain) = await Built(chain: false, requirements: [Bridge], title: "Open the report through the bridge");
        var met = await _exchange.RespondAsync(plain.Id, "done", "Built.", Now, answers: [Met(1)]);
        var (_, departed) = await Built(chain: false);
        var departure = await Done(departed, new QuestAnswer(1, null, "It is written into the page instead.", "the bridge report"), Met(2));

        Assert.False(met.Quest!.Held);
        Assert.False(met.Quest.AwaitsEvidence);
        var standing = (await _quests.FindAsync(departed.Id))!;
        Assert.Equal(QuestHold.Departed, standing.Hold);
        Assert.False(standing.AwaitsEvidence);
        Assert.Empty(standing.EvidenceWanted);
        Assert.Equal(QuestRespondRefusal.None, departure.Refusal);
    }

    /// <summary>
    /// 🔴 Found: the verdict is kept on the quest as an <c>Evidenced</c> operation, the hold lifts, and the held step is
    /// published in the same transaction, as the yes publishes one. The ask reads done once that step is.
    /// </summary>
    [Fact]
    public async Task Evidence_found_releases_the_done_and_publishes_its_held_step()
    {
        var (ask, quest) = await Built();
        await Done(quest);
        var later = Now.AddMinutes(5);

        var read = await _exchange.EvidenceAsync(quest.Id, Read(), later);

        Assert.Equal(QuestRespondRefusal.None, read.Refusal);
        var standing = (await _quests.FindAsync(quest.Id))!;
        Assert.False(standing.Held);
        Assert.False(standing.AwaitsEvidence);
        Assert.Null(standing.Hold);
        Assert.Null(standing.Accepted);
        Assert.Equal(Commit, standing.Evidence!.Commit);
        Assert.Equal(later, standing.Evidence.At);
        Assert.Equal(_quests.Machine, standing.Evidence.Machine);
        Assert.Equal("s1a2b3c4", standing.Evidence.Session);
        var item = Assert.Single(standing.Evidence.Items);
        Assert.Equal(("found", Blob, (bool?)true), (item.Result, item.Object, item.Changed));
        var step = Assert.Single(await FollowUps(ask, quest));
        Assert.Contains($"Then: published `#{step.Id}`", read.Message);
        Assert.Contains("`a1b2c3d`", read.Message);
        Assert.DoesNotContain(quest.Id, (await _quests.ListAsync()).Select(listed => listed.Id));
        Assert.Equal(QuestOperationKind.Evidenced, (await _quests.HistoryAsync(quest.Id))[^1].Kind);
    }

    /// <summary>
    /// 🔴 Missing: the verdict is kept and the quest stays held, now for <c>evidence-missing</c>: its step is not published
    /// and the answer names what was not found and both of the person's doors. A check at a later commit that finds it
    /// lets the done go on, and says it was found later than the done's own commit.
    /// </summary>
    [Fact]
    public async Task Evidence_missing_keeps_the_done_held_until_a_later_check_finds_it()
    {
        var (ask, quest) = await Built();
        await Done(quest);

        var missing = await _exchange.EvidenceAsync(quest.Id, Read("uncommitted"), Now.AddMinutes(5));

        Assert.Equal(QuestRespondRefusal.None, missing.Refusal);
        var held = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestHold.EvidenceMissing, held.Hold);
        Assert.True(held.AwaitsEvidence);
        Assert.Equal("uncommitted", Assert.Single(held.Evidence!.Items).Result);
        Assert.Empty(await FollowUps(ask, quest));
        Assert.Contains("`docs/report-bridge.md`", missing.Message);
        Assert.Contains($"`daoris-driver quest accept {quest.Id}`", missing.Message);
        Assert.Contains($"`daoris-driver quest check {quest.Id} --commit <sha>`", missing.Message);

        var found = await _exchange.EvidenceAsync(quest.Id, Read(commit: Later, how: "terminal") with { Session = null }, Now.AddMinutes(9));

        Assert.Equal(QuestRespondRefusal.None, found.Refusal);
        var released = (await _quests.FindAsync(quest.Id))!;
        Assert.False(released.Held);
        Assert.Equal(("terminal", Later), (released.Evidence!.How, released.Evidence.Commit));
        Assert.Single(await FollowUps(ask, quest));
        Assert.Equal(2, (await _quests.HistoryAsync(quest.Id)).Count(operation => operation.Kind == QuestOperationKind.Evidenced));
    }

    /// <summary>
    /// A done that departed from one requirement and met another naming evidence waits on both: the evidence found is
    /// kept, the departure still holds it for the person's yes, and the yes lets it go on.
    /// </summary>
    [Fact]
    public async Task Evidence_found_beside_a_departure_still_waits_for_the_yes()
    {
        var (ask, quest) = await Built();
        await Done(quest, Met(1), new QuestAnswer(2, null, "It opens on its own page.", "the v3 bridge"));
        Assert.Equal(QuestHold.Departed, (await _quests.FindAsync(quest.Id))!.Hold);

        var read = await _exchange.EvidenceAsync(quest.Id, Read(), Now.AddMinutes(5));

        Assert.Equal(QuestRespondRefusal.None, read.Refusal);
        var standing = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestHold.Departed, standing.Hold);
        Assert.False(standing.AwaitsEvidence);
        Assert.Empty(await FollowUps(ask, quest));
        Assert.Contains("yes", read.Message);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(quest.Id, Now.AddMinutes(9))).Refusal);
        Assert.Single(await FollowUps(ask, quest));
    }

    /// <summary>
    /// 🔴 The person's yes is reused unchanged (D144 §6): it accepts a done whose evidence is unread or missing as it
    /// stands, publishes its step, and no verdict applies after it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_yes_accepts_a_done_held_for_its_evidence_as_it_stands(bool read)
    {
        var (ask, quest) = await Built();
        await Done(quest);
        if (read) await _exchange.EvidenceAsync(quest.Id, Read("missing"), Now.AddMinutes(1));

        var accepted = await _exchange.AcceptAsync(quest.Id, Now.AddMinutes(5));

        Assert.Equal(QuestRespondRefusal.None, accepted.Refusal);
        Assert.Contains("as it stands", accepted.Message);
        var standing = (await _quests.FindAsync(quest.Id))!;
        Assert.False(standing.Held);
        Assert.False(standing.AwaitsEvidence);
        Assert.Single(await FollowUps(ask, quest));
        Assert.Equal(QuestRespondRefusal.NotAwaitingEvidence, (await _exchange.EvidenceAsync(quest.Id, Read(), Now.AddMinutes(9))).Refusal);
    }

    /// <summary>
    /// A verdict is taken only on a done that waits on evidence: a taken quest, a done whose requirements name none, a
    /// done whose evidence was found, and an unknown quest each say why not.
    /// </summary>
    [Fact]
    public async Task A_verdict_is_taken_only_on_a_done_waiting_on_its_evidence()
    {
        var (_, quest) = await Built(chain: false);
        var whileTaken = await _exchange.EvidenceAsync(quest.Id, Read(), Now);
        await Done(quest);
        await _exchange.EvidenceAsync(quest.Id, Read(), Now);
        var again = await _exchange.EvidenceAsync(quest.Id, Read(), Now);
        var (_, plain) = await Built(chain: false, requirements: [Bridge], title: "Open the report through the bridge");
        await _exchange.RespondAsync(plain.Id, "done", "Built.", Now, answers: [Met(1)]);
        var none = await _exchange.EvidenceAsync(plain.Id, Read(), Now);
        var unknown = await _exchange.EvidenceAsync("feedfacecafe", Read(), Now);

        Assert.Equal(QuestRespondRefusal.NotAwaitingEvidence, whileTaken.Refusal);
        Assert.Contains("Taken", whileTaken.Message);
        Assert.Equal(QuestRespondRefusal.NotAwaitingEvidence, again.Refusal);
        Assert.Contains("already found", again.Message);
        Assert.Equal(QuestRespondRefusal.NotAwaitingEvidence, none.Refusal);
        Assert.Contains("names no evidence", none.Message);
        Assert.Equal(QuestRespondRefusal.NotFound, unknown.Refusal);
    }

    /// <summary>
    /// 🔴 A verdict must name the commit and read exactly what the done waits on: each way it does not is refused, saying
    /// which, and nothing is kept — the quest stays held, unread.
    /// </summary>
    [Theory]
    [InlineData("short-commit", "full id")]
    [InlineData("how", "`session-end`, `sweep` or `terminal`")]
    [InlineData("session", "session")]
    [InlineData("unread", "requirement 1: `docs/report-bridge.md`")]
    [InlineData("twice", "read twice")]
    [InlineData("not-named", "`docs/other.md`")]
    [InlineData("not-met", "requirement 2")]
    [InlineData("no-requirement", "requirement 3")]
    [InlineData("result", "`gone`")]
    [InlineData("gate-result", "`no-queue`")]
    [InlineData("object", "object")]
    [InlineData("machine-path", "starts with `/`")]
    public async Task A_verdict_that_does_not_read_what_the_done_waits_on_is_refused_saying_which(string shape, string said)
    {
        var (_, quest) = await Built(chain: false);
        await Done(quest);
        var found = new QuestEvidenceRead(1, "docs/report-bridge.md", null, "found") { Object = Blob };
        var verdict = shape switch
        {
            "short-commit" => Read(commit: "a1b2c3d"),
            "how" => Read(how: "guessed"),
            "session" => Read() with { Session = "s1 a2" },
            "unread" => new QuestEvidenceVerdict(Commit, "session-end", []),
            "twice" => new QuestEvidenceVerdict(Commit, "session-end", [found, found]),
            "not-named" => new QuestEvidenceVerdict(Commit, "session-end", [found, found with { Path = "docs/other.md" }]),
            "not-met" => new QuestEvidenceVerdict(Commit, "session-end", [found, new QuestEvidenceRead(2, "docs/report-bridge.md", null, "found")]),
            "no-requirement" => new QuestEvidenceVerdict(Commit, "session-end", [found, new QuestEvidenceRead(3, "docs/x.md", null, "found")]),
            "result" => new QuestEvidenceVerdict(Commit, "session-end", [found with { Result = "gone" }]),
            "gate-result" => new QuestEvidenceVerdict(Commit, "session-end", [found with { Result = "no-queue" }]),
            "object" => new QuestEvidenceVerdict(Commit, "session-end", [found with { Object = "D:/checkouts/reports/blob" }]),
            _ => new QuestEvidenceVerdict(Commit, "session-end", [found with { Path = "/srv/checkouts/reports/docs/report-bridge.md" }]),
        };

        var refused = await _exchange.EvidenceAsync(quest.Id, verdict, Now);

        Assert.Equal(QuestRespondRefusal.BadVerdict, refused.Refusal);
        Assert.Contains(said, refused.Message);
        var standing = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestHold.EvidenceUnread, standing.Hold);
        Assert.DoesNotContain(await _quests.HistoryAsync(quest.Id), operation => operation.Kind == QuestOperationKind.Evidenced);
    }

    // ——— REFAC3: one judge of a verdict's shape, which both doors call.

    private static QuestEvidenceVerdict Verdict(
        IReadOnlyList<QuestEvidenceRead> items, string commit = Commit, string how = "session-end") => new(commit, how, items);

    private static readonly QuestEvidenceRead Reported =
        new(1, "docs/report-bridge.md", null, "found") { Object = Blob, Changed = true };

    private static readonly QuestEvidenceRead Unfound = new(1, "docs/report-bridge.md", null, "missing");

    private static readonly QuestEvidenceRead Cased = new(1, "docs/report-bridge.md", null, "case");

    /// <summary>
    /// REFAC3's table: each verdict shape, whether both doors take it, the field the one judge of its shape faults (none
    /// for a whole verdict, whether or not it reads what the done waits on), and words the exchange's refusal holds. Read
    /// against <see cref="Built"/>'s quest, done meeting both requirements, the first naming <c>docs/report-bridge.md</c>.
    /// </summary>
    private static readonly Dictionary<string, (QuestEvidenceVerdict Verdict, bool Taken, QuestVerdictField? Field, string? Said)> Shapes = new()
    {
        // Whole, and reading what the done waits on.
        ["found"] = (Verdict([Reported]) with { Session = "s1a2b3c4" }, true, null, null),
        ["missing"] = (Verdict([Unfound]), true, null, null),
        ["uncommitted"] = (Verdict([Unfound with { Result = "uncommitted" }]), true, null, null),
        ["case, spelled"] = (Verdict([Cased with { Spelled = "docs/Report-Bridge.md" }]), true, null, null),
        ["case, unspelled"] = (Verdict([Cased]), true, null, null),
        ["a sha-256 commit in capitals"] = (Verdict([Reported], commit: new string('A', 64)), true, null, null),
        ["the terminal's, no session"] = (Verdict([Reported], how: "terminal"), true, null, null),

        // Not one in shape, at either door.
        ["a short commit"] = (Verdict([Reported], commit: "a1b2c3d"), false, QuestVerdictField.Commit, "full id"),
        ["a commit not hex"] = (Verdict([Reported], commit: new string('g', 40)), false, QuestVerdictField.Commit, "full id"),
        ["an unknown how"] = (Verdict([Reported], how: "guessed"), false, QuestVerdictField.How, "`session-end`, `sweep` or `terminal`"),
        ["a session not an id"] = (Verdict([Reported]) with { Session = "s1 a2" }, false, QuestVerdictField.Session, "session"),
        ["requirement 0"] = (Verdict([Reported with { Requirement = 0 }]), false, QuestVerdictField.Requirement, "numbered from 1"),
        ["a path and a gate"] = (Verdict([Reported with { Gate = "web" }]), false, QuestVerdictField.PathOrGate, "exactly one of `path` or `gate`"),
        ["neither"] = (Verdict([Reported with { Path = null }]), false, QuestVerdictField.PathOrGate, "exactly one of `path` or `gate`"),
        ["a machine's path"] = (Verdict([Reported with { Path = "/srv/checkouts/reports/docs/report-bridge.md" }]), false, QuestVerdictField.Path, "starts with `/`"),
        ["a drive"] = (Verdict([Reported with { Path = "D:/checkouts/reports/docs/report-bridge.md" }]), false, QuestVerdictField.Path, "names a drive"),
        ["a gate misnamed"] = (Verdict([new QuestEvidenceRead(1, null, "web gate", "found")]), false, QuestVerdictField.Gate, "not a gate's name"),
        ["an unknown result"] = (Verdict([Reported with { Result = "gone" }]), false, QuestVerdictField.Result, "`gone`"),
        ["a gate's result on a path"] = (Verdict([Reported with { Result = "no-queue" }]), false, QuestVerdictField.Result, "`no-queue`"),
        ["an object not an id"] = (Verdict([Reported with { Object = "D:/checkouts/reports/blob" }]), false, QuestVerdictField.Object, "full object id"),

        // `spelled` is a `case` read's alone, and names the path in another case (D144 §3).
        ["spelled on a missing read"] = (Verdict([Unfound with { Spelled = "docs/Report-Bridge.md" }]), false, QuestVerdictField.Spelled, "only a `case` read"),
        ["spelled on a found read"] = (Verdict([Reported with { Spelled = "docs/Report-Bridge.md" }]), false, QuestVerdictField.Spelled, "only a `case` read"),
        ["spelled as a machine's path"] = (Verdict([Cased with { Spelled = "/srv/checkouts/reports/docs/Report-Bridge.md" }]), false, QuestVerdictField.Spelled, "starts with `/`"),
        ["spelled as another path"] = (Verdict([Cased with { Spelled = "docs/other.md" }]), false, QuestVerdictField.Spelled, "only in case"),
        ["spelled as the path itself"] = (Verdict([Cased with { Spelled = "docs/report-bridge.md" }]), false, QuestVerdictField.Spelled, "only in case"),

        // Whole, and not what the done waits on: the exchange's coverage and the replay's refuse it alike.
        ["nothing read"] = (Verdict([]), false, null, "requirement 1: `docs/report-bridge.md`"),
        ["read twice"] = (Verdict([Reported, Reported]), false, null, "read twice"),
        ["a path not named"] = (Verdict([Reported with { Path = "docs/other.md" }]), false, null, "`docs/other.md`"),
        ["a requirement naming none"] = (Verdict([Reported with { Requirement = 2 }]), false, null, "requirement 2"),
        ["no such requirement"] = (Verdict([Reported, Reported with { Requirement = 3, Path = "docs/x.md" }]), false, null, "requirement 3"),
        ["a gate not named"] = (Verdict([new QuestEvidenceRead(1, null, "web", "found")]), false, null, "`web`"),
    };

    public static IEnumerable<object[]> VerdictShapes => Shapes.Keys.Select(shape => new object[] { shape });

    /// <summary>
    /// 🔴 REFAC3: each verdict shape is taken or refused alike at both doors that read one: the exchange, which the
    /// evidence door hands what was posted, and the wire, which reads another machine's verdict from its push and
    /// replays it. Before REFAC3 they disagreed on <c>spelled</c>, which only the exchange kept to a <c>case</c> read.
    /// A shape fault is the one judge's, so the wire refuses it as not whole; a whole verdict that reads something else
    /// is the coverage's, so the wire reads it and the replay does not apply it.
    /// </summary>
    [Theory]
    [MemberData(nameof(VerdictShapes))]
    public async Task A_verdict_is_taken_or_refused_alike_at_both_doors(string shape)
    {
        var (verdict, taken, field, said) = Shapes[shape];
        var (_, quest) = await Built(chain: false);
        await Done(quest);
        var history = await _quests.HistoryAsync(quest.Id);

        var pushed = QuestWire.ReadPush(QuestWire.Push(0,
            [.. history, new QuestOperation(quest.Id, QuestOperationKind.Evidenced, "m2", 1, Now.AddMinutes(1), Evidence: verdict)]));
        var replayed = pushed is { } read ? QuestLog.Replay(read.Operations) : null;
        var posted = await _exchange.EvidenceAsync(quest.Id, verdict, Now.AddMinutes(1));

        Assert.Equal((taken, taken), (replayed?.Evidence is not null, posted.Refusal == QuestRespondRefusal.None));
        Assert.Equal(taken ? QuestRespondRefusal.None : QuestRespondRefusal.BadVerdict, posted.Refusal);
        if (said is not null) Assert.Contains(said, posted.Message);
        Assert.Equal(field, verdict.JudgeShape()?.Field);
        Assert.Equal(field is not null, pushed is null);
    }

    // ——— A chain step inherits the evidence where it is a fact about the same tree.

    /// <summary>
    /// 🔴 A step to the same repository inherits each requirement whole; a step to another inherits it without its evidence,
    /// since a path is a fact about one repository's tree, and the step's history says so.
    /// </summary>
    [Fact]
    public async Task A_step_to_another_repository_inherits_the_requirement_without_its_evidence()
    {
        var ask = await Asked();
        var built = (await Publish(ask, [Report],
        [
            new QuestStep("checker", "Verify {parent} in the browser", "Open it and look."),
            new QuestStep("reports", "Report on {parent}", "Say what was found."),
        ])).Quest!;
        await _exchange.RespondAsync(built.Id, "take", null, Now);
        await _exchange.RespondAsync(built.Id, "done", "Built.", Now, answers: [Met(1)]);
        await _exchange.EvidenceAsync(built.Id, Read(), Now);
        var verify = Assert.Single(await FollowUps(ask, built));

        Assert.Equal([Report with { Evidence = [] }], verify.Requirements);
        var published = (await _quests.HistoryAsync(verify.Id))[0];
        Assert.Contains("requirement 1", published.Note);
        Assert.Contains("`reports`", published.Note);
        Assert.Contains("`checker`", published.Note);

        // The verifying step closes on the session's word, and the step after it, back to `reports`, inherits what it has.
        await _exchange.RespondAsync(verify.Id, "take", null, Now);
        await _exchange.RespondAsync(verify.Id, "done", "Verified.", Now, answers: [Met(1)]);
        var report = Assert.Single(await FollowUps(ask, verify));
        Assert.Equal([Report with { Evidence = [] }], report.Requirements);

        var same = (await Publish(ask, [Report], [new QuestStep("reports", "Tidy {parent}", "Tidy it.")], "Write the bridge report again")).Quest!;
        await _exchange.RespondAsync(same.Id, "take", null, Now);
        await _exchange.RespondAsync(same.Id, "done", "Built.", Now, answers: [Met(1)]);
        await _exchange.EvidenceAsync(same.Id, Read(), Now);
        var tidy = Assert.Single(await FollowUps(ask, same));
        Assert.Equal([Report], tidy.Requirements);
        Assert.Null((await _quests.HistoryAsync(tidy.Id))[0].Note);
    }

    // ——— The record: the history, the wire, the replay.

    /// <summary>
    /// 🔴 Evidence and its verdict cross the wire as names and codes only: a replay on any machine reads the same hold,
    /// and nothing in the wire holds a byte of the file or a machine's path.
    /// </summary>
    [Fact]
    public async Task Evidence_and_its_verdict_cross_the_wire_as_names_and_codes()
    {
        var (_, quest) = await Built(chain: false);
        await Done(quest);
        await _exchange.EvidenceAsync(quest.Id, Read("case") with
        {
            Items = [new QuestEvidenceRead(1, "docs/report-bridge.md", null, "case") { Spelled = "docs/Report-Bridge.md" }],
        }, Now.AddMinutes(1));

        var history = await _quests.HistoryAsync(quest.Id);
        var json = QuestWire.Push(0, history);
        var pushed = QuestWire.ReadPush(json)!.Value.Operations;

        var replayed = QuestLog.Replay(pushed)!;
        Assert.Equal([Report, Bridge], replayed.Requirements);
        Assert.Equal(QuestHold.EvidenceMissing, replayed.Hold);
        Assert.Equal("docs/Report-Bridge.md", Assert.Single(replayed.Evidence!.Items).Spelled);
        Assert.Equal(Commit, pushed[^1].Evidence!.Commit);
        Assert.DoesNotContain(_root, json, StringComparison.OrdinalIgnoreCase);
        // When and which machine are the operation's own, never written twice inside the verdict it carries.
        var verdict = json[json.LastIndexOf("\"evidence\"", StringComparison.Ordinal)..];
        Assert.DoesNotContain("\"at\":", verdict);
        Assert.DoesNotContain("\"machine\":", verdict);
    }

    /// <summary>A quest whose requirements name no evidence crosses exactly as it did: no field is written for it.</summary>
    [Fact]
    public async Task A_quest_naming_no_evidence_crosses_as_before()
    {
        var (_, quest) = await Built(chain: false, requirements: [Bridge]);

        Assert.DoesNotContain("evidence", QuestWire.Push(0, await _quests.HistoryAsync(quest.Id)));
    }

    /// <summary>
    /// Half of a fact does not cross: an evidence item naming both or neither, a machine's path, a verdict with no full
    /// commit, an unknown way of reading or result each make the operation not whole.
    /// </summary>
    [Theory]
    [InlineData("""{ "path": "docs/a.md", "gate": "web" }""", null)]
    [InlineData("""{ }""", null)]
    [InlineData("""{ "path": "D:/checkouts/reports/a.md" }""", null)]
    [InlineData(null, """{ "commit": "a1b2c3d", "how": "session-end", "items": [] }""")]
    [InlineData(null, """{ "commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "how": "guessed", "items": [] }""")]
    [InlineData(null, """{ "commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "how": "sweep", "items": [{ "requirement": 1, "path": "a.md", "result": "gone" }] }""")]
    [InlineData(null, """{ "commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "how": "sweep", "items": [{ "requirement": 1, "path": "/srv/checkouts/reports/a.md", "result": "found" }] }""")]
    [InlineData(null, """{ "commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", "how": "sweep" }""")]
    public void Half_of_a_fact_on_the_wire_makes_the_operation_not_whole(string? item, string? verdict)
    {
        var published = $$"""
            { "base": 0, "operations": [{ "machine": "m1", "sequence": 1, "quest": "abcdefabcdef", "kind": "published",
              "at": "2026-10-03T09:00:00Z",
              "asked": { "from": "ask #a1b2c3", "to": "reports", "title": "t", "body": "b", "links": [], "attachments": [], "then": [],
                "requirements": [{ "quote": "q", "check": "c", "evidence": [{{item ?? """{ "path": "docs/a.md" }"""}}] }] } }
              {{(verdict is null ? "" : $$""", { "machine": "m1", "sequence": 2, "quest": "abcdefabcdef", "kind": "evidenced", "at": "2026-10-03T09:00:00Z", "evidence": {{verdict}} }""")}}] }
            """;

        Assert.Null(QuestWire.ReadPush(published));
    }

    /// <summary>
    /// A verdict applies only to a done that waits on its evidence and reads exactly that: the replay's own rule, so a
    /// remote and a rebase refuse what the exchange refuses, and a second machine's verdict on evidence found is no move.
    /// </summary>
    [Fact]
    public void A_verdict_applies_only_while_the_done_waits_on_what_it_reads()
    {
        var done = new Quest("abcdefabcdef", "ask #a1", "reports", "t", "b", QuestStatus.Done, "Built.", Now, Now, "work")
        {
            Requirements = [Report, Bridge],
            Answers = [Met(1), Met(2)],
        };
        var verdict = new QuestOperation("abcdefabcdef", QuestOperationKind.Evidenced, "m2", 7, Now, Evidence: Read());

        Assert.True(QuestLog.Applies(done, verdict));
        Assert.False(QuestLog.Applies(done with { Status = QuestStatus.Taken }, verdict));
        Assert.False(QuestLog.Applies(done with { Accepted = Now }, verdict));
        Assert.False(QuestLog.Applies(done with { Evidence = Read() }, verdict));
        Assert.True(QuestLog.Applies(done with { Evidence = Read("missing") }, verdict));
        Assert.False(QuestLog.Applies(done, verdict with { Evidence = new QuestEvidenceVerdict(Commit, "sweep", []) }));
        Assert.False(QuestLog.Applies(done, verdict with { Evidence = null }));
        Assert.False(QuestLog.Step(done, verdict)!.Held);
    }

    /// <summary>The verdict is kept in the quest's cached row as the replay gives it, and a store opened again reads it, and the hold, back.</summary>
    [Fact]
    public async Task The_verdict_is_kept_in_the_cache_and_read_back()
    {
        var (_, quest) = await Built(chain: false);
        await Done(quest);
        await _exchange.EvidenceAsync(quest.Id, Read("missing"), Now);

        var reopened = await QuestStore.OpenAsync(_connection);
        var standing = (await reopened.FindAsync(quest.Id))!;

        Assert.Equal(QuestHold.EvidenceMissing, standing.Hold);
        Assert.Equal(Now, standing.Evidence!.At);
        Assert.Equal("missing", Assert.Single(standing.Evidence.Items).Result);
    }
}
