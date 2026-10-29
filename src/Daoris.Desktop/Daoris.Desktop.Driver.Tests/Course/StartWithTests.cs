using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// NEW1a (D162 point 2): <c>START_CHAT</c> takes the opening words, their files and a request id. The conversation opens as
/// ever and is handed the words as its first message, files over a message's limit going as the words alone; one the words
/// were not taken by is answered as such and the driver keeps nothing. The request id makes a retry answer the first start,
/// whether it still runs or finished within a day; a refused start is not remembered. The start and the first message stand
/// in, so nothing is spawned.
/// </summary>
public sealed class StartWithTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-start-with-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Task<ChatStart> Opened(CancellationToken _) => Task.FromResult(new ChatStart("c9", "Conversation `c9` opened in `engine`."));

    /// <summary>The words are the conversation's first message, with the files as said, and no preface.</summary>
    [Fact]
    public async Task The_words_and_files_are_the_first_message()
    {
        var said = new List<(string Session, string Text, int Files, string? Preface)>();

        var started = await StartWith.RunAsync(
            Opened, (session, text, files, preface) => { said.Add((session, text, files.Count, preface)); return true; },
            "Fix the build.", [new ChatUpload("log.txt", Encoding.UTF8.GetBytes("x"))]);

        Assert.Equal(("c9", true, (string?)null), (started.SessionId, started.Sent, started.Why));
        Assert.Equal([("c9", "Fix the build.", 1, (string?)null)], said);
    }

    /// <summary>Files more than a message carries: the words go alone rather than not at all.</summary>
    [Fact]
    public async Task Files_over_a_messages_limit_go_as_the_words_alone()
    {
        var counts = new List<int>();

        var started = await StartWith.RunAsync(
            Opened,
            (_, _, files, _) =>
            {
                counts.Add(files.Count);
                return files.Count == 0 ? true : throw new DriverException("too many files");
            },
            "See these.", [new ChatUpload("a.txt", [1])]);

        Assert.True(started.Sent);
        Assert.Equal([1, 0], counts);
    }

    /// <summary>A conversation that opened but did not take the words is named, and not sent.</summary>
    [Fact]
    public async Task Words_not_taken_are_answered_with_the_conversation_that_opened()
    {
        var started = await StartWith.RunAsync(Opened, (_, _, _, _) => false, "Hello.", []);

        Assert.Equal(("c9", false, (string?)null), (started.SessionId, started.Sent, started.Why));
        Assert.Contains("`c9`", started.Message);
        Assert.Contains("did not reach", started.Message);
    }

    /// <summary>Nothing opened: the start's sentence comes back whole and nothing is said.</summary>
    [Fact]
    public async Task A_start_that_does_not_open_says_nothing()
    {
        var said = 0;

        var started = await StartWith.RunAsync(
            _ => Task.FromResult(new ChatStart(null, "no checkout.")), (_, _, _, _) => { said++; return true; }, "Hello.", []);

        Assert.Equal((null, false, "no checkout."), (started.SessionId, started.Sent, started.Message));
        Assert.Equal(0, said);
    }

    /// <summary>The tree choice is three-way: absent is the driver's own (the repository's standing choice), true and false are said.</summary>
    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void The_tree_choice_is_absent_true_or_false(bool? asked, bool standing, bool expected)
    {
        var config = standing ? DriverConfig.Empty.WithTrees("engine", true) : DriverConfig.Empty;

        Assert.Equal(expected, ChatRunner.Isolates(asked, config, "engine"));
    }

    private static StartedFrom Said(string session) => new(session, true, null, $"conversation `{session}` starts.");

    /// <summary>A retry with the same id while the first start runs shares its result: one start, one session.</summary>
    [Fact]
    public async Task A_retry_while_the_first_runs_shares_its_result()
    {
        var requests = new StartRequests(_home);
        var gate = new TaskCompletionSource();
        var runs = 0;

        async Task<StartedFrom> Run()
        {
            Interlocked.Increment(ref runs);
            await gate.Task;
            return Said("c1");
        }

        var first = requests.RunAsync("req-1", Run);
        var second = requests.RunAsync("req-1", Run);
        gate.SetResult();

        Assert.Equal("c1", (await first).SessionId);
        Assert.Equal("c1", (await second).SessionId);
        Assert.Equal(1, runs);
    }

    /// <summary>A retry after it finished is answered from the file, whatever else the retry says, even by a new table.</summary>
    [Fact]
    public async Task A_retry_after_it_finished_answers_the_original_from_its_file()
    {
        await new StartRequests(_home).RunAsync("req-2", () => Task.FromResult(new StartedFrom("c1", false, null, "opened, not taken.")));
        var ran = false;

        var again = await new StartRequests(_home).RunAsync("req-2", () => { ran = true; return Task.FromResult(Said("c2")); });

        Assert.False(ran);
        Assert.Equal(("c1", false, "opened, not taken."), (again.SessionId, again.Sent, again.Message));
        Assert.True(File.Exists(DaorisHome.Entry(_home, HomeEntry.Starts,"req-2.json")));
    }

    /// <summary>A start remembered more than a day ago is a new start.</summary>
    [Fact]
    public async Task A_start_older_than_a_day_is_not_remembered()
    {
        var requests = new StartRequests(_home);
        await requests.RunAsync("req-3", () => Task.FromResult(Said("c1")), DateTimeOffset.UtcNow.AddHours(-25));

        var again = await requests.RunAsync("req-3", () => Task.FromResult(Said("c2")));

        Assert.Equal("c2", again.SessionId);
    }

    /// <summary>A refused start (nothing opened) is not remembered: a retry tries again.</summary>
    [Fact]
    public async Task A_refused_start_is_not_remembered()
    {
        var requests = new StartRequests(_home);
        await requests.RunAsync("req-4", () => Task.FromResult(new StartedFrom(null, false, null, "no checkout.")));

        var again = await requests.RunAsync("req-4", () => Task.FromResult(Said("c1")));

        Assert.Equal("c1", again.SessionId);
        Assert.False(Directory.Exists(DaorisHome.Entry(_home, HomeEntry.Starts)) && !File.Exists(DaorisHome.Entry(_home, HomeEntry.Starts,"req-4.json")));
    }

    /// <summary>An id is one word of letters, digits and dashes, at most 64; another is refused in a sentence.</summary>
    [Theory]
    [InlineData("has space")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("")]
    public async Task A_malformed_id_is_refused_in_a_sentence(string id)
    {
        var ran = false;

        var refused = await Assert.ThrowsAsync<DriverException>(() =>
            new StartRequests(_home).RunAsync(id, () => { ran = true; return Task.FromResult(Said("c1")); }));

        Assert.False(ran);
        Assert.Contains("request id", refused.Message);
    }

    [Fact]
    public async Task A_sixty_five_character_id_is_refused_and_sixty_four_taken()
    {
        var requests = new StartRequests(_home);

        await Assert.ThrowsAsync<DriverException>(() => requests.RunAsync(new string('a', 65), () => Task.FromResult(Said("c1"))));
        Assert.Equal("c1", (await requests.RunAsync(new string('a', 64), () => Task.FromResult(Said("c1")))).SessionId);
    }
}
