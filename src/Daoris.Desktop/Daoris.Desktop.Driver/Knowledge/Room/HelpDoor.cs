namespace Daoris.Driver;

/// <summary>A row of the doors: the act, where it is on the screen, its terminal twin, and how Ask Daoris meets it, where the row says.</summary>
/// <param name="Act">The act in the person's words, saying nothing of Ask Daoris.</param>
internal sealed record HelpDoor(string Act, string Screen, string Terminal, HelpDoorAsk? Ask = null)
{
    /// <summary>The row's first cell: the act, then how Ask Daoris meets it.</summary>
    public string To => Ask is null ? Act : $"{Act} ({Ask.Said})";

    /// <summary>A row that says nothing of Ask Daoris, written as the table's three cells.</summary>
    public static implicit operator HelpDoor((string Act, string Screen, string Terminal) row) => new(row.Act, row.Screen, row.Terminal);
}

/// <summary>
/// Where Ask Daoris never asks a door's act on the person's card, with why; a row with none is one it presses, asked on the card.
/// The row's words are built from it.
/// </summary>
internal abstract record HelpDoorAsk
{
    public abstract string Said { get; }

    public sealed record Never(string Why) : HelpDoorAsk
    {
        public override string Said => $"Ask Daoris never asks it on the card: {Why}";
    }
}
