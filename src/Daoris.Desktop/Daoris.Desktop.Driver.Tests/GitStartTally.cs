using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git a flow of the product starts while open, each with the folder it ran in, through the process gate's stand-in
/// (<see cref="ProcessStarts.Within"/>): what a view, an offer or a landing asks of git, as the person's machine would start it. The
/// test's own git starts outside the gate and is never counted.
/// </summary>
/// <remarks>
/// A start is said by its command past git's own options and its folder relative to <c>relativeTo</c>. Repeated is the same command
/// in the same folder asked again in the one pass; a held reader's is what one long-lived <c>git cat-file --batch</c> of the
/// repository could answer instead (an object by its id, or a name read as its commit or tree).
/// </remarks>
internal sealed class GitStartTally : IDisposable
{
    private readonly string _relativeTo;
    private readonly List<(string Folder, IReadOnlyList<string> Command)> _starts = [];
    private readonly IDisposable _within;

    private GitStartTally(string relativeTo)
    {
        _relativeTo = relativeTo;
        _within = ProcessStarts.Within(info =>
        {
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase))
            {
                var folder = Path.GetRelativePath(_relativeTo, info.WorkingDirectory).Replace('\\', '/');
                lock (_starts) _starts.Add((folder, GitLaunch.Command([.. info.ArgumentList])));
            }

            return Process.Start(info);
        });
    }

    /// <summary>Count from here until disposed, folders said relative to <paramref name="relativeTo"/>.</summary>
    public static GitStartTally Open(string relativeTo) => new(relativeTo);

    /// <summary>Each start as <c>command  @ folder</c>, in the order the gate saw them.</summary>
    public IReadOnlyList<string> Starts
    {
        get
        {
            lock (_starts) return [.. _starts.Select(start => $"{string.Join(' ', start.Command)}  @ {start.Folder}")];
        }
    }

    /// <summary>The starts that ask a command already asked in the same folder in this pass.</summary>
    public int Repeated => Starts.Count - Starts.Distinct(StringComparer.Ordinal).Count();

    /// <summary>The starts a held <c>cat-file --batch</c> of the repository could answer.</summary>
    public int Held
    {
        get
        {
            lock (_starts) return _starts.Count(start => IsObjectQuestion(start.Command));
        }
    }

    /// <summary>Of <see cref="Held"/>, those that name an object by its id alone, whose answer no other process can move.</summary>
    public int HeldById
    {
        get
        {
            lock (_starts) return _starts.Count(start => IsObjectQuestion(start.Command) && start.Command.Skip(1).Any(ById));
        }
    }

    /// <summary>
    /// Whether one long-lived <c>cat-file --batch</c> answers <paramref name="command"/>: an object read by its id (<c>cat-file</c>,
    /// <c>ls-tree</c>), or a name verified as an object (<c>rev-parse --verify</c>, which <c>--batch-check</c> answers as a name).
    /// </summary>
    internal static bool IsObjectQuestion(IReadOnlyList<string> command) =>
        GitLaunch.KindOf(command) == GitKind.Object
        || (command is ["rev-parse", ..] && command.Contains("--verify") && !command.Any(word => word.StartsWith("--show", StringComparison.Ordinal)));

    private static bool ById(string word) => word.Split('^', ':')[0] is { Length: 40 or 64 } id && id.All(char.IsAsciiHexDigit);

    /// <summary>One row of the figures: the starts, the repeated and the held reader's, then each start.</summary>
    public string Report(string view) =>
        $"{view}: {Starts.Count} starts, {Repeated} repeated, {Held} a held reader answers ({HeldById} by id)\n  {string.Join("\n  ", Starts)}";

    public void Dispose() => _within.Dispose();
}
