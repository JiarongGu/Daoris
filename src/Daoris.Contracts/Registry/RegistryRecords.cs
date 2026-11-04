namespace Daoris.Contracts.Registry;

// The repositories a host registers, as the registry's doors take and answer them.

/// <summary>What the semantic half made of the entries (D123): how many, the vectors they became, how many were longer than the window and split.</summary>
public sealed record EmbeddedResponse(int Entries, int Pieces, int Split, int Window);

/// <summary>When a repository's checkout was read and the commit it was at (null for a folder with no commit).</summary>
public sealed record ReadingResponse(string Repository, DateTimeOffset ReadAt, string? Commit);

/// <summary>What a rescan made. <c>Embedded</c> is absent when the semantic half did not run, and <c>SemanticError</c> says why.</summary>
public sealed record RefreshResponse(
    int Entries, int Repositories, int Withheld, string? SemanticError, IReadOnlyList<string> Absent,
    EmbeddedResponse? Embedded = null, IReadOnlyList<ReadingResponse>? Read = null);

/// <summary>What a repository says it is: <c>Uses</c> is what it depends on (D91); absent is nothing declared.</summary>
public sealed record DomainRequest(
    string? Summary, IReadOnlyList<string>? Owns, IReadOnlyList<string>? Accepts, IReadOnlyList<string>? Uses = null);

/// <summary>One lane's words (D115 §2.2), the same shape both ways; judged by the declaration on the way in.</summary>
public sealed record LaneWire(string? Id, string? Title, string? Summary, bool? Steward);

public sealed record RegisteredResponse(string Repository, DateTimeOffset At, string Workspace);

public sealed record RetiredResponse(string Repository, bool Retired, string Message);

/// <summary>The repositories this machine's checkouts took out of a circle, not yet told to its deployment.</summary>
public sealed record RetiredPendingResponse(string Workspace, IReadOnlyList<string> Repositories);

public sealed record ImportedResponse(string Folder, int Imported, IReadOnlyList<string> Repositories, string Message)
{
    /// <summary>The circle they were wired to, when the import named one.</summary>
    public string? Workspace { get; init; }
}

/// <summary>
/// A registered repository. <c>Addressable</c> is the exchange's own judgement (D70), answered so no page re-derives it from
/// <c>Adopted</c>; <c>Root</c> only to a caller on this machine.
/// </summary>
public sealed record RegistrationResponse(
    string Repository, bool Adopted, bool Registered, string? Summary,
    IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts, IReadOnlyList<string> Packs, int Entries,
    string? Root, bool Joined, bool SharesKnowledge, string Workspace, string? DefaultBranch, bool Addressable,
    IReadOnlyList<string> Uses, IReadOnlyList<LaneWire>? Lanes = null);
