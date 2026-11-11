namespace Daoris.Driver;

/// <summary>
/// A place's delivery as its graph says it: the method, who accepts, the plugin whose step opens a pull request
/// (null where none does) and an integration's request. <see cref="Accept"/> is <c>you</c> where a <c>review</c> comes before the <c>land</c>
/// and <c>automatic</c> where none does. <see cref="Method"/> is a <c>land</c>'s method, or <see cref="PullRequest"/> for a branch
/// followed by a push and a plugin's pull-request step.
/// </summary>
public sealed record SetupLand(string Method, string Accept, string? Plugin = null, string? Request = null)
{
    public const string You = "you";
    public const string Automatic = "automatic";

    /// <summary>The delivery of a branch pushed, with a plugin's step that opens a pull request: not a <c>land</c> method.</summary>
    public const string PullRequest = "pull-request";

    /// <summary>The delivery of a graph with no <c>land</c>: the work stays in the department's place, where the person accepts it.</summary>
    public const string InPlace = "in-place";
}

/// <summary>One delivery preset: its id, the name the page shows, and its land; <i>Not driven</i> has none.</summary>
public sealed record SetupPreset(string Id, string Name, SetupLand? Land);

/// <summary>Why a delivery is suggested: <see cref="Code"/> is for the page to word, <see cref="Said"/> is the driver's own sentence.</summary>
public sealed record SuggestionWhy(string Code, string Said)
{
    public const string Kept = "kept";
    public const string Integration = "integration";
    public const string Default = "default";

    /// <summary>The reason a code stands for, in the driver's words.</summary>
    public static SuggestionWhy Of(string code) => new(code, code switch
    {
        Kept => "It is how this place already lands its work.",
        Integration => "Its line's declaration declares an integration.",
        _ => "Nothing here asks for another, so work merges into the line after you accept.",
    });
}

/// <summary>The preset a place's delivery matches, or none, and each option of its own that differs from it.</summary>
public sealed record SetupMatch(string? Preset, IReadOnlyList<string> Own)
{
    public bool Equals(SetupMatch? other) => other is not null && Preset == other.Preset && Own.SequenceEqual(other.Own);

    public override int GetHashCode() => HashCode.Combine(Preset, Own.Count);
}

/// <summary>
/// The delivery presets of a place's quick set-up, beside
/// the workflows' presets: each is a default Daoris ships, whose saved copy a set-up has the place follow, never a rule written beside it. A place's preset is derived from what its work follows (<see cref="LandOf"/>, <see cref="Match"/>),
/// or said as the options of its own. After <i>Not driven</i>, each is a default the data offers a set-up (<see cref="WorkflowDefaults"/>),
/// with its name and the delivery its graph derives.
/// </summary>
public static class SetupPresets
{
    public const string NotDriven = "not-driven";

    /// <summary>The defaults the suggestion names (<see cref="Suggest"/>), each a shipped default's id.</summary>
    public const string Merge = "merge-after-accept";
    public const string Branch = "branch-to-push";
    public const string PullRequest = "pull-request";
    public const string Integrated = "integrated";

    public static IReadOnlyList<SetupPreset> Table { get; } =
    [
        new(NotDriven, "Not driven", null),
        .. WorkflowDefaults.All.Where(shipped => shipped.SetUp).Select(shipped => new SetupPreset(shipped.Id, shipped.DeliveryWords.Name, shipped.Delivery)),
    ];

    /// <summary>
    /// The ask that is no delivery: <i>Keep how it lands</i> writes no landing and no workflow choice, so a saved workflow a place follows survives
    /// a set-up. It is no row of <see cref="Table"/>, which holds the deliveries a set-up can write.
    /// </summary>
    public const string Keep = "keep";

    public const string KeepName = "Keep how it lands";

    /// <summary>Every id a place's ask may name as its preset: the deliveries, then <see cref="Keep"/>.</summary>
    public static IReadOnlyList<string> Asks { get; } = [.. Table.Select(each => each.Id), Keep];

    /// <summary>A preset by its id, or null.</summary>
    public static SetupPreset? Of(string? id) => Table.FirstOrDefault(preset => preset.Id == id);

    /// <summary>
    /// The land a workflow's steps say, Current's drawn or a version's: <c>integrate</c> where an integrate step follows the landing, a pull
    /// request where one does, else the landing's method or form, or <see cref="SetupLand.InPlace"/> where no step lands. Current never says <c>push</c>.
    /// </summary>
    public static SetupLand LandOf(IReadOnlyList<NamedStep> steps)
    {
        var landing = steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Landing);
        var accept = landing?.Field("accept") as string == SetupLand.Automatic ? SetupLand.Automatic : SetupLand.You;
        var plugin = landing?.Field("plugin") as string;
        if (steps.FirstOrDefault(step => step.Kind == NodeKinds.Integrate) is { } integrate)
        {
            return new(LandNodes.Integrate, accept, null, integrate.Field("request") as string ?? LandingIntegrate.Owner);
        }

        if (steps.FirstOrDefault(step => step.Kind == WorkflowKinds.PullRequest) is { } pullRequest)
        {
            return new(SetupLand.PullRequest, accept, pullRequest.Field("plugin") as string ?? plugin);
        }

        if (landing?.Field("method") is string method) return new(method, accept);
        if (landing is null) return new(SetupLand.InPlace, SetupLand.You);
        return landing.Field("form") as string == LandingForm.Branch
            ? new(LandNodes.Branch, accept)
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
    /// The plugin whose step opens a pull request for a remote on <paramref name="host"/>: the first contributing plugin performing steps
    /// (<see cref="HookPoints.Perform"/>) whose name, after its publisher, names the host's platform; null where none does. A choice of the
    /// pull-request delivery needs one; nothing suggests it.
    /// </summary>
    public static string? PluginFor(string? host, IWorkplacePluginCatalog catalog)
    {
        if (PlatformOf(host) is not { } platform) return null;
        return catalog.Contributing.FirstOrDefault(entry => entry.Points.Contains(HookPoints.Perform, StringComparer.Ordinal)
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
    /// The delivery preselected from what the driver already reads: the preset of a place whose delivery is chosen, driven
    /// with a landing rule set (<paramref name="chosen"/>); else <i>Integrated into the line</i> where the line's declaration declares an
    /// <c>integration</c>; else <i>Merge after you accept</i>. A pull request is never suggested: a place has one only where its
    /// workflow holds the steps.
    /// </summary>
    public static string Suggest(SetupMatch match, bool chosen, bool declaresIntegration) => Suggestion(match, chosen, declaresIntegration).Preset;

    /// <summary><see cref="Suggest"/> with the reason: a code the page words, and the driver's sentence beside it.</summary>
    public static (string Preset, SuggestionWhy Why) Suggestion(SetupMatch match, bool chosen, bool declaresIntegration) =>
        chosen && match.Preset is { } kept && kept != NotDriven ? (kept, SuggestionWhy.Of(SuggestionWhy.Kept))
        : declaresIntegration ? (Integrated, SuggestionWhy.Of(SuggestionWhy.Integration))
        : (Merge, SuggestionWhy.Of(SuggestionWhy.Default));
}
