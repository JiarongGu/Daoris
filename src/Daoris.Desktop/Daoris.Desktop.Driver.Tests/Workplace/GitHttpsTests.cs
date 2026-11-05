using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The HTTPS tables (the git design §2.3, D241 point 6): what an identity's HTTPS act is handed and what is taken out of it, the
/// credential door's answers, an identity's tokens, the address's host, user-info and password, and the resolution of an HTTPS address.
/// No process starts.
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
    public void An_identitys_act_loses_every_authentication_input_of_the_persons_and_gains_the_credential_door_alone()
    {
        var home = Path.Combine(_root, "data");
        var door = Path.Combine(_root, "bin");
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_SSL_CERT"] = "c.pem", ["GIT_SSL_KEY"] = "k.pem", ["GIT_SSL_CERT_PASSWORD_PROTECTED"] = "1",
            [GitRouting.IdentityVariable] = "gi-inherited0",
            ["HOME"] = "persons", ["USERPROFILE"] = "persons", ["PATH"] = "elsewhere",
        };

        GitHttps.Apply(environment, home, "gi-000000000001", door);

        Assert.DoesNotContain("GIT_SSL_CERT", environment.Keys);
        Assert.DoesNotContain("GIT_SSL_KEY", environment.Keys);
        Assert.DoesNotContain("GIT_SSL_CERT_PASSWORD_PROTECTED", environment.Keys);
        Assert.Equal("gi-000000000001", environment[GitRouting.IdentityVariable]);
        Assert.Equal(GitHttps.EmptyHome(home), environment["HOME"]);
        Assert.Equal(GitHttps.EmptyHome(home), environment["USERPROFILE"]);
        Assert.True(Directory.Exists(GitHttps.EmptyHome(home)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(GitHttps.EmptyHome(home)));
        Assert.StartsWith(door, environment["PATH"], StringComparison.Ordinal);

        Assert.Equal("", environment["GIT_CONFIG_VALUE_0"]);
        Assert.Equal("credential.helper", environment["GIT_CONFIG_KEY_0"]);
        var scope = Scope(environment);
        Assert.Equal(GitCredentialDoor.Name, scope["credential.helper"]);
        Assert.Equal("", scope["http.extraHeader"]);
        Assert.Equal("", scope["http.cookieFile"]);
    }

    [Fact]
    public void An_anonymous_act_or_one_with_no_door_laid_still_loses_the_inputs_and_is_answered_no_credential()
    {
        foreach (var (identity, door) in new (string?, string?)[] { (null, null), (null, "bin"), ("gi-000000000001", null) })
        {
            var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = "x", [GitRouting.IdentityVariable] = "gi-inherited0" };

            GitHttps.Apply(environment, Path.Combine(_root, "data"), identity, door);

            var scope = Scope(environment);
            Assert.Equal("", scope["credential.helper"]);
            Assert.DoesNotContain(GitRouting.IdentityVariable, environment.Keys);
            Assert.Equal("x", environment["PATH"]);
            Assert.Equal("", scope["http.extraHeader"]);
        }
    }

    [Fact]
    public void The_credential_door_answers_the_identitys_token_for_its_own_host_and_nothing_else()
    {
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var identity = GitIdentityHttps.AddHttps(home, "git.example.invalid", "Work", "alice", null, null, "throwaway-token");
        Func<string, string?> Variable = name => name == GitRouting.HomeVariable ? home : name == GitRouting.IdentityVariable ? identity.Id : null;
        string Ask(string text, Func<string, string?>? variable = null)
        {
            var output = new StringWriter();
            Assert.Equal(0, GitCredentialDoor.Run(["get"], variable ?? Variable, new StringReader(text), output, TextWriter.Null));
            return output.ToString().Replace("\r\n", "\n");
        }

        Assert.Equal("username=alice\npassword=throwaway-token\n", Ask("protocol=https\nhost=git.example.invalid\n\n"));
        Assert.Equal("username=alice\npassword=throwaway-token\n", Ask("protocol=https\nhost=GIT.example.invalid:443\n\n"));
        Assert.Equal("", Ask("protocol=https\nhost=github.com\n\n"));
        Assert.Equal("", Ask("protocol=https\nhost=git.example.invalid:8443\n\n"));
        Assert.Equal("", Ask("protocol=https\nhost=evil.git.example.invalid\n\n"));
        Assert.Equal("", Ask("protocol=ssh\nhost=git.example.invalid\n\n"));
        Assert.Equal("", Ask("host=git.example.invalid\n\n"));
        Assert.Equal("", Ask("protocol=https\nhost=git.example.invalid\n\n", name => name == GitRouting.HomeVariable ? home : null));
        Assert.Equal("", Ask("protocol=https\nhost=git.example.invalid\n\n", name => name == GitRouting.IdentityVariable ? identity.Id : null));
        foreach (var verb in new[] { "store", "erase" })
        {
            var output = new StringWriter();
            Assert.Equal(0, GitCredentialDoor.Run([verb], Variable, new StringReader("protocol=https\nhost=git.example.invalid\nusername=x\npassword=y\n\n"), output, TextWriter.Null));
            Assert.Equal("", output.ToString());
        }

        var refused = new StringWriter();
        Assert.Equal(128, GitCredentialDoor.Run([], Variable, TextReader.Null, TextWriter.Null, refused));
        Assert.StartsWith("git-credential-daoris: ", refused.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_token_lives_in_a_folder_limited_to_the_persons_account_and_is_cleared_and_removed_with_its_identity()
    {
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var identity = GitIdentityHttps.AddHttps(home, "git.example.invalid", "Work", "alice", null, null, "throwaway-token");

        GitIdentityTokens.Set(home, identity.Id, GitTokens.Api, "throwaway-api-token");

        var folder = GitTokens.Folder(home, identity.Id);
        Assert.True(GitPrivateFolder.IsPrivate(folder));
        Assert.Equal("throwaway-token", GitTokens.Read(home, identity.Id, GitTokens.Https));
        Assert.Equal("throwaway-api-token", GitTokens.Read(home, identity.Id, GitTokens.Api));
        Assert.DoesNotContain("throwaway", File.ReadAllText(DaorisHome.Entry(home, HomeEntry.GitIdentities)), StringComparison.Ordinal);

        GitIdentityTokens.Clear(home, identity.Id, GitTokens.Api);
        Assert.False(GitIdentityTokens.Has(home, identity, GitTokens.Api));
        Assert.True(GitIdentityTokens.Has(home, identity, GitTokens.Https));
        Assert.Contains("(token-required)", Assert.Throws<DriverException>(() => GitIdentityTokens.Clear(home, identity.Id, GitTokens.Https)).Message, StringComparison.Ordinal);

        GitIdentityActs.Remove(home, identity.Id);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void An_SSH_identity_keeps_an_API_token_and_no_HTTPS_one()
    {
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        GitIdentities.Change(home, _ => [new("gi-0000000000d1", "Deploy", "git.example.invalid", GitIdentity.Ssh, null, null, null, "ed25519", "SHA256:x", false, null, true)]);

        GitIdentityTokens.Set(home, "Deploy", GitTokens.Api, "throwaway-api-token");

        Assert.Equal("throwaway-api-token", GitTokens.Read(home, "gi-0000000000d1", GitTokens.Api));
        Assert.Contains("(kind)", Assert.Throws<DriverException>(() => GitIdentityTokens.Set(home, "Deploy", GitTokens.Https, "t")).Message, StringComparison.Ordinal);
        Assert.Contains("(token)", Assert.Throws<DriverException>(() => GitIdentityTokens.Set(home, "Deploy", GitTokens.Api, "two\nlines")).Message, StringComparison.Ordinal);
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
    public async Task A_token_with_no_user_and_an_empty_or_missing_token_are_refused_and_no_identity_or_token_is_written()
    {
        var home = Path.Combine(_root, "data");
        Directory.CreateDirectory(home);

        var noUser = Assert.Throws<DriverException>(() => GitIdentityHttps.AddHttps(home, "github.com", "Work", null, null, null, "t"));
        var empty = Assert.Throws<DriverException>(() => GitIdentityHttps.AddHttps(home, "github.com", "Work", "alice", null, null, ""));
        var missing = Assert.Throws<DriverException>(() => GitIdentityHttps.AddHttps(home, "github.com", "Work", "alice", null, null, null));

        Assert.Contains("(user)", noUser.Message, StringComparison.Ordinal);
        Assert.Contains("(token)", empty.Message, StringComparison.Ordinal);
        Assert.Contains("(token)", missing.Message, StringComparison.Ordinal);
        Assert.Empty(GitIdentities.Read(home));
        Assert.False(Directory.Exists(DaorisHome.Entry(home, HomeEntry.GitTokens)));
        await Task.CompletedTask;
    }
}
