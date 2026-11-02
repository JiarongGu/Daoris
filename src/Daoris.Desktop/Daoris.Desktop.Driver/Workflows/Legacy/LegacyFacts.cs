namespace Daoris.Driver;

/// <summary>A landing rule as the conversion reads it: the fields of one <c>landings</c> or <c>workspaceLandings</c> entry, copied out so nothing past this file reads the rule's own types.</summary>
public sealed record LegacyRule(
    string Form, string? Pattern, string? Plugin, bool AutoAccept, string? Key, bool Squash, bool Tidy, string? Cycle, LandingIntegrate? Integrate);

/// <summary>
/// What the old keys say about one place (WORKFLOWCLEAN1 1e; the cleanup design §4.1): a repository, or a workspace read with nothing set for
/// any repository. <see cref="Rule"/> is the entry that governs it (its own, else its workspace's); <see cref="Own"/> says it is the place's own.
/// </summary>
public sealed record LegacyFacts(
    string Place, bool IsWorkspace, string Workspace, LegacyRule? Rule, bool Own, bool Composed, bool Opinion, bool Look, string? Checks, bool OwnChecks,
    bool WorkCycle, bool AccessLanding, string Follows, string Level)
{
    /// <summary>Whether the place follows Current, drawn from the old keys.</summary>
    public bool OnCurrent => Follows == WorkflowSelection.Current;

    /// <summary>The keys of the old shapes this place holds of its own, in the census's words.</summary>
    public IReadOnlyList<string> Shapes
    {
        get
        {
            var shapes = new List<string>();
            if (Own && Rule is not null) shapes.Add("landing");
            if (Own && Rule is { Tidy: true }) shapes.Add("tidy");
            if (Own && Rule is { Cycle: not null }) shapes.Add("cycle");
            if (Own && Rule is { Integrate: not null }) shapes.Add("integrate");
            if (AccessLanding) shapes.Add("access");
            if (Opinion) shapes.Add("opinion");
            if (Look) shapes.Add("review");
            if (OwnChecks) shapes.Add("sessionChecks");
            return shapes;
        }
    }
}

/// <summary>
/// The conversion's own reader of the old rule keys (the cleanup design §13 point 4): the census and the translator read every rule through here
/// and through nothing of Current's derivation, so deleting that at row z leaves the conversion working; this is the one file that then reads the
/// raw keys instead of <see cref="DriverConfig"/>'s.
/// </summary>
public static class LegacyReader
{
    private static LegacyRule? Copy(LandingRule? rule) =>
        rule is null ? null : new(rule.Form, rule.Pattern, rule.Plugin, rule.AutoAccept, rule.Key, rule.Squash, rule.Tidy, rule.Cycle, rule.Integrate);

    /// <summary>The facts of repository <paramref name="repository"/> in <paramref name="workspace"/>.</summary>
    public static LegacyFacts Repository(DriverConfig config, string repository, string? workspace)
    {
        var circle = RemoteTarget.Workspace(workspace);
        var own = config.Landings.TryGetValue(repository, out var mine);
        var shared = config.WorkspaceLandings.TryGetValue(circle, out var theirs);
        var rule = own ? mine : shared ? theirs : null;
        var selected = WorkflowSelection.Resolve(config, repository, workspace, null);
        var opinion = OpinionRules.Resolve(config, repository, workspace) is { Rule.IsNone: false };
        var look = ReviewRules.Resolve(config, repository, workspace) is { Rule: { IsNone: false, Required: true } };
        var checks = SessionChecks.For(config, repository);
        return new(repository, false, circle, Copy(rule), own, Composes(config, repository, circle, rule), opinion, look, checks, checks is not null,
            WorkCycled(config, repository, circle), ExplicitAccess(config, repository, circle), selected.Workflow, selected.Level);
    }

    /// <summary>The facts of workspace <paramref name="workspace"/>: what no repository's own key reaches.</summary>
    public static LegacyFacts Workspace(DriverConfig config, string workspace)
    {
        var circle = RemoteTarget.Workspace(workspace);
        var bare = config with
        {
            Landings = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase),
            Reviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase),
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase),
            Access = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase),
        };
        var has = config.WorkspaceLandings.TryGetValue(circle, out var rule);
        var selected = WorkflowSelection.Resolve(config, null, circle, null);
        var opinion = OpinionRules.Resolve(bare, "", circle) is { Rule.IsNone: false };
        var look = ReviewRules.Resolve(bare, "", circle) is { Rule: { IsNone: false, Required: true } };
        return new(circle, true, circle, Copy(rule), has, Composes(bare, "", circle, rule), opinion, look, null, false,
            config.WorkspaceWorkCycles.ContainsKey(circle), config.WorkspaceAccess.TryGetValue(circle, out var access) && Names(access), selected.Workflow, selected.Level);
    }

    private static bool WorkCycled(DriverConfig config, string repository, string circle) =>
        config.WorkCycles.ContainsKey(repository) || config.WorkspaceWorkCycles.ContainsKey(circle);

    private static bool Names(AccessSetting setting) => setting.Pauses.TryGetValue(AccessKey.Landing, out var value) && value == AccessKey.Rule;

    private static bool ExplicitAccess(DriverConfig config, string repository, string circle) =>
        (config.Access.TryGetValue(repository, out var own) && Names(own)) || (config.WorkspaceAccess.TryGetValue(circle, out var shared) && Names(shared));

    /// <summary>
    /// Whether the access setup composed the switch for the place (AUTONOMY1c2): its <c>landing</c> resolves to <c>rule</c> (full access among
    /// them), over a branch rule not already accepting automatically. A merge keeps no switch whatever the setup.
    /// </summary>
    private static bool Composes(DriverConfig config, string repository, string circle, LandingRule? rule)
    {
        if (rule is { AutoAccept: true } || (rule is not null && rule.Form != LandingForm.Branch)) return false;
        var levels = new List<AccessSetting?>();
        if (repository.Length > 0) levels.Add(config.Access.GetValueOrDefault(repository));
        levels.Add(config.WorkspaceAccess.GetValueOrDefault(circle));
        levels.Add(config.MachineAccess);
        foreach (var level in levels.OfType<AccessSetting>())
        {
            if (level.Full) return rule is { Form: LandingForm.Branch };
            if (level.Pauses.TryGetValue(AccessKey.Landing, out var value)) return value == AccessKey.Rule && rule is { Form: LandingForm.Branch };
        }

        return false;
    }
}
