using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A landing rule as the migration reads it: the fields of one <c>landings</c> or <c>workspaceLandings</c> entry.</summary>
public sealed record MigratedRule(string Form, string? Pattern, string? Plugin, bool AutoAccept, string? Key, bool Squash, string? Cycle, LandingIntegrate? Integrate);

/// <summary>
/// What the keys say about one place: a repository, or a workspace read with nothing set for any repository. <see cref="Rule"/> is the entry
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
/// The keys of <c>driver.json</c> read raw for the migration: the landing rules, the access setup's
/// <c>landing</c> pause and the session checks, which a saved graph's nodes carry instead.
/// </summary>
public sealed class MigratedPlaces
{
    /// <summary>The choice of the workflow the rules drew, which a file may still name.</summary>
    public const string Current = "current";

    private readonly DriverConfig _config;
    private readonly IReadOnlyDictionary<string, MigratedRule> _landings;
    private readonly IReadOnlyDictionary<string, MigratedRule> _shared;

    public MigratedPlaces(DriverConfig config)
    {
        _config = config;
        if (config.Source is { } source)
        {
            using var document = JsonDocument.Parse(source);
            _landings = RuleMap(document.RootElement, "landings");
            _shared = RuleMap(document.RootElement, "workspaceLandings");
        }
        else
        {
            _landings = new Dictionary<string, MigratedRule>(StringComparer.OrdinalIgnoreCase);
            _shared = new Dictionary<string, MigratedRule>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The rules under <paramref name="name"/>, the first spelling of a name in any case kept: a rule whose form is neither merge nor
    /// branch, or whose branch pattern names no branch, is passed over; a merge keeps no pattern; an <c>integrate</c> that does not read is
    /// none.
    /// </summary>
    private static Dictionary<string, MigratedRule> RuleMap(JsonElement root, string name)
    {
        var map = new Dictionary<string, MigratedRule>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;
        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value;
            if (map.ContainsKey(property.Name) || value.ValueKind != JsonValueKind.Object) continue;
            var form = Json.NonEmpty(value, "form");
            var pattern = Json.NonEmpty(value, "pattern");
            if (form == LandingForm.Merge) pattern = null;
            else if (form != LandingForm.Branch || BranchPatterns.Problem(pattern ?? "") is not null) continue;
            var cycle = value.TryGetProperty("cycle", out var written) && written.ValueKind != JsonValueKind.Null ? CycleText.Compact(written) : null;
            var integrate = value.TryGetProperty("integrate", out var policy) && policy.ValueKind == JsonValueKind.Object
                ? LinePolicyKey.IntegrateOf(policy, out _)
                : null;
            map[property.Name] = new MigratedRule(form, pattern, Json.NonEmpty(value, "plugin"),
                value.TryGetProperty("autoAccept", out var auto) && auto.ValueKind == JsonValueKind.True, Json.NonEmpty(value, "key"),
                value.TryGetProperty("squash", out var squash) && squash.ValueKind == JsonValueKind.True, cycle, integrate);
        }

        return map;
    }

    /// <summary>Whether the file holds any landing rule.</summary>
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
        return new(repository, false, circle, rule, own, Composes(_config, repository, circle, rule), opinion, look, checks, checks is not null,
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
        return new(circle, true, circle, rule, has, Composes(bare, "", circle, rule), opinion, look, null, false,
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
    private static bool Composes(DriverConfig config, string repository, string circle, MigratedRule? rule)
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
