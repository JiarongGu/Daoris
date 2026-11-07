using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The conversation's notes of where the person is and of a browser withheld are worded by code, the English each
/// has always been (the transcript and Ask Daoris read it), the agent's and the program's words riding as parts of their own.
/// </summary>
public sealed class ChatNoteCodeTests
{
    private static readonly ServerEntry Attached = new(
        "browser", "npx", ["--cdp-endpoint", "${browser}"], new Dictionary<string, string>());

    private sealed class Browser(Func<string?> answer) : IInAppBrowser
    {
        public Task<string?> EnsureAsync(CancellationToken ct = default) => Task.FromResult(answer());

        public void Show() { }

        public void Open(string address) { }
    }

    [Fact]
    public void Where_the_person_is_is_a_coded_lead_and_the_preface_as_the_programs_words()
    {
        var told = ChatRunner.Told(new ChatMessage("why is this parked?", []) { Preface = "Where the person is now: the Sessions view." })!;

        Assert.Equal(SessionEventKind.Note, told.Kind);
        Assert.Equal("told where the person is: Where the person is now: the Sessions view.", told.Text);
        Assert.Equal(["chat.told-where", null], told.Parts!.Select(part => part.Code).ToArray());
        Assert.Equal(("Where the person is now: the Sessions view.", NoteBy.Program), (told.Parts![1].Words, told.Parts![1].By));
        NoteAssert.Holds(told.Text, told.Parts);
        Assert.Null(ChatRunner.Told(new ChatMessage("hello", [])));
    }

    [Fact]
    public async Task A_host_with_no_browser_says_so_by_code_with_the_English_it_always_had()
    {
        var (_, notice, drives, _) = await InAppBrowserServers.HandNotedAsync([Attached], null, CancellationToken.None);

        Assert.False(drives);
        Assert.Equal(
            "— `browser` drives Daoris's own browser, and was not handed: this host carries no in-app browser — only the desktop shell does (D78).",
            notice!.Note);
        Assert.Equal(["browser.withheld", "browser.no-host"], notice.Parts.Select(part => part.Code).ToArray());
        Assert.Equal(["browser"], (IReadOnlyList<string>)notice.Parts[0].Value("servers")!);
        NoteAssert.Holds(notice.Note, notice.Parts);
    }

    [Fact]
    public async Task A_shell_with_no_browser_to_bring_up_and_one_that_failed_say_why()
    {
        var (_, none, _, _) = await InAppBrowserServers.HandNotedAsync([Attached], new Browser(() => null), CancellationToken.None);
        Assert.Equal(["browser.withheld", "browser.none-up"], none!.Parts.Select(part => part.Code).ToArray());
        Assert.Contains("the shell answered that it has no browser to bring up (D78).", none.Note);
        NoteAssert.Holds(none.Note, none.Parts);

        // The exception's message is a program's words: it may hold a path, so never a value.
        var (_, failed, _, _) = await InAppBrowserServers.HandNotedAsync(
            [Attached], new Browser(() => throw new InvalidOperationException("the in-app browser was closed.")), CancellationToken.None);
        Assert.Equal(["browser.withheld", null], failed!.Parts.Select(part => part.Code).ToArray());
        Assert.Equal(("the in-app browser was closed.", NoteBy.Program), (failed.Parts[1].Words, failed.Parts[1].By));
        Assert.EndsWith("was not handed: the in-app browser was closed. (D78).", failed.Note);
        NoteAssert.Holds(failed.Note, failed.Parts);
    }

    private static readonly ServerEntry Elsewhere = new("browser", "node", ["other.js"], new Dictionary<string, string>());

    [Fact]
    public async Task A_handed_browser_names_its_server_and_a_left_out_or_unneeded_one_names_none()
    {
        var (_, notice, drives, drivers) = await InAppBrowserServers.HandNotedAsync(
            [Elsewhere, Attached with { Name = "daoris-web" }], new Browser(() => "http://127.0.0.1:4810"), CancellationToken.None);
        Assert.Null(notice);
        Assert.True(drives);
        // By what drives Daoris's browser, never by the literal name another plugin's server also carries.
        Assert.Equal(["daoris-web"], drivers);

        var (_, _, _, left) = await InAppBrowserServers.HandNotedAsync([Attached], null, CancellationToken.None);
        Assert.Empty(left);
        var (_, _, _, none) = await InAppBrowserServers.HandNotedAsync([Elsewhere], new Browser(() => "x"), CancellationToken.None);
        Assert.Empty(none);
    }

    [Fact]
    public void A_chats_agent_is_told_the_browser_it_was_handed_by_name_and_nothing_where_none_was()
    {
        Assert.Null(InAppBrowserServers.Told([]));
        var one = InAppBrowserServers.Told(["browser"])!;
        Assert.Equal(
            "Daoris has handed you its own browser as the MCP server `browser`: it drives the browser window the person signs in to and watches, so use its tools for any web page, including one behind the person's sign-in.",
            one);
        var two = InAppBrowserServers.Told(["a", "b"])!;
        Assert.Contains("the MCP servers `a` and `b`", two);
    }

    [Fact]
    public void The_line_leads_the_first_prompt_as_its_own_field_and_is_kept_as_a_note_of_its_own()
    {
        var line = InAppBrowserServers.Told(["browser"])!;
        var message = new ChatMessage("open the board", []) { Briefing = line, Preface = "Where the person is now: the Sessions view." };

        Assert.Equal($"{line}\n\nWhere the person is now: the Sessions view.\n\nopen the board", message.Prompt);
        Assert.Equal("open the board", new ChatMessage("open the board", []).Prompt);

        // The preface keeps no note of its own, and the line has one (the person sees what the agent is told).
        Assert.Null(ChatRunner.Told(new ChatMessage("open the board", []) { Briefing = line }));
        var told = ChatRunner.ToldBrowser(message)!;
        Assert.Equal(SessionEventKind.Note, told.Kind);
        Assert.Equal(["chat.told-browser", null], told.Parts!.Select(part => part.Code).ToArray());
        Assert.Equal((line, NoteBy.Program), (told.Parts![1].Words, told.Parts![1].By));
        NoteAssert.Holds(told.Text, told.Parts);
        Assert.Null(ChatRunner.ToldBrowser(new ChatMessage("hello", [])));
    }

    private static ProcessBriefing Process(string mode = CommitRules.Ask, string source = OpinionSource.Repository) =>
        ChatRunner.ProcessTold(
            new ResolvedCommits(new CommitRule(mode, null), source), new LandingPlan(LandingForm.Merge, "main", LandingSource.Default));

    /// <summary>
    /// A conversation in a tree of its own is told its repository's process in its first message, after the browser's
    /// line and before where the person is, in a field of its own: its words are <see cref="ProcessWords.Conversation"/>'s, its
    /// facts the mode and where the rule is set.
    /// </summary>
    [Fact]
    public void The_process_rides_the_first_prompt_after_the_browser_and_before_where_the_person_is()
    {
        var process = Process();
        Assert.Equal(ProcessWords.Conversation(
            new ResolvedCommits(new CommitRule(CommitRules.Ask, null), OpinionSource.Repository),
            new LandingPlan(LandingForm.Merge, "main", LandingSource.Default)), process.Words);
        Assert.Equal((CommitRules.Ask, OpinionSource.Repository), (process.Mode, process.Source));

        var line = InAppBrowserServers.Told(["browser"])!;
        var message = new ChatMessage("open the board", []) { Briefing = line, Process = process, Preface = "Where the person is now: the Sessions view." };
        Assert.Equal($"{line}\n\n{process.Words}\n\nWhere the person is now: the Sessions view.\n\nopen the board", message.Prompt);
        Assert.Equal($"{process.Words}\n\nopen the board", new ChatMessage("open the board", []) { Process = process }.Prompt);
    }

    /// <summary>
    /// A conversation whose tree has its dependencies copied is told so after its process, in the same field, so
    /// it does not run the install that would delete the copy; where nothing is copied the process words stand alone.
    /// </summary>
    [Fact]
    public void A_copy_of_the_dependencies_is_told_after_the_process_and_otherwise_nothing_changes()
    {
        var commits = new ResolvedCommits(new CommitRule(CommitRules.Ask, null), OpinionSource.Repository);
        var plan = new LandingPlan(LandingForm.Merge, "main", LandingSource.Default);
        var bare = ChatRunner.ProcessTold(commits, plan);
        const string copied = "Its dependencies were copied; do not run npm ci.";

        var told = ChatRunner.ProcessTold(commits, plan, copied);
        Assert.Equal($"{bare.Words}\n\n{copied}", told.Words);
        Assert.Equal((bare.Mode, bare.Source), (told.Mode, told.Source));
        Assert.Equal(bare.Words, ChatRunner.ProcessTold(commits, plan, null).Words);
        Assert.Equal(bare.Words, ChatRunner.ProcessTold(commits, plan, "").Words);
        Assert.Equal($"{told.Words}\n\nhello", new ChatMessage("hello", []) { Process = told }.Prompt);
    }

    /// <summary>The process is kept as a note of its own, by its code with its facts, never under the browser's lead.</summary>
    [Fact]
    public void The_process_told_is_kept_as_a_note_of_its_own_with_its_facts()
    {
        var process = Process(CommitRules.AtEnd, OpinionSource.Workspace);
        var told = ChatRunner.ToldProcess(new ChatMessage("hello", []) { Process = process })!;

        Assert.Equal(SessionEventKind.Note, told.Kind);
        Assert.Equal(["chat.told-process", null], told.Parts!.Select(part => part.Code).ToArray());
        Assert.Equal(CommitRules.AtEnd, told.Parts![0].Value("mode"));
        Assert.Equal(OpinionSource.Workspace, told.Parts![0].Value("source"));
        Assert.Equal((process.Words, NoteBy.Program), (told.Parts![1].Words, told.Parts![1].By));
        NoteAssert.Holds(told.Text, told.Parts);

        Assert.Null(ChatRunner.ToldProcess(new ChatMessage("hello", [])));
        Assert.Null(ChatRunner.ToldBrowser(new ChatMessage("hello", []) { Process = process }));
    }

    /// <summary>What the first message puts in the record, in its prompt's order: the browser, the process, where the person is, their words.</summary>
    [Fact]
    public void The_first_message_records_the_browser_the_process_where_and_the_words_in_order()
    {
        var message = new ChatMessage("open the board", [])
        {
            Briefing = InAppBrowserServers.Told(["browser"]), Process = Process(), Preface = "Where the person is now: the Sessions view.",
        };

        var codes = ChatRunner.Recorded(message).Select(each => each.Parts?[0].Code ?? each.Origin).ToArray();
        Assert.Equal(["chat.told-browser", "chat.told-process", "chat.told-where", "person"], codes);
    }

    /// <summary>The standing block a chat in `engine` is told, its routing one at a time.</summary>
    private static StandingBlockTold Block() =>
        StandingBlock.Compose(StandingBlocks.OnMachine(
            new StandingFacts { Repository = "engine" }, ParticipantWork.Chat, DriverConfig.Empty.WithDrivable("engine", true),
            [new RepoView("engine", true, "/work/engine") { Lanes = ["core"] }], "work"));

    /// <summary>
    /// The standing block rides the first prompt last, after the person's words, in a field of its own, the
    /// routing among it, and is kept as a note of its own in the prompt's order, beside a hand-over's brief too.
    /// </summary>
    [Fact]
    public void The_standing_block_rides_last_and_is_kept_as_a_note_of_its_own_in_order()
    {
        var block = Block();
        Assert.Contains("Routing standing here: one at a time.", block.Text, StringComparison.Ordinal);
        var message = new ChatMessage("open the board", [])
        {
            Briefing = InAppBrowserServers.Told(["browser"]), Process = Process(), Block = block,
            Preface = "Where the person is now: the Sessions view.",
        };
        // The block is the last thing the agent reads, after the person's words.
        Assert.Equal(
            $"{message.Briefing}\n\n{message.Process!.Words}\n\nWhere the person is now: the Sessions view.\n\nopen the board\n\n{block.Text}", message.Prompt);
        Assert.Equal($"open the board\n\n{block.Text}", new ChatMessage("open the board", []) { Block = block }.Prompt);

        var told = ChatRecorded.ToldBlock(block)!;
        Assert.Equal(SessionEventKind.Note, told.Kind);
        Assert.Equal(["chat.told-block", null], told.Parts!.Select(part => part.Code).ToArray());
        Assert.Equal((block.Text, NoteBy.Program), (told.Parts![1].Words, told.Parts![1].By));
        NoteAssert.Holds(told.Text, told.Parts);
        Assert.Null(ChatRecorded.ToldBlock(null));

        var codes = ChatRunner.Recorded(message).Select(each => each.Parts?[0].Code ?? each.Origin).ToArray();
        Assert.Equal(["chat.told-browser", "chat.told-process", "chat.told-block", "chat.told-where", "person"], codes);

        // A hand-over's briefing is Daoris's, never the person's: the note is kept beside the note of where it came from.
        var handed = new ChatMessage("", []) { TakenOver = Noted.Of(ChatNoteCodes.ChatToldBrowser, "taken over"), Block = block };
        Assert.Contains(ChatRunner.Recorded(handed), each => each.Parts?[0].Code == "chat.told-block");
        Assert.DoesNotContain(ChatRunner.Recorded(handed), each => each.Parts?[0].Code == "chat.told-routing");
    }

    /// <summary>
    /// A go-on resumes the same conversation, whose first message carried the standing block (the resume contract): it is handed its words
    /// and its appendix alone, and its opening keeps no block's note.
    /// </summary>
    [Fact]
    public void A_go_on_is_handed_its_words_and_its_appendix_and_never_the_standing_block_again()
    {
        var words = new[] { new SaidWordView("w1", "Also log the port.", DateTimeOffset.UtcNow, [], Reopens: true) };
        var ask = new ResumeAsk("conv-1", words, "resumed", appendix: "tree brought up");

        Assert.Equal(["Also log the port.", "tree brought up"], ask.Blocks);
        Assert.Equal("Also log the port.\n\ntree brought up", ask.Prompt);
        Assert.DoesNotContain(ask.Opening(), each => each.Parts?.Any(part => part.Code is "chat.told-routing" or "chat.told-block") == true);

        var plain = new ResumeAsk("conv-1", words, "resumed");
        Assert.Equal(["Also log the port."], plain.Blocks);
        Assert.Equal("Also log the port.", plain.Prompt);
    }

    [Fact]
    public async Task Nothing_withheld_says_nothing_and_the_string_door_reads_the_same_English()
    {
        var (_, quiet, drives, _) = await InAppBrowserServers.HandNotedAsync([], null, CancellationToken.None);
        Assert.Null(quiet);
        Assert.False(drives);

        var (_, notice, _) = await InAppBrowserServers.HandAsync([Attached], null, CancellationToken.None);
        Assert.Contains("only the desktop shell does (D78).", notice);
    }
}
