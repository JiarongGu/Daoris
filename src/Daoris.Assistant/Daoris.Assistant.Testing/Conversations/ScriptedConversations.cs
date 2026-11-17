using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>
/// Conversations in memory on a scripted agent: each start and each words handed are recorded, and a turn ends when the test says so
/// (<see cref="EndTurn"/>), which is when an access swap asked is made.
/// </summary>
public sealed class ScriptedConversations : IConversations
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ConversationSeen> _held = new(StringComparer.Ordinal);
    private readonly List<ConversationStart> _started = [];
    private readonly List<(string Conversation, string Words)> _said = [];

    /// <summary>The agent every conversation answers it runs on.</summary>
    public string Agent { get; init; } = "stand-in";

    /// <summary>Every start handed, in order.</summary>
    public IReadOnlyList<ConversationStart> Started
    {
        get
        {
            lock (_gate) return [.. _started];
        }
    }

    /// <summary>Every words handed to a conversation going on, in order.</summary>
    public IReadOnlyList<(string Conversation, string Words)> Said
    {
        get
        {
            lock (_gate) return [.. _said];
        }
    }

    public Task<ConversationSeen> StartAsync(ConversationStart start, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var seen = new ConversationSeen($"conversation-{_held.Count + 1}", Agent, start.Mode, start.Access, ConversationState.Turning) { Focus = start.Focus };
            _held[seen.Id] = seen;
            _started.Add(start);
            return Task.FromResult(seen);
        }
    }

    public Task GoOnAsync(string conversation, string words, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var seen = Held(conversation);
            if (seen.State == ConversationState.Stopped) throw new InvalidOperationException($"conversation '{conversation}' is stopped and takes no words");
            _held[conversation] = seen with { State = ConversationState.Turning };
            _said.Add((conversation, words));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(string conversation, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate) _held[conversation] = Held(conversation) with { State = ConversationState.Stopped, AccessAsked = null };
        return Task.CompletedTask;
    }

    public Task SwapAccessAsync(string conversation, string access, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate) _held[conversation] = Held(conversation) with { AccessAsked = access };
        return Task.CompletedTask;
    }

    public Task<ConversationSeen?> ReadAsync(string conversation, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult(_held.GetValueOrDefault(conversation));
    }

    /// <summary>Ends <paramref name="conversation"/>'s turn: it waits for words, under the access a swap asked where one waits.</summary>
    public void EndTurn(string conversation)
    {
        lock (_gate)
        {
            var seen = Held(conversation);
            if (seen.State != ConversationState.Turning) return;
            _held[conversation] = seen with { State = ConversationState.Waiting, Access = seen.AccessAsked ?? seen.Access, AccessAsked = null };
        }
    }

    private ConversationSeen Held(string conversation) =>
        _held.TryGetValue(conversation, out var seen) ? seen : throw new InvalidOperationException($"no conversation '{conversation}'");
}
