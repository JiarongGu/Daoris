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

        // A word that is a repository is judged as one; else one that is another workspace is that workspace.
        bool Beyond(string word) => facts.Registered.Any(known => SameName(known.Repository, word))
            ? Elsewhere(word, circle, facts)
            : !Same(word, circle) && facts.Workspaces.Any(workspace => Same(workspace, word));
        return Words(named.Either).FirstOrDefault(Beyond) is { } beyond ? Outside(circle, $"`{beyond}`, of another workspace") : null;
    }

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
