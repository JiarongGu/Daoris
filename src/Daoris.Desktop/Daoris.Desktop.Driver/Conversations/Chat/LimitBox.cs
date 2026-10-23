using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// An ended conversation's move at its limit's wake, kept until its words are delivered (LIMITRESUME1c, D206 §3.5, Codex 8):
/// <c>&lt;home&gt;/sessions/&lt;session&gt;.limit-move.json</c>, beside D163's hand-over box. Written before the new part opens, it is
/// the move's one durable owner: what the message is composed from, and what a shell coming up delivers again.
/// </summary>
/// <param name="Why">The move's reason: <c>limit</c> for another account ready first, or the refusal's code that proved its own conversation lost.</param>
/// <param name="Before">How many of the record's events stand before the first refused turn's words: the brief is composed from those alone.</param>
/// <param name="Through">How many of the record's events there were when it was written: the refused turns' words are read within them.</param>
/// <param name="Said">The ids of the words said while it waited, still on the old record until they are delivered.</param>
/// <param name="To">The new part once it opened; null before.</param>
public sealed record LimitMoveBox(string Why, int Before, int Through, IReadOnlyList<string> Said, string? To = null);

/// <summary>
/// The ended-record moves this home keeps (LIMITRESUME1c), read without trusting their shape and written whole, as BOM-less UTF-8
/// with LF. <b>This machine's, never the record's.</b> A file that does not read is no move: the wake tries again.
/// </summary>
public sealed class LimitBoxes(string home)
{
    public const string Suffix = ".limit-move.json";

    /// <summary>Where a session's box is kept.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>Keep the move, replacing any before.</summary>
    /// <exception cref="IOException">The box could not be written: no move is made, and the wake tries again.</exception>
    public void Write(string session, LimitMoveBox box)
    {
        if (!SessionEventText.IsLocalId(session)) return;
        JsonFile.Write(PathOf(session), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("why", box.Why);
            writer.WriteNumber("before", box.Before);
            writer.WriteNumber("through", box.Through);
            writer.WriteStartArray("said");
            foreach (var id in box.Said) writer.WriteStringValue(id);
            writer.WriteEndArray();
            if (box.To is { } to) writer.WriteString("to", to);
            writer.WriteEndObject();
        });
    }

    /// <summary>The move kept for this session, or null for none or a file that does not read.</summary>
    public LimitMoveBox? Read(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return null;
        var path = PathOf(session);
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "why") is not { Length: > 0 } why
                || Int(root, "before") is not { } before || Int(root, "through") is not { } through)
            {
                return null;
            }

            return new LimitMoveBox(why, before, through, Strings(root, "said"), Text(root, "to") is { Length: > 0 } to ? to : null);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The sessions with a box, in the order of their ids: what a shell coming up delivers.</summary>
    public IReadOnlyList<string> Pending() =>
        [.. HandOverFiles.Leaves(DaorisHome.Entry(home, HomeEntry.Sessions), "*" + Suffix)
            .Select(leaf => leaf[..^Suffix.Length])
            .Where(SessionEventText.IsLocalId)
            .Order(StringComparer.Ordinal)];

    /// <summary>The session whose box names <paramref name="to"/> as its new part, or null for none.</summary>
    public string? MovedTo(string to) =>
        Pending().FirstOrDefault(session => string.Equals(Read(session)?.To, to, StringComparison.OrdinalIgnoreCase));

    /// <summary>Take a session's box away: its words were delivered, or no move is under way. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        if (SessionEventText.IsLocalId(session)) HandOverFiles.Delete(PathOf(session));
    }
}
