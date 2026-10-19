using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Restricted mode's configured access as the person chose it (HELPMODE1c, D183 point 1; the Ask Daoris modes design §4.1): the
/// home's <c>configured-access.json</c>, the operations chosen per grant by the page's codes. Housekeeping is the grant built
/// first (HELPMODE1c2a); the browser's grant is its plugin's approval (<see cref="HelpGrants"/>), never kept here.
/// </summary>
/// <remarks>
/// <para><b>Read at every start and at every call</b> (the design's trap of a grant read once and reused): each start of Ask Daoris
/// composes its allow-list, its receipt and its room from it, and each housekeeping request is judged by it again
/// (<see cref="HelpHousekeeping"/>), so a revoke refuses the next call.</para>
///
/// <para><b>Absence is none chosen</b>: no file, one that does not read, a grant that is no list. A code this build does not act
/// on (dropping recovery refs) grants nothing, and is never dropped from the file by a reader.</para>
/// </remarks>
/// <param name="Housekeeping">The housekeeping operations chosen, in <see cref="HousekeepingBuilt"/>'s order, once each.</param>
public sealed record ConfiguredAccess(IReadOnlyList<string> Housekeeping)
{
    /// <summary>The grant's name in the file, the page's <c>HelpGrantId</c>.</summary>
    public const string HousekeepingGrant = "housekeeping";

    /// <summary>A look at what the cleanup would remove and what it keeps.</summary>
    public const string Inspect = "inspect";

    /// <summary>Removing the session and landed branches named, with their trees, where the cleanup's judges clear them.</summary>
    public const string CleanUp = "clean-up";

    /// <summary>Discarding one session branch by name, unforced.</summary>
    public const string DiscardBranch = "discard-branch";

    /// <summary>The housekeeping operations this build acts on, in the page's order; the page's <c>drop-recovery</c> is not yet one.</summary>
    public static readonly IReadOnlyList<string> HousekeepingBuilt = [Inspect, CleanUp, DiscardBranch];

    public static readonly ConfiguredAccess None = new([]);

    /// <summary>Where the record is kept: the home's <see cref="HomeEntry.ConfiguredAccess"/>.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.ConfiguredAccess);

    /// <summary>The connector's tool for a housekeeping operation, as it is named on the wire: <c>housekeeping_clean_up</c>.</summary>
    public static string ToolOf(string operation) => $"housekeeping_{operation.Replace('-', '_')}";

    /// <summary>The tools of the operations chosen, in their order.</summary>
    public IReadOnlyList<string> HousekeepingTools => [.. Housekeeping.Select(ToolOf)];

    /// <summary>The record as the home holds it now; <see cref="None"/> where it holds none or it does not read.</summary>
    public static ConfiguredAccess Read(string home)
    {
        try
        {
            var path = PathOf(home);
            if (!File.Exists(path)) return None;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(HousekeepingGrant, out var chosen) || chosen.ValueKind != JsonValueKind.Array)
            {
                return None;
            }

            var named = chosen.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            return new ConfiguredAccess([.. HousekeepingBuilt.Where(named.Contains)]);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return None;
        }
    }
}
