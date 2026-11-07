namespace Daoris.Driver;

/// <summary>What one read came to: a verdict, or why nothing was read. Unread is never found, and says why.</summary>
public sealed record EvidenceReading(EvidenceVerdict? Verdict, string? Unread);

/// <summary>Where a commit stands to the done's on its history: the same, after it, elsewhere, or not known here.</summary>
public enum EvidencePlace
{
    Same,
    After,
    Elsewhere,
    Unknown,
}
