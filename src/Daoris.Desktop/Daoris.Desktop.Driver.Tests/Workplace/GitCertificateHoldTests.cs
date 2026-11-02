using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The §3.3 certificate hold (GITAUTH1c, D198 point 3): a client certificate or key named in configuration (<c>http.sslCert</c>,
/// <c>http.sslKey</c>), which git gives no way to clear, holds an HTTPS remote as <c>own-input</c> where a git identity of Daoris's
/// serves it, naming the key and never its value, until the person chooses their own sign-ins for it or removes the setting. Where
/// nothing serves, or the person chose their own, the remote keeps today's credentials; a key scoped to another address holds nothing.
/// </summary>
/// <remarks>Against a real git in a scratch checkout, its system and global files kept from the person's. Alone: those are the
/// process's variables.</remarks>
[Collection(Alone.Name)]
public sealed class GitCertificateHoldTests : IDisposable
{
    private const string Address = "https://github.com/org-a/engine.git";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-certificate-hold", Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "data");

    private string Repo => Path.Combine(_root, "engine");

    public GitCertificateHoldTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
        var empty = Path.Combine(_root, "global");
        File.WriteAllText(empty, "");
        Set("GIT_CONFIG_NOSYSTEM", "1");
        Set("GIT_CONFIG_GLOBAL", empty);
    }

    private void Set(string name, string value)
    {
        if (!_was.ContainsKey(name)) _was[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _was) Environment.SetEnvironmentVariable(name, value);
        GitSeed.Discard(_root);
    }

    private async Task Git(params string[] arguments)
    {
        var (code, _, error) = await WorkingTree.GitAsync(Repo, arguments, CancellationToken.None);
        Assert.True(code == 0, error);
    }

    private async Task CheckoutAsync(bool identity = true)
    {
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", Address);
        if (!identity) return;
        GitIdentities.Change(Home, _ =>
        [
            new GitIdentity("gi-00000000000b", "Work", "github.com", GitIdentity.Https, "octo-work", null, null, null, null, false, null, Default: true),
        ]);
    }

    private Task<GitHold?> HeldAsync(string address = Address) =>
        GitOwnInput.HeldAsync(Home, Repo, "engine", "games", "origin", address, "git-a", CancellationToken.None);

    [Theory]
    [InlineData("http.sslCert", "http.sslCert")]
    [InlineData("http.sslKey", "http.sslKey")]
    [InlineData("http.https://github.com/.sslCert", "http.https://github.com/.sslCert")]
    public async Task A_client_certificate_or_key_holds_an_https_remote_an_identity_serves_naming_the_key_alone(string key, string named)
    {
        await CheckoutAsync();
        await Git("config", key, "X:/daoris/certificates/client.pem");

        var held = Assert.IsType<GitHold>(await HeldAsync());

        Assert.Equal(("own-input", "all", named), (held.Class, held.Holds, held.Words));
        var said = GitHolds.Said(held);
        Assert.Contains($"`{named}`", said, StringComparison.Ordinal);
        Assert.Contains("choose your own git sign-ins", said, StringComparison.Ordinal);
        var told = GitHolds.Told([held])!;
        Assert.Contains("a client certificate or key", told, StringComparison.Ordinal);
        Assert.Contains("do not change that setting", told, StringComparison.Ordinal);
        foreach (var words in new[] { said, told, File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.GitHeld)) })
            Assert.DoesNotContain("client.pem", words, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_hold_goes_once_the_setting_does()
    {
        await CheckoutAsync();
        await Git("config", "http.sslCert", "X:/daoris/certificates/client.pem");
        Assert.NotNull(await HeldAsync());

        await Git("config", "--unset", "http.sslCert");

        Assert.Null(await HeldAsync());
        Assert.Empty(GitHolds.Read(Home));
    }

    [Fact]
    public async Task Where_nothing_serves_or_the_person_chose_their_own_the_certificate_is_theirs_and_nothing_is_held()
    {
        await CheckoutAsync(identity: false);
        await Git("config", "http.sslCert", "X:/daoris/certificates/client.pem");
        Assert.Null(await HeldAsync());

        GitIdentities.Change(Home, _ =>
        [
            new GitIdentity("gi-00000000000b", "Work", "github.com", GitIdentity.Https, "octo-work", null, null, null, null, false, null, Default: true),
        ]);
        GitIdentityActs.UseMachine(Home, "github.com", own: true);
        Assert.Null(await HeldAsync());
    }

    [Fact]
    public async Task A_key_scoped_to_another_address_or_an_ssh_remote_holds_nothing()
    {
        await CheckoutAsync();
        await Git("config", "http.https://gitlab.com/.sslCert", "X:/daoris/certificates/client.pem");
        Assert.Null(await HeldAsync());

        await Git("config", "http.sslCert", "X:/daoris/certificates/client.pem");
        await Git("remote", "set-url", "origin", "git@github.com:org-a/engine.git");
        Assert.Null(await HeldAsync("git@github.com:org-a/engine.git"));
    }
}
