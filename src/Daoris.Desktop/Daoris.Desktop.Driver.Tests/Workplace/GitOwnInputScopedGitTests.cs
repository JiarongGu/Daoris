using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEW2: a repository's URL-scoped header, cookie file or certificate wins by git's urlmatch over Daoris's empty reset of the unscoped
/// key, so it is found the way git finds it, with a real git and a repository-local setting.
/// </summary>
public sealed class GitOwnInputScopedGitTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-own-scoped-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_root);

    private async Task<string> RepositoryAsync(params string[][] config)
    {
        Directory.CreateDirectory(_root);
        Assert.Equal(0, (await WorkingTree.GitAsync(_root, ["init", "-q"], default)).Code);
        foreach (var args in config) Assert.Equal(0, (await WorkingTree.GitAsync(_root, ["config", .. args], default)).Code);
        return _root;
    }

    [Fact]
    public async Task A_scoped_header_that_matches_the_address_is_found_and_one_for_another_address_is_not()
    {
        var root = await RepositoryAsync(["http.https://host.example/org/.extraHeader", "Authorization: Bearer test-value"]);

        Assert.Equal("http.https://host.example/org/.extraHeader", await GitOwnInput.ScopedInputAsync(root, "https://host.example/org/repo.git", default));
        Assert.Null(await GitOwnInput.ScopedInputAsync(root, "https://host.example/other/repo.git", default));
        Assert.Null(await GitOwnInput.ScopedInputAsync(root, "https://elsewhere.example/org/repo.git", default));
        Assert.Null(await GitOwnInput.ScopedInputAsync(root, "ssh://host.example/org/repo.git", default));
    }

    [Fact]
    public async Task A_scoped_cookie_file_and_a_scoped_certificate_are_found_by_urlmatch()
    {
        var root = await RepositoryAsync(
            ["http.https://host.example.cookieFile", "cookies.txt"],
            ["http.https://host.example/org/.sslCert", "client.pem"]);

        Assert.Equal("http.https://host.example.cookieFile", await GitOwnInput.ScopedInputAsync(root, "https://host.example/x/y", default));
        Assert.Equal("http.https://host.example/org/.sslCert", await GitOwnInput.CertificateAsync(root, "https://host.example/org/y", default));
        Assert.Null(await GitOwnInput.CertificateAsync(root, "https://host.example/other/y", default));
    }

    [Fact]
    public async Task An_empty_reset_of_the_scoped_header_is_no_input()
    {
        var root = await RepositoryAsync(["http.https://host.example/.extraHeader", ""]);

        Assert.Null(await GitOwnInput.ScopedInputAsync(root, "https://host.example/org/repo.git", default));
    }
}
