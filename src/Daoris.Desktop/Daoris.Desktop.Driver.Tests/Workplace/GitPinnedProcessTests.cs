using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The doors' classes run again with the MinGit the toolchain pins (GITAUTH1b, the git design's G1), each fact the runner's git proves
/// proved here with the pinned git named as the test home's git (<see cref="PinnedMinGit"/>): the doors through the remote helper and the
/// ssh door, and the stand-in ssh host's fetch, clone and push.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class GitDoorsPinnedProcessTests : GitDoorsProcessTests
{
    protected override bool Pinned => true;
}

/// <summary>
/// The HTTPS class run with the pinned MinGit as the client (the doors' git, the credential door and the remote helper), the TLS host
/// trusted by <c>http.sslCAInfo</c> through OpenSSL; the HTTP backend behind it is the runner's git, since MinGit ships no
/// <c>git-http-backend</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class GitHttpsPinnedProcessTests(GitTlsHost host) : GitHttpsProcessTests(host), IClassFixture<GitTlsHost>
{
    protected override bool Pinned => true;
}

/// <inheritdoc cref="GitDoorsPinnedProcessTests"/>
[Trait(Category.Name, Category.Process)]
public sealed class GitSshPinnedProcessTests(GitSshHost host, PullRequestApiHost api, ITestOutputHelper output)
    : GitSshProcessTests(host, api, output), IClassFixture<GitSshHost>, IClassFixture<PullRequestApiHost>
{
    protected override bool Pinned => true;
}
