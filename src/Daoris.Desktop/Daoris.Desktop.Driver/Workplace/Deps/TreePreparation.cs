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
    /// Read from <paramref name="checkout"/>'s gates file, never the tree's: what a session's fresh tree is told is the checkout's say, as
    /// the machine-local inputs are. A file that is absent or does not read declares nothing.
    /// </summary>
    internal static TreePrepDeclared Read(string checkout)
    {
        try
        {
            var file = Path.Combine(checkout, DeclarationNames.Gates);
            return File.Exists(file) ? Of(File.ReadAllText(file)) : None;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return None;
        }
    }

    /// <summary>Read from a declaration's <paramref name="text"/>, as a check reads the gates of the one it runs under; one that does not read declares nothing.</summary>
    internal static TreePrepDeclared Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return None;
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return None;
            var regenerate = new List<string>();
            if (root.TryGetProperty("gates", out var gates) && gates.ValueKind == JsonValueKind.Array)
            {
                foreach (var gate in gates.EnumerateArray())
                {
                    if (gate.ValueKind == JsonValueKind.Object && gate.TryGetProperty("regenerate", out var command) && command.ValueKind == JsonValueKind.String
                        && command.GetString() is { Length: > 0 } written && !regenerate.Contains(written))
                    {
                        regenerate.Add(written);
                    }
                }
            }

            var baseline = root.TryGetProperty("treeBaseline", out var named) && named.ValueKind == JsonValueKind.String && named.GetString() is { Length: > 0 } run ? run : null;
            return new(regenerate, baseline);
        }
        catch (JsonException)
        {
            return None;
        }
    }
}

/// <summary>A <c>regenerate</c> that did not finish: its command, and its exit and output.</summary>
internal sealed record RegenerateFailed(string Command, CommandRan Ran);

/// <summary>
/// What a repository declares to make a tree like its checkout, once its dependencies and machine-local files stand: each <c>regenerate</c>
/// makes a generated input current, and the <c>treeBaseline</c> records the base a later check reads its reds against. A session's tree
/// is handed without either and told them (<see cref="Told"/>): run before the hand they held its open minutes with its agent idle, for
/// work only its later checks read. A verification tree, which no session prepares, runs each <c>regenerate</c> of the declaration a
/// check runs under before that check's gates, on every check, never the baseline (<see cref="RegenerateAsync"/>).
/// </summary>
/// <remarks>
/// Run as a gate's command is (<see cref="IInstallRunner.GateEnvironment"/>), inside the machine's admission, and ended with all it started at the
/// bound or a cancel. The commands are the repository's own, and write only what git ignores.
/// </remarks>
internal static class TreePreparation
{
    /// <summary>How long one command may run: a regenerate may build, as a gate does.</summary>
    internal static readonly TimeSpan Bound = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Runs each of <paramref name="commands"/> in <paramref name="tree"/> in order, the first that does not finish ending the run, since a
    /// later one may read what it makes: that one, or null where each finished. Cheap where nothing it makes is stale (<c>--if-stale</c>).
    /// </summary>
    internal static async Task<RegenerateFailed?> RegenerateAsync(string home, string repository, IReadOnlyList<string> commands, string tree, CancellationToken ct)
    {
        if (commands.Count == 0) return null;
        using var admitted = await InstallRunner.Current.AdmitAsync(home, "tree preparation", repository, tree, ct).ConfigureAwait(false);
        var environment = InstallRunner.Current.GateEnvironment();
        foreach (var command in commands)
        {
            var ran = await DependencyCommands.RunAsync(home, tree, command, tree, environment, Bound, ct).ConfigureAwait(false);
            if (ran.Code != 0) return new RegenerateFailed(command, ran);
        }

        return null;
    }

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
