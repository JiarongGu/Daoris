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
/// A new tree made like its checkout before a session gets it: once its dependencies and machine-local files stand, each declared
/// <c>regenerate</c> makes a generated input current, then the declared <c>treeBaseline</c> runs one fast gate whose verdicts the tree
/// keeps as its base, so a later run says which reds are the session's and which the base stood in. A red base is the point of the
/// run, never a failure; a command that could not run, or ran past its bound, is said and the start goes on.
/// </summary>
/// <remarks>
/// Run as a gate's command is (<see cref="IInstallRunner.GateEnvironment"/>), inside the machine's admission, and ended with all it started at the
/// bound, a cancel or a terminal's visit. The commands are the repository's own, from the checkout's declaration, and write only
/// what git ignores.
/// </remarks>
internal static class TreePreparation
{
    /// <summary>How long one command may run: a baseline builds and tests, as a gate does.</summary>
    internal static readonly TimeSpan Bound = TimeSpan.FromMinutes(30);

    /// <summary>What the exit of a baseline command means: 0 green and 1 reds recorded are both a base; any other did not record one.</summary>
    internal static bool Recorded(int code) => code is 0 or 1;

    /// <summary>
    /// Runs the declaration in <paramref name="tree"/>, saying each command that failed. Visited is true where a terminal's visit ended
    /// one, which stops the stage as it does an install; Told is what the agent is told where a baseline ran and recorded.
    /// </summary>
    internal static async Task<(bool Visited, string? Told)> RunAsync(
        string home, string repository, string checkout, string tree, List<Noted> said, CancellationToken ct, CancellationToken visited)
    {
        var declared = TreePrepDeclared.Read(checkout);
        if (declared.Empty) return (false, null);
        string? told = null;
        using var admitted = await InstallRunner.Current.AdmitAsync(home, "tree preparation", repository, tree, ct).ConfigureAwait(false);
        var environment = InstallRunner.Current.GateEnvironment();
        foreach (var command in declared.Regenerate.Concat(declared.Baseline is { } baseline ? [baseline] : []))
        {
            var ran = await DependencyCommands.RunAsync(home, tree, command, tree, environment, Bound, ct, visited).ConfigureAwait(false);
            if (ran.Code == DependencyCommands.Visited) return (true, null);
            if (command == declared.Baseline && Recorded(ran.Code))
            {
                told = $"Daoris ran `{command}` in this tree before you began, and the repository recorded its base there: a check that is red at the base was red before your work.";
                continue;
            }

            if (ran.Code == 0) continue;
            said.Add(Noted.Of(StartNoteCodes.StartedPrepFailed, $"The tree's preparation `{command}` did not finish: {ran.Tail}.", ("command", command), ("why", ran.Tail)));
        }

        return (false, told);
    }
}
