using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's door of a conversation notes their words as the owner's turns a proposed change to the policy is bound to: what the person
/// says through it is a turn, and Daoris's own words to the conversation never are.
/// </summary>
public sealed class OwnerTurnsWiringTests : IDisposable
{
    private const string Id = "c9a2b3c4";
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-owner-turns-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<ChatMessage> _took = [];

    public OwnerTurnsWiringTests() => Directory.CreateDirectory(_home);

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
    public void The_persons_words_said_to_a_conversation_are_noted_and_daorisss_own_words_are_not()
    {
        using var runner = new ChatRunner(
            new ChatLedger().Client(), ProviderRegistry.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        var context = runner.Context;
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true), take: (message, sent) => { _took.Add(message); sent(); return Task.CompletedTask; },
            interrupt: () => Task.CompletedTask, changed: _ => { });
        context.Turned[Id] = new Turned(turns);
        var words = new ChatWords(context, new ChatTold(context, context.ChatRuns), new AttentionWatch());
        var since = DateTimeOffset.MinValue;

        Assert.True(words.Tell(Id, "pc-aaaaaaaa 0123456789ab applied", "fact-key"));
        Assert.Null(runner.OwnerTurns.Binding(Id, since, "pc-aaaaaaaa", "0123456789ab"));

        Assert.True(words.Say(Id, "yes, apply pc-aaaaaaaa (0123456789ab)", out _));
        var turn = runner.OwnerTurns.Binding(Id, since, "pc-aaaaaaaa", "0123456789ab");

        Assert.NotNull(turn);
        Assert.Equal("yes, apply pc-aaaaaaaa (0123456789ab)", turn!.Text);
        Assert.Null(runner.OwnerTurns.Binding("other-session", since, "pc-aaaaaaaa", "0123456789ab"));
        turns.Gone();
    }
}
