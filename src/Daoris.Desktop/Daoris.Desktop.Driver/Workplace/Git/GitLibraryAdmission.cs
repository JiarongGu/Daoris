namespace Daoris.Driver;

/// <summary>
/// Whether a ref question in a folder may be the library's (<see cref="GitLibrary"/>), read at each question from the repository's own
/// files with nothing started, so a replacement added after one question sends the next to git. Null admits it; a word says why git
/// answers instead.
/// </summary>
/// <remarks>
/// <para><b>Refused here:</b> a folder that names no git directory; reftable refs, which libgit2 opens and reads no ref of; any
/// replacement (a ref under <c>refs/replace/</c>, loose or packed, or <c>GIT_REPLACE_REF_BASE</c> set), since git reads a replaced tip's
/// type and tree through it and libgit2 does not; and a branch or remote section in a global file git is handed, which the library,
/// reading the repository's configuration alone, would not see.</para>
/// <para><b>Refused by the library itself</b>, at its open: SHA-256 objects, an extension libgit2 does not know (a partial clone's
/// <c>extensions.partialclone</c> among them) and a folder another account owns. A tip whose object is missing (a partial clone's)
/// refuses the call that meets it (<see cref="GitLibraryRefs"/>). Grafts change parents alone, which no ref question reads.</para>
/// </remarks>
internal static class GitLibraryAdmission
{
    internal const string NoRepository = "no-repository";
    internal const string Reftable = "reftable";
    internal const string Replacements = "replacements";
    internal const string Configuration = "configuration";

    /// <summary>Why <paramref name="folder"/>'s ref questions are git's; null where the library may answer them.</summary>
    internal static string? RefusalOf(string folder)
    {
        if (RefFiles.CommonDirOf(folder) is not { } common || RefFiles.GitDirOf(folder) is null) return NoRepository;
        try
        {
            if (Directory.Exists(Path.Combine(common, "reftable"))) return Reftable;
            if (Replaced(common)) return Replacements;
            if (ProgramHome.Named is { } home && Outside(home)) return Configuration;
            return null;
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

    private static bool Outside(string home)
    {
        var handed = GitGlobalFile.PathOf(home);
        IEnumerable<string> files = [handed, .. GitGlobalFile.Includes(home, Environment.GetEnvironmentVariable)];
        return files.Any(file => File.Exists(file) && Names(File.ReadAllText(file)));

        static bool Names(string text) =>
            text.Contains("[branch", StringComparison.OrdinalIgnoreCase) || text.Contains("[remote", StringComparison.OrdinalIgnoreCase);
    }
}
