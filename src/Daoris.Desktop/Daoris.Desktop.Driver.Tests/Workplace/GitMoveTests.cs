using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The move off ambient credentials (GITAUTH1e, the git-auth design §6.8): a home beside the laid doors is marked once; before it a remote
/// nothing serves is not held, after it an SSH remote nothing serves is held as <c>no-identity</c> before the act and an HTTPS refusal is
/// classed so; a serving identity or the person's own choice lifts the hold; the ssh door refuses a host nothing serves in one line. A
/// home with no doors laid is never marked, and a marked one holds nothing once its doors are gone (GITMOVED1, D233 choice 7).
/// </summary>
[Collection(Alone.Name)]
public sealed class GitMoveTests : IDisposable
{
    private const string Ssh = "git@github.com:org-a/engine.git";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-move", Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "data");

    private string Repo => Path.Combine(_root, "engine");

    private string Bin => Path.Combine([_root, .. Tools.InstallBinLayout]);

    public GitMoveTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        Directory.CreateDirectory(Bin);
        File.WriteAllText(Path.Combine(Bin, GitWiring.SshDoor), "");
        File.WriteAllText(Path.Combine(Bin, GitWiring.RemoteHelper), "");
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
        GitSeed.Discard(_root);
    }

    private Task<GitHold?> HeldAsync(string address = Ssh) =>
        GitOwnInput.HeldAsync(Home, Repo, "engine", "games", "origin", address, "git-a", CancellationToken.None);

    [Fact]
    public void A_home_is_marked_once_and_a_home_never_started_has_not_moved()
    {
        Assert.False(GitMove.Moved(Home));

        Assert.True(GitMove.Ensure(Home, DateTimeOffset.UtcNow));
        Assert.False(GitMove.Ensure(Home, DateTimeOffset.UtcNow));

        Assert.True(GitMove.Moved(Home));
    }

    [Fact]
    public async Task A_home_with_no_doors_laid_is_never_marked_and_a_marked_one_without_them_holds_nothing()
    {
        File.Delete(Path.Combine(Bin, GitWiring.RemoteHelper));

        Assert.False(GitMove.Ensure(Home, DateTimeOffset.UtcNow));
        Assert.False(GitMoved.Moved(Home));

        File.WriteAllText(Path.Combine(Bin, GitWiring.RemoteHelper), "");
        Assert.True(GitMove.Ensure(Home, DateTimeOffset.UtcNow));
        Directory.Delete(Bin, recursive: true);

        Assert.False(GitMove.Moved(Home));
        Assert.Null(await HeldAsync());
    }

    [Fact]
    public async Task Before_the_move_an_ssh_remote_nothing_serves_is_not_held_and_after_it_is_held_as_no_identity()
    {
        Assert.Null(await HeldAsync());

        GitMove.Ensure(Home, DateTimeOffset.UtcNow);
        var held = Assert.IsType<GitHold>(await HeldAsync());

        Assert.Equal(("no-identity", "all", "github.com"), (held.Class, held.Holds, held.Host));
        Assert.Contains("has no git identity on this machine", GitHolds.Said(held));
    }

    [Fact]
    public async Task The_hold_is_lifted_by_the_person_choosing_their_own_sign_ins_for_the_host()
    {
        GitMove.Ensure(Home, DateTimeOffset.UtcNow);
        Assert.NotNull(await HeldAsync());

        GitIdentityActs.UseMachine(Home, "github.com", own: true);

        Assert.Null(await HeldAsync());
        Assert.Empty(GitHolds.Read(Home));
    }

    [Fact]
    public async Task A_local_path_needs_no_identity_and_is_not_held()
    {
        GitMove.Ensure(Home, DateTimeOffset.UtcNow);

        Assert.Null(await HeldAsync(Path.Combine(_root, "elsewhere")));
    }

    [Fact]
    public void An_https_refusal_where_nothing_serves_is_classed_no_identity_only_after_the_move()
    {
        const string Address = "https://github.com/org-a/engine.git";
        Assert.Equal(GitFailureClass.Refused, GitMove.Reclass(Home, "engine", "games", "origin", Address, GitFailureClass.Refused));

        GitMove.Ensure(Home, DateTimeOffset.UtcNow);

        Assert.Equal(GitFailureClass.NoIdentity, GitMove.Reclass(Home, "engine", "games", "origin", Address, GitFailureClass.Refused));
        Assert.Equal(GitFailureClass.HostChanged, GitMove.Reclass(Home, "engine", "games", "origin", Address, GitFailureClass.HostChanged));
        Assert.Equal("no-identity", GitFailures.Code(GitFailureClass.NoIdentity));
        Assert.Equal(GitFailureClass.NoIdentity, GitFailures.OfCode("no-identity"));
    }

    [Fact]
    public void The_ssh_door_refuses_a_host_nothing_serves_in_one_line_classed_refused_by_its_words()
    {
        var line = GitMoved.SshLine("github.com");

        Assert.DoesNotContain('\n', line);
        Assert.Equal(GitFailureClass.Refused, GitFailures.Class(line, GitAct.Read));
    }
}
