using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// HELPMODE1c4 (D183's note; the Ask Daoris modes design §4.1): Ask Daoris reads the checkouts through one typed tool,
/// <c>checkout_read</c>, for every agent. Each call is a keyed request in the home's session requests for the conversation's own
/// session, as a chosen setting's is, which the loop running it takes, judges and answers beside; the tool says the answer's words,
/// then what was read. The connector judges only what it can: that it speaks for an Ask Daoris conversation, and that the call names
/// what its read needs. Which checkout, the path and what git ignores are the driver's.
/// </summary>
/// <remarks>
/// 🔴 A twin: the request's and the answer's fields are the driver's <c>SessionRequests</c> table, held there by
/// <c>CheckoutRequestTests</c> with the same names; the two change together.
/// </remarks>
public sealed class McpCheckoutToolTests : McpToolsFixture
{
    private string Home => Path.Combine(_root, "rules-home");

    private string Requests => DaorisHome.Entry(Home, HomeEntry.Requests);

    // Generous by default, as the settings tool's test is: its stand-in loop runs on the pool, which load can starve.
    private CheckoutBox Box(double? take = null, double? answer = null) => new(Home)
    {
        TakeWithin = take is { } t ? TimeSpan.FromSeconds(t) : Poll.Patience, AnswerWithin = answer is { } a ? TimeSpan.FromSeconds(a) : Poll.Patience,
        Poll = TimeSpan.FromMilliseconds(10),
    };

    /// <summary>The connector a conversation is handed: its session named, a ledger to read its record, the box under the rules home.</summary>
    private async Task<(CheckoutTools Tools, string? Session)> ConnectorAsync(Func<SessionStore, Task<string?>> session, CheckoutBox? box = null)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        var named = await session(sessions);
        return (new CheckoutTools(new IntakeScope(null, named), ledger, box ?? Box()), named);
    }

    private static async Task<string?> AskDaorisAsync(SessionStore sessions) =>
        (await sessions.CreateAsync(null, SessionLedger.HelpRepository, "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat)).Id;

    /// <summary>
    /// A loop standing in for the driver's: takes the first request it finds, as a take removes it, keeps what it read, and
    /// writes <paramref name="answer"/> beside it under the request's own name.
    /// </summary>
    private Task<JsonElement> LoopAsync(string answer) => Task.Run(async () =>
    {
        for (var tries = 0; tries < Poll.Patience.TotalMilliseconds / 10; tries++)
        {
            var found = Directory.Exists(Requests)
                ? Directory.GetFiles(Requests, "*.json").FirstOrDefault(path => !path.EndsWith(".answer.json", StringComparison.Ordinal))
                : null;
            if (found is not null)
            {
                // A request just renamed into place can be held for a moment by a scanner on this machine: a read or a delete
                // refused is tried again at the next turn, never the loop's end.
                string text;
                try
                {
                    text = File.ReadAllText(found);
                    File.Delete(found);
                }
                catch (Exception busy) when (busy is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(10);
                    continue;
                }

                File.WriteAllText(found[..^".json".Length] + ".answer.json", answer);
                using var document = JsonDocument.Parse(text);
                return document.RootElement.Clone();
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("no request was written");
    });

    /// <summary>
    /// 🔴 A read is one keyed request for the conversation's own session, with the move, who asked, the operation, the repository and
    /// the path; the tool says the loop's words and then what was read, and the answer is read once and removed.
    /// </summary>
    [Fact]
    public async Task A_read_is_a_request_for_the_conversations_own_session_and_says_what_was_read()
    {
        var (tools, session) = await ConnectorAsync(AskDaorisAsync);
        var loop = LoopAsync("""
            { "operation": "read", "done": true, "code": null, "said": "`engine`'s `src/Game.cs`, 14 bytes, whole:", "text": "class Game {}\n" }
            """);

        var said = await tools.ReadAsync("engine", "src/Game.cs");

        Assert.Equal("`engine`'s `src/Game.cs`, 14 bytes, whole:\n\nclass Game {}\n", said);
        var request = await loop;
        Assert.Equal(["session", "move", "at", "by", "key", "operation", "repository", "path"], request.EnumerateObject().Select(field => field.Name));
        Assert.Equal(
            (session, "read", "help", "read", "engine", "src/Game.cs"),
            (request.GetProperty("session").GetString(), request.GetProperty("move").GetString(), request.GetProperty("by").GetString(),
                request.GetProperty("operation").GetString(), request.GetProperty("repository").GetString(), request.GetProperty("path").GetString()));
        Assert.Matches("^[0-9a-f]{12}$", request.GetProperty("key").GetString());
        Assert.Empty(Directory.GetFiles(Requests));
    }

    /// <summary>
    /// <c>status</c> and <c>branches</c> are the same request with their operation and no path; a refusal is said in the loop's words
    /// alone, with nothing after them.
    /// </summary>
    [Fact]
    public async Task Status_and_branches_name_no_path_and_a_refusal_is_said_alone()
    {
        var (tools, _) = await ConnectorAsync(AskDaorisAsync);

        var loop = LoopAsync("""{ "operation": "status", "done": true, "code": null, "said": "`engine`'s checkout, `git status`:", "text": "On branch main\n" }""");
        Assert.Equal("`engine`'s checkout, `git status`:\n\nOn branch main\n", await tools.ReadAsync("engine", what: "status"));
        var status = await loop;
        Assert.Equal(["session", "move", "at", "by", "key", "operation", "repository"], status.EnumerateObject().Select(field => field.Name));
        Assert.Equal("status", status.GetProperty("operation").GetString());

        var refused = LoopAsync("""{ "operation": "read", "done": false, "code": "ignored", "said": "Git ignores it. Nothing was read.", "text": null }""");
        Assert.Equal("Git ignores it. Nothing was read.", await tools.ReadAsync("engine", "local/notes.md"));
        await refused;
    }

    /// <summary>
    /// 🔴 What the connector can judge is refused in a sentence and writes nothing: no session named, a session that is not an Ask
    /// Daoris conversation (a driven session shares this connector), no repository, a read that is none of the three, and a path
    /// named for <c>status</c> or <c>branches</c>.
    /// </summary>
    [Fact]
    public async Task What_the_connector_can_judge_is_refused_and_nothing_is_written()
    {
        var (nobody, _) = await ConnectorAsync(_ => Task.FromResult<string?>(null));
        Assert.Contains("speaks for no session", await nobody.ReadAsync("engine"));

        var (driven, id) = await ConnectorAsync(async sessions =>
            (await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"))).Id);
        var refused = await driven.ReadAsync("engine");
        Assert.Contains("Ask Daoris", refused);
        Assert.Contains($"`{id}`", refused);

        var (help, _) = await ConnectorAsync(AskDaorisAsync);
        Assert.Contains("repository", await help.ReadAsync(" "));
        Assert.Contains("`log`", await help.ReadAsync("engine", what: "log"));
        Assert.Contains("no path", await help.ReadAsync("engine", "src", what: "branches"));

        Assert.False(Directory.Exists(Requests) && Directory.GetFiles(Requests).Length > 0);
    }

    /// <summary>
    /// 🔴 A request nobody takes in time is withdrawn, so no loop reads after the agent was told nothing was read; one taken and not
    /// answered says so, never that nothing was read. What was read is bounded on this side too: past 200 KB it is cut.
    /// </summary>
    [Fact]
    public async Task A_request_nobody_takes_is_withdrawn_and_an_answer_is_bounded()
    {
        var (untaken, _) = await ConnectorAsync(AskDaorisAsync, Box(take: 0.2));
        Assert.Contains("Nothing was read", await untaken.ReadAsync("engine"));
        Assert.Empty(Directory.GetFiles(Requests));

        var (unanswered, _) = await ConnectorAsync(AskDaorisAsync, Box(answer: 0.3));
        var taking = Task.Run(async () =>
        {
            while (!Directory.Exists(Requests) || Directory.GetFiles(Requests, "*.json").Length == 0) await Task.Delay(10);
            foreach (var path in Directory.GetFiles(Requests, "*.json")) File.Delete(path);
        });
        var late = await unanswered.ReadAsync("engine");
        await taking;
        Assert.Contains("did not answer", late);
        Assert.DoesNotContain("Nothing was read", late);

        var (bounded, _) = await ConnectorAsync(AskDaorisAsync);
        var huge = new string('x', CheckoutBox.Bound + 5_000);
        var loop = LoopAsync(JsonSerializer.Serialize(new { operation = "read", done = true, code = (string?)null, said = "Read:", text = huge }));
        var said = await bounded.ReadAsync("engine", "big.txt");
        await loop;
        Assert.True(said.Length <= "Read:\n\n".Length + CheckoutBox.Bound + 200);
        Assert.Contains("cut", said[^200..]);
    }
}
