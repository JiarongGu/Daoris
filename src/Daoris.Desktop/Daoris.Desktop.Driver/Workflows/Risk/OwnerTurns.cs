using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One of the owner's own turns in a conversation: where it is said, when, and its words.</summary>
/// <param name="Id">The turn's name, <c>&lt;session&gt;#&lt;seq&gt;</c>, which a change's journal line names.</param>
public sealed record OwnerTurn(string Id, string Session, DateTimeOffset At, string Text);

/// <summary>
/// The person's own turns in a conversation, read from the conversation's kept events: a message the person's own door kept
/// (<see cref="AskQuotes.PersonOrigin"/>), never an agent's claim or a quote an agent relays, a helper's or an opinion's words, a channel
/// message or Daoris's own words. A turn confirms a proposal only in the bounded form its card says, <c>apply &lt;id&gt; &lt;digest&gt;</c>,
/// and the owner's latest turn naming the proposal is the one that stands, so a later refusal withdraws an earlier yes.
/// </summary>
public sealed class OwnerTurns(Func<string, IEnumerable<SessionEvent>> events)
{
    /// <summary>The owner's latest turn of <paramref name="session"/> at or after <paramref name="since"/> that names <paramref name="id"/>, where it confirms the proposal; else null.</summary>
    public OwnerTurn? Binding(string session, DateTimeOffset since, string id, string digest)
    {
        var latest = events(session)
            .Where(e => e.Kind == SessionEventKind.User && e.Origin == AskQuotes.PersonOrigin && e.From is null && e.Channel is null && e.Helper is null && e.Opinion is null)
            .Where(e => e.At >= since && e.Text is { } text && text.Contains(id, StringComparison.OrdinalIgnoreCase))
            .MaxBy(e => e.Seq);
        return latest is not null && Confirms(latest.Text!, id, digest) ? new OwnerTurn($"{session}#{latest.Seq}", session, latest.At, latest.Text!) : null;
    }

    /// <summary>Whether <paramref name="text"/> is the confirmation of the proposal: <c>apply &lt;id&gt; &lt;digest&gt;</c>, with only a yes or a please before it and punctuation around it.</summary>
    public static bool Confirms(string text, string id, string digest) =>
        Regex.IsMatch(
            text.Trim(),
            $@"^(?:(?:yes|ok|okay|please|confirm|confirmed)\W+)*apply\s+{Regex.Escape(id)}\s+\(?{Regex.Escape(digest)}\)?\W*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
