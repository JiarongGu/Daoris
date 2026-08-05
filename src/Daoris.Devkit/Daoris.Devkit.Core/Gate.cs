namespace Daoris.Devkit;

/// <summary>Whether a gate passed, and what to tell the person if it did not.</summary>
/// <param name="Name">The gate's name, as it appears in the run log.</param>
/// <param name="Passed">False fails the whole run.</param>
/// <param name="Detail">
/// What was wrong and where. Written for someone who has just been stopped from committing, so it
/// names files and lines rather than describing a category of problem.
/// </param>
/// <param name="Skipped">
/// True when the gate had nothing to check — a declared file that does not exist yet, say. Reported
/// distinctly from a pass, because "nothing to check" and "checked and fine" are different facts and
/// a gate that silently degrades to the first while reporting the second is worse than no gate.
/// </param>
public sealed record GateResult(string Name, bool Passed, string Detail = "", bool Skipped = false)
{
    public static GateResult Pass(string name, string detail = "") => new(name, true, detail);

    public static GateResult Fail(string name, string detail) => new(name, false, detail);

    public static GateResult Skip(string name, string why) => new(name, true, why, Skipped: true);
}

/// <summary>One check that can stop a run.</summary>
/// <remarks>
/// A gate reads the repository and reports. It never fixes anything: a gate that repairs what it finds
/// removes the reason anyone would look, and the family already learned that from a `doctor --fix` that
/// ran inside the "am I done?" gate and left it scanning files it had itself just rewritten.
/// </remarks>
public interface IGate
{
    string Name { get; }

    GateResult Run(GateContext context);
}

/// <summary>Everything a gate is allowed to know: where the repository is, and what it declared.</summary>
/// <remarks>
/// Deliberately not a service container. A gate that can reach arbitrary services grows dependencies
/// that the AOT binary then has to carry, and the four universal gates need a path and a declaration.
/// </remarks>
public sealed class GateContext(string repositoryRoot, GateDeclaration declaration)
{
    public string RepositoryRoot { get; } = repositoryRoot;

    public GateDeclaration Declaration { get; } = declaration;

    /// <summary>An absolute path from a repository-relative one, using the platform's separator.</summary>
    public string Path(string relative) =>
        System.IO.Path.Combine(RepositoryRoot, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
}
