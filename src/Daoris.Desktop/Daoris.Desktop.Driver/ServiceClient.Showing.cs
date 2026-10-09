using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What the review's showing reads of a session's record (REVIEWENV1d): the tree its set-up was built in.</summary>
public sealed partial class ServiceClient
{
    /// <summary>
    /// The tree a session of this machine's held (D51), closed records included, as the records answer it on loopback; null where
    /// the service holds no such record or says no tree. What <i>Show it again</i> serves a set-up's folder from.
    /// </summary>
    public async Task<string?> SessionTreeAsync(string session, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
        return document.RootElement.EnumerateArray()
            .Where(record => record.ValueKind == JsonValueKind.Object
                             && string.Equals(Text(record, "id"), session, StringComparison.OrdinalIgnoreCase))
            .Select(record => Text(record, "tree"))
            .FirstOrDefault(tree => tree is { Length: > 0 });
    }
}
