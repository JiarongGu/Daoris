namespace Daoris.Driver;

/// <summary>Where a conversation in a repository runs and what its agent may do there.</summary>
internal static class RepositoryPlacement
{
    /// <summary>
    /// Where a conversation in a repository runs: its tree, what it may do there and reach outside it, as a driven session in this repository
    /// would, the plugins' servers, and its conversation kept for it to go on.
    /// </summary>
    /// <param name="repository">The repository as the conversation names it, which its rules are composed for.</param>
    /// <param name="continued">The parts its line continues, by id: their kept folders are read as its own.</param>
    /// <param name="answers">
    /// A chat run whose process names a second opinion: handed <c>opinion_answer</c> from its spawn, since a live conversation's rules are
    /// fixed then, as a driven run is at its resume (<see cref="KnowledgeConnector.AnswersFindings"/>).
    /// </param>
    public static ChatPlace In(
        string home, DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree,
        IReadOnlyList<string>? continued = null, bool answers = false)
    {
        var across = AcrossRules.Reach(config, registry, known.Repository, known.Workspace);
        IReadOnlyList<string> kept = [.. (continued ?? []).Where(SessionEventText.IsLocalId).Select(part => ChatFiles.Folder(home, part))];
        return new ChatPlace(
            known.Repository, workTree,
            (file, id) => RulesFor(file, known.Workspace, repository, workTree, ChatFiles.Folder(home, id), across, kept, answers),
            Plugins: true, ConnectorOnPipe: false, Posture: null)
        {
            Also = [.. across.Writes.Select(target => target.Path)],
            Keeps = true,
            // Its standing block: its tree's facts with its repository's routing and the person's choices on this machine.
            Block = StandingBlocks.OnMachine(StandingBlocks.OfTree(known.Repository, workTree), ParticipantWork.Chat, config, registry, known.Workspace,
                apart: known.Root is not { } root || !string.Equals(Path.GetFullPath(workTree), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)),
        };
    }

    /// <summary>
    /// What a conversation's agent in a repository may do: the same union a driven session in this repository is handed, with what it may reach
    /// across. What the person attaches is kept outside the tree, for this conversation alone, and the agent reads it where it lies: a read of
    /// exactly that folder, every other read there would be asked, and every ask is refused.
    /// </summary>
    /// <param name="continued">
    /// The kept folders of the parts its line continues: a new part is handed their files by name and where they lie, so it may read exactly
    /// those folders too, as its own. None for a conversation that continues none.
    /// </param>
    /// <param name="answers">Whether it is handed <c>opinion_answer</c>, a chat run whose process names a second opinion.</param>
    public static RuleLists RulesFor(
        PermissionFile file, string? workspace, string repository, string tree, string kept, AcrossReach across,
        IReadOnlyList<string>? continued = null, bool answers = false)
    {
        var composed = PermissionRules.Compose(file, workspace, repository);
        IReadOnlyList<string> folders = [kept, .. (continued ?? []).Where(folder => !string.Equals(folder, kept, StringComparison.OrdinalIgnoreCase))];
        IReadOnlyList<string> job = answers ? [.. KnowledgeConnector.AnswersFindings.Where(rule => !composed.Allow.Contains(rule))] : [];
        return (composed with { Allow = [.. composed.Allow, .. folders.Select(PermissionRules.ReadRule), .. job] })
            .Joined(AcrossRules.Rules(across, [tree, .. folders]));
    }
}
