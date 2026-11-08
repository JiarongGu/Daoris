using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The links in a slot the pool may carry: only the one npm's own workspace rule makes. It stands at <c>node_modules/&lt;name&gt;</c> or
/// <c>node_modules/@&lt;scope&gt;/&lt;name&gt;</c> beside a <c>package.json</c> the slot's tip tracks, whose <c>workspaces</c> declare the
/// folder it leads to; that folder lies inside the slot, and its own tracked <c>package.json</c> is named <c>&lt;name&gt;</c>. Any other
/// link, wherever it lies or leads, holds the slot.
/// </summary>
internal static class WorkspaceLinks
{
    /// <summary>The links among <paramref name="links"/> (paths from the slot's top, <c>/</c>-separated) that npm's workspace declarations do not account for.</summary>
    internal static async Task<IReadOnlyList<string>> UnaccountedAsync(string slot, IReadOnlyList<string> links, CancellationToken ct)
    {
        var unaccounted = new List<string>();
        foreach (var link in links)
        {
            if (!await AccountedAsync(slot, link, ct).ConfigureAwait(false)) unaccounted.Add(link);
        }

        return unaccounted;
    }

    private static async Task<bool> AccountedAsync(string slot, string link, CancellationToken ct)
    {
        var segments = link.Split('/');
        var modules = Array.FindLastIndex(segments, segment => segment == "node_modules");
        var rest = modules < 0 ? [] : segments[(modules + 1)..];
        var scoped = rest.Length == 2 && rest[0].StartsWith('@');
        if (modules < 0 || !(rest.Length == 1 || scoped)) return false;

        var folder = modules == 0 ? "" : string.Join('/', segments[..modules]) + "/";
        var name = string.Join('/', rest);
        var info = new DirectoryInfo(Path.Combine(slot, link.Replace('/', Path.DirectorySeparatorChar)));
        if (DependencyLinks.Leads(info) is not { } target || !DependencyLinks.Within(slot, target)) return false;

        var members = await MembersAsync(slot, folder + "package.json", ct).ConfigureAwait(false);
        var inside = Path.GetRelativePath(Path.Combine(slot, folder.Replace('/', Path.DirectorySeparatorChar)), target).Replace('\\', '/');
        if (!members.Any(member => UnionJoins.Glob(member).IsMatch(inside))) return false;

        var leadsTo = Path.GetRelativePath(slot, target).Replace('\\', '/');
        return await NameAsync(slot, leadsTo + "/package.json", ct).ConfigureAwait(false) == name;
    }

    private static async Task<IReadOnlyList<string>> MembersAsync(string slot, string manifest, CancellationToken ct)
    {
        if (await FileGit.ShowAsync(slot, "HEAD", manifest, ct).ConfigureAwait(false) is not { } text) return [];
        try
        {
            using var document = JsonDocument.Parse(text);
            return WorkspaceMembers.OfManifest(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static async Task<string?> NameAsync(string slot, string manifest, CancellationToken ct)
    {
        if (await FileGit.ShowAsync(slot, "HEAD", manifest, ct).ConfigureAwait(false) is not { } text) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
