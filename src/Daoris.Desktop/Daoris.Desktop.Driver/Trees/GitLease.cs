using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The workplace's answer to the reading the instance's leases ask of the git verb's own command (FOUNDATION1 mod-vi, the design §14.3):
/// <c>git branches</c>, <c>git identity list</c> and <c>public-key</c> are reads; an identity's change, a default and a pointer write the
/// home's identities or the driver's file and hold the lease (GITAUTH1c). Installed when the driver's assembly loads, before any host reads
/// a lease.
/// </summary>
internal static class GitLease
{
    [ModuleInitializer]
    internal static void Install() => VerbLeases.Answer("git", new(GitIdentityCommand.Changes));
}
