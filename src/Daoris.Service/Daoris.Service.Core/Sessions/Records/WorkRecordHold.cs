namespace Daoris.Knowledge;

/// <summary>
/// Which hold kept a finished tree's commits as its branch went (the keep-size design §1.1, §3.3): <c>line</c>, <c>landing</c>,
/// <c>squash</c>, <c>content</c>, <c>carried</c> or <c>empty</c>; where (the branch or ref named), and in which form.
/// </summary>
public sealed record WorkRecordHold(string Kind, string? Where, string? Form);
