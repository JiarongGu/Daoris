namespace Daoris.Knowledge;

/// <summary>
/// A knowledge index a newer Daoris wrote, which this build neither reads nor rebuilds (KSCHEMA1): what every operation
/// on that index throws, and what every door answers.
/// </summary>
/// <remarks>
/// <para><b>Only the index is refused.</b> Every host on a machine opens the one file, and a connector an update left
/// behind is an older build. One that rebuilt a newer index broke the newer host serving it. One that refused the whole
/// store would leave an install an update rolled back (D139) with no host at all until a person updated it, while the
/// store's quests, sessions, asks and keys are not derived and an older build works with them (D36's KSCHEMA1 note). So
/// the store opens, drops and stamps nothing, and each way into the index throws this.</para>
///
/// <para><b>Its message names no path</b>, because a door answers it: a browser and a shared host's keyed caller are
/// never told one (D46, D47 §4). <see cref="Store"/> and <see cref="Build"/> are for this machine's own stderr.</para>
/// </remarks>
public sealed class NewerIndexException(string store, int found, int known) : Exception(Sentence(found, known))
{
    /// <summary>The file, as the connection names it.</summary>
    public string Store { get; } = store;

    /// <summary>The schema version the index carries.</summary>
    public int Found { get; } = found;

    /// <summary>The schema version this build writes.</summary>
    public int Known { get; } = known;

    /// <summary>The folder this build runs from: the one to update.</summary>
    public string Build { get; } = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    /// <summary>The same refusal, new for each operation it refuses: one exception is never thrown twice.</summary>
    internal NewerIndexException Again() => new(Store, Found, Known);

    private static string Sentence(int found, int known) =>
        $"The knowledge index here was written by a newer Daoris: its schema is version {found}, and this build's is "
        + $"{known}. This build leaves it as it is and answers nothing from it; quests, sessions and asks work as before. "
        + "Update this Daoris to the one that wrote the index.";
}
