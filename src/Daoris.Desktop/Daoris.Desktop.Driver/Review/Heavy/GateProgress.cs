using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The gates an attempt has passed so far, kept in its folder as each passes (<c>progress.json</c>), so an attempt Daoris's close cut off
/// goes on at the gate it reached when the same attempt is sent again, never started over. It holds only for the same work, base and
/// declaration: anything else, or a file that does not read, holds nothing and every gate runs.
/// </summary>
internal static class GateProgress
{
    internal const string File = "progress.json";

    /// <summary>The gates passed in <paramref name="folder"/>'s attempt for <paramref name="commit"/> on <paramref name="onto"/> under <paramref name="declaration"/>, by name.</summary>
    internal static IReadOnlyDictionary<string, VerifyGate> Read(string folder, string commit, string? onto, string? declaration)
    {
        var path = Path.Combine(folder, File);
        try
        {
            if (!System.IO.File.Exists(path)) return new Dictionary<string, VerifyGate>();
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (Text(root, "commit") != commit || Text(root, "onto") != onto || Text(root, "declaration") != declaration
                || !root.TryGetProperty("passed", out var passed) || passed.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, VerifyGate>();
            }

            var gates = new Dictionary<string, VerifyGate>(StringComparer.Ordinal);
            foreach (var each in passed.EnumerateArray())
            {
                if (Text(each, "name") is not { } name) continue;
                gates[name] = new VerifyGate(name, RunnerVerdicts.Pass, 0)
                {
                    WallSeconds = each.TryGetProperty("wallSeconds", out var wall) && wall.TryGetDouble(out var seconds) ? seconds : null, Log = Text(each, "log"),
                    Command = Text(each, "command"),
                };
            }

            return gates;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return new Dictionary<string, VerifyGate>();
        }
    }

    /// <summary>Keeps <paramref name="passed"/> as the attempt's progress, whole; a write that fails keeps nothing, so the gates run again.</summary>
    internal static void Write(string folder, string commit, string? onto, string? declaration, IReadOnlyList<VerifyGate> passed)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("commit", commit);
            if (onto is not null) writer.WriteString("onto", onto);
            if (declaration is not null) writer.WriteString("declaration", declaration);
            writer.WriteStartArray("passed");
            foreach (var gate in passed.Where(each => each.Verdict == RunnerVerdicts.Pass))
            {
                writer.WriteStartObject();
                writer.WriteString("name", gate.Name);
                if (gate.WallSeconds is { } seconds) writer.WriteNumber("wallSeconds", Math.Round(seconds, 1));
                if (gate.Log is { } log) writer.WriteString("log", log);
                if (gate.Command is { } command) writer.WriteString("command", command);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        try
        {
            Directory.CreateDirectory(folder);
            AtomicFile.WriteText(Path.Combine(folder, File), Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The words that say the attempt went on where Daoris's close had cut it off; null where it carried none.</summary>
    internal static string? Words(IReadOnlyList<string> carried) =>
        carried.Count == 0 ? null : $"It went on at the gate it had reached when Daoris closed: {string.Join(", ", carried)} had passed in this attempt and did not run again.";

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
