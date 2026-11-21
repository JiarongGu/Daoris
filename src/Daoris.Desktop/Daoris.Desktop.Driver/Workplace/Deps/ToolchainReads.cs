using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// One reading of a tree's keys: each <c>toolchain</c> command run once at the tree's root for each environment it runs in, its output
/// handed to every key that names it. The npm signature gives each <c>package.json</c> folder the same three reads, and a key reads
/// them before any install of the stage runs, so their input is the same for every folder: run per folder, they are most of a large
/// web repository's read of its keys. Kept for one reading only, never written: the environment's values may be secret, and a
/// toolchain changes between stages. The npm signature's three reads alone are also held in the driver's memory for
/// <see cref="Memo.Lifetime"/> under <see cref="NpmToolchain"/>'s fingerprint of what decides them, so an opening that follows another
/// does not start npm again for what no program or configuration file has changed.
/// </summary>
internal sealed class ToolchainReads(string home, string tree)
{
    private readonly Dictionary<string, CommandRan> _read = new(StringComparer.Ordinal);

    /// <summary>What <paramref name="command"/> printed at the tree's root in <paramref name="environment"/>, run where this reading has not run it there yet.</summary>
    internal async Task<CommandRan> ReadAsync(string command, IReadOnlyDictionary<string, string> environment, CancellationToken ct)
    {
        var asked = string.Join('\0', new[] { command }.Concat(environment.OrderBy(each => each.Key, StringComparer.OrdinalIgnoreCase).Select(each => $"{each.Key}={each.Value}")));
        if (_read.TryGetValue(asked, out var read)) return read;
        var fingerprint = Memo.Enabled ? NpmToolchain.Fingerprint(command, environment, tree) : null;
        if (fingerprint is not null && Memo.Held(asked, fingerprint) is { } held)
        {
            _read[asked] = held;
            return held;
        }

        read = await DependencyCommands.RunAsync(home, tree, command, tree, environment, DependencyCommands.ReadBound, ct).ConfigureAwait(false);
        if (fingerprint is not null && read.Code == 0 && read.Output.Trim().Length > 0) Memo.Keep(asked, fingerprint, read with { Output = DependencyKeys.Tokened(read.Output, tree) });
        _read[asked] = read;
        return read;
    }

    /// <summary>The npm signature's reads held for a while, each under what decided it; memory only, so a secret in a configuration's output never reaches disk.</summary>
    internal static class Memo
    {
        /// <summary>How long a reading is held: a program or file moved sooner changes the fingerprint, this bounds what no fingerprint sees.</summary>
        internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        /// <summary>The test seam: set where a test runs its own commands, so no reading of another test is handed to it.</summary>
        internal static readonly AsyncLocal<bool> Disabled = new();

        /// <summary>The test seam: milliseconds a flow's readings are older than the machine's monotonic clock says, so a test passes the lifetime without waiting.</summary>
        internal static readonly AsyncLocal<long> Older = new();

        private static long Now() => Environment.TickCount64 + Older.Value;

        private static readonly ConcurrentDictionary<string, (long At, string Fingerprint, CommandRan Ran)> Kept = new(StringComparer.Ordinal);

        internal static bool Enabled => !Disabled.Value;

        internal static CommandRan? Held(string asked, string fingerprint) =>
            Kept.TryGetValue(asked, out var each) && each.Fingerprint == fingerprint && Now() - each.At < Lifetime.TotalMilliseconds ? each.Ran : null;

        internal static void Keep(string asked, string fingerprint, CommandRan ran) => Kept[asked] = (Now(), fingerprint, ran);

        internal static void Clear() => Kept.Clear();
    }
}
