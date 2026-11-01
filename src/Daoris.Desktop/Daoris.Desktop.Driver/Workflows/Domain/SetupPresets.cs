namespace Daoris.Driver;

/// <summary>
/// A place's delivery as `land` options (the land node design §2 and §7): the method, who accepts, the plugin a pull request is opened by
/// (<c>none</c> for a branch left to push) and an integration's request.
/// </summary>
public sealed record SetupLand(string Method, string Accept, string? Plugin = null, string? Request = null);

/// <summary>One delivery preset: its id, the name the page shows, and its land; <i>Not driven</i> has none.</summary>
public sealed record SetupPreset(string Id, string Name, SetupLand? Land);

/// <summary>The preset a place's delivery matches, or none, and each option of its own that differs from it.</summary>
public sealed record SetupMatch(string? Preset, IReadOnlyList<string> Own)
{
    public bool Equals(SetupMatch? other) => other is not null && Preset == other.Preset && Own.SequenceEqual(other.Own);

    public override int GetHashCode() => HashCode.Combine(Preset, Own.Count);
}

/// <summary>
/// The delivery presets of a place's quick set-up (AGENTSPLACE1a2; the Agents-place design §2 point 3 and §4, the land node design §7), beside
/// the workflows' presets: each is a set of `land` options applied to Current through the landing rule, never stored. A place's preset is
/// derived from what its work follows (<see cref="LandOf"/>, <see cref="Match"/>), or said as the options of its own.
/// </summary>
public static class SetupPresets
{
    public const string NotDriven = "not-driven";
    public const string Merge = "merge-after-accept";
    public const string Branch = "branch-to-push";
    public const string PullRequest = "pull-request";
    public const string Integrated = "integrated";

    /// <summary>The pattern a branch preset gives a rule that names none.</summary>
    public const string Pattern = "feature/{quest}-{slug}";

    public static IReadOnlyList<SetupPreset> Table { get; } =
    [
        new(NotDriven, "Not driven", null),
        new(Merge, "Merge after you accept", new(LandNodes.Merge, LandNodes.You)),
        new(Branch, "A branch to push", new(LandNodes.Branch, LandNodes.You, LandNodes.NoPlugin)),
        new(PullRequest, "A pull request", new(LandNodes.PullRequest, LandNodes.You)),
        new(Integrated, "Integrated into the line", new(LandNodes.Integrate, LandNodes.You, LandNodes.NoPlugin, LandingIntegrate.Owner)),
    ];

    /// <summary>A preset by its id, or null.</summary>
    public static SetupPreset? Of(string? id) => Table.FirstOrDefault(preset => preset.Id == id);

    /// <summary>
    /// The land a workflow's steps say, Current's drawn or a version's: <c>integrate</c> where an integrate step follows the landing, a pull
    /// request where one does, else the landing's method or form. Current never says <c>push</c> (the land node design §4).
    /// </summary>
    public static SetupLand LandOf(IReadOnlyList<NamedStep> steps)
    {
        var landing = steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Landing);
        var accept = landing?.Field("accept") as string == LandNodes.Automatic ? LandNodes.Automatic : LandNodes.You;
        var plugin = landing?.Field("plugin") as string;
        if (steps.FirstOrDefault(step => step.Kind == NodeKinds.Integrate) is { } integrate)
        {
            return new(LandNodes.Integrate, accept, LandNodes.NoPlugin, integrate.Field("request") as string ?? LandingIntegrate.Owner);
        }

        if (steps.FirstOrDefault(step => step.Kind == WorkflowKinds.PullRequest) is { } pullRequest)
        {
            return new(LandNodes.PullRequest, accept, pullRequest.Field("plugin") as string ?? plugin);
        }

        if (landing?.Field("method") is string method) return new(method, accept, plugin);
        return landing?.Field("form") as string == LandingForm.Branch
            ? new(LandNodes.Branch, accept, plugin ?? LandNodes.NoPlugin)
            : new(LandNodes.Merge, accept);
    }

    /// <summary>
    /// The preset a place's delivery matches: <i>Not driven</i> where it is not driven, else the one of its method, each of its accept and
    /// request that differs said as its own. A method no preset holds matches none.
    /// </summary>
    public static SetupMatch Match(bool driven, SetupLand land)
    {
        if (!driven) return new(NotDriven, []);
        if (Table.FirstOrDefault(each => each.Land?.Method == land.Method)?.Land is not { } wanted) return new(null, [$"method `{land.Method}`"]);
        var own = new List<string>();
        if (land.Accept != wanted.Accept) own.Add($"accept `{land.Accept}`");
        if (wanted.Request is not null && land.Request != wanted.Request) own.Add($"request `{land.Request}`");
        return new(Table.First(each => each.Land == wanted).Id, own);
    }

    /// <summary>
    /// The landing rule a preset's land writes over the rule standing (Current's `land` options, the land node design §4): a branch keeps the
    /// standing branch's pattern, tidy, key, squash and cycle, or takes <see cref="Pattern"/>; a merge keeps its tidy and cycle. An
    /// integration keeps the line's policy, <paramref name="policy"/>, and asks with the land's request only where it reads as another.
    /// </summary>
    public static LandingRule RuleOf(SetupLand land, LandingRule standing, LandingIntegrate? policy)
    {
        var automatic = land.Accept == LandNodes.Automatic;
        if (land.Method == LandNodes.Merge) return LandingRule.Merge with { Tidy = standing.Tidy, Cycle = standing.Cycle };

        var branch = standing.Form == LandingForm.Branch ? standing : new LandingRule(LandingForm.Branch, Pattern, standing.Tidy) { Cycle = standing.Cycle };
        if (land.Method == LandNodes.Integrate)
        {
            var kept = policy ?? new LandingIntegrate();
            var request = kept.Resolved().Request == land.Request ? kept.Request : land.Request;
            return branch with { Plugin = null, Squash = false, AutoAccept = automatic, Integrate = kept with { Request = request } };
        }

        var plugin = land.Method == LandNodes.PullRequest ? land.Plugin : null;
        return branch with { Plugin = plugin, AutoAccept = automatic, Integrate = null };
    }

    /// <summary>
    /// The landing plugin a pull request is opened by for a remote on <paramref name="host"/>: the rule's own, <paramref name="named"/>, where
    /// it can land work here; else the first contributing plugin speaking on <see cref="HookPoints.Land"/> whose name, after its publisher,
    /// names the host's platform; null where none does.
    /// </summary>
    public static string? PluginFor(string? host, string? named, IWorkplacePluginCatalog catalog)
    {
        if (named is not null && LandingRules.PluginProblem(named, catalog) is null) return named;
        if (PlatformOf(host) is not { } platform) return null;
        return catalog.Contributing.FirstOrDefault(entry => entry.Points.Contains(HookPoints.Land, StringComparer.Ordinal)
            && entry.Id[(entry.Id.IndexOf('.') + 1)..].Contains(platform, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    /// <summary>The platform a remote's host is: <c>github</c>, <c>gitlab</c>, <c>azure</c> or <c>bitbucket</c>; null for one none is known for.</summary>
    public static string? PlatformOf(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var name = host.Trim().Split(':')[0].ToLowerInvariant();
        if (name == "github.com" || name.EndsWith(".github.com", StringComparison.Ordinal) || name.StartsWith("github.", StringComparison.Ordinal)) return "github";
        if (name.Contains("gitlab", StringComparison.Ordinal)) return "gitlab";
        if (name is "dev.azure.com" or "ssh.dev.azure.com" || name.EndsWith(".visualstudio.com", StringComparison.Ordinal)) return "azure";
        return name.Contains("bitbucket", StringComparison.Ordinal) ? "bitbucket" : null;
    }

    /// <summary>
    /// The delivery preselected from what the driver already reads (the design §4): the preset of a place whose delivery was chosen, driven
    /// with a landing rule set (<paramref name="chosen"/>); else <i>Integrated into the line</i> where the line's declaration declares an
    /// <c>integration</c>; else <i>A pull request</i> where a landing plugin serves the remote's platform; else <i>Merge after you accept</i>.
    /// </summary>
    public static string Suggest(SetupMatch match, bool chosen, bool declaresIntegration, string? plugin) =>
        chosen && match.Preset is { } kept && kept != NotDriven ? kept
        : declaresIntegration ? Integrated
        : plugin is not null ? PullRequest
        : Merge;

    /// <summary>How a rule lands its work, in a few words: the plan's before and after of the landing.</summary>
    public static string Said(LandingRule rule)
    {
        if (rule.Form == LandingForm.Merge) return "merged after you accept";
        var press = rule.AutoAccept ? " with no press" : "";
        var asks = rule.Integrate?.Resolved().Request switch
        {
            LandingIntegrate.Done => "requested once its agent is done",
            LandingIntegrate.Steward => "requested by the steward",
            _ => "requested by you",
        };
        return rule.Integrate is not null ? $"on `{rule.Pattern}`{press}, integrated into the line, {asks}"
            : rule.Plugin is { } plugin ? $"on `{rule.Pattern}`{press}, a pull request by `{plugin}`"
            : $"on `{rule.Pattern}`{press}, a branch for you to push";
    }
}
