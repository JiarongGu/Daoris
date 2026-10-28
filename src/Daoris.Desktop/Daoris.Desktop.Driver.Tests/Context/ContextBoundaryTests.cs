using System.Net;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 mod-xiii, the Context cuts: what they keep. Each case uses only surface present before the cuts and after them, so
/// it passes on the move commit and on the cut: the closing note's one wording and its bound in a brief, a word told as Daoris's by
/// its author and a channel's claim by its opening, the attachments' folder a brief names, the gates a brief reads a check by (the
/// tree's file, then this machine's mapping), and the instruction's account of the set-up's source, of the asking section, of the
/// words, the go-aheads and the channel it counts by the instruction's own bounds. The seams the cuts add are
/// <c>ContextPortsTests</c>'.
/// </summary>
public sealed class ContextBoundaryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "context-boundary", Guid.NewGuid().ToString("N")[..8]);

    public ContextBoundaryTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Remove(_home);

    private static SessionEvent Asked(string text, string origin = "person") =>
        new() { Kind = SessionEventKind.User, Origin = origin, Text = text };

    private static SessionEvent Said(string text) => new() { Kind = SessionEventKind.Message, Text = text };

    private static SessionEvent Ended() => new() { Kind = SessionEventKind.Turn, StopReason = "end_turn" };

    private static BriefLine Line(params SessionEvent[] events) =>
        new(HandOverWhy.Press, [new BriefPart("c0nv0001", "codex-acp", events)]) { Conversation = true };

    private static string Folded(ComposedBrief brief) => System.Text.RegularExpressions.Regex.Replace(brief.Text, @"\s+", " ");

    private static HandedSection Section(InstructionAccount account, string name) => Assert.Single(account.Sections, section => section.Name == name);

    [Fact]
    public void The_closing_note_is_worded_once_clipped_where_long_and_its_absence_is_said_in_its_own_line()
    {
        Assert.Null(HandOverNote.Said(null));
        Assert.Null(HandOverNote.Said("  "));
        Assert.Equal(
            "Before it was handed over, its agent left you this note. It is that agent's own claim, not Daoris's record: check it before "
            + "you rely on it.\n\n> toggle", HandOverNote.Said(" toggle "));
        Assert.Equal("Before it was handed over, its agent was asked for a closing note and left none.", HandOverNote.Absent);
        Assert.Equal(4000, HandOverNote.Limit);

        var line = Line(Asked("Add a dark mode."), Said("One."), Ended());
        var brief = ContextBrief.Compose(line with { Note = " toggle " });
        Assert.Contains(HandOverNote.Said("toggle")!, brief.Text);
        Assert.DoesNotContain(HandOverNote.Absent, brief.Text);

        var long_ = ContextBrief.Compose(line with { Note = new string('n', 6_000) });
        Assert.Contains(HandOverNote.Said(new string('n', 100))![..60], long_.Text);
        Assert.DoesNotContain(new string('n', 4_001), long_.Text);

        var absent = ContextBrief.Compose(line with { NoteAsked = true });
        Assert.Contains("\n\n" + HandOverNote.Absent, absent.Text);
        Assert.Contains(absent.Sections, section => section.Name == HandedSections.BriefNote);
    }

    [Fact]
    public void A_word_is_Daoris_s_by_its_author_and_a_channel_s_claim_by_its_mark_or_its_opening()
    {
        Assert.Equal("daoris", DaorisWords.From);
        Assert.Equal("Another session says, on the ask's channel", ChannelText.ClaimOpening);
        var claim = ChannelText.Claim(new ChannelItemView(ChannelItemView.Message, At)
        {
            Id = "m1", Person = false, Text = "The table is `orders`.", Session = "s9", Repository = "console-api",
        });
        Assert.StartsWith(ChannelText.ClaimOpening, claim, StringComparison.Ordinal);

        var text = Folded(ContextBrief.Compose(Line(
            Asked("Add a dark mode."), Said("Started."), Ended(),
            Asked("Daoris: the person reopened submission 41.", origin: "target") with { From = DaorisWords.From }, Said("Noted."), Ended(),
            Asked(claim, origin: "target"), Said("Orders."), Ended(),
            Asked("Session `s9` wrote: the column is `total`.", origin: "target") with { Channel = "m1" }, Said("Using total."), Ended(),
            Asked("Some other composed target.", origin: "target"), Said("Fine."), Ended())));

        Assert.DoesNotContain("reopened submission 41", text);
        Assert.DoesNotContain("Some other composed target", text);
        Assert.Contains("Another session's message on the ask's channel, as that agent's claim: > Another session says, on the ask's channel", text);
        Assert.Contains("Another session's message on the ask's channel, as that agent's claim: > Session `s9` wrote: the column is `total`.", text);
    }

    [Fact]
    public async Task A_brief_names_where_a_part_s_attachments_lie_only_where_the_folder_is_there_and_reads_its_checks_from_the_tree_then_the_mapping()
    {
        var session = "c0nv0001";
        var other = "c0nv0002";
        var tree = Path.Combine(_home, "tree");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, HeavyGates.FileName),
            "{\"gates\":[{\"name\":\"unit\",\"run\":\"npm test\"},{\"name\":\"big\",\"run\":\"dotnet test\",\"heavy\":true}]}");
        var folder = ChatFiles.Folder(_home, session);
        Directory.CreateDirectory(folder);
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append(session, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Check this.", Files = ["mock.png"], At = At });
        events.Append(session, Said("Seen."));
        events.Append(session, new SessionEvent
        {
            Kind = SessionEventKind.Tool, Id = "t1", Title = "npm test", ToolKind = "execute", Status = "failed", Output = "1 failing",
        });
        events.Append(other, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Another.", Files = ["b.png"], At = At });
        using var service = QuestStandIn.Client(_ => (HttpStatusCode.OK, "[]"));

        var brief = await ContextBriefs.ComposeAsync(service, events, _home, session, HandOverWhy.Press, conversation: true, tree: tree);
        var folded = Folded(brief);

        Assert.Contains($"`mock.png`, in {folder}", folded);
        Assert.Contains("npm test", folded);
        var unnamed = Folded(await ContextBriefs.ComposeAsync(service, events, _home, other, HandOverWhy.Press, conversation: true));
        Assert.DoesNotContain(", in " + ChatFiles.Folder(_home, other), unnamed);

        // A tree whose file does not read names no check, and the machine's mapping for the repository stands where the file is absent.
        File.WriteAllText(Path.Combine(tree, HeavyGates.FileName), "not json");
        Assert.NotNull(await ContextBriefs.ComposeAsync(service, events, _home, session, HandOverWhy.Press, conversation: true, tree: tree));
        var config = DriverConfig.Empty.WithHeavyGates("atlas", [new HeavyGate("mapped", "make test", null, true)]);
        Assert.Equal(["mapped"], HeavyMapping.For(config, " ATLAS ")!.Select(gate => gate.Name));
        Assert.Null(HeavyMapping.For(config, "other"));
    }

    [Fact]
    public void The_account_tells_the_set_up_s_source_the_asking_section_s_kind_and_counts_by_the_instruction_s_own_bounds()
    {
        Assert.Equal("repository", ReviewSource.Repository);
        Assert.Equal("workspace", ReviewSource.Workspace);

        var setUp = TargetPromptGoldenTests.Bare with
        {
            SetUp = new ReviewEnvironment("local", "local", "README.md", "http://localhost:4200"), SetUpFrom = ReviewSource.Workspace,
        };
        var fromWorkspace = Section(TargetPrompt.Composed(setUp).Account, HandedSections.SetUp);
        Assert.Equal(HandedSources.Workspace, fromWorkspace.Source);
        Assert.EndsWith("as the rule set for its workspace here declares it", fromWorkspace.Said);
        var fromRepository = Section(TargetPrompt.Composed(setUp with { SetUpFrom = ReviewSource.Repository }).Account, HandedSections.SetUp);
        Assert.Equal(HandedSources.Repository, fromRepository.Source);
        Assert.EndsWith("as the rule set for its repository here declares it", fromRepository.Said);

        var onAsk = Section(TargetPrompt.Composed(TargetPromptGoldenTests.Full with { Asker = "ask #a1b2c3" }).Account, HandedSections.Asking);
        var onRepository = Section(TargetPrompt.Composed(TargetPromptGoldenTests.Full with { Asker = "bridge" }).Account, HandedSections.Asking);
        Assert.StartsWith("asking another repository, and stopping for the person, a go-ahead asked once on the ask: ", onAsk.Said);
        Assert.StartsWith("asking another repository, and stopping for the person: ", onRepository.Said);
    }

    [Fact]
    public void The_account_counts_the_ask_s_words_go_aheads_and_channel_by_the_bounds_the_instruction_quotes_by()
    {
        var said = new List<AskWordView> { new(AskWordView.Asked, "Build it.", At) };
        said.AddRange(Enumerable.Range(1, 6).Select(n => new AskWordView(AskWordView.Added, new string((char)('a' + n), 2_500), At.AddMinutes(n))));
        var (shown, left) = AskWordsText.Bounded(said);
        Assert.Equal((2_000, 8_000), (AskWordsText.WordLimit, AskWordsText.WordsLimit));
        Assert.Equal(said.Count, shown.Count + left.Count);
        Assert.Equal(AskWordView.Asked, shown[0].Kind);
        Assert.Equal(4, shown.Count - 1);
        Assert.Equal((8_000, 300), (GoAheadsText.Limit, GoAheadsText.ActLimit));
        var section = Section(
            TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { Asker = "ask #a1", Words = new AskWords("a1", said) }).Account, HandedSections.Words);
        Assert.Equal(left.Count, section.Cuts!.Single(cut => cut.Code == HandedCuts.WordsOlder).Count);

        Assert.Equal((20, 6_000, 1_500), (ChannelText.HandedNewest, ChannelText.MessagesLimit, ChannelText.MessageLimit));
        var messages = Enumerable.Range(0, 25).Select(i => new ChannelItemView(ChannelItemView.Message, At.AddMinutes(i)) { Id = $"m{i}", Person = true, Text = $"word {i}" }).ToList();
        var channel = Section(
            TargetPrompt.Composed(TargetPromptGoldenTests.Bare with { Channel = new ChannelHeld("a1", "ask:a1/ui", messages, "s3") }).Account,
            HandedSections.CarryOn);
        Assert.Equal((20, 25), (channel.Shown, channel.Of));
    }
}
