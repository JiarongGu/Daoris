namespace Daoris.Driver;

/// <summary>
/// The three words a kept piece comes to (COAGENT1b, the design §7.2) and how the piece's ref reads: pure vocabulary, which
/// <see cref="PartnerStates"/> keeps under its own names for the readers above the workplace, the way <see cref="PartnerWhy"/> is Events'.
/// </summary>
internal static class PieceKeptStates
{
    /// <summary>The import was refused: the copy is kept for the person's look and <i>Discard…</i>.</summary>
    internal const string Held = "held";

    /// <summary>The keeper's commit is in the repository, frozen as <see cref="PieceRefs.Of"/>.</summary>
    internal const string Imported = "imported";

    /// <summary>Daoris cannot know what happened (a restart with the job unknown, an unreadable state): the copy is kept untouched for the person.</summary>
    internal const string Uncertain = "uncertain";

    /// <summary>
    /// How the create-only import reads the piece's ref (the design §7.2's import row): absent, it is created; at the keeper's
    /// commit, the import already happened and is <see cref="Imported"/>; at another commit, <see cref="Uncertain"/>.
    /// </summary>
    internal static string? AfterRef(string? refCommit, string kept) =>
        refCommit is null ? null
        : string.Equals(refCommit, kept, StringComparison.OrdinalIgnoreCase) ? Imported
        : Uncertain;
}
