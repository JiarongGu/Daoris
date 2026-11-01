namespace Daoris.Driver;

/// <summary>
/// A line's epoch (LANDNODE1g; the land node design §5): the resolved shared policy a request was admitted under, plus the blob of the
/// declaration the owner had accepted then. The unit's own <c>request</c> and <c>conflicts</c> are left out, since they say who asked and where a
/// conflict goes, never how the line is integrated. Requests and candidates keep it as one text; two units batch only where their texts are equal.
/// A record written before epochs has none, and reads as its own epoch: it batches alone and promotes under the configuration as it stands.
/// </summary>
public static class IntegrationEpoch
{
    private const string Format = "1";

    private const int Fields = 10;

    private const int BlobAt = 8;

    /// <summary>The epoch text of <paramref name="policy"/> and the accepted declaration's <paramref name="blob"/>; no blob is an empty field.</summary>
    public static string Of(LandingIntegrate policy, string? blob) =>
        string.Join('|', Format, policy.Commit, policy.Batch, policy.Order, policy.Gates, policy.Unreached, policy.Reached, policy.Overlap, blob ?? "", policy.Line ?? "");

    /// <summary>
    /// The accepted declaration's blob the epoch froze; null where the epoch is none (an older record), or where nothing was accepted when the
    /// request was admitted, so the owner's later acceptance is the one that runs.
    /// </summary>
    public static string? BlobOf(string? epoch)
    {
        if (epoch is null) return null;
        var parts = epoch.Split('|', Fields);
        return parts.Length == Fields && parts[0] == Format && parts[BlobAt].Length > 0 ? parts[BlobAt] : null;
    }

    /// <summary>Whether two units share an epoch: both have one, and it is the same; an older record shares none, not even with itself.</summary>
    public static bool Shared(string? first, string? other) => first is not null && string.Equals(first, other, StringComparison.Ordinal);
}
