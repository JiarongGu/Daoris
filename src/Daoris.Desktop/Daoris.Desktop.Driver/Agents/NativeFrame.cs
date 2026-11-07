using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One line of a native door's structured stdout, parsed once: the capture reads each line into one
/// frame, and every reader of the line takes the frame, never the text again: the dialect's mapper (<see cref="IStreamMapper"/>), the
/// streams beside (<see cref="IStreamsReader"/>, a child's words) and the children and background tasks it says (<see cref="NativeChildren"/>,
/// <see cref="NativeTasks"/>), whose readings alone the part's lineage, the run's own ending and the streams beside read of a start or an end.
/// </summary>
public sealed class NativeFrame
{
    private NativeFrame(string line, JsonElement root)
    {
        Line = line;
        Root = root;
    }

    /// <summary>The line as the harness wrote it.</summary>
    public string Line { get; }

    /// <summary>The line's object; <see cref="JsonValueKind.Undefined"/> where it is none (<see cref="IsObject"/>).</summary>
    public JsonElement Root { get; }

    /// <summary>Whether the line parsed to a JSON value at all; a line that did not is said outside the protocol.</summary>
    public bool Parsed => Root.ValueKind != JsonValueKind.Undefined;

    /// <summary>Whether the line is a frame of the protocol: a JSON object.</summary>
    public bool IsObject => Root.ValueKind == JsonValueKind.Object;

    /// <summary>The frame's <c>type</c>; null where it says none in a string.</summary>
    public string? Type => Text("type");

    /// <summary>The frame's <c>subtype</c>; null where it says none in a string.</summary>
    public string? Subtype => Text("subtype");

    /// <summary>A string field of the frame; null where it is absent or not a string, or the line is no frame.</summary>
    public string? Text(string name) => IsObject ? Json.Text(Root, name) : null;

    /// <summary>Read one line, once. A line that is no JSON keeps its text alone.</summary>
    public static NativeFrame Of(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return new(line, default);
        try
        {
            using var document = JsonDocument.Parse(line);
            return new(line, document.RootElement.Clone());
        }
        catch (JsonException)
        {
            return new(line, default);
        }
    }
}
