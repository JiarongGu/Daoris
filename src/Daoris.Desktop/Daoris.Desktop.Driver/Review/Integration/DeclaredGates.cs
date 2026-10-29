using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A gate the accepted declaration declares: the name it writes (the one a runner's answer names it by), its command, the folder it runs in (relative to the tree) and whether it is heavy.</summary>
internal sealed record DeclaredGate(string Name, string Run, string? Cwd, bool Heavy);

/// <summary>
/// The gates of the accepted declaration's text (INTEGRATE1f part f2; the integration design §4.3, F6): read from the blob alone, never from
/// the tree, since the tree is the candidate's and the declaration accepted is the owner's. A text that does not read, or keeps no gate
/// that names a command, declares none, and nothing runs under it.
/// </summary>
internal static class DeclaredGates
{
    /// <summary>The declaration's gates in the order written; a gate without a name or a command is passed over, a name written twice is kept once.</summary>
    internal static IReadOnlyList<DeclaredGate> Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("gates", out var list)
                || list.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var gates = new List<DeclaredGate>();
            foreach (var each in list.EnumerateArray())
            {
                if (each.ValueKind != JsonValueKind.Object) continue;
                string? Text(string name) => each.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } read ? read : null;
                if (Text("name") is not { } gate || Text("run") is not { } run || gates.Any(known => known.Name == gate)) continue;
                gates.Add(new DeclaredGate(gate, run, Text("cwd"), each.TryGetProperty("heavy", out var heavy) && heavy.ValueKind == JsonValueKind.True));
            }

            return gates;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
