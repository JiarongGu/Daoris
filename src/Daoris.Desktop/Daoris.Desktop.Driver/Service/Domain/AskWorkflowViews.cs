namespace Daoris.Driver;

/// <summary>
/// The person's kind and workflow on an ask, with when and their words: the
/// latest stands, and one naming neither is a choice cleared.
/// </summary>
/// <param name="Kind">A kind's id its workspace declares, or null for none.</param>
/// <param name="Workflow">A workflow's id, <c>current</c>, or null for none.</param>
public sealed record AskWorkflowChoiceView(string? Kind, string? Workflow, DateTimeOffset At, string? Words = null);

/// <summary>
/// An intake's kind proposal on an ask: a kind with the person's words as its reason, which
/// only the person's press applies, the intake session that made it and the quest published with it.
/// </summary>
public sealed record AskWorkflowProposalView(string Kind, string Reason, DateTimeOffset At, string? Session = null, string? Quest = null);
