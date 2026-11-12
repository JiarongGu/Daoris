using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One recorded frame of a real door: the program and adapter versions of its capture (<c>unrecorded</c> where the capture names none),
/// its source, the frame and the readings the door's reader is told to give it, each as <c>kind</c> or <c>kind/status</c>.
/// </summary>
internal sealed record CorpusFrame(string Door, string Program, string? Adapter, string Source, JsonElement Frame, IReadOnlyList<string> Events);

/// <summary>
/// A provider's recorded-frame corpus, <c>fixtures/providers/&lt;provider&gt;/corpus.jsonl</c>, one frame per line, read by the dialect
/// case over each door of it: a protocol door's update through the protocol's reader, a native door's line through its dialect's mapper.
/// </summary>
internal static class ProviderCorpus
{
    public const string FileName = "corpus.jsonl";

    public static IReadOnlyList<CorpusFrame> Of(string provider)
    {
        var file = Path.Combine(SourceFiles.Project(SourceFiles.DriverTestsProject), "fixtures", "providers", provider, FileName);
        if (!File.Exists(file)) return [];

        return
        [
            .. File.ReadLines(file).Where(line => line.Trim().Length > 0).Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                return new CorpusFrame(
                    root.GetProperty("door").GetString()!,
                    root.GetProperty("program").GetString()!,
                    root.GetProperty("adapter").GetString(),
                    root.GetProperty("source").GetString()!,
                    root.GetProperty("frame").Clone(),
                    [.. root.GetProperty("events").EnumerateArray().Select(each => each.GetString()!)]);
            }),
        ];
    }

    /// <summary>The frames of one door.</summary>
    public static IReadOnlyList<CorpusFrame> Of(string provider, string door) =>
        [.. Of(provider).Where(frame => frame.Door == door)];

    /// <summary>The readings the door's reader gives a frame, each as the corpus says it.</summary>
    public static IReadOnlyList<string> Read(AgentDoor door, CorpusFrame frame)
    {
        if (door.Transport == Transport.Protocol)
        {
            return AcpRender.Map(frame.Frame) is { } mapped ? [Said(mapped)] : [];
        }

        var mapper = ((INativeDialect)door.Dialect.Required).Mapper();
        return [.. mapper.Read(NativeFrame.Of(frame.Frame.GetRawText())).Events.Select(Said)];
    }

    private static string Said(SessionEvent mapped) => mapped.Status is { Length: > 0 } status ? $"{mapped.Kind}/{status}" : mapped.Kind.ToString();
}
