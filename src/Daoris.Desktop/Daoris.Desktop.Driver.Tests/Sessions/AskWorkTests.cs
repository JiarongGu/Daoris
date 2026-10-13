using System.Net;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one reader of an ask's work, and of one quest's (PAUSE1a, D132 §1, design §1): the quests the ask asked, chain steps
/// included; every quest a session of the work published, so a question asked of another repository joins it, and so on
/// down; the sessions of those quests and the ask's intake; and each session's tree and branch here, with the landings
/// that name one. The pause (PAUSE1b) and the abandon (PAUSE1d) read this answer, so neither holds a second copy of the rule.
/// </summary>
/// <remarks>Records and quests built in the test, and a tree predicate handed in: no process, so this is the fast half.</remarks>
public sealed class AskWorkTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private const string TreeA = "X:/daoris/trees/aurora/engine/s-1a2b3c4d";

    private const string TreeB = "X:/daoris/trees/aurora/game/s-9f8e7d6c";

    /// <summary>A registered root: a session that ran there opened no tree of the home's.</summary>
    private const string Root = "X:/work/engine";

    /// <summary>Only a path under the home's trees is a tree this home opened, either separator, as <see cref="SessionTrees.Holds"/> answers.</summary>
    private static bool Held(string path) => SessionGroups.Normal(path).StartsWith("X:/daoris/trees/", StringComparison.OrdinalIgnoreCase);

    private static QuestView Quest(string id, string from, string to = "engine", string status = "Open") =>
        new(id, from, to, $"The work of #{id}", "A body.", status);

    private static SessionRecord Session(
        string id, string? quest, string state = "completed", string repository = "engine", string? tree = null, string? ask = null,
        int at = 0) =>
        new(id, repository, state) { Quest = quest, Tree = tree, Ask = ask, Created = T0.AddMinutes(at), Updated = T0.AddMinutes(at + 1) };

    private static AskWorkLook Look(QuestView[] quests, SessionRecord[]? records = null, LandedBranch[]? landings = null) =>
        new(quests, records ?? []) { Landings = landings ?? [] };

    private static LandedBranch Landing(string session, string branch, DateTimeOffset? gone = null) =>
        new("engine", "aurora", branch, "main", "9f3e2a1", session, "q1", "The work of #q1", T0) { GoneAt = gone };

    /// <summary>
    /// Every quest the ask asked is its work, and a chain's later step with it: the service asks each step on the chain's
    /// asker's behalf (D65 §4 as built), so the step names the ask as its sender, as the first did.
    /// </summary>
    [Fact]
    public void An_asks_work_is_every_quest_it_asked_chain_steps_included()
    {
        var look = Look(
        [
            Quest("q1", "ask #a1", status: "Done"),
            Quest("q2", "ask #a1") with { Parent = "q1" },
            Quest("q3", "ask #a2"),
            Quest("q4", "game"),
        ]);

        var work = AskWork.Read(look, WorkScope.Ask, "a1", Held);

        Assert.Equal(["q1", "q2"], work.Quests.Select(quest => quest.Quest.Id));
        Assert.All(work.Quests, quest => Assert.Equal(WorkJoin.Asked, quest.Joined));
        Assert.All(work.Quests, quest => Assert.Null(quest.By));
        Assert.True(work.Has("Q2"));
        Assert.False(work.Has("q3"));
        Assert.Equal(WorkScope.Ask, work.Scope);
        Assert.Equal("a1", work.Id);

        // An ask is named as a person writes it: `#` and case aside.
        Assert.Equal(["q1", "q2"], AskWork.Read(look, WorkScope.Ask, "#A1", Held).Quests.Select(quest => quest.Quest.Id));
    }

    /// <summary>A quest's work starts from that quest alone: a chain's next step is a quest of its own, asked by the ask.</summary>
    [Fact]
    public void A_quests_work_starts_from_that_quest_and_a_chains_next_step_is_not_in_it()
    {
        var look = Look([Quest("q1", "ask #a1", status: "Done"), Quest("q2", "ask #a1") with { Parent = "q1" }]);

        var work = AskWork.Read(look, WorkScope.Quest, "q1", Held);

        var only = Assert.Single(work.Quests);
        Assert.Equal("q1", only.Quest.Id);
        Assert.Equal(WorkJoin.Named, only.Joined);
        Assert.Equal(["q2"], AskWork.Read(look, WorkScope.Quest, "#Q2", Held).Quests.Select(quest => quest.Quest.Id));
    }

    /// <summary>
    /// A question a session of the work asked of another repository (D79) is in the work, named by the session that
    /// published it (SESS1), and a question that question's session asked is too, all the way down. A session outside the
    /// work brings nothing in.
    /// </summary>
    [Fact]
    public void A_question_a_session_of_the_work_asked_joins_it_and_so_on_down()
    {
        var look = Look(
            [
                Quest("q1", "ask #a1", status: "Taken") with { Awaits = "q2" },
                Quest("q2", "engine", to: "game", status: "Taken") with { PublishedBy = "s-1", Awaits = "q3" },
                Quest("q3", "game", to: "tools") with { PublishedBy = "s-2" },
                Quest("q8", "tools", to: "game"),
                Quest("q9", "tools", to: "engine") with { PublishedBy = "s-8" },
            ],
            [
                Session("s-1", "q1", "awaiting-reply"),
                Session("s-2", "q2", "working", repository: "game", at: 5),
                Session("s-8", "q8", repository: "tools", at: 9),
            ]);

        var work = AskWork.Read(look, WorkScope.Ask, "a1", Held);

        Assert.Equal(["q1", "q2", "q3"], work.Quests.Select(quest => quest.Quest.Id));
        Assert.Equal([WorkJoin.Asked, WorkJoin.Published, WorkJoin.Published], work.Quests.Select(quest => quest.Joined));
        Assert.Equal([null, "s-1", "s-2"], work.Quests.Select(quest => quest.By));
        Assert.Equal(["s-1", "s-2"], work.Sessions.Select(session => session.Record.Id));

        // A quest's work takes its questions too, and never the quest that asked it.
        Assert.Equal(["q2", "q3"], AskWork.Read(look, WorkScope.Quest, "q2", Held).Quests.Select(quest => quest.Quest.Id));
    }

    /// <summary>
    /// The ask's intake (D65 §1b) is a session of its work, and what it published is asked by the ask. A quest's work has no
    /// intake, and another ask's intake is never this one's.
    /// </summary>
    [Fact]
    public void The_asks_intake_is_in_its_work_and_never_in_a_quests()
    {
        var look = Look(
            [
                Quest("q1", "ask #a1") with { PublishedBy = "s-in" },
                Quest("q2", "ask #a2") with { PublishedBy = "s-in2" },
            ],
            [
                Session("s-in", null, repository: "ask #a1", ask: "a1"),
                Session("s-in2", null, repository: "ask #a2", ask: "a2", at: 3),
                Session("s-1", "q1", "working", at: 6),
            ]);

        var work = AskWork.Read(look, WorkScope.Ask, "A1", Held);

        Assert.Equal(["q1"], work.Quests.Select(quest => quest.Quest.Id));
        Assert.Equal(["s-in", "s-1"], work.Sessions.Select(session => session.Record.Id));
        Assert.Equal([true, false], work.Sessions.Select(session => session.Intake));
        Assert.Equal(["s-in"], work.Intake.Select(session => session.Record.Id));

        var quest = AskWork.Read(look, WorkScope.Quest, "q1", Held);
        Assert.Equal(["s-1"], quest.Sessions.Select(session => session.Record.Id));
        Assert.Empty(quest.Intake);
    }

    /// <summary>
    /// A teammate's record of the work (SYNC4) is named, and holds no tree or branch here: its process and its tree are on
    /// their machine (D47 §4), whatever path its record carries. A question their session asked joins the work: the quest
    /// names the session as their machine spells it, and the record here is keyed by their machine first.
    /// </summary>
    [Fact]
    public void A_teammates_record_is_named_and_holds_no_tree_here()
    {
        var look = Look(
            [
                Quest("q1", "ask #a1", status: "Taken"),
                Quest("q5", "engine", to: "game") with { PublishedBy = "s-9" },
            ],
            [Session("laptop/s-9", "q1", "working", tree: TreeA)]);

        var work = AskWork.Read(look, WorkScope.Ask, "a1", Held);

        var teammate = Assert.Single(work.Sessions);
        Assert.True(teammate.Teammate);
        Assert.Null(teammate.Tree);
        Assert.Null(teammate.Branch);
        Assert.Empty(work.Trees);
        Assert.Equal(["q1", "q5"], work.Quests.Select(quest => quest.Quest.Id));
        Assert.Equal("laptop/s-9", work.Quests[1].By);
    }

    /// <summary>
    /// Each session's tree and branch on this machine (D51): the branch is <c>daoris/</c> and the tree's folder name. A tree
    /// several sessions went back into is one tree; a session that ran in the registered root opened none; and every
    /// landing that names a session of the work is in it, a trace included (D102, D113).
    /// </summary>
    [Fact]
    public void Each_sessions_tree_and_branch_here_and_the_landings_that_name_one()
    {
        var look = Look(
            [Quest("q1", "ask #a1", status: "Done"), Quest("q2", "ask #a1", to: "game", status: "Taken"), Quest("q3", "ask #a1")],
            [
                Session("s-1", "q1", "failed", tree: TreeA),
                Session("s-2", "q1", tree: TreeA.Replace('/', '\\') + "\\", at: 10),
                Session("s-3", "q2", "stopped", repository: "game", tree: TreeB, at: 20),
                Session("s-4", "q3", tree: Root, at: 30),
                Session("s-x", "q7", tree: "X:/daoris/trees/aurora/engine/s-00000000", at: 40),
            ],
            [
                Landing("s-2", "feature/q1-the-work", gone: T0.AddHours(2)),
                Landing("s-x", "feature/q7-other"),
            ]);

        var work = AskWork.Read(look, WorkScope.Ask, "a1", Held);

        Assert.Equal(["s-1", "s-2", "s-3", "s-4"], work.Sessions.Select(session => session.Record.Id));
        Assert.Equal(["daoris/s-1a2b3c4d", "daoris/s-1a2b3c4d", "daoris/s-9f8e7d6c", null], work.Sessions.Select(session => session.Branch));

        Assert.Equal(2, work.Trees.Count);
        Assert.Equal(("engine", "daoris/s-1a2b3c4d"), (work.Trees[0].Repository, work.Trees[0].Branch));
        Assert.Equal(TreeA, work.Trees[0].Path);
        Assert.Equal(["s-1", "s-2"], work.Trees[0].Sessions);
        Assert.Equal(("game", "daoris/s-9f8e7d6c", TreeB), (work.Trees[1].Repository, work.Trees[1].Branch, work.Trees[1].Path));
        Assert.Equal(["s-3"], work.Trees[1].Sessions);

        var landing = Assert.Single(work.Landings);
        Assert.Equal(("s-2", "feature/q1-the-work"), (landing.Session, landing.Branch));
        Assert.NotNull(landing.GoneAt);
    }

    /// <summary>An ask that asked nothing here, and a quest the service does not list, have no work: nothing to name.</summary>
    [Fact]
    public void What_the_service_does_not_list_has_no_work()
    {
        var look = Look([Quest("q1", "ask #a1")], [Session("s-1", "q1")]);

        Assert.True(AskWork.Read(look, WorkScope.Ask, "a9", Held) is { Quests.Count: 0, Sessions.Count: 0, Trees.Count: 0, Landings.Count: 0 });
        Assert.True(AskWork.Read(look, WorkScope.Quest, "q9", Held) is { Quests.Count: 0, Sessions.Count: 0 });
        Assert.Throws<ArgumentException>(() => AskWork.Read(look, WorkScope.Ask, " # ", Held));
    }

    /// <summary>
    /// The work is read again at every look and never kept (design §1): a question asked a moment after one look is in the
    /// next.
    /// </summary>
    [Fact]
    public void A_question_asked_after_one_look_is_in_the_next()
    {
        QuestView[] before = [Quest("q1", "ask #a1", status: "Taken")];
        SessionRecord[] records = [Session("s-1", "q1", "working")];

        Assert.Equal(["q1"], AskWork.Read(Look(before, records), WorkScope.Ask, "a1", Held).Quests.Select(quest => quest.Quest.Id));
        Assert.Equal(["q1", "q2"], AskWork.Read(
            Look([.. before, Quest("q2", "engine", to: "game") with { PublishedBy = "s-1" }], records),
            WorkScope.Ask, "a1", Held).Quests.Select(quest => quest.Quest.Id));
    }

    /// <summary>A path no folder could have is no tree of this home's, as the session list reads it.</summary>
    [Fact]
    public void A_tree_the_predicate_cannot_judge_is_no_tree_here()
    {
        var look = Look([Quest("q1", "ask #a1")], [Session("s-1", "q1", tree: TreeA)]);

        var work = AskWork.Read(look, WorkScope.Ask, "a1", _ => throw new ArgumentException("illegal characters"));

        Assert.Null(Assert.Single(work.Sessions).Tree);
        Assert.Empty(work.Trees);
    }

    /// <summary>
    /// The driver shares no code with the service, so it spells an ask's sender again: held to the service's own source, so
    /// a sender changed on one side alone fails here rather than leaving every ask with no work.
    /// </summary>
    [Fact]
    public void An_ask_is_named_as_the_sender_the_service_spells()
    {
        var service = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Service", "Daoris.Service.Core", "Asks.cs"));

        Assert.Contains("SenderOf(string askId) => $\"ask #{askId}\";", service, StringComparison.Ordinal);
        Assert.Equal("ask #a1b2c3", AskWork.SenderOf("a1b2c3"));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("could not find the repository root");
    }

    /// <summary>The service answers which session published a quest (SESS1); the driver reads it onto its view, and absent is none.</summary>
    [Fact]
    public async Task The_service_answers_which_session_published_a_quest()
    {
        using var client = new ServiceClient("http://service.example", null, new HttpClient(new Canned("""
            [{ "id": "q2", "from": "engine", "to": "game", "title": "t", "body": "b", "status": "Open", "publishedBy": "s-1" },
             { "id": "q1", "from": "ask #a1", "to": "engine", "title": "t", "body": "b", "status": "Taken" }]
            """)));

        var quests = await client.EveryQuestAsync();

        Assert.Equal("s-1", quests.Single(quest => quest.Id == "q2").PublishedBy);
        Assert.Null(quests.Single(quest => quest.Id == "q1").PublishedBy);
    }

    /// <summary>Answers the quests door with what it was given.</summary>
    private sealed class Canned(string quests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/api/quests"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(quests, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{ "error": "no" }""") });
    }
}
