namespace Daoris.Desktop;

/// <summary>A repository's integration; <c>Unit</c> alone answers that unit's head, else the <c>Repository</c> is read.</summary>
public sealed record IntegrationRead(string? Unit, string? Repository);

/// <summary>The owner's act on a repository's integration; each act reads the fields it names.</summary>
public sealed record IntegratePress(
    string Repository, string Act, string? Branch, string? Request, string? Candidate, string? Decide, string? Place, string? Commit,
    string? Answer, bool Landed = false);
