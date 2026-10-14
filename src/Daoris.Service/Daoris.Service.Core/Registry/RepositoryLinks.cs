namespace Daoris.Knowledge;

/// <summary>
/// Links, and links held as text, in a repository the scanner reads (LAYOUT4; D117,
/// `docs/2026-10-01-agent-layout-design.md` §5.5 and §5.4's last table).
/// </summary>
/// <remarks>
/// <para><b>Skipped, never followed.</b> A link in a repository points somewhere its own files are not:
/// another folder, another repository, a machine path. Indexing through it would put somebody else's
/// document under this repository's name. And on a checkout without links (<c>core.symlinks=false</c>,
/// the Windows default) a link is a small text file holding its target, so reading it as a document would put
/// a path in a search where a document should be.</para>
///
/// <para>🔴 <b>A twin</b> (<c>.claude/knowledge/twins.md</c>, *a link held as text*): the CLI's
/// <c>heldAsText</c> (<c>src/Daoris.Cli/src/links.ts</c>) refuses to write through the same files this
/// skips reading, by the same content cases. The two share no code; <c>RepositoryLayoutTests</c> holds
/// the CLI's cases in its order.</para>
/// </remarks>
public static class RepositoryLinks
{
    /// <summary>The characters a token cannot start with: an import, a heading, markup, a list, a quote.</summary>
    private const string NotAPath = "@#<>![-*|`";

    /// <summary>
    /// Whether the path, or any folder on the way to it from the repository root, is a link or a
    /// junction. The repository root itself is where the person put it, and is never asked about.
    /// </summary>
    public static bool Crosses(string repositoryRoot, string relative)
    {
        var current = repositoryRoot;
        foreach (var part in relative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Join(current, part);
            if (IsLink(current)) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether a file's text is a link checked out as text: its whole content one token that is its
    /// partner's name (<c>AGENTS.md</c> for <c>CLAUDE.md</c>, and back), a relative path starting
    /// <c>./</c> or <c>../</c>, or the name of something beside it.
    /// </summary>
    /// <remarks>
    /// A probable, as the CLI's is: the reference's <c>CLAUDE.md</c> is the nine bytes <c>AGENTS.md</c>,
    /// which names its partner whether or not the partner exists, and a folder link reads
    /// <c>../.agents/skills</c>. A one-word file that names nothing beside it is prose, however short.
    /// Skipping on a probable costs one document that was a single word naming a file; reading on one
    /// puts a path in the index.
    /// </remarks>
    /// <param name="repositoryRoot">The repository, for what sits beside the file.</param>
    /// <param name="relative">The file, repository-relative.</param>
    /// <param name="text">Its content as read, a BOM and surrounding whitespace included or not.</param>
    public static bool HeldAsText(string repositoryRoot, string relative, string text)
    {
        var held = Token(text);
        if (held is null) return false;

        var normal = relative.Replace('\\', '/');
        var slash = normal.LastIndexOf('/');
        var name = normal[(slash + 1)..];
        var partner = name == "CLAUDE.md" ? "AGENTS.md" : name == "AGENTS.md" ? "CLAUDE.md" : null;
        if (held == partner
            || held.StartsWith("./", StringComparison.Ordinal)
            || held.StartsWith("../", StringComparison.Ordinal))
        {
            return true;
        }

        // Path.Join, never Path.Combine: a token that is rooted must not replace the folder it sits in.
        var beside = Path.Join(repositoryRoot, slash < 0 ? string.Empty : normal[..slash], held);
        return File.Exists(beside) || Directory.Exists(beside);
    }

    /// <summary>The single token a link held as text is: no whitespace, not prose, not an import, not markup.</summary>
    private static string? Token(string text)
    {
        var trimmed = text.TrimStart('﻿').Trim();
        if (trimmed.Length == 0 || trimmed.Length > 255) return null;
        if (trimmed.Any(char.IsWhiteSpace) || NotAPath.Contains(trimmed[0])) return null;
        return trimmed;
    }

    /// <summary>
    /// Whether one path is a link or a junction, without following it. Nothing there is not a link;
    /// a path that cannot be asked about is treated as one, since the scanner then reads nothing.
    /// </summary>
    private static bool IsLink(string path)
    {
        try
        {
            // LinkTarget reads the link itself: a symbolic link either way, and a junction on Windows.
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.LinkTarget is not null;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
