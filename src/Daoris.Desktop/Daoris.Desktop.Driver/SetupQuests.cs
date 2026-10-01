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
/// makes a second press on the same day the same quest. The composer (LAYOUT7) takes its words from here, so
/// the two cannot drift.</para>
///
/// <para><b>Only the press's own words.</b> A title is a set-up when it is one of these exactly, or one of
/// these followed by a space and a bracket. A quest someone titled the same way by hand is a set-up in every
/// sense that matters to its cost.</para>
/// </remarks>
public static class SetupQuests
{
    /// <summary>The titles the press composes, before the day.</summary>
    public static readonly IReadOnlyList<string> Titles =
    [
        // An unadopted repository, set up whole (D124 §2.2).
        "Set up this repository for every agent",
        // An adopted repository that declares nothing: the knowledge step alone (D124 §2.1).
        "Declare and document what this repository owns",
        // An adopter on the old layout (D117 §6.2).
        "Move this repository to the agents layout",
    ];

    /// <summary>Whether a quest so titled is a set-up.</summary>
    public static bool IsSetup(string? title) =>
        title is not null
        && Titles.Any(stem => title.Length == stem.Length
            ? string.Equals(title, stem, StringComparison.Ordinal)
            : title.StartsWith(stem + " (", StringComparison.Ordinal));
}
