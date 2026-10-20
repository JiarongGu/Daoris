using System.Text.Json;
using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// HELPMODE1c3 (D183's note; the Ask Daoris modes design §4.1): Ask Daoris's chosen settings, five typed tools. Each is a keyed
/// request in the home's session requests for the conversation's own session, as housekeeping's is, which the loop running it
/// takes, judges and answers beside; the tool says the answer's words. The connector judges only what it can: that it speaks for
/// an Ask Daoris conversation, and that the call names what its setting needs. The grant, the value and the writer are the
/// driver's.
/// </summary>
/// <remarks>
/// 🔴 A twin: the request's and the answer's fields are the driver's <c>SessionRequests</c> table, held there by
/// <c>SettingRequestTests</c> with the same names; the two change together.
/// </remarks>
public sealed class McpChosenSettingsToolTests : McpToolsFixture
{
    private string Home => Path.Combine(_root, "rules-home");

    private string Requests => DaorisHome.Entry(Home, HomeEntry.Requests);

    // Generous by default, as the housekeeping tool's test is: its stand-in loop runs on the pool, which load can starve.
    private SettingsBox Box(double? take = null, double? answer = null) => new(Home)
    {
        TakeWithin = take is { } t ? TimeSpan.FromSeconds(t) : Poll.Patience, AnswerWithin = answer is { } a ? TimeSpan.FromSeconds(a) : Poll.Patience,
        Poll = TimeSpan.FromMilliseconds(10),
    };

    /// <summary>The connector a conversation is handed: its session named, a ledger to read its record, the box under the rules home.</summary>
    private async Task<(ChosenSettingsTools Tools, string? Session)> ConnectorAsync(
        Func<SessionStore, Task<string?>> session, SettingsBox? box = null)
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var ledger = new SessionLedger(_quests, sessions, _service, await AskStore.OpenAsync(_connection));
        var named = await session(sessions);
        return (new ChosenSettingsTools(new IntakeScope(null, named), ledger, box ?? Box()), named);
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
                // refused is tried again at the next turn, never the loop's end (the full set on 195e6f9 waited a minute on it).
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
    /// 🔴 A setting is one keyed request for the conversation's own session, with the move, who asked, the setting and its value as
    /// the driver's door takes it, and says what the loop answered beside it; the answer is read once and removed.
    /// </summary>
    [Fact]
    public async Task A_setting_is_a_request_for_the_conversations_own_session_and_says_the_loops_answer()
    {
        var (tools, session) = await ConnectorAsync(AskDaorisAsync);
        var loop = LoopAsync("""{ "operation": "timeout", "done": true, "code": null, "said": "Set: one session runs up to 45 minutes." }""");

        var said = await tools.TimeoutAsync(45);

        Assert.Equal("Set: one session runs up to 45 minutes.", said);
        var request = await loop;
        Assert.Equal(["session", "move", "at", "by", "key", "operation", "value"], request.EnumerateObject().Select(field => field.Name));
        Assert.Equal(
            (session, "set", "help", "timeout", "45"),
            (request.GetProperty("session").GetString(), request.GetProperty("move").GetString(), request.GetProperty("by").GetString(),
                request.GetProperty("operation").GetString(), request.GetProperty("value").GetString()));
        Assert.Matches("^[0-9a-f]{12}$", request.GetProperty("key").GetString());
        Assert.True(DateTimeOffset.TryParse(request.GetProperty("at").GetString(), out _));
        Assert.Empty(Directory.GetFiles(Requests));
    }

    /// <summary>
    /// Each tool's typed value is written as the driver's door spells it: notify's flag as <c>on</c> or <c>off</c>, the numbers as
    /// numbers, and a language with the one repository or workspace it is set for.
    /// </summary>
    [Fact]
    public async Task Each_tool_writes_its_value_as_the_door_spells_it()
    {
        var (tools, _) = await ConnectorAsync(AskDaorisAsync);

        async Task<JsonElement> Asked(Func<Task<string>> call)
        {
            var loop = LoopAsync("""{ "operation": "x", "done": true, "code": null, "said": "Set." }""");
            Assert.Equal("Set.", await call());
            return await loop;
        }

        var notify = await Asked(() => tools.NotifyAsync(false));
        Assert.Equal(("notify", "off"), (notify.GetProperty("operation").GetString(), notify.GetProperty("value").GetString()));
        var cap = await Asked(() => tools.CapAsync(3));
        Assert.Equal(("cap", "3"), (cap.GetProperty("operation").GetString(), cap.GetProperty("value").GetString()));
        var cool = await Asked(() => tools.CoolOffAsync(90));
        Assert.Equal(("cooloff", "90"), (cool.GetProperty("operation").GetString(), cool.GetProperty("value").GetString()));

        var repository = await Asked(() => tools.LanguageAsync("zh", repository: "engine"));
        Assert.Equal(
            ["session", "move", "at", "by", "key", "operation", "value", "repository"], repository.EnumerateObject().Select(field => field.Name));
        Assert.Equal(("language", "zh", "engine"),
            (repository.GetProperty("operation").GetString(), repository.GetProperty("value").GetString(), repository.GetProperty("repository").GetString()));
        var workspace = await Asked(() => tools.LanguageAsync("clear", workspace: "aurora"));
        Assert.Equal(("clear", "aurora"), (workspace.GetProperty("value").GetString(), workspace.GetProperty("workspace").GetString()));
        Assert.False(workspace.TryGetProperty("repository", out _));
    }

    /// <summary>
    /// 🔴 What the connector can judge is refused in a sentence and writes nothing: no session named, a session that is not an Ask
    /// Daoris conversation (a driven session shares this connector), and a language naming no scope, or both.
    /// </summary>
    [Fact]
    public async Task What_the_connector_can_judge_is_refused_and_nothing_is_written()
    {
        var (nobody, _) = await ConnectorAsync(_ => Task.FromResult<string?>(null));
        Assert.Contains("speaks for no session", await nobody.NotifyAsync(true));

        var (driven, id) = await ConnectorAsync(async sessions =>
            (await sessions.CreateAsync("q1", "Owner", "claude-code", DateTimeOffset.UtcNow, tree: Path.Combine(_root, "trees", "work"))).Id);
        var refused = await driven.CapAsync(2);
        Assert.Contains("Ask Daoris", refused);
        Assert.Contains($"`{id}`", refused);

        var (help, _) = await ConnectorAsync(AskDaorisAsync);
        Assert.Contains("repository or a workspace", await help.LanguageAsync("en"));
        Assert.Contains("repository or a workspace", await help.LanguageAsync("en", repository: "engine", workspace: "aurora"));
        Assert.Contains("language", await help.LanguageAsync(" ", repository: "engine"));

        Assert.False(Directory.Exists(Requests) && Directory.GetFiles(Requests).Length > 0);
    }

    /// <summary>
    /// 🔴 A request nobody takes in time is withdrawn, so no loop changes the setting after the agent was told nothing was done; one
    /// taken and not answered says it may still be under way, never that nothing was done.
    /// </summary>
    [Fact]
    public async Task A_request_nobody_takes_is_withdrawn_and_one_unanswered_may_still_be_under_way()
    {
        var (untaken, _) = await ConnectorAsync(AskDaorisAsync, Box(take: 0.2));
        var said = await untaken.NotifyAsync(true);
        Assert.Contains("Nothing was changed", said);
        Assert.Empty(Directory.GetFiles(Requests));

        var (unanswered, _) = await ConnectorAsync(AskDaorisAsync, Box(answer: 0.3));
        var taking = Task.Run(async () =>
        {
            while (!Directory.Exists(Requests) || Directory.GetFiles(Requests, "*.json").Length == 0) await Task.Delay(10);
            foreach (var path in Directory.GetFiles(Requests, "*.json")) File.Delete(path);
        });

        var late = await unanswered.NotifyAsync(true);
        await taking;

        Assert.Contains("may still be under way", late);
        Assert.DoesNotContain("Nothing was changed", late);
    }
}
