using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A transient try carries every held word in one prompt, and a turn that ends well receipts each of them <c>ended</c>, not the first
/// alone. The scripted agent is <see cref="AcpTransientTests"/>'.
/// </summary>
public sealed class AcpTransientWordsTests
{
    private const string Overloaded = "Internal error: API Error: 529 Overloaded. This is a server-side issue, usually temporary.";

    private static readonly ProviderRegistry Built = ProviderRegistry.Built();

    private static string Failed(JsonElement request) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = request.GetProperty("id").GetInt32(),
            error = new { code = -32603, message = Overloaded, data = new { errorKind = "overloaded" } },
        });

    [Fact]
    public async Task Each_word_a_try_carries_is_receipted_ended_when_it_ends_well()
    {
        var clock = new MovedClock();
        var events = new List<SessionEvent>();
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = AcpTransientTests.Scripted((n, frame, self) =>
        {
            if (n != 1) return AcpTransientTests.Answer(frame, "end_turn");
            _ = Task.Run(async () =>
            {
                await go.Task;
                self.Push(Failed(frame));
            });
            return null;
        });
        var retry = new TransientRetry
        {
            Read = refused => AgentErrors.Read(
                Built, Built.Door("claude-code-acp"), DoorFailures.OfPrompt(refused), clock.GetUtcNow(), TimeZoneInfo.Utc),
            Clock = clock,
        };
        var inbox = new DrivenInbox(_ => { });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: e => { lock (events) events.Add(e); });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, inbox: inbox, retry: retry);
        await Poll.Until(() => agent.Prompts.Count == 1, () => "the first prompt never went", Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("use port 9090", []) { Level = MessageLevel.Ordinary }));
        Assert.True(inbox.Hold(new ChatMessage("and log it", []) { Level = MessageLevel.Ordinary }));
        go.SetResult();
        await run.WaitAsync(Poll.Patience);

        Assert.Contains(agent.Prompts, prompt => prompt.Contains("use port 9090") && prompt.Contains("and log it"));
        string[] ended;
        lock (events) ended = [.. events.Where(e => e.Kind == SessionEventKind.Receipt && e.Receipt == Receipts.Ended).Select(e => e.Id!)];
        Assert.Equal(["said-1", "said-2"], ended);
    }
}
