using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What Settings → Git and the remote rows read, from the files alone, as <c>TOOLS_LIST</c>'s
/// <c>identities</c>: the identities with what points at them, the hosts, and the hosts the person's own sign-ins are chosen for. No secret by construction: a public key, a fingerprint and whether an API token is set are the most it holds.
/// </summary>
internal static class GitIdentityPage
{
    internal static object Read(string home)
    {
        var identities = GitIdentities.Read(home);
        var pointers = GitIdentityUse.Read(home);
        string? ssh = null;
        try
        {
            if (identities.Any(each => each.Way == GitIdentity.Ssh)) ssh = GitIdentityActs.Ssh(home);
        }
        catch (DriverException)
        {
            // No ssh is found: the page shows none, and an SSH act says why.
        }

        return new
        {
            Problem = GitIdentities.Problem(home),
            Identities = identities.Select(each =>
            {
                var used = GitSshFiles.LastUsed(home, each.Id);
                return new
                {
                    each.Id, each.Label, each.Host, each.Way, each.User, each.KeyType, each.Fingerprint, each.ReadOnly, each.Default,
                    each.CommitsName, each.CommitsEmail,
                    Test = each.Test is { } test ? new { test.Result, test.User, At = Moments.Write(test.At) } : null,
                    LastUsed = used is { } at ? Moments.Write(at) : null,
                    // Whether a token is set, never the token.
                    ApiToken = GitTokens.Has(home, each.Id, GitTokens.Api),
                    Ssh = each.Way == GitIdentity.Ssh ? ssh : null,
                    PublicKey = each.Way == GitIdentity.Ssh ? GitKeys.PublicKey(home, each.Id) : null,
                    KeyPage = GitHosts.Known.TryGetValue(each.Host, out var known) ? known.KeyPage : null,
                    Repositories = pointers.Repository.Sum(repository => repository.Value.Count(pointer => pointer.Value == each.Id)),
                    Workspaces = pointers.Workspace.Count(workspace => workspace.Value.Any(pointer => pointer.Value == each.Id)),
                    PointedAt = GitIdentityUse.PointingAt(pointers, each.Id).ToArray(),
                };
            }).ToArray(),
            Own = pointers.Machine.Keys.Order(StringComparer.Ordinal).ToArray(),
            // Each workspace's choice for a host: an identity's id or `own`, as an array so no name is a key.
            Choices = pointers.Workspace.SelectMany(workspace => workspace.Value.Select(host => new { Workspace = workspace.Key, Host = host.Key, Value = host.Value }))
                .OrderBy(each => each.Workspace, StringComparer.Ordinal).ThenBy(each => each.Host, StringComparer.Ordinal).ToArray(),
            Reads = GitLibraryJournal.Page(),
            Hosts = identities.Select(each => each.Host).Concat(pointers.Machine.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
    }
}
