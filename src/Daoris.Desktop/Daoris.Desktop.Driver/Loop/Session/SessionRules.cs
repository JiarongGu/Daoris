using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The permission rules a session is handed beside its instruction: this machine's rules composed for its workspace and repository, what
/// its job, its kind of session (a reviewer, a helper, a partner, a brainstorm) and its run's checks add or take away, written to its
/// own settings file, which goes when the run does.
/// </summary>
internal static class SessionRules
{
    /// <summary>
    /// The file, which goes when the session does, what the protocol door carries, and what was handed, for the instruction's account,
    /// which outlives the file. A harness a Claude Code rule means nothing to is handed nothing, and no file is written.
    /// </summary>
    public static (string? File, object? Meta, HandedSection Handed) Hand(
        string home, ISessionAdapter adapter, ProcessStartInfo info, string sessionId, string? workspace, string? repository,
        string tree, string? kept, IReadOnlyList<string>? job, AcrossReach? across, string? said,
        bool opinion, bool helper, IReadOnlyList<string>? plugins, string? brainstorm,
        IReadOnlyList<string>? continued, HeavyHanded? heavy, bool partner)
    {
        if (!adapter.TakesSettings) return (null, null, Handed(takes: false, PermissionFile.Empty, RuleLists.Empty, workspace, repository));

        var rules = PermissionRules.Load(home);
        var composed = PermissionRules.Compose(rules, workspace, repository);
        // `no-push`'s forms follow the session's git access, and the classifier is told as git access says it.
        var gitWrite = GitAccessKeys.Resolve(home, repository, workspace).Of(GitAccessKeys.Write).Value;
        composed = GitAccessPolicy.Compose(composed, gitWrite);
        var pushTold = GitAccessPolicy.HardDeny(PermissionRules.HardDeny(rules), gitWrite);
        var folders = new[] { kept, said }.Concat(continued ?? []).OfType<string>().Where(folder => folder.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var folder in folders)
        {
            composed = composed with { Allow = [.. composed.Allow, PermissionRules.ReadRule(folder)] };
        }

        if (across is not null) composed = composed.Joined(AcrossRules.Rules(across, [tree, kept ?? "", said ?? "", .. folders]));

        // 🔴 What the session's job needs, beside the person's rules: an intake's room names its tools in its own settings file, and over
        // the protocol door only what is handed at spawn counts. The first real intake was refused `WebFetch` that way. A deny the
        // person wrote still wins.
        if (job is { Count: > 0 })
        {
            composed = composed with { Allow = [.. composed.Allow, .. job.Where(rule => !composed.Allow.Contains(rule))] };
        }

        // A helper's session is the reviewer's shape with its own one tool.
        if (opinion || helper) composed = OpinionPermissions.Shape(composed, helper);

        // A partner's session writes its copy and calls its reading tools and its answer; the server's refusals by role beside it.
        if (partner) composed = PartnerPermissions.Shape(composed);

        // A brainstorm's pass reads its room and gives its one answer: the `brainstorm` posture, never the reviewer's.
        if (brainstorm is not null) composed = BrainstormPermissions.Shape(composed, tree, brainstorm);

        // Under a light pipeline each heavy gate's declared command, exactly, beside `no-push`, and the classifier told; a deny the
        // person wrote still stands beside them.
        if (heavy is { Deny.Count: > 0 }) composed = composed with { Deny = [.. composed.Deny.Union(heavy.Deny, StringComparer.Ordinal)] };
        IReadOnlyList<string> heavyTold = heavy?.HardDeny is { } sentence ? [sentence] : [];

        var file = SpawnSettings.Write(
            home, sessionId, composed,
            brainstorm is not null ? [.. pushTold, BrainstormPermissions.HardDeny]
            : partner ? [.. pushTold, PartnerPermissions.HardDeny]
            : helper ? [.. pushTold, OpinionPermissions.HelperHardDeny]
            : opinion ? [.. pushTold, OpinionPermissions.HardDeny] : [.. pushTold, .. heavyTold],
            // A partner is held to its copy whatever the person's defaults say.
            PermissionRules.GuardsTree(rules) || partner ? TreeGuard.For(home, tree, across?.Writes.Select(target => target.Path)) : null,
            // Which agent made each call, and each subagent's start and stop: the part's children bound by its lineage.
            ChildLineage.HookFor(home, sessionId));
        var handed = Handed(takes: true, rules, composed, workspace, repository);
        if (adapter.Wire == SessionWire.Pipe)
        {
            adapter.HandSettings(info, file);
            return (file, null, handed);
        }

        return (file, adapter.AcpSessionMeta(file, plugins ?? []), handed);
    }

    /// <summary>
    /// What a partner's start hands its agent beside the instruction: the rules <see cref="Hand"/> writes for it, its classifier's hard
    /// denials and its tree guard, which always holds it; what its effective configuration's digest is taken over.
    /// </summary>
    public static (RuleLists Rules, IReadOnlyList<string> HardDeny, bool Guarded) PartnerHanded(string home, string? workspace, string? repository)
    {
        var rules = PermissionRules.Load(home);
        return (PartnerPermissions.Shape(PermissionRules.Compose(rules, workspace, repository)),
            [.. PermissionRules.HardDeny(rules), PartnerPermissions.HardDeny], true);
    }

    /// <summary>
    /// The permission rules a session was handed, kept in its instruction's account: how many in each list, the scopes they were composed
    /// for, the hard denials and the tree guard. The rules file goes when the run does, and the account keeps what it held by count.
    /// </summary>
    /// <param name="takes">Whether the session's agent takes Daoris's rules at all (<see cref="ISessionAdapter.TakesSettings"/>).</param>
    /// <param name="handed">The rules as handed: the composition, with any read of the quest's files, reach across and job's own.</param>
    public static HandedSection Handed(bool takes, PermissionFile file, RuleLists handed, string? workspace, string? repository)
    {
        const string Name = HandedSections.Rules;
        const string Source = HandedSources.Permissions;
        if (!takes)
        {
            return new HandedSection(Name, Source, "the permission rules: none handed, since its agent takes no rules from Daoris")
            {
                Shown = 0,
                None = HandedNones.Agent,
            };
        }

        var count = handed.Allow.Count + handed.Ask.Count + handed.Deny.Count;
        var hard = PermissionRules.HardDeny(file).Count;
        var guard = PermissionRules.GuardsTree(file);
        if (count == 0 && hard == 0 && !guard)
        {
            return new HandedSection(Name, Source, "the permission rules: none handed, since none were composed for it")
            {
                Shown = 0,
                None = HandedNones.Empty,
            };
        }

        var scope = $"workspace `{RemoteTarget.Workspace(workspace)}`" + (repository is { Length: > 0 } named ? $" and repository `{named}`" : "");
        var beside = new List<string>();
        if (hard > 0) beside.Add(InstructionAccounting.Plural(hard, "hard denial"));
        if (guard) beside.Add("the tree guard");
        return new HandedSection(
            Name, Source,
            $"the permission rules: {count} handed beside it ({handed.Allow.Count} allow, {handed.Ask.Count} ask, {handed.Deny.Count} deny), "
            + $"composed from this machine's rules for {scope}" + (beside.Count > 0 ? "; " + InstructionAccounting.Joined(beside) : ""))
        {
            Shown = count,
            Cuts = file.Problem is null
                ? null
                : [new HandedCut(HandedCuts.RulesUnread, "this machine's rules file could not be read, so only the defaults were handed")],
        };
    }
}
