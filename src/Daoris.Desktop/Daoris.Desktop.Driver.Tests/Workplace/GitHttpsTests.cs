using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAUTH1d's tables (the git-auth design §3.3, §4.3 and §14 row 4): what an identity's HTTPS act is handed and what is taken out of
/// it, the address's host, user-info and password, GCM found beside git, and the resolution of an HTTPS address. No process starts.
/// </summary>
public sealed class GitHttpsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-https-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Dictionary<string, string?> Scope(IDictionary<string, string?> environment)
    {
        var count = int.Parse(environment["GIT_CONFIG_COUNT"]!, System.Globalization.CultureInfo.InvariantCulture);
        var pairs = new List<KeyValuePair<string, string?>>();
        for (var index = 0; index < count; index++) pairs.Add(new(environment[$"GIT_CONFIG_KEY_{index}"]!, environment[$"GIT_CONFIG_VALUE_{index}"]));
        // The last of a key wins, as git reads the command scope; credential.helper appears twice, the empty reset first.
        return pairs.GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value);
    }

    [Fact]
    public void An_identitys_act_loses_every_authentication_input_of_the_persons_and_gains_GCM_in_Daoris_namespace()
    {
        var home = Path.Combine(_root, "data");
        var manager = Path.Combine(_root, "git", "git-credential-manager.exe");
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_SSL_CERT"] = "c.pem", ["GIT_SSL_KEY"] = "k.pem", ["GIT_SSL_CERT_PASSWORD_PROTECTED"] = "1",
            ["GCM_TRACE"] = "1", ["GCM_PROVIDER"] = "github", ["GCM_NAMESPACE"] = "theirs", ["GCM_CREDENTIAL_STORE"] = "plaintext",
            ["HOME"] = "persons", ["USERPROFILE"] = "persons", ["PATH"] = "elsewhere",
        };

        GitHttps.Apply(environment, home, "alice", manager);

        Assert.DoesNotContain("GIT_SSL_CERT", environment.Keys);
        Assert.DoesNotContain("GIT_SSL_KEY", environment.Keys);
        Assert.DoesNotContain("GIT_SSL_CERT_PASSWORD_PROTECTED", environment.Keys);
        Assert.DoesNotContain("GCM_TRACE", environment.Keys);
        Assert.DoesNotContain("GCM_PROVIDER", environment.Keys);
        Assert.Equal("daoris", environment["GCM_NAMESPACE"]);
        Assert.Equal("wincredman", environment["GCM_CREDENTIAL_STORE"]);
        Assert.Equal("never", environment["GCM_INTERACTIVE"]);
        Assert.Equal(GitHttps.EmptyHome(home), environment["HOME"]);
        Assert.Equal(GitHttps.EmptyHome(home), environment["USERPROFILE"]);
        Assert.True(Directory.Exists(GitHttps.EmptyHome(home)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(GitHttps.EmptyHome(home)));
        Assert.StartsWith(Path.GetDirectoryName(manager)!, environment["PATH"], StringComparison.Ordinal);

        Assert.Equal("", environment["GIT_CONFIG_VALUE_0"]);
        Assert.Equal("credential.helper", environment["GIT_CONFIG_KEY_0"]);
        var scope = Scope(environment);
        Assert.Equal("manager", scope["credential.helper"]);
        Assert.Equal("daoris", scope["credential.namespace"]);
        Assert.Equal("wincredman", scope["credential.credentialStore"]);
        Assert.Equal("never", scope["credential.interactive"]);
        Assert.Equal("false", scope["credential.trace"]);
        Assert.Equal("false", scope["credential.traceSecrets"]);
        Assert.Equal("alice", scope["credential.username"]);
        Assert.Equal("", scope["http.extraHeader"]);
        Assert.Equal("", scope["http.cookieFile"]);
    }

    [Fact]
    public void Where_no_GCM_is_found_the_act_still_loses_the_inputs_and_reads_anonymously()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = "x" };

        GitHttps.Apply(environment, Path.Combine(_root, "data"), "alice", manager: null);

        var scope = Scope(environment);
        Assert.Equal("", scope["credential.helper"]);
        Assert.DoesNotContain("credential.namespace", scope.Keys);
        Assert.DoesNotContain("credential.username", scope.Keys);
        Assert.Equal("", scope["http.extraHeader"]);
        Assert.DoesNotContain("GCM_NAMESPACE", environment.Keys);
    }

    [Theory]
    [InlineData("https://GitHub.com/org/engine.git", "github.com")]
    [InlineData("https://github.com:443/org/engine.git", "github.com")]
    [InlineData("http://git.example.com:8080/org/engine", "git.example.com:8080")]
    [InlineData("daoris::https://alice@dev.azure.com/org/p/_git/r", "dev.azure.com")]
    [InlineData("git@github.com:org/engine.git", null)]
    [InlineData("C:/work/engine", null)]
    public void An_addresss_host_is_its_canonical_authority_and_none_for_other_ways(string address, string? host) =>
        Assert.Equal(host, GitHttps.HostOf(address));

    [Theory]
    [InlineData("https://alice:secret@github.com/org/e", true, "https://github.com/org/e", "alice")]
    [InlineData("https://alice@github.com/org/e", false, "https://github.com/org/e", "alice")]
    [InlineData("https://github.com/org/e", false, "https://github.com/org/e", null)]
    [InlineData("https://github.com/org/e@v:1", false, "https://github.com/org/e@v:1", null)]
    public void An_addresss_user_info_is_dropped_and_a_password_is_found(string address, bool password, string bare, string? user)
    {
        Assert.Equal(password, GitHttps.HasPassword(address));
        Assert.Equal(bare, GitHttps.Bare(address));
        Assert.Equal(user, GitHttps.UserOf(address));
    }

    [Fact]
    public void GCM_is_found_beside_the_git_on_the_path_in_Git_for_Windowss_layout_and_on_the_path_else()
    {
        var git = Path.Combine(_root, "Git");
        Directory.CreateDirectory(Path.Combine(git, "cmd"));
        Directory.CreateDirectory(Path.Combine(git, "mingw64", "bin"));
        File.WriteAllText(Path.Combine(git, "cmd", OperatingSystem.IsWindows() ? "git.exe" : "git"), "");
        var name = OperatingSystem.IsWindows() ? "git-credential-manager.exe" : "git-credential-manager";
        File.WriteAllText(Path.Combine(git, "mingw64", "bin", name), "");

        Assert.Equal(Path.Combine(git, "mingw64", "bin", name), GitHttps.Manager(variable => variable == "PATH" ? Path.Combine(git, "cmd") : null));
        Assert.Null(GitHttps.Manager(variable => variable == "PATH" ? Path.Combine(_root, "nowhere") : null));
    }

    [Fact]
    public void An_HTTPS_address_resolves_to_an_HTTPS_identity_of_its_host_by_default_and_never_to_an_SSH_one()
    {
        GitIdentity Identity(string id, string way, string host) => new(id, id, host, way, null, null, null, null, null, false, null, true);
        var ssh = Identity("gi-000000000001", GitIdentity.Ssh, "github.com");
        var https = Identity("gi-000000000002", GitIdentity.Https, "github.com");

        var served = GitIdentityUse.Resolve([ssh, https], GitIdentityUse.Pointers.None, null, null, [], GitHttps.HostOf("https://github.com/o/r")!, GitIdentity.Https);

        Assert.Equal(GitServeKind.Identity, served.Kind);
        Assert.Equal(https.Id, served.Identity!.Id);
        Assert.Equal(GitServeKind.None,
            GitIdentityUse.Resolve([ssh], GitIdentityUse.Pointers.None, null, null, [], "github.com", GitIdentity.Https).Kind);
    }

    [Fact]
    public async Task A_default_is_per_way_so_an_HTTPS_identity_and_an_SSH_one_of_a_host_each_hold_theirs()
    {
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        GitIdentity Make(string id, string way, bool isDefault) => new(id, id, "github.com", way, null, null, null, null, null, false, null, isDefault);
        GitIdentities.Change(home, _ => [Make("gi-00000000000a", GitIdentity.Ssh, true), Make("gi-00000000000b", GitIdentity.Https, false)]);

        GitIdentityActs.Default(home, "github.com", "gi-00000000000b");

        var all = GitIdentities.Read(home);
        Assert.True(all.Single(each => each.Way == GitIdentity.Ssh).Default);
        Assert.True(all.Single(each => each.Way == GitIdentity.Https).Default);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_token_with_no_user_and_an_empty_token_are_refused_before_GCM_is_asked()
    {
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);

        var noUser = await Assert.ThrowsAsync<DriverException>(() =>
            GitIdentityHttps.AddHttpsAsync(home, "github.com", "Work", null, null, null, "t", "https", CancellationToken.None));
        var empty = await Assert.ThrowsAsync<DriverException>(() =>
            GitIdentityHttps.AddHttpsAsync(home, "github.com", "Work", "alice", null, null, "", "https", CancellationToken.None));

        Assert.Contains("(user)", noUser.Message, StringComparison.Ordinal);
        Assert.Contains("(token)", empty.Message, StringComparison.Ordinal);
        Assert.Empty(GitIdentities.Read(home));
    }
}
