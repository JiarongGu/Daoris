namespace Daoris.Driver;

/// <summary>
/// Whether a question in a folder may be the library's (<see cref="GitLibrary"/>), read at each question from the repository's own
/// files with nothing started, so a replacement added after one question sends the next to git. Null admits it; a word says why git
/// answers instead.
/// </summary>
/// <remarks>
/// <para><b>Refused here, every question:</b> a folder that names no git directory; reftable refs, which libgit2 opens and reads no ref
/// of; and any replacement (a ref under <c>refs/replace/</c>, loose or packed, or <c>GIT_REPLACE_REF_BASE</c> set), since git reads a
/// replaced commit's parents, type and tree through it and libgit2 does not.</para>
/// <para><b>Refused by question:</b> for the refs, a branch or remote section in a global file git is handed; for the names, a
/// <c>diff.orderFile</c> or <c>diff.relative</c> git reads in the repository's, the tree's or that global file, which reorder or cut
/// the paths it prints. The library, reading the repository's configuration alone, would see neither. Grafts and a shallow boundary
/// it reads as git does.</para>
/// <para><b>Refused by the library itself</b>, at its open: SHA-256 objects, an extension libgit2 does not know (a partial clone's
/// <c>extensions.partialclone</c> among them) and a folder another account owns. An object a call needs that is not here (a partial
/// clone's) refuses that call.</para>
/// </remarks>
internal static class GitLibraryAdmission
{
    internal const string NoRepository = "no-repository";
    internal const string Reftable = "reftable";
    internal const string Replacements = "replacements";
    internal const string Configuration = "configuration";

    /// <summary>Why <paramref name="question"/> in <paramref name="folder"/> is git's; null where the library may answer it.</summary>
    internal static string? RefusalOf(string folder, GitLibraryQuestion question)
    {
        if (RefFiles.CommonDirOf(folder) is not { } common || RefFiles.GitDirOf(folder) is not { } gitDir) return NoRepository;
        try
        {
            if (Directory.Exists(Path.Combine(common, "reftable"))) return Reftable;
            if (Replaced(common)) return Replacements;
            return question switch
            {
                GitLibraryQuestion.Refs => ProgramHome.Named is { } home && Handed(home).Any(text => Names(text, "[branch", "[remote")) ? Configuration : null,
                GitLibraryQuestion.Names when Reordered(gitDir, common) => Configuration,
                _ => null,
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return NoRepository;
        }
    }

    private static bool Replaced(string common)
    {
        if (Environment.GetEnvironmentVariable("GIT_REPLACE_REF_BASE") is { Length: > 0 }) return true;
        var loose = Path.Combine(common, "refs", "replace");
        if (Directory.Exists(loose) && Directory.EnumerateFiles(loose, "*", SearchOption.AllDirectories).Any()) return true;
        var packed = Path.Combine(common, "packed-refs");
        return File.Exists(packed) && File.ReadLines(packed).Any(line => line.Contains(" refs/replace/", StringComparison.Ordinal));
    }

    private static bool Reordered(string gitDir, string common)
    {
        IEnumerable<string> own = [Path.Combine(common, "config"), Path.Combine(gitDir, "config.worktree")];
        var texts = own.Where(File.Exists).Select(File.ReadAllText);
        if (ProgramHome.Named is { } home) texts = texts.Concat(Handed(home));
        return texts.Any(text => Keys.IsMatch(text));
    }

    private static readonly System.Text.RegularExpressions.Regex Keys = new(
        @"^\s*(orderfile|relative)\s*(=|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

    /// <summary>The global file git is handed under <paramref name="home"/>, and the files it includes, as text.</summary>
    private static IEnumerable<string> Handed(string home)
    {
        IEnumerable<string> files = [GitGlobalFile.PathOf(home), .. GitGlobalFile.Includes(home, Environment.GetEnvironmentVariable)];
        return files.Where(File.Exists).Select(File.ReadAllText);
    }

    private static bool Names(string text, params string[] words) => words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
}
