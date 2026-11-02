using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A repository's git remotes with what serves each (GITAUTH1e, the git-auth design §3.2 and §7.3), read through the one reader of an
/// address: the repository's own choice, its workspace's, the host's default, nothing, the person's own sign-ins, and a local path.
/// </summary>
/// <remarks>Against a real git in a scratch checkout, its system and global files kept from the person's. Alone: those are the process's variables.</remarks>
[Collection(Alone.Name)]
public sealed class GitRemoteRowsTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-remote-rows", Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "data");

    private string Repo => Path.Combine(_root, "engine");

    public GitRemoteRowsTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
        var empty = Path.Combine(_root, "global");
        File.WriteAllText(empty, "");
        foreach (var (name, value) in new[] { ("GIT_CONFIG_NOSYSTEM", "1"), ("GIT_CONFIG_GLOBAL", empty) })
        {
            _was[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _was) Environment.SetEnvironmentVariable(name, value);
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private async Task Git(params string[] arguments)
    {
        var (code, _, error) = await WorkingTree.GitAsync(Repo, arguments, CancellationToken.None);
        Assert.True(code == 0, error);
    }

    private static GitIdentity Identity(string id, string label, string host, string way, bool isDefault = false) =>
        new(id, label, host, way, null, null, null, null, null, false, null, isDefault);

    [Fact]
    public async Task Each_remote_says_what_serves_it_and_where_that_came_from()
    {
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", "git@github.com:org-a/engine.git");
        await Git("remote", "add", "upstream", "https://github.com/upstream/engine.git");
        await Git("remote", "add", "deploy", "git@ssh.dev.azure.com:v3/org-a/engine");
        await Git("remote", "add", "backup", "../engine-backup.git");
        GitIdentities.Change(Home, _ =>
        [
            Identity("gi-00000000000a", "Work", "github.com", GitIdentity.Ssh),
            Identity("gi-00000000000b", "Personal", "github.com", GitIdentity.Https, isDefault: true),
        ]);
        GitIdentityActs.UseWorkspace(Home, "games", "github.com", "gi-00000000000a");

        var rows = (await GitRemoteRows.ReadAsync(Home, Repo, "engine", "games", CancellationToken.None)).ToDictionary(row => row.Name);

        Assert.Equal(("identity", "Work", "workspace"), (rows["origin"].Serves, rows["origin"].Label, rows["origin"].Source));
        Assert.Equal(("identity", "Personal", "default"), (rows["upstream"].Serves, rows["upstream"].Label, rows["upstream"].Source));
        Assert.Equal(("none", "dev.azure.com", "ssh"), (rows["deploy"].Serves, rows["deploy"].Host, rows["deploy"].Way));
        Assert.Equal(("local", null), (rows["backup"].Serves, rows["backup"].Host));
    }

    [Fact]
    public async Task A_remote_chosen_here_wins_and_the_persons_own_sign_ins_are_marked_as_such()
    {
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", "git@github.com:org-a/engine.git");
        GitIdentities.Change(Home, _ => [Identity("gi-00000000000a", "Work", "github.com", GitIdentity.Ssh, isDefault: true)]);
        GitIdentityActs.UseRepository(Home, "engine", "origin", GitIdentityUse.Own, new Dictionary<string, string?> { ["origin"] = "git@github.com:org-a/engine.git" });

        var origin = Assert.Single(await GitRemoteRows.ReadAsync(Home, Repo, "engine", null, CancellationToken.None));

        Assert.Equal(("own", "repository", GitIdentityUse.Own), (origin.Serves, origin.Source, origin.Chosen));
    }

    [Fact]
    public async Task A_choice_of_an_identity_that_is_gone_is_passed_over_and_said()
    {
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", "git@github.com:org-a/engine.git");
        GitIdentities.Change(Home, _ => [Identity("gi-00000000000a", "Work", "github.com", GitIdentity.Ssh)]);
        GitIdentityActs.UseRepository(Home, "engine", "origin", "gi-00000000000a", new Dictionary<string, string?> { ["origin"] = "git@github.com:org-a/engine.git" });
        GitIdentities.Change(Home, _ => []);

        var origin = Assert.Single(await GitRemoteRows.ReadAsync(Home, Repo, "engine", null, CancellationToken.None));

        Assert.Equal("none", origin.Serves);
        Assert.Contains("gone", Assert.Single(origin.Passed), StringComparison.Ordinal);
    }
}
