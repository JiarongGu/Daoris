using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A message no turn takes (its conversation finishing, held or gone) consumes nothing: what a conversation is handed once ahead of the
/// person's words stays for the words said again, and the person waits on no answer to a message nobody took.
/// </summary>
public sealed class ChatWordsUntakenTests : IDisposable
{
    private const string Id = "c1a2b3c4";
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-chat-untaken-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<ChatMessage> _took = [];

    public ChatWordsUntakenTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Turned(ChatTurns turns) : ITurnedChat
    {
        public IChatTurns Turns => turns;

        public Task FinishAsync(Func<Task> close) => Task.CompletedTask;
    }

    [Fact]
    public async Task A_message_a_held_conversation_does_not_take_keeps_its_extras_for_the_words_said_again()
    {
        using var runner = new ChatRunner(
            new ChatLedger().Client(), ProviderRegistry.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        var context = runner.Context;
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true), take: (message, sent) => { _took.Add(message); sent(); return Task.CompletedTask; },
            interrupt: () => Task.CompletedTask, changed: _ => { });
        context.Turned[Id] = new Turned(turns);
        var draft = new WorkflowDraftTold("fingerprint", "place", default(JsonElement));
        var brief = new ComposedBrief([], []);
        context.Handing[Id] = new Handing(brief);
        context.Browsers[Id] = "the browser line";
        context.Drafts[Id] = draft;
        var attention = new AttentionWatch();
        var told = new ChatTold(context, context.ChatRuns);
        var process = new ProcessBriefing("the process", "mode", "source");
        told.HoldProcess(Id, process);
        var words = new ChatWords(context, told, attention);

        Assert.True(turns.Hold());
        Assert.False(words.Say(Id, "refused", out _));
        Assert.Same(brief, context.Handing[Id].Brief);
        Assert.Equal("the browser line", context.Browsers[Id]);
        Assert.Same(draft, context.Drafts[Id]);
        // No answer is owed for words nobody took: the next turn's end is nobody's answer.
        Assert.Null(attention.Turned(Id, "engine", new ChatQueue(false, [], DateTimeOffset.UnixEpoch.AddMinutes(1))));

        turns.Release();
        Assert.True(words.Say(Id, "said again", out _));
        await Poll.Until(() => _took.Count == 1, () => "the words said again never reached the turns");
        Assert.Same(brief, _took[0].Brief);
        Assert.Equal("the browser line", _took[0].Briefing);
        Assert.Same(draft, _took[0].Draft);
        Assert.Same(process, _took[0].Process);
        Assert.Empty(context.Browsers);
        Assert.Empty(context.Drafts);
        Assert.Empty(context.Handing);
        turns.Gone();
    }
}
