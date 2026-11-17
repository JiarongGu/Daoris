namespace Daoris.Driver;

/// <summary>
/// One reading of a tree's keys: each <c>toolchain</c> command run once at the tree's root for each environment it runs in, its output
/// handed to every key that names it. The npm signature gives each <c>package.json</c> folder the same three reads, and a key reads
/// them before any install of the stage runs, so their input is the same for every folder: run per folder, they are most of a large
/// web repository's read of its keys. Kept for one reading only, never written: the environment's values may be secret, and a
/// toolchain changes between stages.
/// </summary>
internal sealed class ToolchainReads(string home, string tree)
{
    private readonly Dictionary<string, CommandRan> _read = new(StringComparer.Ordinal);

    /// <summary>What <paramref name="command"/> printed at the tree's root in <paramref name="environment"/>, run where this reading has not run it there yet.</summary>
    internal async Task<CommandRan> ReadAsync(string command, IReadOnlyDictionary<string, string> environment, CancellationToken ct)
    {
        var asked = string.Join('\0', new[] { command }.Concat(environment.OrderBy(each => each.Key, StringComparer.OrdinalIgnoreCase).Select(each => $"{each.Key}={each.Value}")));
        if (_read.TryGetValue(asked, out var read)) return read;
        read = await DependencyCommands.RunAsync(home, tree, command, tree, environment, DependencyCommands.ReadBound, ct).ConfigureAwait(false);
        _read[asked] = read;
        return read;
    }
}
