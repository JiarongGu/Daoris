using System.Globalization;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>What a done waits on (EVID1a, D144 §3): one item of one requirement it answered met, by the requirement's number.</summary>
public sealed record WantedEvidence(int Requirement, QuestEvidenceItem Item);

/// <summary>
/// Which paths Daoris asks git for (EVID1b, D144 §2–§3): the service's own judge (<c>QuestEvidence.JudgePath</c>), read
/// again on this side before a path becomes a git argument. The two share no code, so each holds the same rows (twins).
/// </summary>
public static class EvidencePaths
{
    /// <summary>How long a path may be.</summary>
    public const int MaxLength = 300;

    /// <summary>Why <paramref name="path"/> is not one a commit can be asked for, or null when it is.</summary>
    public static string? Judge(string path)
    {
        if (path.Length == 0) return "it is empty";
        if (path.Length > MaxLength) return $"it is longer than {MaxLength} characters";
        if (path.Any(char.IsControl)) return "it holds a control character";
        if (path.Contains('\\')) return "it holds a backslash, and a path is written with forward slashes";
        if (path.StartsWith('/')) return "it starts with `/`, and a path is relative to the repository's root";
        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':') return "it names a drive, and a path is relative to the repository's root";
        if (path.StartsWith('-')) return "it starts with `-`, which git would read as an option";

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0) return "it holds an empty segment (a doubled or trailing `/`), and a folder is named without one";
            if (segment == ".") return "it holds a `.` segment, and a path is named one way, from the repository's root";
            if (segment == "..") return "it holds a `..` segment, which reaches outside the path it names";
            if (string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase)) return "it reaches into `.git`, which is git's, never the work's";
        }

        return null;
    }
}
