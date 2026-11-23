namespace Daoris.Driver;

/// <summary>One of the owner's own turns in a conversation: where it is said, when, and its words.</summary>
/// <param name="Id">The turn's name, <c>&lt;session&gt;#&lt;seq&gt;</c>, which a change's journal line names.</param>
public sealed record OwnerTurn(string Id, string Session, DateTimeOffset At, string Text);

/// <summary>
/// The person's own turns in a conversation, read from the conversation's kept events: a message the person's own door kept
/// (<see cref="AskQuotes.PersonOrigin"/>), never an agent's claim or a quote an agent relays, a helper's or an opinion's words, a channel
/// message or Daoris's own words. What a proposed change to the policy is bound to.
/// </summary>
public sealed class OwnerTurns(Func<string, IEnumerable<SessionEvent>> events)
{
    /// <summary>The first turn of <paramref name="session"/> at or after <paramref name="since"/> that names both <paramref name="id"/> and <paramref name="digest"/>.</summary>
    public OwnerTurn? Binding(string session, DateTimeOffset since, string id, string digest) =>
        events(session)
            .Where(e => e.Kind == SessionEventKind.User && e.Origin == AskQuotes.PersonOrigin && e.From is null && e.Channel is null && e.Helper is null && e.Opinion is null)
            .Where(e => e.At >= since && e.Text is { } text
                && text.Contains(id, StringComparison.OrdinalIgnoreCase) && text.Contains(digest, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Seq)
            .Select(e => new OwnerTurn($"{session}#{e.Seq}", session, e.At, e.Text!))
            .FirstOrDefault();
}
