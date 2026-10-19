namespace Daoris.Driver;

/// <summary>What a proposal names (ASKSCOPE1c): the workspaces and the repositories, as its kind reads its fields.</summary>
/// <param name="Workspaces">The workspaces it names; a blank one names none.</param>
/// <param name="Repositories">The repositories it names, by the registry's names; a blank one names none.</param>
internal sealed record HelpNames(IReadOnlyList<string?> Workspaces, IReadOnlyList<string?> Repositories)
{
    /// <summary>Words that are a repository's name or a workspace's, which its judge finds which of (a workflow's scope).</summary>
    public IReadOnlyList<string?> Either { get; init; } = [];

    /// <summary>Whether it reaches every workspace at once: a look and a press that name neither a workspace nor a repository.</summary>
    public bool Every { get; init; }

    /// <summary>
    /// The records it names by id (ASKSCOPE1c2), each as a sentence names it, with the workspace the facts its kind reads file it
    /// under; one they do not hold is left out (<see cref="HelpProposalScope"/>).
    /// </summary>
    public IReadOnlyList<(string Said, string? Workspace)> Records { get; init; } = [];

    public static (string Said, string? Workspace) Quest(string id, string? workspace) => ($"quest `#{id}`", workspace);

    public static (string Said, string? Workspace) Ask(string id, string? workspace) => ($"ask `#{id}`", workspace);

    public static (string Said, string? Workspace) Session(string id, string? workspace) => ($"session `{id}`", workspace);

    /// <summary>The quest <paramref name="id"/> names among those a delete and a go's item are judged against; none where none is.</summary>
    public static IReadOnlyList<(string Said, string? Workspace)> OfQuest(HelpMachineFacts facts, string id) =>
        facts.Quests.FirstOrDefault(each => Is(each.Id, id)) is { } quest ? [Quest(quest.Id, quest.Workspace)] : [];

    /// <summary>The ask <paramref name="id"/> names among those a delete and a go's item are judged against; none where none is.</summary>
    public static IReadOnlyList<(string Said, string? Workspace)> OfAsk(HelpMachineFacts facts, string id) =>
        facts.Asks.FirstOrDefault(each => Is(each.Id, id)) is { } ask ? [Ask(ask.Id, ask.Workspace)] : [];

    /// <summary>The session <paramref name="id"/> names among this machine's records; none where none is.</summary>
    public static IReadOnlyList<(string Said, string? Workspace)> OfSession(HelpMachineFacts facts, string id) =>
        facts.Sessions.FirstOrDefault(each => Is(each.Id, id)) is { } session ? [Session(session.Id, session.Workspace)] : [];

    /// <summary>
    /// ASKSCOPE1c3: the held or parked quest a retry names, filed under its repository's workspace (a quest record carries a
    /// repository, not a workspace). A repository registered in several workspaces files it under the conversation's where any
    /// registration is, as a repository is judged (<c>Elsewhere</c>); a quest on neither list, or whose repository is registered
    /// nowhere, names nothing, and <c>retry</c> refuses it.
    /// </summary>
    public static IReadOnlyList<(string Said, string? Workspace)> OfParked(HelpMachineFacts facts, string id)
    {
        var (quest, repository) = facts.Held.FirstOrDefault(each => Is(each.Quest, id)) is { } held ? (held.Quest, held.Repository)
            : facts.Parked.FirstOrDefault(each => Is(each.Quest, id)) is { } parked ? (parked.Quest, parked.Repository)
            : (null, null);
        if (quest is null || repository is null) return [];
        var workspaces = facts.Registered.Where(known => string.Equals(known.Repository.Trim(), repository.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(known => known.Workspace).ToList();
        if (workspaces.Count == 0) return [];
        var workspace = workspaces.FirstOrDefault(each => string.Equals(RemoteTarget.Workspace(each), RemoteTarget.Workspace(facts.Circle), StringComparison.OrdinalIgnoreCase))
            ?? workspaces[0];
        return [Quest(quest, workspace)];
    }

    // An id matched as each kind's judge matches it: without case, its `#` already stripped.
    private static bool Is(string one, string other) => string.Equals(one, other.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A proposal judged against the workspace its conversation is bound to (ASKSCOPE1c, D158's ASKSCOPE1 note point 5): one naming
/// another workspace, a repository of another, or every workspace at once is refused before its kind judges it, and says so.
/// </summary>
/// <remarks>
/// <para><b>By the record, never by the proposal.</b> The workspace is the conversation's (<see cref="HelpMachineFacts.Circle"/>),
/// read from its record, so nothing the helper writes in a proposal moves it. <c>default</c>'s room is the whole machine
/// (ASKSCOPE1b), so its conversation's proposals are judged by their kind alone, as before.</para>
///
/// <para><b>A name registered nowhere is left to the kind</b>, whose own refusal names what is registered. One registered in
/// several workspaces is the conversation's where any of them is.</para>
///
/// <para><b>A record named by id is its workspace's</b> (ASKSCOPE1c2): a quest, an ask or a session a delete, an accept, a pause,
/// a clear, an archive or a go's item names is refused where the facts its kind reads file it under another workspace. One those
/// facts do not hold names no workspace, and its kind refuses an id it does not know.</para>
/// </remarks>
internal static class HelpProposalScope
{
    /// <summary>The refusal of a proposal naming what <paramref name="named"/> says, where it is outside its conversation's workspace; null where it is not.</summary>
    public static string? Refusal(HelpNames named, HelpMachineFacts facts)
    {
        if (facts.Circle is not { Length: > 0 } circle) return null;
        if (named.Every)
        {
            return $"this conversation is workspace `{circle}`'s, and this proposal reaches every workspace at once: name workspace "
                + $"`{circle}`, or one of its repositories.";
        }

        if (Words(named.Workspaces).FirstOrDefault(workspace => !Same(workspace, circle)) is { } other)
        {
            return Outside(circle, $"workspace `{other}`");
        }

        if (Words(named.Repositories).FirstOrDefault(repository => Elsewhere(repository, circle, facts)) is { } theirs)
        {
            return Outside(circle, $"`{theirs}`, a repository of another workspace");
        }

        foreach (var (said, workspace) in named.Records)
        {
            if (!string.IsNullOrWhiteSpace(workspace) && !Same(workspace, circle)) return Outside(circle, $"{said}, of workspace `{workspace.Trim()}`");
        }

        // A word that is a repository is judged as one; else one that is another workspace is that workspace.
        bool Beyond(string word) => facts.Registered.Any(known => SameName(known.Repository, word))
            ? Elsewhere(word, circle, facts)
            : !Same(word, circle) && facts.Workspaces.Any(workspace => Same(workspace, word));
        return Words(named.Either).FirstOrDefault(Beyond) is { } beyond ? Outside(circle, $"`{beyond}`, of another workspace") : null;
    }

    /// <summary>
    /// ASKSCOPE1c3: whether the placed session <paramref name="session"/> is the conversation's workspace's, by the record that files
    /// it; true for all where the conversation has no circle. A row no record files is not (its workspace is unknown).
    /// </summary>
    public static bool InCircle(HelpMachineFacts facts, string session) =>
        facts.Circle is not { Length: > 0 } circle
        || (facts.Sessions.FirstOrDefault(each => string.Equals(each.Id, session, StringComparison.OrdinalIgnoreCase)) is { } record && Same(record.Workspace, circle));

    private static string Outside(string circle, string named) =>
        $"this conversation is workspace `{circle}`'s, and this proposal names {named}: it proposes for `{circle}` alone. Another "
        + "workspace's change is proposed in a conversation started from a window on that workspace.";

    private static IEnumerable<string> Words(IEnumerable<string?> names) =>
        names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!.Trim());

    /// <summary>Whether <paramref name="repository"/> is registered here and in no registration of <paramref name="circle"/>.</summary>
    private static bool Elsewhere(string repository, string circle, HelpMachineFacts facts)
    {
        var registered = facts.Registered.Where(known => SameName(known.Repository, repository)).ToList();
        return registered.Count > 0 && !registered.Any(known => Same(known.Workspace, circle));
    }

    private static bool Same(string? one, string? other) =>
        string.Equals(RemoteTarget.Workspace(one), RemoteTarget.Workspace(other), StringComparison.OrdinalIgnoreCase);

    private static bool SameName(string one, string other) => string.Equals(one.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase);
}
