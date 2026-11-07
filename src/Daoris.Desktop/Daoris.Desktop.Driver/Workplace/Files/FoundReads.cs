using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// When the analysis writes the found layer (the tree-pool design §28.4, <i>When it reads</i>): at a repository's registration, where
/// its line's tip moved and the commits between name a signature file, a manifest, an ignore file or the gates file, where its
/// toolchain's answers moved (asked at most once a day), and at a line's finish, which adds what the session wrote and reads nothing
/// again. A look that finds none of these writes nothing, so the file, which a person may be reading, is never touched by one.
/// </summary>
internal static class FoundReads
{
    /// <summary>What a rule says of a top a session was seen to write; the finish's read looks for it, and a later reading keeps it.</summary>
    internal const string WrittenBySession = "written by a session";

    private static readonly TimeSpan ToolchainEvery = TimeSpan.FromDays(1);

    /// <summary>The last time each repository's toolchain was asked in this process, so a run of looks asks it once a day, not at each.</summary>
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Asked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One look at <paramref name="repository"/>'s line at <paramref name="commit"/>: the found layer written again where a trigger holds,
    /// and the words saying which; null where none does and nothing was written. A git or file failure throws, and the next look tries again.
    /// </summary>
    /// <param name="ask">How a toolchain command is answered in the checkout; <see cref="AskAsync"/> in production.</param>
    internal static async Task<string?> LookAsync(
        string home, string repository, string root, string commit, DateTimeOffset now, Func<string, string, CancellationToken, Task<string>> ask,
        CancellationToken ct)
    {
        var last = FoundFiles.Read(home, repository).Reading;
        var why = last is null ? "it was registered" : null;
        if (why is null && last!.Commit != commit && await MovedAsync(root, last.Commit, commit, ct).ConfigureAwait(false))
        {
            why = "its line moved past a file the analysis reads";
        }

        if (why is null && last is not null && Due(home, repository, now))
        {
            why = await DigestAsync(root, Commands(last.Rules), ask, ct).ConfigureAwait(false) != last.Toolchain ? "its toolchain moved" : null;
        }

        if (why is null) return null;
        var kept = last?.Rules.Where(rule => rule.Kind == FileKind.Scratch && rule.Evidence.Contains(WrittenBySession)).SelectMany(rule => rule.Paths).ToList() ?? [];
        var reading = await FileAnalysis.ReadAsync(root, commit, FileSignatures.Shipped, kept, ct, now).ConfigureAwait(false);
        var digest = await DigestAsync(root, Commands(reading.Found.Rules), ask, ct).ConfigureAwait(false);
        FoundFiles.Write(home, repository, reading.Found with { Toolchain = digest });
        Asked[Key(home, repository)] = now;
        return why;
    }

    /// <summary>
    /// A line's finish: each top the last reading left <c>unknown</c> that the tree held (<paramref name="observed"/>, paths from the tree's
    /// root) is now <c>scratch</c>, written by a session. Nothing is read again, and nothing is written where no top changed. True where it wrote.
    /// </summary>
    internal static bool Finished(string home, string repository, IEnumerable<string> observed)
    {
        if (FoundFiles.Read(home, repository).Reading is not { } last) return false;
        var seen = observed.ToList();
        var changed = false;
        var rules = last.Rules.Select(rule =>
        {
            if (rule.Kind != FileKind.Unknown || !seen.Any(path => rule.Paths.Any(top => path == top || path.StartsWith(top, StringComparison.Ordinal)))) return rule;
            changed = true;
            return rule with { Kind = FileKind.Scratch, Evidence = [WrittenBySession] };
        }).ToList();
        if (!changed) return false;
        FoundFiles.Write(home, repository, last with { Rules = rules });
        return true;
    }

    /// <summary>One toolchain command asked in <paramref name="root"/> through the shell a person's own would be, its answer or why it gave none; never throws.</summary>
    internal static async Task<string> AskAsync(string root, string command, CancellationToken ct)
    {
        var windows = OperatingSystem.IsWindows();
        var shell = windows ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe" : "/bin/sh";
        var answer = await ToolQuestions.AskAsync(shell, [windows ? "/c" : "-c", command], ct, root).ConfigureAwait(false);
        return answer.Problem ?? $"{answer.ExitCode}\n{answer.Output.Trim()}";
    }

    private static async Task<bool> MovedAsync(string root, string from, string to, CancellationToken ct)
    {
        // A commit git no longer holds cannot be compared: the layer is read again.
        if (await FileGit.ChangedAsync(root, from, to, ct).ConfigureAwait(false) is not { } changed) return true;
        return changed.Any(path => path == GeneratedWriters.DeclarationFile || FileSignatures.Shipped.Reaches(path));
    }

    /// <summary>Whether the toolchain is asked now: not yet today in this process; true marks it asked.</summary>
    private static bool Due(string home, string repository, DateTimeOffset now)
    {
        var key = Key(home, repository);
        if (Asked.TryGetValue(key, out var at) && now - at < ToolchainEvery) return false;
        Asked[key] = now;
        return true;
    }

    private static string Key(string home, string repository) => $"{home}|{repository}";

    private static List<string> Commands(IEnumerable<FileRule> rules) =>
        [.. rules.SelectMany(rule => rule.Toolchain ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>The digest of every command's answer, null where there is no command to ask.</summary>
    private static async Task<string?> DigestAsync(
        string root, IReadOnlyList<string> commands, Func<string, string, CancellationToken, Task<string>> ask, CancellationToken ct)
    {
        if (commands.Count == 0) return null;
        var text = new StringBuilder();
        foreach (var command in commands) text.Append(command).Append('\n').Append(await ask(root, command, ct).ConfigureAwait(false)).Append('\0');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }
}
