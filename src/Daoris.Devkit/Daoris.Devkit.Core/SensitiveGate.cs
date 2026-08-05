using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Devkit;

/// <summary>What the scan is looking at.</summary>
public enum ScanScope
{
    /// <summary>Staged changes — what a pre-commit hook checks. The gate that actually protects a repository.</summary>
    Staged,

    /// <summary>Every tracked file in the current checkout — what the "am I done?" gate checks.</summary>
    Tree,

    /// <summary>A commit message. History too, and the easiest place to forget.</summary>
    Message,
}

/// <summary>
/// Blocks developer-machine paths, private project names and credentials from entering a repository.
/// </summary>
/// <remarks>
/// <para>Canonized from eleven hand-copied versions, of which this is the only one that had been
/// through an actual incident — a leak that reached history and needed a rewrite to remove. Four
/// properties are carried over deliberately, because each one exists because something got through:</para>
///
/// <para><b>Paths are matched as well as content.</b> A file whose NAME contains a banned token passed
/// every earlier version, because only the bytes inside were scanned.</para>
///
/// <para><b>It fails closed when the private pattern list is missing.</b> The structural patterns here
/// are publishable by construction — a Windows user-home path is a leak in any repository. The tokens
/// that are actually secret cannot live in the repository being scanned, so they load from a gitignored
/// file. When that file was absent the earlier version printed a notice and continued with the
/// built-ins, which means on a fresh clone and in CI the half of the guard that knew the private names
/// silently did not run. Opting out is now explicit.</para>
///
/// <para><b>Renames count as changes.</b> `--diff-filter=ACM` misses the R that `git mv` produces, so a
/// file renamed INTO a banned name was never scanned.</para>
///
/// <para><b>Commit messages are scanned.</b> They are history, they are rarely reviewed, and nothing
/// looked at them at all.</para>
/// </remarks>
public sealed class SensitiveGate(ScanScope scope, IGit git, bool allowBuiltinsOnly = false, string? messageFile = null)
    : IGate
{
    public string Name => "sensitive";

    /// <summary>
    /// Structural patterns, safe to publish because they describe a SHAPE rather than a secret.
    /// </summary>
    private static readonly (Regex Pattern, string Why)[] Builtins =
    [
        (new Regex(@"[A-Za-z]:\\Users\\[A-Za-z0-9._-]+", RegexOptions.IgnoreCase), "Windows user-home absolute path"),
        (new Regex(@"/(?:home|Users)/[a-z][a-z0-9._-]+", RegexOptions.IgnoreCase), "Unix home absolute path"),
        (new Regex(@"\b(?:ghp|gho|ghs|ghu)_[A-Za-z0-9]{20,}"), "GitHub token"),
        (new Regex(@"\bsk-[A-Za-z0-9]{20,}"), "API secret key"),
        (new Regex(@"\bAKIA[0-9A-Z]{16}\b"), "AWS access key id"),
        (new Regex(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"), "private key block"),
    ];

    public GateResult Run(GateContext context)
    {
        var patterns = new List<(Regex Pattern, string Why)>(Builtins);

        var listFile = context.Path(context.Declaration.Sensitive.PatternsFile);
        if (File.Exists(listFile))
        {
            patterns.AddRange(ReadPrivatePatterns(listFile));
        }
        else if (!allowBuiltinsOnly)
        {
            return GateResult.Fail(Name,
                $"the private pattern list '{context.Declaration.Sensitive.PatternsFile}' is missing, so the "
                + "half of this scan that knows your private names would not run.\n"
                + "Write the file, or pass --allow-builtins-only to accept structural patterns alone.");
        }

        var findings = new List<string>();
        foreach (var (label, text) in Subjects(context))
        {
            foreach (var (pattern, why) in patterns)
            {
                var match = pattern.Match(text);
                if (match.Success) findings.Add($"{label}: {why} — '{Redact(match.Value)}'");
            }
        }

        return findings.Count == 0
            ? GateResult.Pass(Name, $"{patterns.Count} patterns, nothing found")
            : GateResult.Fail(Name, string.Join('\n', findings));
    }

    /// <summary>Every (label, text) pair the scope covers — the path itself included.</summary>
    private IEnumerable<(string Label, string Text)> Subjects(GateContext context)
    {
        if (scope == ScanScope.Message)
        {
            var file = messageFile ?? throw new DevkitException("--message needs a file path");
            yield return ("commit message", File.ReadAllText(file));
            yield break;
        }

        foreach (var relative in scope == ScanScope.Staged ? git.StagedFiles() : git.TrackedFiles())
        {
            // The PATH is scanned as well as the content: a file named after a banned token is a leak
            // whose bytes may be perfectly innocent.
            yield return ($"{relative} (path)", relative);

            var absolute = context.Path(relative);
            if (!File.Exists(absolute) || IsBinary(absolute)) continue;
            yield return (relative, File.ReadAllText(absolute));
        }
    }

    /// <summary>Each non-comment line is a regex. Comments and blanks are ignored so the file can explain itself.</summary>
    private static IEnumerable<(Regex, string)> ReadPrivatePatterns(string file)
    {
        foreach (var line in File.ReadAllLines(file))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            Regex compiled;
            try
            {
                compiled = new Regex(trimmed, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException error)
            {
                throw new DevkitException($"{file}: '{trimmed}' is not a valid regex — {error.Message}");
            }

            yield return (compiled, "private pattern");
        }
    }

    /// <summary>
    /// A NUL byte in the first block means binary, which is the same test git itself uses.
    /// </summary>
    private static bool IsBinary(string file)
    {
        using var stream = File.OpenRead(file);
        Span<byte> head = stackalloc byte[8000];
        var read = stream.Read(head);
        return head[..read].IndexOf((byte)0) >= 0;
    }

    /// <summary>
    /// The report says enough to find it and not enough to spread it.
    /// </summary>
    /// <remarks>
    /// A gate that prints the secret it caught has written that secret to a build log, which is often
    /// more public than the commit it just blocked.
    /// </remarks>
    private static string Redact(string value) =>
        value.Length <= 8 ? value : $"{value[..4]}…{value[^2..]}";
}

/// <summary>The git queries the gates need, behind a seam so they can be tested without a repository.</summary>
public interface IGit
{
    IReadOnlyList<string> StagedFiles();

    IReadOnlyList<string> TrackedFiles();
}

/// <summary>git, through the command line.</summary>
public sealed class CommandLineGit(string repositoryRoot) : IGit
{
    /// <summary>
    /// `--diff-filter=ACMR` — R for renames.
    /// </summary>
    /// <remarks>
    /// The R is the whole point. Without it `git mv secret-name.md public-name.md` staged a rename that
    /// no scan ever looked at, and a file renamed INTO a banned name went straight through.
    /// </remarks>
    public IReadOnlyList<string> StagedFiles() =>
        Lines("diff", "--cached", "--name-only", "--diff-filter=ACMR");

    public IReadOnlyList<string> TrackedFiles() => Lines("ls-files");

    private IReadOnlyList<string> Lines(params string[] arguments)
    {
        var output = Process.Run("git", arguments, repositoryRoot);
        if (output.ExitCode != 0)
        {
            throw new DevkitException($"git {string.Join(' ', arguments)} failed: {output.Error.Trim()}");
        }

        return output.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
