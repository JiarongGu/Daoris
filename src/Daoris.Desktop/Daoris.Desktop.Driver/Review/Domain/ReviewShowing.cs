namespace Daoris.Driver;

/// <summary>
/// What the shell serves to a set-up step's tab (REVIEWENV1d, D154 point 5; the review environment design §2.3 step 3): a
/// build's folder in the step's tree, at the rule's address only, under the build's own base, until the person's verdict.
/// </summary>
/// <param name="Quest">The set-up step whose tab it is.</param>
/// <param name="Title">The tab's title before its session navigates it (<see cref="ReviewDesk.TabTitle"/>).</param>
/// <param name="Folder">The build's folder: a full path in the step's tree, reached by no link.</param>
/// <param name="Address">The rule's address, an origin with no last slash: the one place it is served at.</param>
/// <param name="Base">The path under <paramref name="Address"/> it is served at, from <c>/</c>, ending in <c>/</c>.</param>
/// <param name="Look">Where the tab is taken once served: under <paramref name="Address"/> and <paramref name="Base"/>.</param>
public sealed record ReviewServe(string Quest, string Title, string Folder, string Address, string Base, string Look)
{
    /// <summary>The set-up it shows, as <c>machine/sequence</c>, which a verdict names; null where the set-up carried none.</summary>
    public string? SetUp { get; init; }

    /// <summary>
    /// The one pattern the tab's requests are held at, in CDP's <c>Fetch</c> wildcard: the address and the base, anything below.
    /// A request outside it, the app's calls to the development environment's data among them, goes where it would go for the
    /// person (design §2.3 step 3, measured in <c>docs/2026-10-09-review-serving-evidence.md</c>).
    /// </summary>
    public string Pattern => Address + Base + "*";

    /// <summary>
    /// Every pattern held: <see cref="Pattern"/>, and under a base of its own the base without its last slash too, since
    /// <c>/v3</c> is the same page as <c>/v3/</c> and would otherwise reach the person's server.
    /// </summary>
    public IReadOnlyList<string> Patterns => Base == "/" ? [Pattern] : [Pattern, Address + Base.TrimEnd('/')];
}

/// <summary>
/// A set-up waiting for the person on this machine (REVIEWENV1d; design §3.3), as the strip's chip says it: the step, what it
/// showed and where, and whether Daoris serves its tab now.
/// </summary>
public sealed record ReviewWaiting(string Quest, string Title, string Environment, string? Look, string? Shows, bool Served);
