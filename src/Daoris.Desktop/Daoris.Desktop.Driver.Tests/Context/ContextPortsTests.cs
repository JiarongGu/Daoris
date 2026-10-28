using System.Net;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xiii, the Context cuts: the seams they add. The note's wording, the channel claim's opening, the set-up source's
/// word and the attachments' folder owned by Context or a lower home, with each higher spelling held to it; the installed review and
/// work-order operations being the owners' and doing what the brief and the account did, a supplied stand-in used at those points, and
/// a missing operation required only where it is reached. What the cuts keep is <c>ContextBoundaryTests</c>'.
/// </summary>
[Collection("agent operation ports")]
public sealed class ContextPortsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "context-ports", Guid.NewGuid().ToString("N")[..8]);

    public ContextPortsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    [Fact]
    public void The_lower_words_are_spelled_once_and_each_higher_spelling_holds_them()
    {
        Assert.Equal(OutgoingNote.Limit, HandOverNote.Limit);
        Assert.Equal(OutgoingNote.Absent, HandOverNote.Absent);
        Assert.Equal(OutgoingNote.Said(" a note "), HandOverNote.Said(" a note "));
        Assert.Null(OutgoingNote.Said(null));
        Assert.Equal(ContextBrief.ChannelClaimOpening, ChannelText.ClaimOpening);
        Assert.Equal(CommitRules.RepositorySource, ReviewSource.Repository);
        Assert.Equal(CommitRules.WorkspaceSource, ReviewSource.Workspace);
        Assert.Equal(SaidFrom.Daoris, DaorisWords.From);
        Assert.Equal(AskWords.AskOf("ask #a1"), AskSender.AskOf("ask #a1"));
        Assert.Null(AskSender.AskOf("bridge"));
    }

    [Fact]
    public void The_attachments_folder_is_Context_s_and_the_composer_s_answer_is_the_same_place_or_the_same_refusal()
    {
        Assert.Equal(SessionAttachments.Folder(_home, "c0nv0001"), ChatFiles.Folder(_home, "c0nv0001"));
        var refused = Assert.Throws<DriverException>(() => SessionAttachments.Folder(_home, "../x"));
        Assert.Equal(refused.Message, Assert.Throws<DriverException>(() => ChatFiles.Folder(_home, "../x")).Message);
    }

    [Fact]
    public void The_installed_review_operations_are_Review_s_and_read_the_gates_as_the_brief_did()
    {
        Assert.IsType<ContextReviewOperations>(ContextReview.Current);
        Assert.Equal(HeavyGates.FileName, ContextReview.Current.GatesFile);
        var tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, HeavyGates.FileName), "{\"gates\":[{\"name\":\"unit\",\"run\":\"npm test\"}]}");
        Assert.Equal(HeavyGates.Read(tree), ContextReview.Current.DeclaredGates(tree));
        Assert.Throws<DriverException>(() => ContextReview.Current.DeclaredGates(_home));

        var config = DriverConfig.Empty.WithHeavyGates("atlas", [new HeavyGate("mapped", "make test", null, true)]);
        Assert.Equal(HeavyMapping.For(config, " Atlas "), ContextReview.Current.MappedGates(config, " Atlas "));
        Assert.Null(ContextReview.Current.MappedGates(config, "other"));
    }

    [Fact]
    public void The_installed_work_order_operations_are_Work_orders_and_bound_as_the_sections_are_written()
    {
        Assert.IsType<ContextWorkOrdersOperations>(ContextWorkOrders.Current);
        var work = ContextWorkOrders.Current;
        Assert.Equal((AskWordsText.WordsLimit, AskWordsText.WordLimit), (work.WordsLimit, work.WordLimit));
        Assert.Equal((GoAheadsText.Limit, GoAheadsText.ActLimit), (work.GoAheadsLimit, work.GoAheadActLimit));

        var said = new List<AskWordView> { new(AskWordView.Asked, "Build it.", At) };
        said.AddRange(Enumerable.Range(1, 6).Select(n => new AskWordView(AskWordView.Added, new string('w', 2_500), At.AddMinutes(n))));
        var asked = AskWordsText.Bounded(said);
        var answered = work.AskBounded(said);
        Assert.Equal(asked.Shown, answered.Shown);
        Assert.Equal(asked.Left, answered.Left);

        var request = new GoAheadRequestView("s1", "q1", At, "why");
        var held = Enumerable.Range(1, 30).Select(n => new GoAheadView(n, "write", "production", new string('x', 290), [request])).ToList();
        Assert.Equal(GoAheadsText.Shown(held), work.GoAheadsShown(held));

        var messages = Enumerable.Range(0, 25).Select(i => new ChannelItemView(ChannelItemView.Message, At.AddMinutes(i)) { Id = $"m{i}", Text = $"word {i}" }).ToList();
        var bounded = ChannelText.Bounded(messages);
        var through = work.ChannelBounded(messages);
        Assert.Equal(bounded.Shown, through.Shown);
        Assert.Equal(bounded.Left, through.Left);
    }

    private sealed class WorkOrders : IContextWorkOrders
    {
        public List<string> Calls { get; } = [];
        public int WordsLimit { get { Calls.Add("words-limit"); return 123; } }
        public int WordLimit { get { Calls.Add("word-limit"); return 45; } }
        public int GoAheadsLimit { get { Calls.Add("go-aheads-limit"); return 678; } }
        public int GoAheadActLimit { get { Calls.Add("act-limit"); return 9; } }

        public (IReadOnlyList<AskWordView> Shown, IReadOnlyList<AskWordView> Left) AskBounded(IReadOnlyList<AskWordView> said)
        {
            Calls.Add("ask-bounded");
            return ([said[0]], [.. said.Skip(1)]);
        }

        public int GoAheadsShown(IReadOnlyList<GoAheadView> held)
        {
            Calls.Add("go-aheads-shown");
            return 1;
        }

        public (IReadOnlyList<ChannelItemView> Shown, IReadOnlyList<ChannelItemView> Left) ChannelBounded(IReadOnlyList<ChannelItemView> messages)
        {
            Calls.Add("channel-bounded");
            return ([messages[^1]], [.. messages.Take(messages.Count - 1)]);
        }
    }

    [Fact]
    public void A_supplied_work_order_operation_counts_the_account_s_sections_only_where_they_are_reached()
    {
        var actual = ContextWorkOrders.Current;
        try
        {
            var stand = new WorkOrders();
            ContextWorkOrders.Current = stand;

            // Nothing on the ask: no section is counted, so no operation is reached.
            _ = TargetPrompt.Composed(TargetPromptGoldenTests.Bare).Account;
            Assert.Empty(stand.Calls);

            var said = new List<AskWordView> { new(AskWordView.Asked, "Build it.", At), new(AskWordView.Added, "More.", At.AddMinutes(1)) };
            var request = new GoAheadRequestView("s1", "q1", At, "why");
            var held = new List<GoAheadView> { new(1, "write", "production", "one", [request]), new(2, "push", "origin", new string('a', 50), [request]) };
            var target = TargetPromptGoldenTests.Bare with { Asker = "ask #a1", Words = new AskWords("a1", said) { GoAheads = held } };
            var account = TargetPrompt.Composed(target).Account;

            var words = Assert.Single(account.Sections, section => section.Name == HandedSections.Words);
            var older = Assert.Single(words.Cuts!, cut => cut.Code == HandedCuts.WordsOlder);
            Assert.Equal((1, 123), (older.Count, older.Limit));
            var goAheads = Assert.Single(account.Sections, section => section.Name == HandedSections.GoAheads);
            var bound = Assert.Single(goAheads.Cuts!, cut => cut.Code == HandedCuts.GoAheadsBound);
            Assert.Equal((1, 678), (bound.Count, bound.Limit));
            Assert.Contains("ask-bounded", stand.Calls);
            Assert.Contains("go-aheads-shown", stand.Calls);
        }
        finally { ContextWorkOrders.Current = actual; }
    }

    [Fact]
    public void A_supplied_channel_operation_bounds_the_carry_on_section_and_its_fact()
    {
        var actual = ContextWorkOrders.Current;
        try
        {
            var stand = new WorkOrders();
            ContextWorkOrders.Current = stand;
            var messages = Enumerable.Range(0, 3).Select(i => new ChannelItemView(ChannelItemView.Message, At.AddMinutes(i)) { Id = $"m{i}", Person = true, Text = $"word {i}" }).ToList();

            var account = TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { Channel = new ChannelHeld("a1", "ask:a1/ui", messages, "s3") }).Account;

            var section = Assert.Single(account.Sections, each => each.Name == HandedSections.CarryOn);
            Assert.Equal((1, 3), (section.Shown, section.Of));
            Assert.Contains("channel-bounded", stand.Calls);
        }
        finally { ContextWorkOrders.Current = actual; }
    }

    private sealed class Review : IContextReview
    {
        public List<string> Calls { get; } = [];
        public string GatesFile { get { Calls.Add("file"); return "stand-in.gates.json"; } }

        public IReadOnlyList<HeavyGate> DeclaredGates(string checkout)
        {
            Calls.Add("declared:" + Path.GetFileName(checkout));
            return [new HeavyGate("declared", "run declared", null, false)];
        }

        public IReadOnlyList<HeavyGate>? MappedGates(DriverConfig config, string repository)
        {
            Calls.Add("mapped:" + repository);
            return [new HeavyGate("mapped", "run mapped", null, true)];
        }
    }

    [Fact]
    public async Task A_supplied_review_operation_reads_the_gates_where_the_tree_has_its_file_and_the_mapping_for_the_record_s_repository()
    {
        var actual = ContextReview.Current;
        try
        {
            var stand = new Review();
            ContextReview.Current = stand;
            var tree = Path.Combine(_home, "tree");
            Directory.CreateDirectory(tree);
            var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
            events.Append("c0nv0001", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Go.", At = At });
            var records = new JsonArray(new JsonObject { ["id"] = "c0nv0001", ["tree"] = tree, ["state"] = "ended", ["repository"] = "atlas" }).ToJsonString();
            using var service = QuestStandIn.Client(_ => (HttpStatusCode.OK, records));

            // No file at the stand-in's name: the file is not read, the mapping is, once, for the record's repository.
            await ContextBriefs.ComposeAsync(service, events, _home, "c0nv0001", HandOverWhy.Press, conversation: true, config: DriverConfig.Empty);
            Assert.Equal(["file", "mapped:atlas"], stand.Calls);

            stand.Calls.Clear();
            File.WriteAllText(Path.Combine(tree, "stand-in.gates.json"), "{}");
            await ContextBriefs.ComposeAsync(service, events, _home, "c0nv0001", HandOverWhy.Press, conversation: true, config: DriverConfig.Empty);
            Assert.Equal(["file", "declared:tree", "mapped:atlas"], stand.Calls);
        }
        finally { ContextReview.Current = actual; }
    }

    [Fact]
    public void A_missing_operation_is_required_only_where_it_is_reached()
    {
        var review = ContextReview.Current;
        var work = ContextWorkOrders.Current;
        try
        {
            ContextReview.Current = null!;
            ContextWorkOrders.Current = null!;
            Assert.Contains("Review installs them", Assert.Throws<InvalidOperationException>(() => ContextReview.Current).Message);
            Assert.Contains("Work orders installs them", Assert.Throws<InvalidOperationException>(() => ContextWorkOrders.Current).Message);

            // Neither is reached by an instruction with no ask, no go-aheads and no channel, nor by a brief with no tree and no repository.
            Assert.NotEmpty(TargetPrompt.Composed(TargetPromptGoldenTests.Bare).Account.Sections);
            var line = new BriefLine(HandOverWhy.Press, [new BriefPart("c0nv0001", "codex-acp", [new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Go." }])]);
            Assert.NotEmpty(ContextBrief.Compose(line).Text);

            // Where reached, a missing operation says so and the failure propagates.
            var held = new AskWords("a1", [new AskWordView(AskWordView.Asked, "Build it.", At)]);
            Assert.Throws<InvalidOperationException>(() => TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { Asker = "ask #a1", Words = held }));
        }
        finally
        {
            ContextReview.Current = review;
            ContextWorkOrders.Current = work;
        }
    }
}
