using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A gate a checkout's <c>daoris.gates.json</c> declares, as a heavy run reads it (HEAVYRUN1a).</summary>
/// <param name="Name">Its name.</param>
/// <param name="Run">Its command line, run through the platform's shell.</param>
/// <param name="WorkingDirectory">Its <c>cwd</c>, under the checkout; null for the checkout itself.</param>
/// <param name="Heavy">Whether the repository marks it <c>"heavy": true</c>: one that runs only through the slot.</param>
public sealed record HeavyGate(string Name, string Run, string? WorkingDirectory, bool Heavy);

/// <summary>The gates a run takes, or why it takes none and the exit that says it.</summary>
/// <param name="Gates">In the order the repository declares them.</param>
/// <param name="Refusal">Why nothing is run; null where something is.</param>
/// <param name="Exit">0 where something is run; 1 a gate named that is not heavy; 2 nothing there to run.</param>
public sealed record HeavyChoice(IReadOnlyList<HeavyGate> Gates, string? Refusal, int Exit);

/// <summary>
/// Which gates a repository's heavy run runs (HEAVYRUN1a, D180's HEAVYRUN1 note): those its <c>daoris.gates.json</c> marks
/// <c>"heavy": true</c>, where it declares its gates, in the order it declares them; a repository names its heavy gates itself.
/// </summary>
/// <remarks>
/// The file's other readers, the devkit's <c>GateDeclaration</c> and the merge tool's <c>readDeclared</c>, read a gate's name, command and
/// folder and pass over a key they do not know, so <c>heavy</c> changes nothing for them. This reads those three as the devkit does,
/// and refuses what the devkit refuses: a file that is not JSON, and a gate with no name or no command.
/// </remarks>
public static class HeavyGates
{
    /// <summary>The file, at the checkout's root.</summary>
    public const string FileName = "daoris.gates.json";

    /// <summary>The checkout's declared gates, in order.</summary>
    /// <exception cref="DriverException">There is no file, or it does not read: a heavy run cannot tell what to run.</exception>
    public static IReadOnlyList<HeavyGate> Read(string checkout)
    {
        var file = Path.Combine(checkout, FileName);
        if (!File.Exists(file))
        {
            throw new DriverException($"no {FileName} at {checkout}: `run-heavy` runs the gates a repository declares there, and this one declares none.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(file));
        }
        catch (JsonException error)
        {
            throw new DriverException($"{FileName} is not valid JSON: {error.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("gates", out var gates) || gates.ValueKind != JsonValueKind.Array) return [];

            var declared = new List<HeavyGate>();
            foreach (var gate in gates.EnumerateArray())
            {
                if (Text(gate, "name") is not { } name || Text(gate, "run") is not { } run)
                {
                    throw new DriverException($"{FileName}: every entry in 'gates' needs both 'name' and 'run'.");
                }

                var heavy = false;
                if (gate.TryGetProperty("heavy", out var marked))
                {
                    heavy = marked.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => throw new DriverException($"{FileName}: `{name}`'s \"heavy\" is true or false, not {marked.GetRawText()}."),
                    };
                }

                declared.Add(new HeavyGate(name, run, Text(gate, "cwd"), heavy));
            }

            return declared;
        }
    }

    /// <summary>Every heavy gate in declared order, or the one <paramref name="named"/>; or why none, with the exit that says it.</summary>
    public static HeavyChoice Choose(IReadOnlyList<HeavyGate> declared, string? named)
    {
        var heavy = declared.Where(gate => gate.Heavy).ToList();
        if (named is null)
        {
            return heavy.Count > 0
                ? new HeavyChoice(heavy, null, 0)
                : new HeavyChoice([], $"{FileName} marks no gate `\"heavy\": true`, so there is nothing for `run-heavy` to run.", 2);
        }

        if (declared.FirstOrDefault(gate => gate.Name == named) is not { } chosen)
        {
            return new HeavyChoice([], $"{FileName} declares no gate `{named}`; its gates: {Names(declared)}.", 2);
        }

        return chosen.Heavy
            ? new HeavyChoice([chosen], null, 0)
            : new HeavyChoice(
                [],
                $"`{named}` is not marked heavy in {FileName}, so `run-heavy` does not run it: run it as you would, or mark it `\"heavy\": true`. Its heavy gates: {Names(heavy)}.",
                1);
    }

    private static string Names(IReadOnlyList<HeavyGate> gates) => gates.Count == 0 ? "none" : string.Join(", ", gates.Select(gate => gate.Name));

    private static string? Text(JsonElement gate, string name) =>
        gate.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
