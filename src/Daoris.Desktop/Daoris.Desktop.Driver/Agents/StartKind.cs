namespace Daoris.Driver;

/// <summary>What a start is, for the walk: driven work never starts on a kept account.</summary>
public enum StartKind
{
    /// <summary>A driven start, a carry-on, a resume, an intake or a set-up.</summary>
    Driven,

    /// <summary>A conversation or Ask Daoris's opening.</summary>
    Conversation,
}
