namespace Daoris.Driver;

/// <summary>
/// How a step's session's record ends once its run exits, by the step's answer: <c>completed</c> where it answered the step reached, its
/// note the evidence's message; <c>failed</c> where it answered failed, its turn failed, or it ended unanswered (<see cref="StepAnswer.NoAnswer"/>).
/// </summary>
public static class StepConclusion
{
    public static SessionConclusion Of(StepAgentAsk ask, WorkReconcileAnswer? answer, string? turnFailed)
    {
        if (answer?.Outcome is { State: "reached" } reached)
        {
            var said = reached.Evidence?.Message ?? reached.Evidence?.Url ?? "";
            return SessionConclusion.Of("completed", Noted.Of(
                EndNoteCodes.StepReached, $"It reached the step `{ask.Step}`: {said}", ("step", ask.Step), ("message", said)));
        }

        if (answer?.Outcome is { } failed && failed.Code != StepAnswer.NoAnswer)
        {
            return Failed(ask, failed.Code ?? "failed", failed.Evidence?.Message ?? "");
        }

        return turnFailed is { Length: > 0 } turn ? Failed(ask, "turn-failed", turn) : Failed(ask, StepAnswer.NoAnswer, "it ended without answering the step.");
    }

    private static SessionConclusion Failed(StepAgentAsk ask, string code, string message) =>
        SessionConclusion.Of("failed", Noted.Of(
            EndNoteCodes.StepFailed, $"The step `{ask.Step}` failed ({code}): {message}", ("step", ask.Step), ("code", code), ("message", message)));
}
