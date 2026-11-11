namespace Daoris.Driver;

/// <summary>
/// Why a saved workflow cannot bind a run or be drawn here: a <see cref="Code"/> and the <see cref="Values"/> it names, which the page words
/// from its catalogue in each language, beside <see cref="Said"/>, the driver's own English for a terminal.
/// </summary>
public sealed record BindingProblem(string Code, IReadOnlyDictionary<string, string> Values)
{
    /// <summary>The workflow is not saved here (<c>workflow</c>).</summary>
    public const string NotSaved = "not-saved";

    /// <summary>The workflow's file does not read (<c>workflow</c>, <c>why</c>).</summary>
    public const string Unread = "unread";

    /// <summary>The workflow keeps no version that reads (<c>workflow</c>).</summary>
    public const string NoVersion = "no-version";

    /// <summary>Its newest version does not read (<c>workflow</c>, <c>version</c>, <c>why</c>).</summary>
    public const string VersionUnread = "version-unread";

    /// <summary>A step is a plugin's pull request that no plugin here performs (<c>workflow</c>, <c>version</c>, <c>step</c>, <c>plugin</c>).</summary>
    public const string PullRequestUnserved = "pull-request-unserved";

    /// <summary>A step its department's place cannot run (<c>workflow</c>, <c>version</c>, <c>step</c>, <c>kind</c>, <c>place</c>, <c>serves</c>).</summary>
    public const string PlaceKindUnserved = "place-kind";

    private static BindingProblem Of(string code, params (string Name, string Value)[] values) =>
        new(code, values.ToDictionary(each => each.Name, each => each.Value, StringComparer.Ordinal));

    public static BindingProblem ForNotSaved(string workflow) => Of(NotSaved, ("workflow", workflow));

    public static BindingProblem ForUnread(string workflow, string why) => Of(Unread, ("workflow", workflow), ("why", why));

    public static BindingProblem ForNoVersion(string workflow) => Of(NoVersion, ("workflow", workflow));

    public static BindingProblem ForVersionUnread(string workflow, int version, string why) =>
        Of(VersionUnread, ("workflow", workflow), ("version", version.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("why", why));

    public static BindingProblem ForUnserved(string workflow, int version, string step, string plugin) =>
        Of(PullRequestUnserved, ("workflow", workflow), ("version", version.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("step", step), ("plugin", plugin));

    public static BindingProblem ForPlaceKind(string workflow, int version, string step, string kind, string place, IReadOnlyList<string> serves) =>
        Of(PlaceKindUnserved, ("workflow", workflow), ("version", version.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("step", step),
            ("kind", kind), ("place", place), ("serves", string.Join(',', serves)));

    /// <summary>The driver's English for the problem.</summary>
    public string Said => Code switch
    {
        PlaceKindUnserved => $"`{Values["workflow"]}` v{Values["version"]}'s step `{Values["step"]}` is a `{Values["kind"]}`, which "
            + $"{PlaceKind.Said(Values["place"])} cannot run: it runs in {string.Join(" or ", Values["serves"].Split(',').Select(PlaceKind.Said))} alone.",
        NotSaved => $"no workflow `{Values["workflow"]}` is saved here.",
        Unread => $"`{Values["workflow"]}` cannot be read: {Values["why"]}",
        NoVersion => $"`{Values["workflow"]}` keeps no version that reads here.",
        VersionUnread => $"`{Values["workflow"]}` v{Values["version"]} cannot be read here: {Values["why"]}",
        PullRequestUnserved => $"`{Values["workflow"]}` v{Values["version"]}'s step `{Values["step"]}` is a pull request of plugin `{Values["plugin"]}`, which is not installed here, "
            + $"is switched off, contributes nothing or performs no `{HookPoints.Perform}` steps, so no run starts on it — `daoris plugin list` says what there is.",
        _ => Code,
    };
}
