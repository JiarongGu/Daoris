using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// A session's tree git binds to another checkout is never removed, reset or written through (GITWORKTREECONFIG1, D51's note). With
/// worktree-specific configuration on (<c>extensions.worktreeConfig</c>), a <c>core.worktree</c> in the repository's shared configuration
/// applies to every linked tree, and git then reports that folder, the main checkout on 2026-10-27, as each tree's working root: a status
/// asked there reads the checkout's files against the tree's index, and a switch, a rebase, a reset or an index write there acts on the
/// checkout's files. The one check, asked by each door before its first git write through a tree: the working root git reports for the
/// tree must be the folder the tree is at.
/// </summary>
/// <remarks>
/// <para><b>Daoris writes no configuration.</b> The value is another tool's, so the refusal names it and where git read it
/// (<c>git config --show-origin</c>), and the person moves it into the owning checkout's own worktree configuration or unsets it.</para>
///
/// <para><b>Asked as written, never kept.</b> <c>rev-parse --show-toplevel</c> is a layout fact a landing's table keeps
/// (<see cref="GitFacts"/>): the check asks it with an environment of its own, empty, which the runner always asks of git. Where a door's
/// own start already read the root (<see cref="TreeGit.CommonDirAndBranchAsync"/>, which asks it with the tree's repository and branch),
/// that answer is compared, and no git starts.</para>
///
/// <para><b>Only a folder holding <c>.git</c></b> is judged: git asked in any other walks up to whatever holds it, which each door's own
/// checks refuse as no tree. A root git could not say keeps the tree too, said: it is no proof the tree is its own.</para>
/// </remarks>
internal static class TreeBinding
{
    /// <summary>The ask: the working root git reports where it is started.</summary>
    private static readonly string[] Root = ["rev-parse", "--show-toplevel"];

    /// <summary>
    /// An environment of the call's own, with nothing in it: the runner then asks git as written, never a fact a flow kept
    /// (<see cref="WorkingTree"/>'s runner, LANDGIT1's rule for a call with variables of its own).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AsWritten = new Dictionary<string, string>();

    /// <summary>
    /// Why nothing may be written through <paramref name="tree"/>: the clause a door refuses with, with no closing stop, where git reports
    /// another folder as its working root or could not say; null where it reports the tree's own, or where the folder holds no
    /// <c>.git</c>.
    /// </summary>
    /// <param name="reported">The working root a door's own start read a moment ago (<see cref="TreeGit.CommonDirAndBranch.TopLevel"/>);
    /// null or empty asks git.</param>
    internal static async Task<string?> RefusalAsync(string tree, CancellationToken ct, string? reported = null)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tree));
        if (!Path.Exists(Path.Combine(folder, ".git"))) return null;
        if (string.IsNullOrWhiteSpace(reported))
        {
            var (code, stdout, stderr) = await WorkingTree.GitAsync(folder, Root, AsWritten, timeout: null, ct).ConfigureAwait(false);
            if (code != 0 || stdout.Trim().Length == 0)
            {
                return $"git could not say where the tree at {folder} has its working root ({FirstLine(stderr.Length > 0 ? stderr : "it said nothing")}), "
                    + "so nothing was removed, reset or written through it; ask again";
            }

            reported = stdout;
        }

        var root = reported.Trim();
        if (Own(root, folder)) return null;
        return Said(folder, root, await OriginAsync(folder, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// The refusal's words: what git reports, what binds it there and where git read it, that nothing was done, and what the person does.
    /// </summary>
    internal static string Said(string tree, string reported, (string File, string Value)? origin) =>
        $"git reports {reported} as the working root of the tree at {tree}, not that folder: "
        + (origin is { } at ? $"`core.worktree = {at.Value}` in {at.File}" : "a `core.worktree` in the repository's shared git configuration")
        + ", set by something other than Daoris, binds it there, so nothing was removed, reset or written through it. Move that value into "
        + "the owning checkout's own worktree configuration, or unset it, then ask again";

    /// <summary>The same folder, spelled as git spells it or reached through a link (AUTOTIDY1's <see cref="TreeGit.Normal"/>).</summary>
    private static bool Own(string reported, string folder) => SamePath(reported, folder) || SamePath(Normal(reported), Normal(folder));

    /// <summary>
    /// Where git read the tree's <c>core.worktree</c>, and its value: <c>git config --show-origin --get</c>'s first line, the file without
    /// git's <c>file:</c>. Null where git names none: the binding is said where such a value is, never a file git did not name.
    /// </summary>
    private static async Task<(string File, string Value)?> OriginAsync(string tree, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(tree, ["config", "--show-origin", "--get", "core.worktree"], ct).ConfigureAwait(false);
        var line = FirstLine(stdout);
        var tab = line.IndexOf('\t');
        if (code != 0 || tab <= 0) return null;
        var origin = line[..tab];
        return (origin.StartsWith("file:", StringComparison.Ordinal) ? origin["file:".Length..] : origin, line[(tab + 1)..].Trim());
    }
}
