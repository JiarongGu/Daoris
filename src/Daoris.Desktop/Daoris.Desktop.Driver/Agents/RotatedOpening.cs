namespace Daoris.Driver;

/// <summary>
/// What a start says once its record is open: its record's first line,
/// naming the account it opened on and why, and <c>account.rotated</c> in the machine log when it ran somewhere other than
/// where its scope begins. One home for a driven start, a carry-on, an intake, a conversation and Ask Daoris's opening, so
/// none says it differently.
/// </summary>
public static class RotatedOpening
{
    /// <summary>The cut-off session a rotated carry-on carries on, with its refused turn and context after a limit.</summary>
    /// <param name="Session">The cut-off session's record.</param>
    /// <param name="Turn">Its refused turn (the turns its record ended, plus one), where a limit cut it off; else null.</param>
    /// <param name="Used">Its context at its high-water, where a limit cut it off and its door reported one; else null.</param>
    public sealed record Carried(string Session, long? Turn, long? Used);

    /// <summary>
    /// Say it: under the goal, every start whose list offered a choice names the step that chose its account, then what each
    /// account of its list said about its windows, or that none has said what it has left; under <c>order</c>, a
    /// start that rotated says why, and what each account said where one has; otherwise nothing. The
    /// record's failure costs its line, never the start.
    /// </summary>
    internal static void Say(
        ServiceClient service, ISessionEvents events, string sessionId, string adapter, HarnessSelection selection, Carried? carried)
    {
        if (selection.Profile is not { } to || Line(selection, carried) is not { } line) return;

        // The conversation record is the machine's (its events never sync and never cross HTTP), so this line may name
        // both accounts, in its English and in its parts' values; the note, which travels, never does.
        events.Keep(
            sessionId,
            new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null },
            say: null);
        if (selection.Rotated is { } moved)
        {
            service.AccountSaid(AccountLine.Rotated(
                sessionId, adapter, moved.From, to, carried?.Session, moved.Step, moved.Scope, said: selection.SaidLine is not null,
                fromSaid: moved.FromSaid, toSaid: moved.ToSaid));
        }
    }

    /// <summary>
    /// The record's first line: its English, and its
    /// lines by code where the walk handed its steps by code (<see cref="HarnessSelection.Steps"/>): the account it opened on
    /// with the first step's reason, the rest's step and a refused turn riding inside it, then what each account said. A line
    /// whose steps are not the ones its English says is kept with no parts, so the page draws the English whole rather than
    /// half of it worded. Null where the start says nothing.
    /// </summary>
    internal static Noted? Line(HarnessSelection selection, Carried? carried)
    {
        if (selection.Profile is not { } to) return null;

        string clause;
        IReadOnlyList<StepSaid>? steps;
        Noted? said;
        if (selection.Choice is { } choice)
        {
            (clause, steps) = (choice.Clause, selection.Steps);
            said = selection.AccountsSaid ?? Noted.Of(StartNoteCodes.OpeningUnsaid, RotationWords.Unsaid);
        }
        else if (selection.Rotated is { } rotated)
        {
            // Under `order` the line says the step that moved it off where its scope begins, never the rest's.
            (clause, steps) = (rotated.Why, selection.Steps is { Count: > 0 } all ? [all[0]] : null);
            said = selection.AccountsSaid;
        }
        else
        {
            return null;
        }

        var english = carried is { } cut
            ? RotationWords.CarriedOn(cut.Session, to, clause, cut.Turn, cut.Used)
            : RotationWords.Opened(to, clause);
        if (steps is not { Count: > 0 } || steps.Any(step => step.Why is null) || RotationWords.Text(steps) != clause)
        {
            return new Noted(said is null ? english : english + " " + said.Note, []);
        }

        (string Name, object? Value)[] lead = [("account", to), ("why", steps[0].Why), .. steps[0].Values];
        var line = carried is { } from
            ? Noted.Of(StartNoteCodes.OpeningCarriedOn, english, [("session", from.Session), .. lead])
            : Noted.Of(StartNoteCodes.OpeningOpened, english, lead);
        if (steps.Count > 1)
        {
            line = line.Also(StartNoteCodes.OpeningRest.Part(RotationWords.Rest(steps[1].Text), [("why", steps[1].Why), .. steps[1].Values]));
        }

        if (carried?.Turn is { } turn)
        {
            line = line.Also(carried.Used is { } used
                ? StartNoteCodes.OpeningTurnContext.Part(RotationWords.Refused(turn, used), ("turn", turn), ("tokens", used))
                : StartNoteCodes.OpeningTurnRefused.Part(RotationWords.Refused(turn, null), ("turn", turn)));
        }

        return said is null ? line : line.Then(" ", said);
    }

    /// <summary>
    /// A session's context at its high-water: what this machine's usage record kept of it, else the highest its
    /// own record's usage reports reached; null where neither says, never zero.
    /// </summary>
    internal static long? ContextOf(SessionUsage? usage, ISessionEventHistory events, string session)
    {
        if (usage?.Of(session) is { } kept) return kept.Used;
        if (!SessionEventText.IsId(session)) return null;
        try
        {
            return events.ReadAll(session).Where(e => e.Kind == SessionEventKind.Usage).Max(e => e.Used);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }
}
