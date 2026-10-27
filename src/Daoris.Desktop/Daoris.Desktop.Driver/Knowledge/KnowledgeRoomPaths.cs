using System.Text;

namespace Daoris.Driver;

/// <summary>Ask Daoris room placement and workspace folder spelling, shared by its writers and provenance readers.</summary>
internal static class KnowledgeRoomPaths
{
    internal const string Folder = "help";

    /// <summary>
    /// The longest a named workspace's part of its room's folder name is spelled out (ASKSCOPE1b); a longer one keeps its first
    /// <see cref="NameKept"/> characters and a digest of the whole name, so a folder's name stays well inside what a file system holds.
    /// </summary>
    private const int NameSpelled = 48;

    private const int NameKept = 32;

    internal static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.HelpRoom);

    internal static string PathOf(string home, string? workspace) =>
        ScopeOf(workspace) is { } named ? Path.Combine(Path.GetDirectoryName(PathOf(home))!, FolderOf(named)) : PathOf(home);

    internal static string? ScopeOf(string? workspace)
    {
        var circle = RemoteTarget.Workspace(workspace);
        return string.Equals(circle, RemoteTarget.DefaultWorkspace, StringComparison.OrdinalIgnoreCase) ? null : circle;
    }

    /// <summary>
    /// A named workspace's room folder (ASKSCOPE1b): <c>help-</c> and the name as one workspace is one name, whatever its letters'
    /// case (D48), its ASCII letters and digits kept, lowercase, and every other character spelled by its UTF-8 bytes, each
    /// <c>_</c> and two hex digits. One name is one folder and two names are two, and none is <see cref="Folder"/>: no character
    /// is kept that a file system or the harness's own naming of a folder would change. A name spelled longer than
    /// <see cref="NameSpelled"/> keeps its first <see cref="NameKept"/> characters, <c>-</c> and a digest of the whole name, a
    /// <c>-</c> no shorter one holds.
    /// </summary>
    internal static string FolderOf(string workspace)
    {
        var spelled = new StringBuilder();
        Span<byte> bytes = stackalloc byte[4];
        foreach (var rune in workspace.Trim().ToUpperInvariant().EnumerateRunes())
        {
            if (rune.IsAscii && char.IsAsciiLetterOrDigit((char)rune.Value))
            {
                spelled.Append(char.ToLowerInvariant((char)rune.Value));
                continue;
            }

            var length = rune.EncodeToUtf8(bytes);
            foreach (var each in bytes[..length]) spelled.Append('_').Append(each.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        var name = spelled.ToString();
        if (name.Length > NameSpelled)
        {
            var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(workspace.Trim().ToUpperInvariant())));
            name = $"{name[..NameKept]}-{digest[..16].ToLowerInvariant()}";
        }

        return $"{Folder}-{name}";
    }
}
