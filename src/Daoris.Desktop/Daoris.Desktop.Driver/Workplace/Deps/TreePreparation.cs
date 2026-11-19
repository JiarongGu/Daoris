using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a repository declares a prepared tree runs: the gates' regenerate commands, then one fast gate run whose verdicts are the base.</summary>
/// <param name="Regenerate">Each distinct <c>regenerate</c> of its gates, in the gates' order.</param>
/// <param name="Baseline">The top-level <c>treeBaseline</c> command, or null where none is declared.</param>
internal sealed record TreePrepDeclared(IReadOnlyList<string> Regenerate, string? Baseline)
{
    internal static readonly TreePrepDeclared None = new([], null);

    internal bool Empty => Regenerate.Count == 0 && Baseline is null;

    /// <summary>
    /// Read from <paramref name="checkout"/>'s gates file, never the tree's: what runs as a tree opens is the checkout's say, as the
    /// machine-local inputs are. A file that is absent or does not read declares nothing.
    /// </summary>
    internal static TreePrepDeclared Read(string checkout)
    {
        try
        {
            var file = Path.Combine(checkout, DeclarationNames.Gates);
            if (!File.Exists(file)) return None;
            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return None;
            var regenerate = new List<string>();
            if (root.TryGetProperty("gates", out var gates) && gates.ValueKind == JsonValueKind.Array)
            {
                foreach (var gate in gates.EnumerateArray())
                {
                    if (gate.ValueKind == JsonValueKind.Object && gate.TryGetProperty("regenerate", out var command) && command.ValueKind == JsonValueKind.String
                        && command.GetString() is { Length: > 0 } text && !regenerate.Contains(text))
                    {
                        regenerate.Add(text);
                    }
                }
            }

            var baseline = root.TryGetProperty("treeBaseline", out var named) && named.ValueKind == JsonValueKind.String && named.GetString() is { Length: > 0 } run ? run : null;
            return new(regenerate, baseline);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return None;
        }
    }
}

/// <summary>
/// What a repository declares to make a tree like its checkout, once its dependencies and machine-local files stand: each <c>regenerate</c>
/// makes a generated input current, and the <c>treeBaseline</c> records the base a later check reads its reds against. A session's tree
/// is handed without either and told them (<see cref="Told"/>): run before the hand they held its open minutes with its agent idle, for
/// work only its later checks read. A verification tree, which no session prepares, runs the <c>regenerate</c> of the gates a check runs,
/// before each check (<c>VerifyExecution</c>), never the baseline.
/// </summary>
internal static class TreePreparation
{
    /// <summary>What a session handed a fresh tree is told of its checkout's declaration; null where it declares nothing.</summary>
    internal static string? Told(string checkout)
    {
        var declared = TreePrepDeclared.Read(checkout);
        if (declared.Empty) return null;
        var commands = string.Join(", then ", declared.Regenerate.Concat(declared.Baseline is { } named ? [named] : []).Select(command => $"`{command}`"));
        return $"This tree is fresh and Daoris ran none of the preparation its repository declares, so you started at once: run {commands} "
            + (declared.Baseline is null
                ? "before you change a file, so its generated inputs are current."
                : "before you change a file: the last records the base, so a check that is red at the base reads as red before your work.")
            + " They take minutes and change no tracked file, so you may read and plan while they run.";
    }
}
