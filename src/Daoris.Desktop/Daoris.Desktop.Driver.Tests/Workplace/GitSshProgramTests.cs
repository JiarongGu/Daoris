using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ssh an identity's act runs: one program, named by its full path, the one beside the git Daoris uses, whatever order the
/// act's <c>PATH</c> is in.
/// </summary>
public sealed class GitSshProgramTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-git-ssh-program-" + Guid.NewGuid().ToString("N")[..8]);

    private static string Exe(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    private string Home => Path.Combine(_root, "data");

    private string Touch(params string[] parts)
    {
        var file = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "");
        return file;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void An_identity_runs_the_ssh_beside_its_git_not_the_first_ssh_on_PATH()
    {
        Directory.CreateDirectory(Home);
        var git = Touch("gfw", "cmd", Exe("git"));
        var beside = Touch("gfw", "usr", "bin", Exe("ssh"));
        var system = Touch("system32", "OpenSSH", Exe("ssh"));

        var (file, problem) = GitSshProgram.Of(Home, name => name == "PATH" ? Path.GetDirectoryName(system) : null, git);

        Assert.Null(problem);
        Assert.Equal(beside, file);
    }

    [Fact]
    public void The_door_finds_the_same_ssh_from_the_exec_path_git_hands_its_child()
    {
        Directory.CreateDirectory(Home);
        Touch("gfw", "mingw64", "bin", Exe("git"));
        var beside = Touch("gfw", "usr", "bin", Exe("ssh"));
        var system = Touch("system32", "OpenSSH", Exe("ssh"));
        var exec = Path.Combine(_root, "gfw", "mingw64", "libexec", "git-core");
        Directory.CreateDirectory(exec);

        var (file, _) = GitSshProgram.Of(Home, name => name switch
        {
            "PATH" => Path.GetDirectoryName(system),
            "GIT_EXEC_PATH" => exec,
            _ => null,
        });

        Assert.Equal(beside, file);
    }

    [Fact]
    public void With_no_git_beside_it_the_ssh_on_PATH_is_the_fallback_and_none_is_a_problem()
    {
        Directory.CreateDirectory(Home);
        var onPath = Touch("bin", Exe("ssh"));

        Assert.Equal(onPath, GitSshProgram.Of(Home, name => name == "PATH" ? Path.GetDirectoryName(onPath) : null).File);
        Assert.Contains("no ssh", GitSshProgram.Of(Home, _ => null).Problem);
    }
}
