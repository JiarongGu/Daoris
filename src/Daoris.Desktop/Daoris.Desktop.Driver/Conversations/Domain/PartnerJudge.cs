namespace Daoris.Driver;

/// <summary>
/// What only the ask and its lead's record can say of a piece (COAGENT1b; the co-agent design §2.3 step 1): pure, so each refusal is a
/// fast test. The rule, the choice and the reservation are judged after it.
/// </summary>
public static class PartnerJudge
{
    /// <summary>
    /// Why the piece is refused before anything is chosen or reserved, by its code; null where it may go on. The lead is a working
    /// session of this machine's: no partner, helper or reviewer (each carries <c>serves</c> or <c>opinion</c>), no intake, no Ask Daoris
    /// conversation and no brainstorm pass, with a tree to grow from; the piece is not blank and within its bound; each path stays inside
    /// the repository; the base names a commit.
    /// </summary>
    public static string? Refusal(PartnerAsk ask, PriorSession? lead)
    {
        if (lead is null) return PartnerWhy.AskerGone;
        if (lead.Serves is not null || lead.Opinion is not null || lead.Ask is { Length: > 0 } || lead.HelpMode is { Length: > 0 }
            || lead.Tree is not { Length: > 0 })
        {
            return PartnerWhy.AskerUnfit;
        }

        if (string.IsNullOrWhiteSpace(ask.Piece) || ask.Piece.Length > PartnerAsk.PieceLimit) return PartnerWhy.Blank;
        if (ask.Paths.Any(path => !Inside(path))) return PartnerWhy.PathOutside;
        if (string.IsNullOrWhiteSpace(ask.Base) || !ask.Base.All(Uri.IsHexDigit)) return PartnerWhy.NoCommit;
        return null;
    }

    /// <summary>A repository-relative path that stays inside: no root, no drive, no <c>..</c> step.</summary>
    public static bool Inside(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var trimmed = path.Trim().Replace('\\', '/');
        if (trimmed.StartsWith('/') || trimmed.Contains(':')) return false;
        return trimmed.Split('/').All(step => step is not "..");
    }
}
