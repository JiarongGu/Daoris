namespace Daoris.Driver;

/// <summary>
/// The git the quests' Application runs by default (STRUCT3f): the working tree's own read, and the two questions about a
/// tree or an id it asks beside it. The Application composes these as its ports' defaults and names the working tree
/// nowhere else, so the module's rules stay git-free and a test stands git in at the same seam (REVIEW3).
/// </summary>
internal static class QuestGit
{
    /// <summary>How git is read when no stand-in is handed: the run the working tree makes.</summary>
    public static readonly WorkingTree.GitRead Read = WorkingTree.ReadGitAsync;

    /// <summary>Whether <paramref name="text"/> is a commit's hex id, 7 to 64 characters.</summary>
    public static bool IsCommitId(string text) => WorkingTree.IsCommitId(text);

    /// <summary>Whether <paramref name="root"/> is the top of a repository of its own, asked through <paramref name="git"/>.</summary>
    public static Task<bool> IsTopLevelAsync(string root, WorkingTree.GitRead git, CancellationToken ct) =>
        WorkingTree.IsTopLevelAsync(root, git, ct);
}
