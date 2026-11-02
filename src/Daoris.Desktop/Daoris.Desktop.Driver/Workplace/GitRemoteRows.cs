namespace Daoris.Driver;

/// <summary>One remote of a repository as its Setup shows it (the git-auth design §7.3): its address, and what serves it.</summary>
/// <param name="Name">The remote's name.</param>
/// <param name="Address">The effective address (§4.4) with no user-info; what a person reads.</param>
/// <param name="Host">The address's canonical host, or null for a local path, which needs no identity.</param>
/// <param name="Way"><c>ssh</c> or <c>https</c>; null for a local path.</param>
/// <param name="Serves"><c>identity</c>, <c>own</c>, <c>none</c>, or <c>local</c>.</param>
/// <param name="Identity">The serving identity's id, and its label.</param>
/// <param name="Source"><c>repository</c>, <c>workspace</c>, <c>machine</c> or <c>default</c>; null where nothing serves.</param>
/// <param name="Passed">Each pointer passed over and why (§3.2).</param>
/// <param name="Chosen">The repository's own pointer for this remote: an identity's id, <c>own</c>, or null.</param>
public sealed record GitRemoteRow(
    string Name, string Address, string? Host, string? Way, string Serves, string? Identity, string? Label, string? Source,
    IReadOnlyList<string> Passed, string? Chosen);

/// <summary>
/// A repository's git remotes with what serves each (GITAUTH1e, the git-auth design §3.2 and §7.3), read through §4.4's one reader of an
/// address. Starts git once for the names and once for each address.
/// </summary>
public static class GitRemoteRows
{
    /// <summary>Each remote of the checkout at <paramref name="root"/> and its effective address, as <c>git use</c> reads them.</summary>
    public static async Task<IReadOnlyDictionary<string, string?>> AddressesAsync(string root, CancellationToken ct)
    {
        var addresses = new Dictionary<string, string?>(StringComparer.Ordinal);
        var (code, said, _) = await WorkingTree.GitAsync(root, ["remote"], ct).ConfigureAwait(false);
        if (code != 0) return addresses;
        foreach (var name in said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            addresses[name] = await GitRemotes.AddressOf(root, name, ct: ct).ConfigureAwait(false);
        return addresses;
    }

    /// <summary>The last folder of a local remote's path or <c>file://</c> address, with no parent, drive or user-info.</summary>
    internal static string LocalName(string address)
    {
        var path = address.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? address[7..] : address;
        var name = path.TrimEnd('/', '\\').Split('/', '\\').LastOrDefault() ?? "";
        return name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) && name.Length > 4 ? name[..^4] : name;
    }

    /// <summary>The rows of the checkout at <paramref name="root"/>, in the order git lists the remotes.</summary>
    public static async Task<IReadOnlyList<GitRemoteRow>> ReadAsync(string home, string root, string repository, string? workspace, CancellationToken ct)
    {
        var addresses = await AddressesAsync(root, ct).ConfigureAwait(false);
        var identities = GitIdentities.Read(home);
        var pointers = GitIdentityUse.Read(home);
        pointers.Repository.TryGetValue(repository, out var chosen);
        var rows = new List<GitRemoteRow>();
        foreach (var (name, address) in addresses)
        {
            var own = chosen is not null && chosen.TryGetValue(name, out var pointer) ? pointer : null;
            var host = address is null ? null : GitIdentityActs.GitAddressHost(address);
            if (address is null || host is null)
            {
                // A local remote is its folder's name: the route answers no machine path.
                rows.Add(new GitRemoteRow(name, address is null ? "" : LocalName(address), null, null, "local", null, null, null, [], own));
                continue;
            }

            var way = TreeRefs.ReachOf(address) is "https" or "http" ? GitIdentity.Https : GitIdentity.Ssh;
            var key = GitAddressKeys.Of(address);
            var same = key is null ? [name] : addresses.Where(each => each.Value is not null
                && string.Equals(GitAddressKeys.Of(each.Value), key, StringComparison.OrdinalIgnoreCase)).Select(each => each.Key).ToList();
            var serves = GitIdentityUse.Resolve(identities, pointers, repository, workspace, same, host, way);
            rows.Add(new GitRemoteRow(name, GitWords.Redact(address) ?? "", host, way, serves.Kind switch
            {
                GitServeKind.Identity => "identity",
                GitServeKind.Own => "own",
                _ => "none",
            }, serves.Identity?.Id, serves.Identity?.Label, serves.Source, serves.Passed, own));
        }

        return rows;
    }
}
