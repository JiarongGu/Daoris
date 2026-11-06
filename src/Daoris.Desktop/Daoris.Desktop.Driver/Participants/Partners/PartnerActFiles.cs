using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A piece's act on file, <c>&lt;id&gt;.act.json</c> beside the piece's other files (COAGENT1b2): the person's press writes it, and the
/// driver's look, the one writer of the piece's state, takes it, does the act and removes it.
/// </summary>
public sealed class PartnerActFiles(string home)
{
    private readonly PartnerFiles _files = new(home);

    /// <summary>The act waiting on a piece, or null where none waits or the file is unreadable.</summary>
    public PartnerActAsk? Read(string id)
    {
        var path = _files.PathOf(id, "act");
        try
        {
            if (!File.Exists(path)) return null;
            using var root = JsonDocument.Parse(AtomicFile.ReadText(path));
            var element = root.RootElement;
            return Text(element, "act") is { } act && PartnerPersonActs.All.Contains(act)
                ? new PartnerActAsk(Text(element, "id") ?? id, act, Moments.Read(element, "at") ?? DateTimeOffset.UtcNow)
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether a file waits for the piece, readable or not.</summary>
    public bool Waits(string id) => File.Exists(_files.PathOf(id, "act"));

    /// <summary>Keep the act whole. A second press before the look takes the first replaces it: the person's latest word stands.</summary>
    public void Write(PartnerActAsk ask) =>
        JsonFile.Write(_files.PathOf(ask.Id, "act"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", ask.Id);
            writer.WriteString("act", ask.Act);
            writer.WriteString("at", Moments.Write(ask.At));
            writer.WriteEndObject();
        });

    /// <summary>The act is taken, done or refused: its file goes.</summary>
    public void Clear(string id)
    {
        try { File.Delete(_files.PathOf(id, "act")); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
