using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// HELPMODE1c2a (D183's note): Ask Daoris's three housekeeping tools. Each is a keyed request in the home's session requests for
/// the conversation's own session, which the loop running it takes, judges and answers beside; the tool says the answer's words.
/// The connector judges only what it can: that it speaks for an Ask Daoris conversation, and that the call names what its
/// operation needs. The grant, the workspace and the cleanup's judges are the driver's.
/// </summary>
/// <remarks>
/// 🔴 A twin: the request's and the answer's fields are the driver's <c>SessionRequests</c> table, held there by
/// <c>HousekeepingRequestTests</c> with the same names; the two change together.
/// </remarks>
public sealed class McpHousekeepingToolTests : McpToolsFixture
{
    private string Home => Path.Combine(_root, "rules-home");

    private string Requests => DaorisHome.Entry(Home, HomeEntry.Requests);

    private HousekeepingBox Box(double take = 5, double answer = 5) => new(Home)
    {
        TakeWithin = TimeSpan.FromSeconds(take), AnswerWithin = TimeSpan.FromSeconds(answer), Poll = TimeSpan.FromMilliseconds(10),
    };

    /// <summary>The connector a conversation is handed: its session named, a ledger to read its record, the box under the rules home.</summary>
    private async Task<(HousekeepingTools Tools, string? Session)> ConnectorAsync(
        Func<SessionStore, Task<string?>> session, HousekeepingBox? box = null)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        var named = await session(sessions);
        return (new HousekeepingTools(new IntakeScope(null, named), ledger, box ?? Box()), named);
    }

    private static async Task<string?> AskDaorisAsync(SessionStore sessions) =>
        (await sessions.CreateAsync(null, SessionLedger.HelpRepository, "claude-code", DateTimeOffset.UtcNow, kind: SessionKind.Chat)).Id;

    /// <summary>
    /// A loop standing in for the driver's: takes the first request it finds, as a take removes it, keeps what it read, and
    /// writes <paramref name="answer"/> beside it under the request's own name.
    /// </summary>
    private Task<JsonElement> LoopAsync(string answer) => Task.Run(async () =>
    {
        for (var tries = 0; tries < 500; tries++)
        {
            var found = Directory.Exists(Requests)
                ? Directory.GetFiles(Requests, "*.json").FirstOrDefault(path => !path.EndsWith(".answer.json", StringComparison.Ordinal))
                : null;
            if (found is not null)
            {
                var text = File.ReadAllText(found);
                File.Delete(found);
                File.WriteAllText(found[..^".json".Length] + ".answer.json", answer);
                using var document = JsonDocument.Parse(text);
                return document.RootElement.Clone();
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("no request was written");
    });

    /// <summary>
    /// 🔴 The tool writes one keyed request for its conversation's own session, with the move, who asked, the operation and its
    /// targets by name, and says what the loop answered beside it; the answer is read once and removed.
    /// </summary>
    [Fact]
    public async Task A_clean_up_is_a_request_for_the_conversations_own_session_and_says_the_loops_answer()
    {
        var (tools, session) = await ConnectorAsync(AskDaorisAsync);
        var loop = LoopAsync("""{ "operation": "clean-up", "done": true, "code": null, "said": "`daoris/s-one` in `engine` was removed." }""");

        var said = await tools.CleanUpAsync("engine", ["daoris/s-one", "daoris/s-two"]);

        Assert.Equal("`daoris/s-one` in `engine` was removed.", said);
        var request = await loop;
        Assert.Equal(
            ["session", "move", "at", "by", "key", "operation", "repository", "branches"],
            request.EnumerateObject().Select(field => field.Name));
        Assert.Equal(
            (session, "housekeep", "help", "clean-up", "engine"),
            (request.GetProperty("session").GetString(), request.GetProperty("move").GetString(), request.GetProperty("by").GetString(),
                request.GetProperty("operation").GetString(), request.GetProperty("repository").GetString()));
        Assert.Equal(["daoris/s-one", "daoris/s-two"], request.GetProperty("branches").EnumerateArray().Select(branch => branch.GetString()));
        Assert.Matches("^[0-9a-f]{12}$", request.GetProperty("key").GetString());
        Assert.True(DateTimeOffset.TryParse(request.GetProperty("at").GetString(), out _));
        Assert.Empty(Directory.GetFiles(Requests));
    }

    /// <summary>A look names its repository where one is given, and every one of the conversation's workspace where none is; a discard names its one branch.</summary>
    [Fact]
    public async Task An_inspection_and_a_discard_name_their_targets_as_given()
    {
        var (tools, _) = await ConnectorAsync(AskDaorisAsync);

        var looking = LoopAsync("""{ "operation": "inspect", "done": true, "code": null, "said": "Nothing to clean up." }""");
        Assert.Equal("Nothing to clean up.", await tools.InspectAsync());
        var look = await looking;
        Assert.Equal("inspect", look.GetProperty("operation").GetString());
        Assert.False(look.TryGetProperty("repository", out _));
        Assert.Empty(look.GetProperty("branches").EnumerateArray());

        var discarding = LoopAsync("""{ "operation": "discard-branch", "done": false, "code": "held", "said": "It is kept." }""");
        Assert.Equal("It is kept.", await tools.DiscardBranchAsync("engine", "daoris/s-old"));
        var discard = await discarding;
        Assert.Equal(("discard-branch", "engine"), (discard.GetProperty("operation").GetString(), discard.GetProperty("repository").GetString()));
        Assert.Equal(["daoris/s-old"], discard.GetProperty("branches").EnumerateArray().Select(branch => branch.GetString()));
    }

    /// <summary>
    /// 🔴 What the connector can judge is refused in a sentence and writes nothing: no session named, a session that is not an Ask
    /// Daoris conversation (a driven session shares this connector), and a call missing what its operation acts on.
    /// </summary>
    [Fact]
    public async Task What_the_connector_can_judge_is_refused_and_nothing_is_written()
    {
        var (nobody, _) = await ConnectorAsync(_ => Task.FromResult<string?>(null));
        Assert.Contains("speaks for no session", await nobody.InspectAsync());

        var (driven, id) = await ConnectorAsync(async sessions =>
            (await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"))).Id);
        var refused = await driven.CleanUpAsync("Owner", ["daoris/s-one"]);
        Assert.Contains("Ask Daoris", refused);
        Assert.Contains($"`{id}`", refused);

        var (help, _) = await ConnectorAsync(AskDaorisAsync);
        Assert.Contains("repository", await help.CleanUpAsync(" ", ["daoris/s-one"]));
        Assert.Contains("branch", await help.CleanUpAsync("engine", []));
        Assert.Contains("branch", await help.DiscardBranchAsync("engine", " "));

        Assert.False(Directory.Exists(Requests) && Directory.GetFiles(Requests).Length > 0);
    }

    /// <summary>
    /// 🔴 A request nobody takes in time is withdrawn, so no loop acts on it after the agent was told nothing was done (REQTAKE2's
    /// withdrawal, on this side of the twin).
    /// </summary>
    [Fact]
    public async Task A_request_nobody_takes_is_withdrawn_and_says_nothing_was_done()
    {
        var (tools, _) = await ConnectorAsync(AskDaorisAsync, Box(take: 0.2));

        var said = await tools.CleanUpAsync("engine", ["daoris/s-one"]);

        Assert.Contains("Nothing was done", said);
        Assert.Empty(Directory.GetFiles(Requests));
    }

    /// <summary>A request taken and not answered in time says so, and that the act may still be under way: never that nothing was done.</summary>
    [Fact]
    public async Task A_request_taken_and_not_answered_in_time_says_it_may_still_be_under_way()
    {
        var (tools, _) = await ConnectorAsync(AskDaorisAsync, Box(answer: 0.3));
        var taking = Task.Run(async () =>
        {
            while (!Directory.Exists(Requests) || Directory.GetFiles(Requests, "*.json").Length == 0) await Task.Delay(10);
            foreach (var path in Directory.GetFiles(Requests, "*.json")) File.Delete(path);
        });

        var said = await tools.CleanUpAsync("engine", ["daoris/s-one"]);
        await taking;

        Assert.Contains("housekeeping_inspect", said);
        Assert.DoesNotContain("Nothing was done", said);
    }
}
