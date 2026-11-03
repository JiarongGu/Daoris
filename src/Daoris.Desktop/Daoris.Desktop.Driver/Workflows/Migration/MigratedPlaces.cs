using System.Text.Json;

namespace Daoris.Driver;

/// <summary>An old landing rule as the migration reads it: the fields of one <c>landings</c> or <c>workspaceLandings</c> entry.</summary>
public sealed record MigratedRule(string Form, string? Pattern, string? Plugin, bool AutoAccept, string? Key, bool Squash, string? Cycle, LandingIntegrate? Integrate);

/// <summary>
/// What the old keys say about one place: a repository, or a workspace read with nothing set for any repository. <see cref="Rule"/> is the entry
/// that governed it (its own, else its workspace's); <see cref="Own"/> says it is the place's own.
/// </summary>
public sealed record MigratedPlace(
    string Place, bool IsWorkspace, string Workspace, MigratedRule? Rule, bool Own, bool Composed, bool Opinion, bool Look, string? Checks, bool OwnChecks,
    bool AccessLanding, string? Follows)
{
    /// <summary>Whether the place followed no saved workflow: nothing named one, or its choice named <c>current</c>.</summary>
    public bool Unconverted => Follows is null or MigratedPlaces.Current;
}

/// <summary>
/// The old keys of <c>driver.json</c> read raw for the migration (D232 point 2): the landing rules no build reads any more, the access setup's
/// <c>landing</c> pause and the session checks, which a saved graph's nodes carry instead.
/// </summary>
public sealed class MigratedPlaces
{
    /// <summary>The old choice of the workflow the rules drew, which a file may still name.</summary>
    public const string Current = "current";

    private readonly DriverConfig _config;
    private readonly IReadOnlyDictionary<string, LandingRule> _landings;
    private readonly IReadOnlyDictionary<string, LandingRule> _shared;

    public MigratedPlaces(DriverConfig config)
    {
        _config = config;
        if (config.Source is { } source)
        {
            using var document = JsonDocument.Parse(source);
            _landings = LandingKey.RuleMap(document.RootElement, "landings");
            _shared = LandingKey.RuleMap(document.RootElement, "workspaceLandings");
        }
        else
        {
            _landings = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase);
            _shared = new Dictionary<string, LandingRule>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Whether the file holds any old landing rule.</summary>
    public bool AnyRule => _landings.Count > 0 || _shared.Count > 0;

    /// <summary>The places this home has: its registry's repositories and the workspaces they and the file name, each once.</summary>
    public (IReadOnlyList<(string Repository, string Workspace)> Repositories, IReadOnlyList<string> Workspaces) Of(IReadOnlyList<RepoView> registry)
    {
        var repositories = registry.Select(row => (row.Repository, RemoteTarget.Workspace(row.Workspace))).ToList();
        foreach (var name in _landings.Keys.Concat(_config.Workflows.Keys).Concat(_config.SessionChecks.Keys).Concat(_config.Access.Keys))
        {
            if (repositories.All(each => !string.Equals(each.Repository, name, StringComparison.OrdinalIgnoreCase))) repositories.Add((name, RemoteTarget.Workspace(null)));
        }

        var workspaces = repositories.Select(each => each.Item2)
            .Concat(_shared.Keys).Concat(_config.WorkspaceWorkflows.Keys).Concat(_config.WorkspaceAccess.Keys)
            .Select(RemoteTarget.Workspace).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return ([.. repositories.OrderBy(each => each.Repository, StringComparer.OrdinalIgnoreCase)], workspaces);
    }

    private static MigratedRule? Copy(LandingRule? rule) =>
        rule is null ? null : new(rule.Form, rule.Pattern, rule.Plugin, rule.AutoAccept, rule.Key, rule.Squash, rule.Cycle, rule.Integrate);

    /// <summary>The facts of repository <paramref name="repository"/> in <paramref name="workspace"/>.</summary>
    public MigratedPlace Repository(string repository, string? workspace)
    {
        var circle = RemoteTarget.Workspace(workspace);
        var own = _landings.TryGetValue(repository, out var mine);
        var shared = _shared.TryGetValue(circle, out var theirs);
        var rule = own ? mine : shared ? theirs : null;
        var opinion = OpinionRules.Resolve(_config, repository, workspace) is { Rule.IsNone: false };
        var look = ReviewRules.Resolve(_config, repository, workspace) is { Rule: { IsNone: false, Required: true } };
        var checks = SessionChecks.For(_config, repository);
        return new(repository, false, circle, Copy(rule), own, Composes(_config, repository, circle, rule), opinion, look, checks, checks is not null,
            ExplicitAccess(_config, repository, circle), Follows(_config, repository, workspace));
    }

    /// <summary>The facts of workspace <paramref name="workspace"/>: what no repository's own key reaches.</summary>
    public MigratedPlace Workspace(string workspace)
    {
        var circle = RemoteTarget.Workspace(workspace);
        var bare = _config with
        {
            Reviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase),
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase),
            Access = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase),
        };
        var has = _shared.TryGetValue(circle, out var rule);
        var opinion = OpinionRules.Resolve(bare, "", circle) is { Rule.IsNone: false };
        var look = ReviewRules.Resolve(bare, "", circle) is { Rule: { IsNone: false, Required: true } };
        return new(circle, true, circle, Copy(rule), has, Composes(bare, "", circle, rule), opinion, look, null, false,
            _config.WorkspaceAccess.TryGetValue(circle, out var access) && Names(access), Follows(_config, null, circle));
    }

    /// <summary>The saved workflow a place follows, or null where nothing names one; <see cref="Current"/> where its choice still names it.</summary>
    public static string? Follows(DriverConfig config, string? repository, string? workspace) =>
        WorkflowSelection.Resolve(config, repository, workspace, null).Workflow;

    private static bool Names(AccessSetting setting) => setting.Pauses.TryGetValue(AccessKey.Landing, out var value) && value == AccessKey.Rule;

    private static bool ExplicitAccess(DriverConfig config, string repository, string circle) =>
        (config.Access.TryGetValue(repository, out var own) && Names(own)) || (config.WorkspaceAccess.TryGetValue(circle, out var shared) && Names(shared));

    /// <summary>
    /// Whether the access setup composed the automatic switch for the place: its <c>landing</c> resolves to <c>rule</c> (full access among them),
    /// over a branch rule not already accepting automatically. A merge kept no switch whatever the setup.
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
