namespace Daoris.Driver;

/// <summary>
/// The set-up quests' titles (D117 §6.2, D124 §2.1–§2.2): what tells a set-up's session from any other, so
/// the machine log can mark its start and the usage report can say what each set-up cost (WSSETUP11, D124
/// §7.3).
/// </summary>
/// <remarks>
/// <para><b>A quest carries no kind</b>, and the set-up press publishes an ordinary ask with its receiver
/// named (D117 §6.1), so its title is the one mark it has. The press composes the title from these words with
/// the day after it in brackets (<c>Set up this repository for every agent (2026-10-01)</c>), which is what
/// makes a second press on the same day the same quest. The composer (LAYOUT7, <see cref="SetupBrief"/>) takes
/// its words from here, through <see cref="Title"/>, so the two cannot drift.</para>
///
/// <para><b>Only the press's own words.</b> A title is a set-up when it is one of these exactly, or one of
/// these followed by a space and a bracket. A quest someone titled the same way by hand is a set-up in every
/// sense that matters to its cost.</para>
/// </remarks>
public static class SetupQuests
{
    /// <summary>An unadopted repository, set up whole (D124 §2.2).</summary>
    public const string SetUp = "Set up this repository for every agent";

    /// <summary>An adopted repository that declares nothing: the knowledge step alone (D124 §2.1).</summary>
    public const string Declare = "Declare and document what this repository owns";

    /// <summary>An adopter on the old layout, or one whose move is unfinished (D117 §6.2).</summary>
    public const string Move = "Move this repository to the agents layout";

    /// <summary>The titles the press composes, before the day.</summary>
    public static readonly IReadOnlyList<string> Titles = [SetUp, Declare, Move];

    /// <summary>Whether a quest so titled is a set-up.</summary>
    public static bool IsSetup(string? title) =>
        title is not null
        && Titles.Any(stem => title.Length == stem.Length
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
