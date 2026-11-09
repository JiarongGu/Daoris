using LibGit2Sharp;

namespace Daoris.Driver;

/// <summary>
/// One library call's repository, open for that call alone (<see cref="GitLibrary"/>), and the two git directories its folder names:
/// the tree's own, where its per-tree refs are, and the common one, where the shared refs and configuration are.
/// </summary>
internal sealed record GitLibraryCall(Repository Repository, string GitDir, string CommonDir);
