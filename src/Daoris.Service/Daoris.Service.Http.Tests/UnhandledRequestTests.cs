using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// A route that throws (HOSTLOG1): the machine log carries what was thrown, by its type, its message and
/// its first frames, beside the route's pattern and method; the caller is answered the house's
/// <c>ErrorResponse</c> sentence instead of an empty 500, and that sentence names nothing of the exception.
/// Nothing of the request's body, query or headers reaches the log.
/// </summary>
/// <remarks>
/// No route has an input that throws, so each failure comes from where a real one would: the store,
/// refusing a write through a trigger whose words the test chooses. A host per test, since each one's
/// store is broken on purpose.
/// </remarks>
public sealed class UnhandledRequestTests
{
    private const string BodyWords = "words-only-the-body-holds";
    private const string QueryWords = "words-only-the-query-holds";
    private const string HeaderWords = "words-only-a-header-holds";

    private static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;

    private static string? Event(string line) => Parse(line).GetProperty("event").GetString();

    /// <summary>Every write to the quest log fails from now on, with <paramref name="words"/> as the store's message.</summary>
    private static async Task RefuseQuestWritesAsync(string database, string words)
    {
        await using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        await connection.OpenAsync();
        await using var trigger = connection.CreateCommand();
        trigger.CommandText = $"""
            CREATE TRIGGER refuse_quest_writes BEFORE INSERT ON quest_log
            BEGIN SELECT RAISE(ABORT, '{words.Replace("'", "''", StringComparison.Ordinal)}'); END;
            """;
        await trigger.ExecuteNonQueryAsync();
    }

    private static string PublishBody() => JsonSerializer.Serialize(new
    {
        from = "Asker", to = "Keeper", title = BodyWords, body = $"More {BodyWords}.",
    });

    [Fact]
    public async Task A_route_that_throws_is_logged_by_what_it_threw_and_answered_with_a_sentence()
    {
        using var host = new LocalHost();
        await RefuseQuestWritesAsync(host.Database, "the store refused this write");

        var answer = await host.SendAsync(
            "POST", $"/api/quests?note={QueryWords}", DaorisHost.Loopback, key: HeaderWords, json: PublishBody());

        Assert.Equal(500, answer.Status);
        Assert.StartsWith("application/json", answer.ContentType);
        Assert.Equal(UnhandledRequests.Logged, answer.Error);
        // The sentence is the whole answer: nothing of what was thrown.
        Assert.DoesNotContain("Sqlite", answer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("the store refused this write", answer.Body);

        var lines = host.LogLines();
        var error = Parse(Assert.Single(lines, line => Event(line) == "error"));
        Assert.Equal("error", error.GetProperty("level").GetString());
        Assert.Equal("host", error.GetProperty("source").GetString());
        var data = error.GetProperty("data");
        Assert.Equal("POST /api/quests", data.GetProperty("where").GetString());
        Assert.Equal("POST", data.GetProperty("method").GetString());
        Assert.Equal("/api/quests", data.GetProperty("route").GetString());
        Assert.Equal(typeof(SqliteException).FullName, data.GetProperty("type").GetString());
        Assert.Contains("the store refused this write", data.GetProperty("message").GetString());

        // The first frames, as the runtime gives them, and no more than the cut. Which method is on top varies
        // between runs (a hot throw helper is inlined into its caller once it is tiered up), so the frames are
        // held to naming the store's library, not one method of it.
        var stack = data.GetProperty("stack").GetString()!;
        var frames = stack.Split('\n');
        Assert.InRange(frames.Length, 1, UnhandledRequests.Frames);
        Assert.True(frames[0].TrimStart().StartsWith("at ", StringComparison.Ordinal), stack);
        Assert.Contains("Microsoft.Data.Sqlite.", stack);

        // The request's line still says it failed, by its pattern and status.
        var failed = Parse(Assert.Single(lines, line => Event(line) == "request.failed")).GetProperty("data");
        Assert.Equal(500, failed.GetProperty("status").GetInt32());
        Assert.Equal("/api/quests", failed.GetProperty("route").GetString());

        // Not in any line the request caused: its body, its query, its headers.
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain(BodyWords, line);
            Assert.DoesNotContain(QueryWords, line);
            Assert.DoesNotContain(HeaderWords, line);
        });
    }

    /// <summary>
    /// An exception's message may repeat what the request carried (a search a store's parser refused, an id
    /// it could not find). The message is kept, and the request's values are cut out of it: a search is
    /// never logged (machine-log design §5).
    /// </summary>
    [Fact]
    public async Task A_request_value_the_exceptions_message_repeats_is_cut_from_the_log()
    {
        using var host = new LocalHost();
        await RefuseQuestWritesAsync(host.Database, $"refused near {QueryWords} here");
        await using (var connection = new SqliteConnection($"Data Source={host.Database};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = "DROP TABLE asks";
            await drop.ExecuteNonQueryAsync();
        }

        // A query's value, repeated by the store's message.
        Assert.Equal(500, (await host.SendAsync(
            "POST", $"/api/quests?note={QueryWords}", DaorisHost.Loopback, json: PublishBody())).Status);
        // A path's value: an ask whose id is the missing table's name, which the store's message names.
        Assert.Equal(500, (await host.GetAsync("/api/asks/asks")).Status);

        var messages = host.LogLines()
            .Where(line => Event(line) == "error")
            .Select(line => Parse(line).GetProperty("data"))
            .ToDictionary(data => data.GetProperty("route").GetString()!, data => data.GetProperty("message").GetString()!);
        Assert.Contains("refused near … here", messages["/api/quests"]);
        Assert.DoesNotContain(QueryWords, messages["/api/quests"]);
        Assert.Contains("no such table: …", messages["/api/asks/{id}"]);
    }

    /// <summary>The cut keeps the stack's first lines as written, and drops the blank ones a trace can hold.</summary>
    [Fact]
    public void The_first_frames_are_the_stacks_first_lines_as_written()
    {
        var lines = Enumerable.Range(1, 8).Select(n => $"   at Some.Place.Step{n}() in a-file.cs:line {n}").ToList();
        lines.Insert(3, "");
        var stack = string.Join("\r\n", lines);

        var frames = UnhandledRequests.FirstFrames(stack)!.Split('\n');

        Assert.Equal(UnhandledRequests.Frames, frames.Length);
        Assert.Equal("   at Some.Place.Step1() in a-file.cs:line 1", frames[0]);
        Assert.Equal("   at Some.Place.Step5() in a-file.cs:line 5", frames[^1]);
        Assert.Null(UnhandledRequests.FirstFrames(null));
        Assert.Null(UnhandledRequests.FirstFrames("  "));
    }

    /// <summary>
    /// 🔴 A shared host answers keyed callers off the machine (D47 §4): what was thrown can name a machine
    /// path, and the answer carries none of it. The log keeps it, since the log never leaves the machine.
    /// </summary>
    [Fact]
    public async Task A_throw_on_a_shared_host_answers_no_machine_path()
    {
        using var host = new SharedHost();
        var key = (await host.MintAsync("thrower@a-machine")).Key;
        foreach (var repository in new[] { "Asker", "Keeper" })
        {
            Assert.Equal(200, (await host.PostAsync("/api/registry", new
            {
                repository, packs = Array.Empty<string>(), join = true, shareKnowledge = true,
                domain = new { summary = $"{repository}'s own area.", owns = new[] { "its own tree" }, accepts = new[] { "a quest" } },
            }, key: key)).Status);
        }

        await RefuseQuestWritesAsync(host.Database, $"cannot write {host.Database}");

        var answer = await host.SendAsync("POST", "/api/quests", DaorisHost.OffMachine, key, PublishBody());

        Assert.Equal(500, answer.Status);
        Assert.Equal(UnhandledRequests.Logged, answer.Error);
        var forbidden = new[]
        {
            host.Scratch, JsonEncodedText.Encode(host.Scratch).ToString(),
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), JsonEncodedText.Encode(Path.GetTempPath()).ToString(),
        };
        Assert.All(forbidden, path => Assert.DoesNotContain(path, answer.Body, StringComparison.OrdinalIgnoreCase));

        var data = Parse(Assert.Single(host.LogLines(), line => Event(line) == "error")).GetProperty("data");
        Assert.Contains(host.Database, data.GetProperty("message").GetString());
    }

    /// <summary>A host with no home keeps no log (machine-log design §2), so its sentence does not claim one.</summary>
    [Fact]
    public async Task A_host_with_no_home_says_it_kept_no_log()
    {
        using var host = new DaorisHost(
            ServiceMode.Local, environment: new Dictionary<string, string?> { [DaorisHome.Variable] = null });
        await using (var connection = new SqliteConnection($"Data Source={host.Database};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var drop = connection.CreateCommand();
            drop.CommandText = "DROP TABLE asks";
            await drop.ExecuteNonQueryAsync();
        }

        var answer = await host.GetAsync("/api/asks/an-ask");

        Assert.Equal(500, answer.Status);
        Assert.Equal(UnhandledRequests.Unlogged, answer.Error);
        Assert.Empty(host.LogLines());
    }
}
