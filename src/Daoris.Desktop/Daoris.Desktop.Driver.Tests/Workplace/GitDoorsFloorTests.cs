using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The doors' version floor (GITAUTH1c, TOOLS6; the git-auth design §4.1): where Daoris's doors are laid out and the git Tools resolves
/// is older than 2.31, which reads no command scope, Daoris's own remote acts refuse naming both versions before any git connects, each a
/// <c>git.refused</c> line coded <c>old-git</c>; with no door laid out the floor stands in front of nothing.
/// </summary>
/// <remarks>
/// The version is pretended for the flow (<see cref="GitVersions.Pretending"/>), so no older git is needed. Alone: <c>TreeRefs</c>'
/// fetch resolves the home from the process's <c>DAORIS_HOME</c>, and the git variables keep the person's configuration out.
/// </remarks>
[Collection(Alone.Name)]
public sealed class GitDoorsFloorTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-doors-floor", Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "data");

    private string Repo => Path.Combine(_root, "engine");

    private static readonly Version Older = new(2, 30, 1);

    public GitDoorsFloorTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var empty = Path.Combine(_root, "global");
        File.WriteAllText(empty, "");
        Set("GIT_CONFIG_NOSYSTEM", "1");
        Set("GIT_CONFIG_GLOBAL", empty);
        Set(DaorisHome.Variable, Home);
    }

    private void Set(string name, string value)
    {
        if (!_was.ContainsKey(name)) _was[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _was) Environment.SetEnvironmentVariable(name, value);
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The doors laid out beside the home, as empty files: nothing here starts them.</summary>
    private void LayDoors()
    {
        var bin = Path.Combine([_root, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, GitWiring.SshDoor), "");
        File.WriteAllText(Path.Combine(bin, GitWiring.RemoteHelper), "");
    }

    private async Task Git(params string[] arguments)
    {
        var (code, _, error) = await WorkingTree.GitAsync(Repo, arguments, CancellationToken.None);
        Assert.True(code == 0, error);
    }

    private JsonElement[] Refused()
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Logs);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.jsonl")
                .SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == "git.refused")
                .Select(line => line.GetProperty("data")),
        ];
    }

    [Fact]
    public async Task Daoriss_fetches_refuse_on_a_git_older_than_its_doors_need_and_start_none()
    {
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", "git@github.com:org-a/engine.git");
        LayDoors();
        var fetches = 0;

        using (GitVersions.Pretending(Older))
        {
            WorkingTree.Started.Value = arguments => { if (arguments.Contains("fetch")) Interlocked.Increment(ref fetches); };
            try
            {
                var (failed, _) = await TreeRefs.FetchAsync(Repo, "main", CancellationToken.None, "engine", "work");
                Assert.Contains("2.30.1", failed, StringComparison.Ordinal);
                Assert.Contains("2.31", failed, StringComparison.Ordinal);

                var (address, refusal) = await GitFetchActs.PlanAsync(Home, new FetchAsk("engine", Repo, "work"), CancellationToken.None);
                Assert.Null(address);
                Assert.StartsWith("The git Daoris runs is 2.30.1, older than the 2.31", refusal, StringComparison.Ordinal);
                Assert.EndsWith("so nothing was fetched.", refusal, StringComparison.Ordinal);
            }
            finally
            {
                WorkingTree.Started.Value = null;
            }
        }

        Assert.Equal(0, fetches);
        Assert.Empty(GitHolds.Read(Home));
        var line = Refused().First();
        Assert.Equal(("old-git", "engine", "origin", "github.com", "read"), (
            line.GetProperty("code").GetString(), line.GetProperty("repository").GetString(), line.GetProperty("remote").GetString(),
            line.GetProperty("host").GetString(), line.GetProperty("class").GetString()));
    }

    [Fact]
    public async Task Where_no_door_is_laid_out_the_floor_stands_in_front_of_nothing()
    {
        using (GitVersions.Pretending(Older))
        {
            Assert.Null(await GitWiring.OlderAsync(Home, CancellationToken.None));
            LayDoors();
            Assert.Equal(Older, await GitWiring.OlderAsync(Home, CancellationToken.None));
        }

        using (GitVersions.Pretending(GitVersions.Doors))
        {
            Assert.Null(await GitWiring.OlderAsync(Home, CancellationToken.None));
        }
    }
}
