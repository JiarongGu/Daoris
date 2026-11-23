namespace Daoris.Driver;

/// <summary>One of the owner's own turns in a conversation: where it is said, when, and its words.</summary>
/// <param name="Id">The ledger's name for the turn, <c>&lt;session&gt;#&lt;n&gt;</c>, which a change's journal line names.</param>
public sealed record OwnerTurn(string Id, string Session, DateTimeOffset At, string Text);

/// <summary>
/// The person's own words as the conversations' door took them, kept in memory by conversation: what a proposed change to the policy is
/// bound to. Only a message the person said is noted (<see cref="ChatMessage.Persons"/>); an agent's claim, a quote an agent
/// relays, a helper's answer and Daoris's own words are never turns of the owner's. Memory alone, so a restart empties it: the owner says it again.
/// </summary>
public sealed class OwnerTurns(Func<DateTimeOffset> clock)
{
    /// <summary>How many turns a conversation keeps; an older one is dropped.</summary>
    public const int Kept = 50;

    private readonly object _gate = new();
    private readonly Dictionary<string, List<OwnerTurn>> _turns = new(StringComparer.OrdinalIgnoreCase);
    private int _next;

    /// <summary>The person's <paramref name="word"/> noted as a turn of <paramref name="session"/>'s; null where it is not the person's own.</summary>
    public OwnerTurn? Note(string session, ChatMessage word)
    {
        if (!word.Persons || string.IsNullOrWhiteSpace(session) || string.IsNullOrEmpty(word.Text)) return null;
        lock (_gate)
        {
            if (!_turns.TryGetValue(session, out var list)) _turns[session] = list = [];
            var turn = new OwnerTurn($"{session}#{++_next}", session, clock(), word.Text);
            list.Add(turn);
            if (list.Count > Kept) list.RemoveAt(0);
            return turn;
        }
    }

    /// <summary>The first turn of <paramref name="session"/> at or after <paramref name="since"/> that names both <paramref name="id"/> and <paramref name="digest"/>.</summary>
    public OwnerTurn? Binding(string session, DateTimeOffset since, string id, string digest)
    {
        lock (_gate)
        {
            return _turns.GetValueOrDefault(session)?.FirstOrDefault(turn =>
                turn.At >= since
                && turn.Text.Contains(id, StringComparison.OrdinalIgnoreCase)
                && turn.Text.Contains(digest, StringComparison.OrdinalIgnoreCase));
        }
    }
}
