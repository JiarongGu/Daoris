using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// When the analysis writes the found layer (design §28.4, <i>When it reads</i>): at registration, where the line moved past a signature,
/// manifest, ignore or gates file, where the toolchain's answers moved, and at a finish. A look that finds none writes nothing.
/// </summary>
internal static class FoundReads
{
    /// <summary>The evidence a finish gives a top, which a later reading finds by it and keeps.</summary>
    internal const string WrittenBySession = "written by a session";

    private static readonly TimeSpan ToolchainEvery = TimeSpan.FromDays(1);

    /// <summary>The last asking of each repository's toolchain, so a run of looks asks it once a day.</summary>
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Asked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The layer written where a trigger holds, and why; null where none does. A failure throws, and the next look tries again.</summary>
    /// <param name="ask">Answers a toolchain command in the checkout; <see cref="AskAsync"/> in production.</param>
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
        var kept = last?.Rules.Where(rule => rule.Kind == FileKind.Scratch && rule.Evidence.Contains(WrittenBySession)).SelectMany(rule => rule.Paths.Select(path => GitPattern.LiteralOf(rule, path))).OfType<string>().ToList() ?? [];
        var reading = await FileAnalysis.ReadAsync(root, commit, FileSignatures.Shipped, kept, ct, now).ConfigureAwait(false);
        var digest = await DigestAsync(root, Commands(reading.Found.Rules), ask, ct).ConfigureAwait(false);
        FoundFiles.Write(home, repository, reading.Found with { Toolchain = digest });
        Asked[Key(home, repository)] = now;
        return why;
    }

    /// <summary>A finish: an <c>unknown</c> top among <paramref name="observed"/> becomes <c>scratch</c>, nothing read again. True where it wrote.</summary>
    internal static bool Finished(string home, string repository, IEnumerable<string> observed)
    {
        if (FoundFiles.Read(home, repository).Reading is not { } last) return false;
        var seen = observed.ToList();
        var changed = false;
        var rules = last.Rules.Select(rule =>
        {
            if (rule.Kind != FileKind.Unknown || !seen.Any(path => rule.Paths.Select(each => GitPattern.LiteralOf(rule, each)).OfType<string>().Any(top => path == top || path.StartsWith(top, StringComparison.Ordinal)))) return rule;
            changed = true;
            return rule with { Kind = FileKind.Scratch, Evidence = [WrittenBySession] };
        }).ToList();
        if (!changed) return false;
        FoundFiles.Write(home, repository, last with { Rules = rules });
        return true;
    }

    /// <summary>One toolchain command asked in <paramref name="root"/> through the shell, its answer or why it gave none.</summary>
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
        return changed.Any(path => path == DeclarationNames.Gates || FileSignatures.Shipped.Reaches(path));
    }

    /// <summary>Whether the toolchain is due; true marks it asked.</summary>
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
