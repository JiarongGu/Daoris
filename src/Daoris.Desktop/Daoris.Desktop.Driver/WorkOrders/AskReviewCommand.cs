namespace Daoris.Driver;

/// <summary>
/// The review a new ask carries from a terminal: the choice sent, null where none is (<c>rule</c>, or nothing said),
/// and the person's words with it.
/// </summary>
public sealed record AskReviewComposed(string? Choice, string? Words);

/// <summary>The request of <c>daoris ask review</c>: the ask, the choice, and the person's words.</summary>
/// <param name="Choice"><c>off</c>, <c>on</c> or an environment's name, as the service spells it.</param>
public sealed record AskReviewAsk(string Ask, string Choice, string? Words);

/// <summary>What the ask's review doors reach: the service, and this machine's driver config, read only to judge a named environment.</summary>
public sealed record AskReviewWorld(ServiceClient Service, Func<DriverConfig> Config);

/// <summary>
/// The ask's review choice at a terminal, the twin of the
/// ask composer's and the ask page's:
/// <c>daoris ask … --review rule|on|&lt;environment&gt;|off [--review-words "…"]</c> with a new ask, and
/// <c>daoris ask review &lt;id&gt; on|&lt;environment&gt;|off ["…"]</c> on one, which applies an intake's proposal too.
/// </summary>
/// <remarks>
/// <para><b>The doors the page presses</b>: a new ask's <c>POST /api/asks</c> with <c>review</c> and <c>reviewWords</c>, and the ask's
/// <c>POST /api/asks/{id}/review</c>. The service's sentence is printed whole, a refusal included, never re-worded.</para>
///
/// <para><b>A named environment is one the ask's workspace declares</b>: its own rule's, or a rule of a repository in it, as the
/// page offers only those. The service holds no rule, so this door reads it; <c>on</c> and <c>off</c> need none.</para>
///
/// <para><b>Not the design's <c>ask --review &lt;id&gt;</c></b> (§1.5): an ask's id is six hex characters, which an environment's
/// name may also be, so <c>--review &lt;id&gt;</c> could not be told from a new ask's <c>--review &lt;environment&gt;</c>.</para>
///
/// <para><b>Exit codes</b>: 0 kept, 1 refused (an environment not declared, a service refusal), 2 the usage.</para>
/// </remarks>
public static class AskReviewCommand
{
    /// <summary>The composer's default, <i>As each repository's rule says</i>: a new ask sends no choice.</summary>
    public const string Rule = "rule";

    private const string Off = "off";
    private const string On = "on";

    /// <summary>
    /// A new ask's <c>--review</c> and <c>--review-words</c> read as the composer sends them, or null with why not: <c>rule</c> and
    /// nothing said send none; words go only with a choice.
    /// </summary>
    public static AskReviewComposed? Compose(string? review, string? words, out string? problem)
    {
        problem = null;
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        if (review is null or Rule)
        {
            if (said is null) return new AskReviewComposed(null, null);
            problem = "--review-words go with a review choice: --review on|<environment>|off.";
            return null;
        }

        if (Unfit(review, "`rule`, `on`, an environment's name or `off`") is { } unfit)
        {
            problem = unfit;
            return null;
        }

        return new AskReviewComposed(review, said);
    }

    /// <summary>An ask's <c>--set-review &lt;id&gt;</c> and the words after it read, or null with why not: the choice, then the person's words.</summary>
    public static AskReviewAsk? Read(string id, IReadOnlyList<string> words, out string? problem)
    {
        problem = null;
        if (id.TrimStart('#').Length == 0 || id.StartsWith('-') || words is not [var choice, ..])
        {
            problem = "--set-review takes one ask's id, then on, an environment's name or off, then your words.";
            return null;
        }

        if (choice == Rule)
        {
            problem = "`rule` is a new ask's: once an ask has a choice, the latest stands, so choose `on`, an environment or `off`.";
            return null;
        }

        if (Unfit(choice, "`on`, an environment's name or `off`") is { } unfit)
        {
            problem = unfit;
            return null;
        }

        var said = string.Join(" ", words.Skip(1)).Trim();
        return new AskReviewAsk(id.TrimStart('#'), choice, said.Length == 0 ? null : said);
    }

    /// <summary>
    /// Why a named environment cannot be chosen for an ask in <paramref name="workspace"/>, or null where it can (design §1.4): one its
    /// own rule or a rule of a repository in it declares, as the page offers. <c>on</c> and <c>off</c> read nothing.
    /// </summary>
    public static async Task<string?> UndeclaredAsync(string choice, string workspace, AskReviewWorld world, CancellationToken ct = default) =>
        choice is On or Off
            ? null
            : Undeclared(choice, workspace, world.Config(),
                (await world.Service.RegistryAsync(ct).ConfigureAwait(false)).Select(row => (row.Repository, (string?)row.Workspace)));

    /// <summary>The same, judged against <paramref name="config"/> and the registry's rows as a press's view reads them.</summary>
    public static string? Undeclared(string choice, string workspace, DriverConfig config, IEnumerable<(string Repository, string? Workspace)> registry)
    {
        if (choice is On or Off) return null;

        var circle = RemoteTarget.Workspace(workspace);
        var repositories = registry
            .Where(row => string.Equals(row.Workspace, circle, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.Repository)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = config.WorkspaceReviews.Where(pair => string.Equals(pair.Key, circle, StringComparison.OrdinalIgnoreCase))
            .Concat(config.Reviews.Where(pair => repositories.Contains(pair.Key)))
            .Select(pair => pair.Value);
        // In the rules' own order, the workspace's first, so a rule's default is named before what follows it.
        var declared = rules.SelectMany(rule => rule.Environments.Select(environment => environment.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (declared.Contains(choice)) return null;
        return declared.Count == 0
            ? $"No review environment is declared in workspace `{circle}`, so nothing of its work can be shown in `{choice}`: "
              + $"`daoris place review <repository>|--workspace {circle} {choice} --kind local|deployed --procedure <path>` declares one. "
              + "Nothing was kept."
            : $"No review environment `{choice}` is declared in workspace `{circle}`: its rules declare {ReviewDoorWords.Either(declared)}. "
              + "Nothing was kept.";
    }

    public static Task<int> PressAsync(AskReviewAsk ask, AskReviewWorld world, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.AskReview, InstanceDoor.Said(("ask", ask.Ask), ("choice", ask.Choice), ("words", ask.Words)),
            () => RunAsync(ask, world, output, ct), output, ct);

    /// <summary>Set the choice on the ask, as its page's <i>Set</i> does, and print the service's sentence.</summary>
    public static async Task<int> RunAsync(AskReviewAsk ask, AskReviewWorld world, TextWriter output, CancellationToken ct = default)
    {
        // A named environment is judged in the ask's workspace; an ask the service does not hold is its to refuse.
        if (ask.Choice is not (On or Off) && await world.Service.FindAskAsync(ask.Ask, ct).ConfigureAwait(false) is { } held
            && await UndeclaredAsync(ask.Choice, held.Workspace, world, ct).ConfigureAwait(false) is { } undeclared)
        {
            output.WriteLine(undeclared);
            return 1;
        }

        var answer = await world.Service.ChooseAskReviewAsync(ask.Ask, ask.Choice, ask.Words, ct).ConfigureAwait(false);
        output.WriteLine(answer.Message);
        return answer.Ok ? 0 : 1;
    }

    /// <summary>Why a choice is not one, or null: <c>on</c>, <c>off</c>, or a name a rule could declare.</summary>
    private static string? Unfit(string choice, string choices) =>
        choice is On or Off ? null
        : ReviewKey.NameProblem(choice) is { } unnamed
            ? $"`{(choice.Length == 0 ? "--review" : choice)}` is not a review choice: {choices}, and {unnamed}"
            : null;
}
