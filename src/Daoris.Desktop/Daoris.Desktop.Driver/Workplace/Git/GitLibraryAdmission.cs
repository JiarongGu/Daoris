namespace Daoris.Driver;

/// <summary>
/// Whether a question in a folder may be the library's (<see cref="GitLibrary"/>), read at each question from the repository's own
/// files with nothing started, so a replacement added after one question sends the next to git. Null admits it; a word says why git
/// answers instead.
/// </summary>
/// <remarks>
/// <para><b>Refused here, every question:</b> a folder that names no git directory; a format the library cannot read
/// (<see cref="GitLibraryFormats"/>); a replacement that can be in force (<see cref="GitReplacements"/>), which libgit2 does not read; and a pack rewrite under way (<see cref="GitLibraryHousekeeping"/>).</para>
/// <para><b>Refused by question:</b> for the refs, a branch or remote section in a global file git is handed; for the names, a
/// <c>diff.orderFile</c> or <c>diff.relative</c> in the repository's, the tree's or that file, which the library would not see.
/// Grafts and a shallow boundary it reads as git does.</para>
/// <para><b>Refused by the library itself</b>, at its open: an extension libgit2 does not know (a partial clone's
/// <c>extensions.partialclone</c> among them) and a folder another account owns. An object a call needs that is not here (a partial
/// clone's) refuses that call.</para>
/// </remarks>
internal static class GitLibraryAdmission
{
    internal const string NoRepository = "no-repository";
    internal const string Reftable = "reftable";
    internal const string Sha256 = "sha256";
    internal const string ObjectFormat = "object-format";
    internal const string RefFormat = "ref-format";
    internal const string Replacements = "replacements";
    internal const string Configuration = "configuration";
    internal const string Housekeeping = "housekeeping";

    /// <summary>Why <paramref name="question"/> in <paramref name="folder"/> is git's; null where the library may answer it.</summary>
    internal static string? RefusalOf(string folder, GitLibraryQuestion question)
    {
        if (RefFiles.CommonDirOf(folder) is not { } common || RefFiles.GitDirOf(folder) is not { } gitDir) return NoRepository;
        try
        {
            if (FormatRefusal(GitLibraryFormats.Of(common)) is { } format) return format;
            if (GitReplacements.CanBeInForce(common)) return Replacements;
            if (GitLibraryHousekeeping.Running(common)) return Housekeeping;
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

    /// <summary>Why a repository's formats are git's (<see cref="GitLibraryFormats"/>): the library is not opened on them.</summary>
    private static string? FormatRefusal(GitLibraryFormat format) => format switch
    {
        { Readable: true } => null,
        { Objects: "sha256" } => Sha256,
        { Objects: not "sha1" } => ObjectFormat,
        { Refs: "reftable" } => Reftable,
        _ => RefFormat,
    };

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
