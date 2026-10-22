using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// An agent's ask to hand its session over, two files per session under the home's sessions folder (MIXAGENT1c):
/// <c>&lt;session&gt;.hand-over-ask.json</c>, read as the connector's <c>handover_propose</c> writes it, and
/// <c>&lt;session&gt;.hand-over-state.json</c>, the driver's alone, written as the service's <c>HandOverAskBox</c> reads it. The file
/// is the contract; the two share no code, and each side's tests hold the same shapes.
/// </summary>
/// <remarks>
/// Neither is the hand-over's box (<see cref="HandOverBox"/>, <c>&lt;session&gt;.hand-over.json</c>), which whoever runs the session takes
/// at once: an ask becomes a box only where the person's rule or their yes lets it go.
/// </remarks>
public sealed class HandOverAskFiles(string home)
{
    public const string AskSuffix = ".hand-over-ask.json";

    public const string StateSuffix = ".hand-over-state.json";

    private string PathOf(string session, string suffix) => DaorisHome.Entry(home, HomeEntry.Sessions, session + suffix);

    /// <summary>Every ask on file, oldest first; one unreadable or of another shape is not an ask here.</summary>
    public IReadOnlyList<HandOverAsk> Asks() =>
        [.. HandOverFiles.Leaves(DaorisHome.Entry(home, HomeEntry.Sessions), "*" + AskSuffix)
            .Select(leaf => leaf[..^AskSuffix.Length])
            .Where(SessionEventText.IsLocalId)
            .Select(Ask)
            .OfType<HandOverAsk>()
            .OrderBy(ask => ask.At)
            .ThenBy(ask => ask.Session, StringComparer.Ordinal)];

    /// <summary>The session's ask, or null for none, a file that does not read, or an id that is not one.</summary>
    public HandOverAsk? Ask(string session)
    {
        if (!SessionEventText.IsLocalId(session) || Parse(PathOf(session, AskSuffix)) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (element.ValueKind != JsonValueKind.Object || Text(element, "id") is not { Length: > 0 } id || Text(element, "note") is not { } note)
            {
                return null;
            }

            return new HandOverAsk(id, session, note, Moments.Read(element, "at") ?? File.GetLastWriteTimeUtc(PathOf(session, AskSuffix)));
        }
    }

    /// <summary>The session's ask's state, or null where the driver wrote none or it is unreadable.</summary>
    public HandOverAskState? ReadState(string session)
    {
        if (!SessionEventText.IsLocalId(session) || Parse(PathOf(session, StateSuffix)) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (element.ValueKind != JsonValueKind.Object || Text(element, "id") is not { Length: > 0 } id
                || Text(element, "state") is not { } state || !HandOverAskStates.All.Contains(state))
            {
                return null;
            }

            return new HandOverAskState(id, state, Text(element, "code"), Text(element, "agent"), Moments.Read(element, "at") ?? DateTimeOffset.UtcNow);
        }
    }

    /// <summary>Keep the session's ask's state whole, atomically: the driver is its only writer.</summary>
    public void WriteState(string session, HandOverAskState state)
    {
        if (!SessionEventText.IsLocalId(session)) return;
        JsonFile.Write(PathOf(session, StateSuffix), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("state", state.State);
            if (state.Code is null) writer.WriteNull("code"); else writer.WriteString("code", state.Code);
            if (state.Agent is null) writer.WriteNull("agent"); else writer.WriteString("agent", state.Agent);
            writer.WriteString("at", Moments.Write(state.At));
            writer.WriteEndObject();
        });
    }

    /// <summary>Both of a session's files gone: its session left this machine. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        if (!SessionEventText.IsLocalId(session)) return;
        HandOverFiles.Delete(PathOf(session, AskSuffix));
        HandOverFiles.Delete(PathOf(session, StateSuffix));
    }

    private static JsonDocument? Parse(string path)
    {
        try
        {
            return File.Exists(path) ? JsonDocument.Parse(AtomicFile.ReadText(path)) : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
