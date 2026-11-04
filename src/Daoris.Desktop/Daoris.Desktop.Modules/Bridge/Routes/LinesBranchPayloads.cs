namespace Daoris.Desktop;

/// <summary>
/// A session's or a branch's removal. <c>Named</c> is the one name <c>trees remove</c> takes, with <c>Repository</c> only where it must
/// say which; the page's <c>Repository</c> and <c>Branch</c> are read with no <c>Named</c>.
/// </summary>
public sealed record DiscardBranchPress(string? Named, string? Repository, string? Branch, bool Force = false);

/// <summary>A landing branch withdrawn, its commits kept on a recovery ref; <c>Plan</c> changes nothing.</summary>
public sealed record WithdrawLandingBranchPress(string Repository, string Branch, string? Why, bool Plan = false);

/// <summary>A held attempt of a node worded with its <c>Resolution</c>.</summary>
public sealed record WorkHeldPress(string Unit, string Node, int Attempt, string Resolution);
