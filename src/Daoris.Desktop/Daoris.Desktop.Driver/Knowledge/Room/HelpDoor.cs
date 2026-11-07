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
/// How Ask Daoris meets a door's act: it proposes it through a door its kind takes, only when the person asks for it, or
/// never, with why. The row's words are built from it, and <c>HelpRoomDoorAsksTests</c> holds a proposing row to the kind's own doors.
/// </summary>
internal abstract record HelpDoorAsk
{
    /// <summary>The words the row ends on, in brackets.</summary>
    public abstract string Said { get; }

    /// <summary>The proposal kind and the door it takes the act through; none where Ask Daoris never proposes it.</summary>
    public abstract (string Kind, string Door)? Through { get; }

    /// <summary>Ask Daoris proposes it through <paramref name="Kind"/>'s <paramref name="Door"/>, with what the row adds.</summary>
    public sealed record Proposes(string Kind, string Door, string? Also = null) : HelpDoorAsk
    {
        public override string Said => Also is null ? "Ask Daoris proposes it" : $"Ask Daoris proposes it, {Also}";

        public override (string Kind, string Door)? Through => (Kind, Door);
    }

    /// <summary>Ask Daoris never proposes it.</summary>
    public sealed record Never(string Why) : HelpDoorAsk
    {
        public override string Said => $"Ask Daoris never proposes it: {Why}";

        public override (string Kind, string Door)? Through => null;
    }
}
