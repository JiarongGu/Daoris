using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONVNOTE2c: the conversation's notes of where the person is and of a browser withheld are worded by code, the English each
/// has always been (the transcript and Ask Daoris read it), the agent's and the program's words riding as parts of their own.
/// </summary>
public sealed class ChatNoteCodeTests
{
    private static readonly AcpMcpServer Attached = new(
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

    private static readonly AcpMcpServer Elsewhere = new("browser", "node", ["other.js"], new Dictionary<string, string>());

    [Fact]
    public async Task A_handed_browser_names_its_server_and_a_left_out_or_unneeded_one_names_none()
    {
        var (_, notice, drives, drivers) = await InAppBrowserServers.HandNotedAsync(
            [Elsewhere, Attached with { Name = "daoris-web" }], new Browser(() => "http://127.0.0.1:4810"), CancellationToken.None);
        Assert.Null(notice);
        Assert.True(drives);
        // By what drives Daoris's browser, never by the literal name another plugin's server also carries (BRWHINT1a).
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

        // The preface's note is as it was, and the line's is its own (the person sees what the agent was told).
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
    /// PROCESS1d-ii: a conversation in a tree of its own is told its repository's process in its first message, after the browser's
    /// line and before where the person is, in a field of its own: its words are <see cref="ProcessWords.Conversation"/>'s, its
    /// facts the mode and where the rule was set.
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
    /// TREEDEPS1b: a conversation whose tree had its dependencies copied is told so after its process, in the same field, so
    /// it does not run the install that would delete the copy; where nothing was copied the words are as they were.
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
