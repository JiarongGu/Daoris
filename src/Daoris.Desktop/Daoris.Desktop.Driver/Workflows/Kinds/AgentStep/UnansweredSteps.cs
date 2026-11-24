namespace Daoris.Driver;

/// <summary>
/// A step an agent performs (it declares a <c>goal</c>) whose session ended without calling <c>step_answer</c> (<see cref="StepAnswer.NoAnswer"/>)
/// is no question for the person: its failure is transient by this rule, so its node is asked again by itself as a new attempt, a few times on
/// one binding (<see cref="Bound"/>, or the step's own <c>retry</c> where that allows more). Past them Daoris gives it up as said: its node is
/// stopped by Daoris with the reason (<see cref="GaveUp"/>), which ends its run and holds no row for anyone. A session refused for its sign-in
/// (<see cref="StepAnswer.SignIn"/>) is the person's to mend and is not asked again here.
/// </summary>
public static class UnansweredSteps
{
    /// <summary>The asks an unanswered step takes on one binding before Daoris gives it up.</summary>
    public const int Bound = 3;

    /// <summary>The code of a step given up as said.</summary>
    public const string GaveUp = "unsaid";

    /// <summary>Whether <paramref name="code"/> on <paramref name="node"/> is a session that ended without an answer.</summary>
    public static bool Is(NodeDeclaration node, string? code) => node.Goal is not null && code == StepAnswer.NoAnswer;

    /// <summary>The attempts <paramref name="node"/> takes on one binding after a failure with <paramref name="code"/>: the step's own bound, or <see cref="Bound"/> where it ended unanswered.</summary>
    public static int Attempts(NodeDeclaration node, string? code) =>
        Is(node, code) ? Math.Max(Bound, node.Retry?.MaxAttempts ?? 1) : node.Retry?.MaxAttempts ?? 1;

    /// <summary>The words its node is stopped with once its asks are spent: why, and that Daoris gave it up.</summary>
    public static string Words(NodeDeclaration node, int attempts, string? why) =>
        $"the step's session ended without answering `{node.Id}` after {attempts} asks, so Daoris gave it up"
        + (string.IsNullOrWhiteSpace(why) ? "" : $": {why.Trim().ReplaceLineEndings(" ")}");
}
