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
        var (_, notice, drives) = await InAppBrowserServers.HandNotedAsync([Attached], null, CancellationToken.None);

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
        var (_, none, _) = await InAppBrowserServers.HandNotedAsync([Attached], new Browser(() => null), CancellationToken.None);
        Assert.Equal(["browser.withheld", "browser.none-up"], none!.Parts.Select(part => part.Code).ToArray());
        Assert.Contains("the shell answered that it has no browser to bring up (D78).", none.Note);
        NoteAssert.Holds(none.Note, none.Parts);

        // The exception's message is a program's words: it may hold a path, so never a value.
        var (_, failed, _) = await InAppBrowserServers.HandNotedAsync(
            [Attached], new Browser(() => throw new InvalidOperationException("the in-app browser was closed.")), CancellationToken.None);
        Assert.Equal(["browser.withheld", null], failed!.Parts.Select(part => part.Code).ToArray());
        Assert.Equal(("the in-app browser was closed.", NoteBy.Program), (failed.Parts[1].Words, failed.Parts[1].By));
        Assert.EndsWith("was not handed: the in-app browser was closed. (D78).", failed.Note);
        NoteAssert.Holds(failed.Note, failed.Parts);
    }

    [Fact]
    public async Task Nothing_withheld_says_nothing_and_the_string_door_reads_the_same_English()
    {
        var (_, quiet, drives) = await InAppBrowserServers.HandNotedAsync([], null, CancellationToken.None);
        Assert.Null(quiet);
        Assert.False(drives);

        var (_, notice, _) = await InAppBrowserServers.HandAsync([Attached], null, CancellationToken.None);
        Assert.Contains("only the desktop shell does (D78).", notice);
    }
}
