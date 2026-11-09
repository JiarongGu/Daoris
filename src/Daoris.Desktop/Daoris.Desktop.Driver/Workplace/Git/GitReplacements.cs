namespace Daoris.Driver;

/// <summary>
/// Whether git may answer an object's id with another object's bytes in a repository: the one judgement of <i>a replacement can stand</i>,
/// which the library's admission (<see cref="GitLibraryAdmission"/>) reads.
/// </summary>
/// <remarks>
/// It holds where a file stands under <c>refs/replace/</c> at any depth, a <c>packed-refs</c> line names a ref under it,
/// <c>GIT_REPLACE_REF_BASE</c> is set, or the refs are a reftable, read from the files at each ask with nothing started. It reads no
/// configuration: <c>core.useReplaceRefs=false</c> and <c>GIT_NO_REPLACE_OBJECTS</c> can only turn a replacement off, so leaving them out
/// can send git an ask it did not need, never answer one git would answer otherwise.
/// </remarks>
internal static class GitReplacements
{
    /// <summary>Whether a replacement can be in force in the repository whose common directory is <paramref name="common"/>.</summary>
    internal static bool CanBeInForce(string common, Func<string, string?>? variable = null)
    {
        if ((variable ?? Environment.GetEnvironmentVariable)("GIT_REPLACE_REF_BASE") is { Length: > 0 }) return true;
        if (Directory.Exists(Path.Combine(common, "reftable"))) return true;
        var loose = Path.Combine(common, "refs", "replace");
        if (Directory.Exists(loose) && Directory.EnumerateFiles(loose, "*", SearchOption.AllDirectories).Any()) return true;
        return GitPackedRefs.Of(common).Refs.Replacements.Count > 0;
    }
}
