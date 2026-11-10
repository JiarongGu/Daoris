namespace Daoris.Desktop;

/// <summary>
/// A press of Ask Daoris's that no line waits on any more, said in its conversation once it settles, as Daoris's word. A press of
/// any other asker is none of its business.
/// </summary>
public static class SettledSaid
{
    /// <summary>The pipe's <see cref="PipeConfirmations.Settled"/> sink over a telling of Daoris's word into a conversation.</summary>
    public static Action<PipeConfirmation, PipeEnding> Of(Func<Func<string, string, string, bool>?> tell, PressedEnds? ends = null) => (shown, ending) =>
    {
        if (!shown.Asker.Help || shown.Asker.Session is not { } session) return;
        if (ending.End == PipeConfirmationEnd.Confirmed) ends?.Wait(shown);
        if (Word(shown, ending.End) is { } word) tell()?.Invoke(session, word, $"press-settled/{shown.Id}");
    };

    private static string? Word(PipeConfirmation shown, PipeConfirmationEnd end)
    {
        var command = shown.View?.Command ?? $"{shown.Module} {shown.Route}";
        return end switch
        {
            PipeConfirmationEnd.Confirmed => $"Daoris: the person confirmed `{command}` and it ran; `daoris press result` reads what it answered.",
            PipeConfirmationEnd.Refused => $"Daoris: the person refused `{command}`; nothing ran.",
            PipeConfirmationEnd.Changed => $"Daoris: what `{command}` rested on changed before the person confirmed it, so nothing ran.",
            PipeConfirmationEnd.Lost => $"Daoris: `{command}` was lost as Daoris closed; nothing ran.",
            _ => null,
        };
    }
}
