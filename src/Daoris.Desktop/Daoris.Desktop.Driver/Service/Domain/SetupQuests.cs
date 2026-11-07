namespace Daoris.Driver;

/// <summary>
/// The set-up quests' titles: what tells a set-up's session from any other, so
/// the machine log can mark its start and the usage report can say what each set-up cost.
/// </summary>
/// <remarks>
/// <para><b>A quest carries no kind</b>, and the set-up press publishes an ordinary ask with its receiver
/// named, so its title is the one mark it has. The press composes the title from these words with
/// the day after it in brackets (<c>Set up this repository for every agent (2026-10-01)</c>), which is what
/// makes a second press on the same day the same quest. The composer (<see cref="SetupBrief"/>) takes
/// its words from here, through <see cref="Title"/>, so the two cannot drift.</para>
///
/// <para><b>Only the press's own words.</b> A title is a set-up when it is one of these exactly, or one of
/// these followed by a space and a bracket. A quest someone titled the same way by hand is a set-up in every
/// sense that matters to its cost.</para>
/// </remarks>
public static class SetupQuests
{
    /// <summary>An unadopted repository, set up whole.</summary>
    public const string SetUp = "Set up this repository for every agent";

    /// <summary>An adopted repository that declares nothing: the knowledge step alone.</summary>
    public const string Declare = "Declare and document what this repository owns";

    /// <summary>An adopter not on the agents layout, or one whose move is unfinished.</summary>
    public const string Move = "Move this repository to the agents layout";

    /// <summary>
    /// A repository set up and declaring no index of where things are: the index alone, its own quest after the set-up,
    /// so an index that fails never holds a set-up back.
    /// </summary>
    public const string Index = "Give this repository an index of where things are";

    /// <summary>
    /// An adopted repository reconciling with the canon this machine's doctrine tool carries: a case of the press, so a
    /// second is refused beside it, as beside any open set-up.
    /// </summary>
    public const string Reconcile = "Reconcile this repository with the shared doctrine";

    /// <summary>The titles the press composes, before the day.</summary>
    public static readonly IReadOnlyList<string> Titles = [SetUp, Declare, Move, Index, Reconcile];

    /// <summary>Whether a quest so titled is a set-up, the index quest among them.</summary>
    public static bool IsSetup(string? title) => Titles.Any(stem => Is(title, stem));

    /// <summary>Whether a quest so titled is the index quest: a workspace plan paces it after the set-up.</summary>
    public static bool IsIndex(string? title) => Is(title, Index);

    /// <summary>The set-up quest open or taken for <paramref name="repository"/>, the one the press refuses a second beside and an adoption is running by; null where none is.</summary>
    public static QuestView? OpenFor(IEnumerable<QuestView> quests, string repository) =>
        quests.FirstOrDefault(quest => string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase)
            && quest.Status is "Open" or "Taken" && IsSetup(quest.Title));

    private static bool Is(string? title, string stem) =>
        title is not null
        && (title.Length == stem.Length
            ? string.Equals(title, stem, StringComparison.Ordinal)
            : title.StartsWith(stem + " (", StringComparison.Ordinal));

    /// <summary>The title a press composes: one of <see cref="Titles"/>, then the day in brackets.</summary>
    /// <exception cref="ArgumentException">Words that are not one of the press's own.</exception>
    public static string Title(string stem, DateOnly day) =>
        Titles.Contains(stem, StringComparer.Ordinal)
            // Invariant: a machine whose culture keeps another calendar would otherwise write another year.
            ? $"{stem} ({day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)})"
            : throw new ArgumentException($"`{stem}` is not a set-up's title — one of {string.Join("; ", Titles)}.", nameof(stem));
}
