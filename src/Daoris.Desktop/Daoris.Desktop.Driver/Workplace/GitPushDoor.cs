namespace Daoris.Driver;

/// <summary>
/// What both doors do with a push's updates once they have read them (D241 point 3): kind each by git's answer on history, judge the batch
/// by the grant and the place's Dangerous value, take the grant once where it carried the batch, and say a refusal as a coded line. A
/// refusal comes before anything is forwarded, so no ref moves.
/// </summary>
/// <remarks>The base library alone: the doors compile it in (<c>Daoris.Desktop.GitDoor</c>).</remarks>
internal static class GitPushDoor
{
    /// <summary>
    /// The line that refuses the push, or null where it may pass.
    /// </summary>
    /// <param name="write">The act's Write verdict, from the door's first judgement: its code <c>granted</c> says Write passed on the grant alone.</param>
    /// <param name="keepsHistory">Whether the first commit is an ancestor of the second, asked of git where the push runs.</param>
    public static string? Refusal(
        string home, GitVerdict write, IReadOnlyList<GitPushUpdate> updates, Func<string, string, bool> keepsHistory, string? key,
        Func<string, string?> variable, string? remote, string? host, string folder)
    {
        var kinds = GitPushUpdates.KindsOf(updates, keepsHistory);
        var id = variable(GitRouting.GrantVariable);
        var grant = GitGrants.Read(home, id, key, DateTimeOffset.UtcNow);
        var (repository, workspace) = GitFolders.RepositoryOf(home, folder);
        var dangerous = GitSessionAccess.Held(home, variable(GitLog.SessionVariable), repository, workspace).Of(GitAccessKeys.Dangerous).Value;

        var verdict = GitPushJudge.Judge(updates, kinds, grant, write.Code == "granted", dangerous);
        if (verdict.Allowed && verdict.UsedGrant && !GitGrants.Take(home, id, key, DateTimeOffset.UtcNow))
            verdict = new(false, "grant-taken", GitPushJudge.TakenLine);
        if (!verdict.Allowed)
        {
            GitLog.Refused(home, variable, verdict.Code, GitAct.Write, remote, host);
            return verdict.Line;
        }

        if (verdict.UsedGrant) GitLog.GrantUsed(home, variable, remote, host);
        return null;
    }
}
