using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The terminal's git identities (GITAUTH1c; the git-auth design §8): the words each verb takes, the lease a change holds, a session's
/// refusal, the widenings held for the window, and what the verbs say. ssh's tools are never started here: an import needs none.
/// </summary>
public sealed class GitIdentityCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-identity-command-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    public GitIdentityCommandTests()
    {
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private GitIdentityWorld World(bool window = false, string? session = null) => new(
        Home,
        () => Task.FromResult<IReadOnlyList<(string, string, string)>>([]),
        () => Task.FromResult(window),
        name => name == DriverChoicesCommand.SessionVariable ? session : null);

    private async Task<(int Code, string Out, string Error)> Run(GitIdentityWorld world, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await GitIdentityCommand.RunAsync(args, world, output, error);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>An unencrypted OpenSSH key file around a fixture's public key, as the identities' table builds one.</summary>
    private string KeyFile()
    {
        static void Put(MemoryStream stream, byte[] bytes)
        {
            stream.Write([(byte)(bytes.Length >> 24), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 8), (byte)bytes.Length]);
            stream.Write(bytes);
        }

        var body = new MemoryStream();
        body.Write("openssh-key-v1\0"u8);
        Put(body, "none"u8.ToArray());
        Put(body, "none"u8.ToArray());
        Put(body, []);
        body.Write([0, 0, 0, 1]);
        Put(body, Convert.FromBase64String("AAAAC3NzaC1lZDI1NTE5AAAAIMW8OpV8TxlaO8rBndwxdZUP6WFznyRHY9Callh5332I"));
        Put(body, new byte[64]);
        var file = Path.Combine(_root, "id_fixture");
        File.WriteAllText(file, GitIdentitiesTests.Pem("OPENSSH PRIVATE KEY", Convert.ToBase64String(body.ToArray())), Encoding.ASCII);
        return file;
    }

    [Theory]
    [InlineData(new[] { "identity", "list" }, false)]
    [InlineData(new[] { "identity", "public-key", "Work" }, false)]
    [InlineData(new[] { "identity", "add-ssh", "github.com", "--label", "Work" }, true)]
    [InlineData(new[] { "identity", "test", "Work" }, true)]
    [InlineData(new[] { "default", "github.com", "Work" }, true)]
    [InlineData(new[] { "use", "engine", "Work" }, true)]
    [InlineData(new[] { "branches" }, false)]
    [InlineData(new[] { "identity", "add-ssh", "--help" }, false)]
    public void A_change_holds_the_homes_lease_and_a_read_none(string[] words, bool holds)
    {
        Assert.Equal(holds, GitIdentityCommand.Changes(words));
        Assert.Equal(holds, VerbLeases.Holds(["git", .. words]));
    }

    [Theory]
    [InlineData(new[] { "identity", "add-ssh", "github.com", "--label", "W", "--import", "C:/k" }, true)]
    [InlineData(new[] { "identity", "add-ssh", "github.com", "--label", "W" }, false)]
    [InlineData(new[] { "identity", "test", "W", "--trust-host", "SHA256:x" }, true)]
    [InlineData(new[] { "identity", "test", "W" }, false)]
    [InlineData(new[] { "identity", "read-only", "W", "off" }, true)]
    [InlineData(new[] { "identity", "read-only", "W", "on" }, false)]
    [InlineData(new[] { "identity", "remove", "W" }, true)]
    [InlineData(new[] { "use", "engine", "own" }, true)]
    [InlineData(new[] { "use", "--machine", "--host", "github.com", "own" }, true)]
    [InlineData(new[] { "use", "engine", "W" }, false)]
    [InlineData(new[] { "use", "engine", "--clear" }, false)]
    [InlineData(new[] { "default", "github.com", "W" }, false)]
    public void The_widenings_are_named(string[] args, bool widens)
    {
        var flags = GitIdentityCommand.Flags(args, out var words);

        Assert.Equal(widens, GitIdentityCommand.Widens(words, flags));
    }

    [Fact]
    public async Task A_change_from_a_sessions_terminal_is_refused_and_does_nothing()
    {
        var (code, _, error) = await Run(World(session: "s-1"), "identity", "add-ssh", "github.com", "--label", "Work", "--import", KeyFile());

        Assert.Equal(1, code);
        Assert.Contains("DAORIS_SESSION_ID", error);
        Assert.Empty(GitIdentities.Read(Home));
    }

    [Fact]
    public async Task A_widening_while_the_window_runs_waits_for_it_and_does_nothing_here()
    {
        var (code, _, error) = await Run(World(window: true), "identity", "add-ssh", "github.com", "--label", "Work", "--import", KeyFile());

        Assert.Equal(1, code);
        Assert.Contains("(window-first)", error);
        Assert.Empty(GitIdentities.Read(Home));
    }

    [Fact]
    public async Task An_import_adds_the_identity_and_says_its_public_key_and_where_it_goes()
    {
        var (code, output, error) = await Run(World(), "identity", "add-ssh", "github.com", "--label", "Work", "--import", KeyFile(),
            "--name", "Work Name", "--email", "work@example.invalid");

        Assert.True(code == 0, error);
        var identity = Assert.Single(GitIdentities.Read(Home));
        Assert.Contains($"git: Work added for github.com: SSH ed25519 {identity.Fingerprint}, github.com's default.", output);
        Assert.Contains("https://github.com/settings/keys", output);
        Assert.Contains("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIMW8OpV8TxlaO8rBndwxdZUP6WFznyRHY9Callh5332I Work", output);
        Assert.DoesNotContain("PRIVATE", output);

        var listed = await Run(World(), "identity", "list");
        Assert.Contains("Work  github.com · SSH ed25519", listed.Out);
        Assert.Contains("commits as Work Name <work@example.invalid>", listed.Out);
        var json = await Run(World(), "identity", "list", "--json");
        Assert.Contains("\"fingerprint\": \"" + identity.Fingerprint + "\"", json.Out);
        Assert.Equal((0, identity.Id), ((await Run(World(), "identity", "public-key", "work")).Code, identity.Id));
    }

    [Fact]
    public async Task A_refusal_is_one_line_with_its_code()
    {
        var (code, _, error) = await Run(World(), "identity", "rename", "Nobody", "Other");

        Assert.Equal(1, code);
        Assert.Equal("git: no git identity is named Nobody (no-identity)", error.Trim());
        Assert.Equal(2, (await Run(World(), "identity", "frobnicate")).Code);
        Assert.Contains("takes a value", (await Run(World(), "identity", "add-ssh", "github.com", "--label")).Error);
    }

    [Fact]
    public async Task The_machine_chooses_own_or_nothing_and_a_workspace_any_identity_of_the_host()
    {
        await Run(World(), "identity", "add-ssh", "github.com", "--label", "Work", "--import", KeyFile());

        Assert.Equal(0, (await Run(World(), "use", "--machine", "--host", "github.com", "own")).Code);
        Assert.Equal(1, (await Run(World(), "use", "--machine", "--host", "github.com", "Work")).Code);
        Assert.Equal(0, (await Run(World(), "use", "--workspace", "games", "--host", "github.com", "Work")).Code);
        Assert.Equal(1, (await Run(World(), "use", "nowhere", "Work")).Code);

        var pointers = GitIdentityUse.Read(Home);
        Assert.Equal("own", pointers.Machine["github.com"]);
        Assert.Equal(GitIdentities.Read(Home)[0].Id, pointers.Workspace["games"]["github.com"]);
    }
}
