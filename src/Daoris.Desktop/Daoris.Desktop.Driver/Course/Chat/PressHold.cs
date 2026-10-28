namespace Daoris.Driver;

/// <summary>What reserving a conversation for the person's press came to (CHATFLOW1d, <c>ChatTurns.PressAsync</c>).</summary>
public enum PressHold
{
    /// <summary>Reserved: no turn starts until the press answers.</summary>
    Held,

    /// <summary>A turn is taking, opening or waiting, or another press holds it: the person presses again once it ends.</summary>
    Busy,

    /// <summary>The conversation is finishing, held for a hand-over's take, or gone.</summary>
    Ending,

    /// <summary>No runner in this process holds the conversation.</summary>
    NotHere,
}
