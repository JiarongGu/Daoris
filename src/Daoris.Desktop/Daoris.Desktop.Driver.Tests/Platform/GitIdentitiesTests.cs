using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAUTH1c's tables (D198 points 1, 4 and 5; the git-auth design §3, §4.2 and §14 row 3): the identities' file, which holds no
/// secret; the key folder's access, set before any key byte; the one key file an import reads, and what it refuses; a key's type and
/// fingerprint as OpenSSH writes them; the pointers' resolution, each step and each pointer passed over; the folder's repository; the
/// ssh door's arguments for an identity; Daoris's known hosts and a host trusted only at a press; and the acts, with ssh's tools stood
/// in for. No key, agent, configuration or known-hosts file of the person's is read, and no host is reached.
/// </summary>
public sealed class GitIdentitiesTests : IDisposable
{
    // Public keys made for this table by Windows' ssh-keygen, with the fingerprints its -l printed for them.
    private const string Ed25519 = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIMW8OpV8TxlaO8rBndwxdZUP6WFznyRHY9Callh5332I fixture";
    private const string Ed25519Print = "SHA256:MaZc7lv4JWmcM2yqqj0anWA5tFXcZpnxN29otmsh21k";
    private const string Rsa2048 = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQCrLoBUZ4zqlxuiTSS7RGAcd7HirmyoCTJ+vOIuZs9oHlhANUeDot0zHgl1L/KZhepn3xz2nkyr6CYnwwC7UV2+xgxJ+hRXulU5giikQLiwIjamyDscjEXgGHl4SWGHgpjlVM3eK7+u4ye1DAjTlrtO/2Mr7Nlg58F61dyW0SchS+Cq4XUQnVTvG/vr2VEzR8gtUXc0EnIkKQd7Wawn2sZtd7T9Htrg2kxjgkY0qakM+WuDNSVaPVJRw2g1Xl6nKy04uw6Z4+09HcuQSOHbqYwpu7t+HuFOiKoPp9nPXNfNnnrQxl9ujqvkBJFXc5GryC249zgsIWjkPYAIH5G4pOup r";
    private const string RsaPrint = "SHA256:+R35vyuPICO43oC7ZYpmrzNtuMj95cf1SWYlXyA8iZQ";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-identities-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Func<string, IReadOnlyList<string>, string, Task<GitToolRun>> _runner = GitIdentityActs.Runner;

    private readonly List<(string File, IReadOnlyList<string> Arguments)> _ran = [];

    private string Home => Path.Combine(_root, "data");

    public GitIdentitiesTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose()
    {
        GitIdentityActs.Runner = _runner;
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static GitIdentity Identity(string id, string label, string host, string way = GitIdentity.Ssh, bool isDefault = false) =>
        new(id, label, host, way, null, null, null, "ed25519", Ed25519Print, false, null, isDefault);

    /// <summary>An OpenSSH private key's text around <paramref name="publicLine"/>'s blob, its cipher named; its private part is filler.</summary>
    private static string OpenSshKey(string publicLine, string cipher = "none", uint count = 1)
    {
        static void Put(MemoryStream stream, byte[] bytes)
        {
            stream.Write([(byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length]);
            stream.Write(bytes);
        }

        var body = new MemoryStream();
        body.Write("openssh-key-v1\0"u8);
        Put(body, Encoding.ASCII.GetBytes(cipher));
        Put(body, Encoding.ASCII.GetBytes(cipher == "none" ? "none" : "bcrypt"));
        Put(body, []);
        body.Write([0, 0, 0, (byte)count]);
        Put(body, Convert.FromBase64String(publicLine.Split(' ')[1]));
        Put(body, new byte[64]);
        return "-----BEGIN OPENSSH PRIVATE KEY-----\n" + Convert.ToBase64String(body.ToArray(), Base64FormattingOptions.InsertLineBreaks)
            + "\n-----END OPENSSH PRIVATE KEY-----\n";
    }

    // ── The file (§3.1) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_file_keeps_every_field_and_no_secret()
    {
        var identity = new GitIdentity("gi-0123456789ab", "Work", "github.com", GitIdentity.Ssh, "octo-work", "Work Name", "work@example.invalid",
            "ed25519", Ed25519Print, true, new GitIdentityTest("reached", "octo-work", DateTimeOffset.Parse("2026-10-23T01:02:03Z")), true);

        GitIdentities.Change(Home, _ => [identity]);

        Assert.Equal(identity, Assert.Single(GitIdentities.Read(Home)));
        var text = File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.GitIdentities));
        Assert.EndsWith("data" + Path.DirectorySeparatorChar + "git-identities.json", DaorisHome.Entry(Home, HomeEntry.GitIdentities));
        Assert.DoesNotContain("PRIVATE", text);
        Assert.DoesNotContain("AAAA", text);
    }

    [Fact]
    public void A_file_that_does_not_read_is_said_and_never_written_over()
    {
        var path = DaorisHome.Entry(Home, HomeEntry.GitIdentities);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");

        Assert.Empty(GitIdentities.Read(Home));
        Assert.NotNull(GitIdentities.Problem(Home));
        Assert.Throws<DriverException>(() => GitIdentities.Change(Home, all => all));
        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    [Fact]
    public void An_identity_is_named_by_its_id_or_its_label_in_any_case()
    {
        GitIdentity[] all = [Identity("gi-000000000001", "Work", "github.com"), Identity("gi-000000000002", "Personal", "github.com")];

        Assert.Equal("gi-000000000002", GitIdentities.Named(all, "personal")!.Id);
        Assert.Equal("gi-000000000001", GitIdentities.Named(all, "gi-000000000001")!.Id);
        Assert.Null(GitIdentities.Named(all, "Other"));
        Assert.True(GitIdentities.IsId(GitIdentities.NewId()));
        Assert.False(GitIdentities.IsId("gi-../../x"));
    }

    // ── The key folder (§3.4) ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_key_folder_is_made_private_before_anything_is_in_it_and_never_twice()
    {
        var folder = GitKeys.MakeFolder(Home, "gi-0123456789ab");

        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
        Assert.True(GitKeys.IsPrivate(folder));
        Assert.EndsWith(Path.Combine("profiles", "git", "keys", "gi-0123456789ab"), folder);
        Assert.Contains("key-exists", Assert.Throws<DriverException>(() => GitKeys.MakeFolder(Home, "gi-0123456789ab")).Message);

        // A file written into it takes the folder's access: this account alone.
        File.WriteAllText(Path.Combine(folder, GitKeys.KeyFile), "x");
        if (OperatingSystem.IsWindows())
        {
            var rules = new FileInfo(Path.Combine(folder, GitKeys.KeyFile)).GetAccessControl()
                .GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
            foreach (System.Security.AccessControl.FileSystemAccessRule rule in rules) Assert.Equal(user, rule.IdentityReference);
        }
    }

    [Fact]
    public void An_ordinary_folder_is_not_private()
    {
        var folder = Path.Combine(_root, "open");
        Directory.CreateDirectory(folder);

        Assert.False(GitKeys.IsPrivate(folder));
    }

    // ── A key's type and fingerprint ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Ed25519, "ed25519", Ed25519Print)]
    [InlineData(Rsa2048, "rsa-2048", RsaPrint)]
    public void A_public_key_is_described_as_ssh_keygen_describes_it(string line, string type, string print)
    {
        Assert.Equal((type, print), GitKeys.Describe(line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ssh-ed25519")]
    [InlineData("ssh-ed25519 not-base64!")]
    [InlineData("ssh-dss AAAAB3NzaC1kc3MAAAA=")]
    public void A_line_that_is_no_key_Daoris_takes_is_not_described(string line)
    {
        Assert.Null(GitKeys.Describe(line));
    }

    // ── An import reads the one file named (§3.3, §7.2) ────────────────────────────────────────────

    public static TheoryData<string, string> Refused => new()
    {
        { OpenSshKey(Ed25519, "aes256-ctr"), "import-passphrase" },
        { "-----BEGIN RSA PRIVATE KEY-----\nProc-Type: 4,ENCRYPTED\nAAAA\n-----END RSA PRIVATE KEY-----\n", "import-passphrase" },
        { "-----BEGIN ENCRYPTED PRIVATE KEY-----\nAAAA\n-----END ENCRYPTED PRIVATE KEY-----\n", "import-passphrase" },
        { "-----BEGIN RSA PRIVATE KEY-----\nAAAA\n-----END RSA PRIVATE KEY-----\n", "import-format" },
        { "PuTTY-User-Key-File-3: ssh-ed25519\n", "import-format" },
        { "hello", "import-format" },
        { OpenSshKey(Ed25519, count: 2), "import-format" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void An_import_refuses_a_key_with_a_passphrase_or_in_another_format(string text, string code)
    {
        var (blob, problem) = GitKeys.ReadPrivate(text);

        Assert.Null(blob);
        Assert.EndsWith($"({code})", problem);
    }

    [Fact]
    public void An_import_reads_an_OpenSSH_key_without_a_passphrase_and_its_public_part()
    {
        var file = Path.Combine(_root, "mine");
        File.WriteAllText(file, OpenSshKey(Ed25519));

        var read = GitKeys.ReadImport(file, "Work");

        Assert.Equal(("ed25519", Ed25519Print), (read.Type, read.Fingerprint));
        Assert.Equal(Ed25519.Replace(" fixture", " Work"), read.PublicLine);
        Assert.Equal(File.ReadAllBytes(file), read.Bytes);
    }

    [Fact]
    public void An_import_names_its_file_by_a_whole_path_that_is_there()
    {
        Assert.Contains("import-path", Assert.Throws<DriverException>(() => GitKeys.ReadImport("key", "W")).Message);
        Assert.Contains("import-missing", Assert.Throws<DriverException>(() => GitKeys.ReadImport(Path.Combine(_root, "none"), "W")).Message);
    }

    [Fact]
    public async Task An_imported_identity_is_kept_in_its_private_folder_and_is_its_hosts_first_default()
    {
        var file = Path.Combine(_root, "mine");
        File.WriteAllText(file, OpenSshKey(Ed25519));

        var identity = await GitIdentityActs.AddSshAsync(Home, "GitHub.com", "Work", "Work Name", "work@example.invalid", file, rsa: false, CancellationToken.None);

        Assert.Equal(("github.com", "ed25519", Ed25519Print, true), (identity.Host, identity.KeyType, identity.Fingerprint, identity.Default));
        Assert.Equal(File.ReadAllBytes(file), File.ReadAllBytes(GitKeys.KeyPath(Home, identity.Id)));
        Assert.True(GitKeys.IsPrivate(GitKeys.Folder(Home, identity.Id)));
        Assert.StartsWith("ssh-ed25519 ", GitKeys.PublicKey(Home, identity.Id));
        Assert.True(File.Exists(GitSshFiles.Config(Home)));
        Assert.Equal(identity, Assert.Single(GitIdentities.Read(Home)));

        var second = await GitIdentityActs.AddSshAsync(Home, "github.com", "Personal", null, null, file, rsa: false, CancellationToken.None);
        Assert.False(second.Default);
        Assert.Contains("label-taken", (await Assert.ThrowsAsync<DriverException>(() =>
            GitIdentityActs.AddSshAsync(Home, "github.com", "work", null, null, file, rsa: false, CancellationToken.None))).Message);
    }

    [Fact]
    public async Task A_refused_import_leaves_nothing_behind()
    {
        var file = Path.Combine(_root, "locked");
        File.WriteAllText(file, OpenSshKey(Ed25519, "aes256-ctr"));

        await Assert.ThrowsAsync<DriverException>(() => GitIdentityActs.AddSshAsync(Home, "github.com", "Work", null, null, file, false, CancellationToken.None));

        Assert.Empty(GitIdentities.Read(Home));
        Assert.False(Directory.Exists(DaorisHome.Entry(Home, HomeEntry.GitKeys)) && Directory.EnumerateDirectories(DaorisHome.Entry(Home, HomeEntry.GitKeys)).Any());
    }

    [Fact]
    public async Task A_generated_key_is_made_by_ssh_keygen_into_the_private_folder_RSA_where_the_host_takes_RSA_alone()
    {
        GitIdentityActs.Runner = (file, arguments, folder) =>
        {
            _ran.Add((file, arguments));
            Assert.True(GitKeys.IsPrivate(folder));
            var key = arguments[arguments.Count - 1];
            File.WriteAllText(key, "private");
            File.WriteAllText(key + ".pub", (arguments.Contains("rsa") ? Rsa2048 : Ed25519) + "\n");
            return Task.FromResult(new GitToolRun(0, "", ""));
        };

        var github = await GitIdentityActs.AddSshAsync(Home, "github.com", "Work", null, null, null, rsa: false, CancellationToken.None);
        var azure = await GitIdentityActs.AddSshAsync(Home, "ssh.dev.azure.com", "Org", null, null, null, rsa: false, CancellationToken.None);

        Assert.Equal("ed25519", github.KeyType);
        Assert.Equal(("dev.azure.com", "rsa-2048"), (azure.Host, azure.KeyType));
        Assert.EndsWith("ssh-keygen" + (OperatingSystem.IsWindows() ? ".exe" : ""), _ran[0].File);
        Assert.Equal(["-t", "ed25519", "-q", "-N", "", "-C", "Work", "-f", GitKeys.KeyPath(Home, github.Id)], _ran[0].Arguments);
        Assert.Equal(["-t", "rsa", "-b", "4096"], _ran[1].Arguments.Take(4));
    }

    // ── Hosts ───────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GitHub.com", "github.com")]
    [InlineData("git@github.com", "github.com")]
    [InlineData("ssh.dev.azure.com", "dev.azure.com")]
    [InlineData("vs-ssh.visualstudio.com", "dev.azure.com")]
    [InlineData("git.example.com:22", "git.example.com")]
    [InlineData("git.example.com:2222", "git.example.com:2222")]
    public void A_host_is_compared_by_its_canonical_name(string host, string canonical)
    {
        Assert.Equal(canonical, GitHosts.Canonical(host));
    }

    [Fact]
    public void An_identitys_ssh_connection_goes_to_its_hosts_ssh_host()
    {
        Assert.Equal(("ssh.dev.azure.com", (string?)null), GitHosts.SshTarget("dev.azure.com"));
        Assert.Equal(("git.example.com", "2222"), GitHosts.SshTarget("git.example.com:2222"));
        Assert.Equal("git.example.com:2222", GitHosts.Of("git@git.example.com", "2222"));
        Assert.Equal("github.com", GitHosts.Of("git@github.com", null));
    }

    [Theory]
    [InlineData("not a host")]
    [InlineData("-oProxyCommand=x")]
    [InlineData("host/path")]
    public void A_host_that_is_no_host_is_refused(string host)
    {
        Assert.Contains("(host)", Assert.Throws<DriverException>(() => GitIdentityActs.HostOf(host)).Message);
    }

    // ── The resolution (§3.2) ───────────────────────────────────────────────────────────────────────

    private static readonly GitIdentity[] Set =
    [
        Identity("gi-00000000000a", "Work", "github.com"),
        Identity("gi-00000000000b", "Personal", "github.com", isDefault: true),
        Identity("gi-00000000000c", "Lab", "gitlab.com"),
        Identity("gi-00000000000d", "Web", "github.com", GitIdentity.Https),
    ];

    private static GitIdentityUse.Pointers Pointers(string json) => GitIdentityUse.Of((JsonObject)JsonNode.Parse(json)!);

    public static TheoryData<string, string?, string[], string, string, string?, int> Resolutions => new()
    {
        // pointers, workspace, remotes at the address, host, served, source, passed over
        { "{}", "games", ["origin"], "github.com", "gi-00000000000b", "default", 0 },
        { "{}", "games", ["origin"], "gitlab.com", "none", null, 0 },
        { """{"gitIdentity":{"engine":{"origin":"gi-00000000000a"}}}""", "games", ["origin"], "github.com", "gi-00000000000a", "repository", 0 },
        { """{"gitIdentity":{"engine":{"upstream":"gi-00000000000a"}}}""", "games", ["origin"], "github.com", "gi-00000000000b", "default", 0 },
        { """{"gitIdentity":{"engine":{"origin":"own"}},"workspaceGitIdentity":{"games":{"github.com":"gi-00000000000a"}}}""", "games", ["origin"], "github.com", "own", "repository", 0 },
        { """{"workspaceGitIdentity":{"games":{"github.com":"gi-00000000000a"}}}""", "games", ["origin"], "github.com", "gi-00000000000a", "workspace", 0 },
        { """{"workspaceGitIdentity":{"games":{"GitHub.com":"gi-00000000000a"}}}""", "games", ["origin"], "github.com", "gi-00000000000a", "workspace", 0 },
        { """{"workspaceGitIdentity":{"games":{"github.com":"gi-00000000000a"}}}""", "other", ["origin"], "github.com", "gi-00000000000b", "default", 0 },
        { """{"machineGitIdentity":{"github.com":"own"}}""", "games", ["origin"], "github.com", "own", "machine", 0 },
        { """{"machineGitIdentity":{"github.com":"gi-00000000000a"}}""", "games", ["origin"], "github.com", "gi-00000000000b", "default", 0 },
        // Passed over, never read as none above it: a gone identity, another host's, another way's.
        { """{"gitIdentity":{"engine":{"origin":"gi-0000000000ff"}},"workspaceGitIdentity":{"games":{"github.com":"gi-00000000000a"}}}""", "games", ["origin"], "github.com", "gi-00000000000a", "workspace", 1 },
        { """{"gitIdentity":{"engine":{"origin":"gi-00000000000c"}}}""", "games", ["origin"], "github.com", "gi-00000000000b", "default", 1 },
        { """{"gitIdentity":{"engine":{"origin":"gi-00000000000d"}}}""", "games", ["origin"], "github.com", "gi-00000000000b", "default", 1 },
        // Two remotes at one address: either's pointer serves.
        { """{"gitIdentity":{"engine":{"mirror":"gi-00000000000a"}}}""", "games", ["origin", "mirror"], "github.com", "gi-00000000000a", "repository", 0 },
    };

    [Theory]
    [MemberData(nameof(Resolutions))]
    public void Each_step_of_the_resolution_answers_with_its_source(
        string json, string? workspace, string[] remotes, string host, string served, string? source, int passed)
    {
        var answer = GitIdentityUse.Resolve(Set, Pointers(json), "engine", workspace, remotes, host, GitIdentity.Ssh);

        Assert.Equal((served, source, passed), (answer.Served, answer.Source, answer.Passed.Count));
    }

    [Fact]
    public void A_folder_no_repository_Daoris_knows_resolves_by_the_machines_steps_alone()
    {
        var pointers = Pointers("""{"gitIdentity":{"engine":{"origin":"gi-00000000000a"}},"workspaceGitIdentity":{"games":{"github.com":"gi-00000000000a"}}}""");

        Assert.Equal("gi-00000000000b", GitIdentityUse.Resolve(Set, pointers, null, null, [], "github.com", GitIdentity.Ssh).Served);
    }

    [Fact]
    public void A_pointer_is_written_and_cleared_and_an_emptied_scope_goes()
    {
        var root = new JsonObject { ["access"] = "full" };

        GitIdentityUse.Set(root, GitIdentityUse.RepositoryKey, "engine", "origin", "gi-00000000000a");
        GitIdentityUse.Set(root, GitIdentityUse.MachineKey, "github.com", null, GitIdentityUse.Own);
        Assert.Equal("""{"access":"full","gitIdentity":{"engine":{"origin":"gi-00000000000a"}},"machineGitIdentity":{"github.com":"own"}}""", root.ToJsonString());

        GitIdentityUse.Set(root, GitIdentityUse.RepositoryKey, "engine", "origin", null);
        GitIdentityUse.Set(root, GitIdentityUse.MachineKey, "github.com", null, null);
        Assert.Equal("""{"access":"full"}""", root.ToJsonString());
    }

    // ── Which repository a folder is (§4.2 step 1) ──────────────────────────────────────────────────

    [Fact]
    public void A_sessions_tree_is_its_repositorys_by_the_layout_and_a_checkout_by_the_registrys_roots()
    {
        var tree = Path.Combine(DaorisHome.Entry(Home, HomeEntry.Trees), "games", "engine", "s-1234", "src");
        var checkout = Path.Combine(_root, "code", "engine");
        var nested = Path.Combine(_root, "code", "engine", "vendor", "lib");
        GitFolders.Remember(Home, [(checkout, "engine", "games"), (nested, "lib", "games")]);

        Assert.Equal(("engine", "games"), GitFolders.RepositoryOf(Home, tree));
        Assert.Equal(("engine", "games"), GitFolders.RepositoryOf(Home, Path.Combine(checkout, "src")));
        Assert.Equal(("lib", "games"), GitFolders.RepositoryOf(Home, Path.Combine(nested, "x")));
        Assert.Equal(((string?)null, (string?)null), GitFolders.RepositoryOf(Home, Path.Combine(_root, "elsewhere")));
        Assert.Equal(((string?)null, (string?)null), GitFolders.RepositoryOf(Home, Path.Combine(_root, "code", "engine-other")));
    }

    // ── The ssh door's arguments for an identity (§4.2 step 3) ──────────────────────────────────────

    [Fact]
    public void An_identitys_ssh_is_handed_batch_mode_first_then_its_key_alone_Daoris_known_hosts_then_gits_own()
    {
        var (call, _) = GitDoors.ReadSsh(["-o", "SendEnv=GIT_PROTOCOL", "-p", "2222", "git@git.example.com", "git-upload-pack '/org/engine.git'"]);
        var config = GitSshFiles.Slashed(GitSshFiles.Config(Home));
        var key = GitSshFiles.Slashed(GitKeys.KeyPath(Home, "gi-00000000000a"));

        var arguments = GitDoors.IdentityArguments(Home, "gi-00000000000a", call!);

        Assert.Equal(
            [
                "-o", "BatchMode=yes", "-F", config, "-i", key, "-o", "IdentitiesOnly=yes", "-o", "IdentityAgent=none",
                "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=" + GitSshFiles.Option(GitSshFiles.KnownHosts(Home)),
                "-o", "ForwardAgent=no", "-o", "SendEnv=GIT_PROTOCOL", "-p", "2222", "git@git.example.com", "git-upload-pack '/org/engine.git'",
            ],
            arguments);
        Assert.Equal("2222", GitDoors.PortOf(call!.Options));
        Assert.DoesNotContain(arguments, word => word.Contains(".ssh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_path_with_a_space_is_quoted_inside_an_option_as_ssh_reads_it()
    {
        Assert.Equal("\"C:/Daoris home/known_hosts\"", GitSshFiles.Option(@"C:\Daoris home\known_hosts"));
        Assert.Equal("C:/home/known_hosts", GitSshFiles.Option(@"C:\home\known_hosts"));
    }

    [Fact]
    public void The_door_serves_an_act_from_the_files_alone()
    {
        GitIdentities.Change(Home, _ => [Identity("gi-00000000000a", "Work", "github.com", isDefault: true)]);
        var (call, _) = GitDoors.ReadSsh(["git@github.com", "git-receive-pack 'org/engine.git'"]);

        var served = GitDoors.Serving(Home, _root, call!, _ => null);

        Assert.Equal(("gi-00000000000a", "default"), (served.Served, served.Source));
        Assert.Equal("none", GitDoors.Serving(Home, _root, GitDoors.ReadSsh(["git@gitlab.com", "git-upload-pack 'a/b'"]).Call!, _ => null).Served);
    }

    [Fact]
    public void Daoris_configuration_holds_every_option_the_door_gives_and_writes_no_host_key_itself()
    {
        GitSshFiles.Ensure(Home);
        var text = File.ReadAllText(GitSshFiles.Config(Home));

        foreach (var line in new[] { "BatchMode yes", "IdentitiesOnly yes", "IdentityAgent none", "StrictHostKeyChecking yes", "UpdateHostKeys no", "GlobalKnownHostsFile none" })
            Assert.Contains("\t" + line + "\n", text);
        Assert.True(File.Exists(GitSshFiles.KnownHosts(Home)));
        Assert.Equal("", File.ReadAllText(GitSshFiles.KnownHosts(Home)));
    }

    // ── Host keys: asked, then written, never silently (§3.5) ───────────────────────────────────────

    [Fact]
    public void A_trusted_key_replaces_every_other_key_its_host_had_and_keeps_other_hosts()
    {
        GitSshFiles.Trust(Home, "github.com", "ssh-ed25519 AAAAold");
        GitSshFiles.Trust(Home, "[git.example.com]:2222", "ssh-ed25519 AAAAport");
        GitSshFiles.Trust(Home, "github.com", "ssh-ed25519 AAAAnew");

        Assert.Equal(["ssh-ed25519 AAAAnew"], GitSshFiles.Known(Home, "github.com"));
        Assert.Equal(["ssh-ed25519 AAAAport"], GitSshFiles.Known(Home, GitSshFiles.KnownName("git.example.com", "2222")));
        Assert.Equal("github.com", GitSshFiles.KnownName("github.com", "22"));
    }

    [Fact]
    public void The_shipped_list_carries_the_public_hosts_published_fingerprints()
    {
        var list = GitHostKeys.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "resources.json")))!;

        Assert.Contains("SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU", list["github.com"]);
        Assert.Contains("SHA256:eUXGGm1YGsMAS7vkcx6JOJdOGHPem5gQp4taiCfCLB8", list["gitlab.com"]);
        Assert.Equal(["SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og"], list["dev.azure.com"]);
        Assert.Null(GitHostKeys.Parse("""{"schema":1,"tools":{}}"""));
    }

    [Fact]
    public void A_scanned_key_is_described_and_compared_with_the_published_list()
    {
        var keys = GitIdentityActs.HostKeys($"# github.com:22 SSH-2.0-x\ngithub.com {Ed25519}\ngithub.com {Rsa2048}\n", [Ed25519Print]);

        Assert.Equal([(Ed25519Print, (bool?)true), (RsaPrint, false)], keys.Select(each => (each.Fingerprint, each.Published)));
        Assert.Null(Assert.Single(GitIdentityActs.HostKeys($"host {Ed25519}\n", null)).Published);
    }

    public static TheoryData<int, string, string, string, string?> Greetings => new()
    {
        { 1, "", "Hi octo-work! You've successfully authenticated, but GitHub does not provide shell access.", "reached", "octo-work" },
        { 0, "Welcome to GitLab, @octo!", "", "reached", "octo" },
        { 1, "", "remote: Shell access is not supported.\nshell request failed on channel 0", "reached", null },
        { 255, "", "git@github.com: Permission denied (publickey).", "refused", null },
        { 255, "", "Host key verification failed.", "host-unknown", null },
        { 255, "", "@@@ WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED! @@@", "host-changed", null },
        { 255, "", "ssh: connect to host github.com port 22: Connection refused", "unreachable", null },
    };

    [Theory]
    [MemberData(nameof(Greetings))]
    public void A_hosts_greeting_is_read_as_a_tests_answer(int code, string output, string error, string result, string? user)
    {
        var answer = GitIdentityActs.Greeting(new GitToolRun(code, output, error));

        Assert.Equal((result, user), (answer.Result, answer.User));
    }

    private async Task<GitIdentity> ImportedAsync(string label = "Work", string host = "github.com")
    {
        var file = Path.Combine(_root, label);
        File.WriteAllText(file, OpenSshKey(Ed25519));
        return await GitIdentityActs.AddSshAsync(Home, host, label, null, null, file, rsa: false, CancellationToken.None);
    }

    [Fact]
    public async Task A_test_of_a_host_never_trusted_shows_its_keys_and_connects_nowhere_until_the_person_names_one()
    {
        var identity = await ImportedAsync();
        GitIdentityActs.Runner = (file, arguments, _) =>
        {
            _ran.Add((file, arguments));
            return Task.FromResult(Path.GetFileNameWithoutExtension(file) == "ssh-keyscan"
                ? new GitToolRun(0, $"github.com {Ed25519}\n", "")
                : new GitToolRun(1, "", "Hi octo-work! You've successfully authenticated, but GitHub does not provide shell access."));
        };

        var first = await GitIdentityActs.TestAsync(Home, "Work", null, CancellationToken.None);
        Assert.Equal("host-unknown", first.Result);
        Assert.Equal(Ed25519Print, Assert.Single(first.HostKeys).Fingerprint);
        Assert.DoesNotContain(_ran, run => Path.GetFileNameWithoutExtension(run.File) == "ssh");
        Assert.Empty(GitSshFiles.Known(Home, "github.com"));

        Assert.Contains("trust-mismatch", (await Assert.ThrowsAsync<DriverException>(() =>
            GitIdentityActs.TestAsync(Home, "Work", RsaPrint, CancellationToken.None))).Message);
        Assert.Empty(GitSshFiles.Known(Home, "github.com"));

        var trusted = await GitIdentityActs.TestAsync(Home, "Work", Ed25519Print, CancellationToken.None);
        Assert.Equal(("reached", "octo-work"), (trusted.Result, trusted.User));
        Assert.Equal([string.Join(' ', Ed25519.Split(' ')[..2])], GitSshFiles.Known(Home, "github.com"));
        var ssh = _ran.Last();
        Assert.Equal("ssh", Path.GetFileNameWithoutExtension(ssh.File));
        Assert.Equal(["-o", "BatchMode=yes"], ssh.Arguments.Take(2));
        Assert.Equal("git@github.com", ssh.Arguments[^1]);

        var kept = GitIdentities.Find(Home, identity.Id)!;
        Assert.Equal(("reached", "octo-work"), (kept.Test!.Result, kept.User));
        Assert.NotNull(GitSshFiles.LastUsed(Home, identity.Id));
    }

    // ── Pointers and the rest of the family (§8) ────────────────────────────────────────────────────

    [Fact]
    public async Task Two_remotes_at_one_address_must_point_at_one_identity()
    {
        var work = await ImportedAsync("Work");
        var personal = await ImportedAsync("Personal");
        var addresses = new Dictionary<string, string?>
        {
            ["origin"] = "git@github.com:org/engine.git",
            ["mirror"] = "ssh://git@github.com/org/engine",
            ["upstream"] = "git@github.com:other/engine.git",
        };

        GitIdentityActs.UseRepository(Home, "engine", "origin", "Work", addresses);
        GitIdentityActs.UseRepository(Home, "engine", "upstream", "Personal", addresses);
        Assert.Contains("same-address", Assert.Throws<DriverException>(() => GitIdentityActs.UseRepository(Home, "engine", "mirror", "Personal", addresses)).Message);
        GitIdentityActs.UseRepository(Home, "engine", "mirror", "Work", addresses);
        Assert.Contains("no-remote", Assert.Throws<DriverException>(() => GitIdentityActs.UseRepository(Home, "engine", "fork", "Work", addresses)).Message);

        var pointers = GitIdentityUse.Read(Home);
        Assert.Equal(work.Id, pointers.Repository["engine"]["origin"]);
        Assert.Equal(personal.Id, pointers.Repository["engine"]["upstream"]);
    }

    [Fact]
    public async Task A_pointer_names_an_identity_of_its_hosts_or_own()
    {
        await ImportedAsync("Lab", "gitlab.com");

        Assert.Contains("host-mismatch", Assert.Throws<DriverException>(() => GitIdentityActs.UseWorkspace(Home, "games", "github.com", "Lab")).Message);
        GitIdentityActs.UseWorkspace(Home, "games", "GitHub.com", GitIdentityUse.Own);
        GitIdentityActs.UseMachine(Home, "github.com", own: true);

        var pointers = GitIdentityUse.Read(Home);
        Assert.Equal("own", pointers.Workspace["games"]["github.com"]);
        Assert.Equal("own", pointers.Machine["github.com"]);
    }

    [Fact]
    public async Task Driver_json_keeps_every_key_a_pointer_has_no_part_in()
    {
        var path = DaorisHome.Entry(Home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"access":{"engine":"full"},"maxSessions":3}""");
        await ImportedAsync();

        GitIdentityActs.UseWorkspace(Home, "games", "github.com", "Work");

        var root = (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(3, (int)root["maxSessions"]!);
        Assert.Equal("full", (string?)root["access"]!["engine"]);
        Assert.NotNull(root["workspaceGitIdentity"]);
    }

    [Fact]
    public async Task Removing_an_identity_clears_every_pointer_at_it_and_deletes_its_key()
    {
        var work = await ImportedAsync("Work");
        GitIdentityActs.UseWorkspace(Home, "games", "github.com", "Work");
        GitIdentityActs.UseRepository(Home, "engine", "origin", "Work", new Dictionary<string, string?> { ["origin"] = "git@github.com:org/engine.git" });

        var pointing = GitIdentityActs.Remove(Home, "Work");

        Assert.Equal(["engine's origin", "workspace games's github.com"], pointing);
        Assert.Equal(0, GitIdentityUse.Read(Home).Count);
        Assert.Empty(GitIdentities.Read(Home));
        Assert.False(Directory.Exists(GitKeys.Folder(Home, work.Id)));
    }

    [Fact]
    public async Task A_hosts_default_is_one_identity_of_that_host()
    {
        var work = await ImportedAsync("Work");
        var personal = await ImportedAsync("Personal");
        await ImportedAsync("Lab", "gitlab.com");

        GitIdentityActs.Default(Home, "github.com", "Personal");
        Assert.Equal([personal.Id], GitIdentities.Read(Home).Where(each => each.Default && each.Host == "github.com").Select(each => each.Id));
        Assert.Contains("host-mismatch", Assert.Throws<DriverException>(() => GitIdentityActs.Default(Home, "github.com", "Lab")).Message);
        GitIdentityActs.Default(Home, "github.com", null);
        Assert.DoesNotContain(GitIdentities.Read(Home), each => each.Default && each.Host == "github.com");
        Assert.True(GitIdentities.Find(Home, (await Task.FromResult(work)).Id) is { Default: false });
    }

    [Fact]
    public async Task Rename_commits_as_and_read_only_change_the_one_identity()
    {
        await ImportedAsync("Work");

        GitIdentityActs.Rename(Home, "Work", "Day job");
        GitIdentityActs.CommitsAs(Home, "Day job", "Work Name", "work@example.invalid");
        GitIdentityActs.ReadOnly(Home, "day job", on: true);
        Assert.Contains("commits-as", Assert.Throws<DriverException>(() => GitIdentityActs.CommitsAs(Home, "Day job", "Only a name", null)).Message);

        var kept = Assert.Single(GitIdentities.Read(Home));
        Assert.Equal(("Day job", "Work Name", "work@example.invalid", true), (kept.Label, kept.CommitsName, kept.CommitsEmail, kept.ReadOnly));
        GitIdentityActs.CommitsAs(Home, "Day job", null, null);
        Assert.Null(GitIdentities.Read(Home)[0].CommitsName);
    }
}
