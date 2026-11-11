using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The place an Ask Daoris press put to the person on a card named, whether it lay beyond its conversation's focus, and that focus,
/// each spelled as the page reads a place: the card's line <i>In Work, outside this conversation's focus (Games)</i>
/// (`docs/2026-11-11-ask-scope-ux-design.md` §4), from the judgement the receipt and Notifications use (<see cref="HelpPressPlace"/>).
/// </summary>
public sealed record HelpCardPlace(IReadOnlyDictionary<string, string> Place, bool Beyond, IReadOnlyDictionary<string, string>? Focus)
{
    /// <summary>The card's place from the press's; null where the press named none.</summary>
    public static HelpCardPlace? Of(HelpPressPlace? placed) =>
        placed is null ? null : new(placed.Place.Wire(), placed.Beyond, placed.Focus?.Wire());
}
