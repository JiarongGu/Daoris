using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ASKHIST1 (D50): <c>daoris-driver help</c>, Ask Daoris's history from a terminal, the panel's other door. It lists and searches
/// this machine's Ask Daoris conversations, says words to one through <c>sessions say</c>'s door, names, pins and unpins one, and
/// deletes one listed first. Any other session is refused, naming the verb that acts on it. Files and an in-process ledger only:
/// the suite's fast half.
/// </summary>
public sealed class HelpCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-help-verb-" + Guid.NewGuid().ToString("N")[..8]);

    public HelpCommandTests() => Directory.CreateDirectory(Path.Combine(_home, "sessions"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Events => new(Path.Combine(_home, "sessions"));

    private string Room => HelpRoom.PathOf(_home);

    /// <summary>The ledger with two Ask Daoris conversations, one nobody spoke in, and a repository's chat.</summary>
    private ChatLedger Ledger()
    {
        var ledger = new ChatLedger();
        ledger.Chat("h1", "completed", "claude-code-acp", Room, repository: HelpRoom.Repository);
        ledger.Chat("h2", "stopped", "claude-code-acp", Room, repository: HelpRoom.Repository);
        ledger.Chat("h3", "completed", "claude-code-acp", Room, repository: HelpRoom.Repository);
        ledger.Chat("c1", "completed", "claude-code-acp", Room);
        var events = Events;
        Said(events, "h1", "what is a workspace?", "A circle of repositories that share a remote.");
        Said(events, "h2", "how do I land on a branch?", "Repositories → Setup → Line and landing.");
        Said(events, "c1", "fix the build", "Done.");
        new HarnessConversations(_home).Keep("h1", "claude-code-acp", "conv-h1");
        return ledger;
    }

    private static void Said(SessionEvents events, string id, string question, string answer)
    {
        events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = question });
        events.Append(id, new SessionEvent { Kind = SessionEventKind.Message, Text = answer });
    }

    private async Task<(int Exit, string Said)> RunAsync(ChatLedger ledger, params string[] args)
    {
        var ask = HelpCommand.Read(args, out var problem);
        Assert.True(problem is null, problem);
        using var service = ledger.Client();
        var output = new StringWriter();
        var world = new SessionsWorld(service, _home, DriverConfig.Empty, SessionWire.Acp, null) { LoopRuns = () => false };
        var exit = await HelpCommand.RunAsync(ask!, world, adapter => adapter == "claude-code-acp", output);
        return (exit, output.ToString().ReplaceLineEndings("\n"));
    }

    public static TheoryData<string[]> Problems => new()
    {
        new[] { "rename", "h1" },
        new[] { "pin" },
        new[] { "unpin", "../h1" },
        new[] { "delete", "h1", "--now" },
        new[] { "resume", "h1" },
        new[] { "list", "--search" },
        new[] { "list", "--all" },
        new[] { "list", "--offset" },
        new[] { "list", "--offset", "-1" },
        new[] { "list", "--limit", "0" },
        new[] { "list", "--limit", "many" },
        new[] { "forget", "h1" },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Words_it_does_not_take_are_a_problem_said_with_the_usage(string[] args)
    {
        Assert.Null(HelpCommand.Read(args, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void Each_verb_reads_what_it_names()
    {
        Assert.Equal(new HelpVerbAsk("list") { Search = "remote", Json = true }, HelpCommand.Read(["list", "--search", " remote ", "--json"], out _));
        Assert.Equal(new HelpVerbAsk("list") { Offset = 50, Limit = 20 }, HelpCommand.Read(["list", "--offset", "50", "--limit", "20"], out _));
        var resume = HelpCommand.Read(["resume", "h1", "and", "the", "remote?", "--file", "notes.md"], out _)!;
        Assert.Equal(("resume", "h1", "and the remote?"), (resume.Verb, resume.Id, resume.Text));
        Assert.Equal(["notes.md"], resume.Files);
        Assert.Equal(new HelpVerbAsk("rename") { Id = "h1", Text = "Workspaces, explained" }, HelpCommand.Read(["rename", "h1", "Workspaces,", "explained"], out _));
        Assert.Equal(new HelpVerbAsk("rename") { Id = "h1", Clear = true }, HelpCommand.Read(["rename", "h1", "--clear"], out _));
        Assert.Equal(new HelpVerbAsk("unpin") { Id = "h1" }, HelpCommand.Read(["unpin", "h1"], out _));
        Assert.Equal(new HelpVerbAsk("delete") { Id = "h1", Yes = true }, HelpCommand.Read(["delete", "h1", "--yes"], out _));
    }

    /// <summary>The listing: Ask Daoris's conversations the person spoke in, each with when, its title and its line; nothing else.</summary>
    [Fact]
    public async Task The_list_holds_ask_daoris_conversations_with_their_title_and_line()
    {
        var (exit, said) = await RunAsync(Ledger(), "list");

        Assert.Equal(0, exit);
        Assert.Contains("what is a workspace?", said);
        Assert.Contains("    A circle of repositories that share a remote.", said);
        Assert.Contains("how do I land on a branch?  · starts anew", said);
        Assert.DoesNotContain("h3", said);
        Assert.DoesNotContain("fix the build", said);
    }

    /// <summary>A search finds by words, saying where; one that finds nothing says so.</summary>
    [Fact]
    public async Task A_search_finds_by_words_and_says_where()
    {
        var ledger = Ledger();

        var (_, found) = await RunAsync(ledger, "list", "--search", "remote");
        var (_, none) = await RunAsync(ledger, "list", "--search", "nothing like this");

        Assert.StartsWith("h1  ", found);
        Assert.Contains("found: ", found);
        Assert.DoesNotContain("h2", found);
        Assert.Equal("help: no Ask Daoris conversation on this machine holds “nothing like this”.\n", none);
    }

    /// <summary>The listing as the panel's route answers it, field for field, in order.</summary>
    [Fact]
    public async Task The_json_is_the_panels_answer_field_for_field()
    {
        var (_, said) = await RunAsync(Ledger(), "list", "--json");

        using var document = JsonDocument.Parse(said);
        var first = document.RootElement.GetProperty("conversations")[0];
        Assert.Equal(HelpCommand.JsonFields, first.EnumerateObject().Select(field => field.Name));
        Assert.Equal(["conversations", "cut", "total", "next"], document.RootElement.EnumerateObject().Select(field => field.Name));
        Assert.False(document.RootElement.GetProperty("cut").GetBoolean());
        Assert.Equal(2, document.RootElement.GetProperty("conversations").GetArrayLength());
        Assert.Equal(2, document.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("next").ValueKind);
    }

    /// <summary>
    /// ASKHIST1d: a listing is a page, as the panel's is: <c>--limit</c> says how many, <c>--offset</c> where it starts, and a page
    /// that does not hold them all says which it holds of how many, and the words that list the next.
    /// </summary>
    [Fact]
    public async Task A_listing_is_a_page_that_says_how_to_list_the_next()
    {
        var ledger = Ledger();

        var (exit, first) = await RunAsync(ledger, "list", "--limit", "1");
        var (_, last) = await RunAsync(ledger, "list", "--offset", "1", "--limit", "1");
        var (_, found) = await RunAsync(ledger, "list", "--search", "repositories", "--limit", "1");
        var (_, json) = await RunAsync(ledger, "list", "--offset", "1", "--limit", "1", "--json");
        var (_, past) = await RunAsync(ledger, "list", "--offset", "5");

        Assert.Equal(0, exit);
        Assert.Equal("help: the list holds 2, so none from 6 on.\n", past);
        Assert.EndsWith("\n1–1 of 2 are listed; `daoris-driver help list --offset 1 --limit 1` lists the next.\n", first);
        Assert.EndsWith("\n2–2 of 2 are listed.\n", last);
        Assert.EndsWith("\n1–1 of 2 are listed; `daoris-driver help list --search \"repositories\" --offset 1 --limit 1` lists the next.\n", found);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("conversations").GetArrayLength());
        Assert.Equal((false, 2, JsonValueKind.Null), (root.GetProperty("cut").GetBoolean(), root.GetProperty("total").GetInt32(), root.GetProperty("next").ValueKind));
    }

    /// <summary>
    /// ASKHIST1d: the line of what a conversation was about is its answer's first line whole, for the page to parse before it
    /// cuts it; the terminal shows it raw, so it cuts it to a line itself, and its <c>--json</c> carries it whole as the page's does.
    /// </summary>
    [Fact]
    public async Task The_terminal_cuts_a_long_line_where_its_json_keeps_it_whole()
    {
        var ledger = Ledger();
        var line = "**Setup** " + new string('w', 300);
        Said(Events, "h3", "a long answer?", line);

        var (_, listed) = await RunAsync(ledger, "list");
        var (_, json) = await RunAsync(ledger, "list", "--json");

        Assert.Contains($"\n    {line[..HelpCommand.LineLimit]}…\n", listed);
        using var document = JsonDocument.Parse(json);
        Assert.Contains(document.RootElement.GetProperty("conversations").EnumerateArray(), row => row.GetProperty("about").GetString() == line);
    }

    /// <summary>A name and a pin are the person's, kept on this machine, and the listing says them; a clear gives the question back.</summary>
    [Fact]
    public async Task Rename_pin_and_unpin_are_kept_and_listed()
    {
        var ledger = Ledger();

        Assert.Equal((0, "help: h2 is named “Landing”.\n"), await RunAsync(ledger, "rename", "h2", "Landing"));
        Assert.Equal((0, "help: h2 is pinned to the top of the list.\n"), await RunAsync(ledger, "pin", "h2"));
        var (_, listed) = await RunAsync(ledger, "list");
        Assert.StartsWith("h2  ", listed);
        Assert.Contains("Landing  · pinned", listed);

        await RunAsync(ledger, "unpin", "h2");
        Assert.Equal((0, "help: h2 is called by its first question again.\n"), await RunAsync(ledger, "rename", "h2", "--clear"));
        Assert.Equal(HelpKept.None, new HelpConversations(_home).Read("h2"));
    }

    /// <summary>A name that is too long is refused in the store's words, exit 1, and nothing is kept.</summary>
    [Fact]
    public async Task A_name_too_long_is_refused()
    {
        var (exit, said) = await RunAsync(Ledger(), "rename", "h1", new string('x', HelpConversations.NameLimit + 1));

        Assert.Equal(1, exit);
        Assert.Contains("at most 80 characters", said);
        Assert.Equal(HelpKept.None, new HelpConversations(_home).Read("h1"));
    }

    /// <summary>Any other session, or none, is refused, exit 1, naming the verb that acts on the others.</summary>
    [Theory]
    [InlineData("c1", "help: c1 is no Ask Daoris conversation of this machine's; `daoris-driver sessions` acts on the others.\n")]
    [InlineData("n0b0dy00", "help: no session here is n0b0dy00.\n")]
    public async Task Any_other_session_is_refused(string id, string why)
    {
        Assert.Equal((1, why), await RunAsync(Ledger(), "pin", id));
        Assert.Equal(HelpKept.None, new HelpConversations(_home).Read(id));
    }

    /// <summary>A delete is listed first, as the panel asks once; it takes nothing until it is pressed with --yes.</summary>
    [Fact]
    public async Task A_delete_is_listed_first_and_takes_nothing_without_yes()
    {
        var ledger = Ledger();

        var (exit, said) = await RunAsync(ledger, "delete", "h1");

        Assert.Equal(0, exit);
        Assert.Contains("deleting h1 “what is a workspace?” removes its record", said);
        Assert.Contains("`daoris-driver help delete h1 --yes` deletes it.", said);
        Assert.Equal(4, ledger.Count);
        Assert.True(File.Exists(Events.PathOf("h1")));
    }

    /// <summary>
    /// Words said to one go through <c>sessions say</c>'s door; with no driver on the home nothing could open it again, so they
    /// are refused naming the window, where its panel holds it.
    /// </summary>
    [Fact]
    public async Task Words_go_through_the_say_door_and_go_on_in_the_window()
    {
        var (exit, said) = await RunAsync(Ledger(), "resume", "h1", "and the remote?");

        Assert.Equal(1, exit);
        Assert.Contains("h1 is Ask Daoris's conversation, which goes on in the window", said);
    }

    /// <summary>The host's usage names the verb, and its own usage spells each sub-verb.</summary>
    [Fact]
    public void The_usages_name_the_verb()
    {
        var usage = DriverCommand.Usage.ReplaceLineEndings("\n");
        Assert.Contains("\n  help list [--search \"…\"] [--json]", usage);
        Assert.Contains("help delete <id> [--yes]", usage);
        foreach (var verb in HelpCommand.Verbs) Assert.Contains($"help {verb}", HelpCommand.Usage);
    }
}
