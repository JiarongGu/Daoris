namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A throwaway git checkout on a named branch, for the questions only git can answer.
/// </summary>
/// <remarks>
/// The branch is set explicitly because `git init` picks `master` or `main` depending on the version
/// and the person's config — a fixture whose behaviour changed with the developer's git would make
/// these tests prove different things on different machines. The identity is passed per command, so
/// the suite runs on a machine with no git config at all.
/// </remarks>
internal sealed class GitTree : IDisposable
{
    private const string Identity =
        "-c user.name=\"Driver Tests\" -c user.email=\"tests@example.invalid\"";

    public string Root { get; }

    public GitTree(string name, string branch = "main")
    {
        Root = Path.Combine(Path.GetTempPath(), $"daoris-{name}-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root);
        Git("init -q");
        Git($"symbolic-ref HEAD refs/heads/{branch}");
        File.WriteAllText(Path.Combine(Root, "README.md"), "# fixture\n");
        Git($"{Identity} add -A");
        Git($"{Identity} commit -q -m \"the fixture is born\"");
    }

    public void Git(string arguments) => Output(arguments);

    /// <summary>Run git and answer what it printed, trimmed — a SHA, a branch name.</summary>
    public string Output(string arguments) => GitFixture.RunLine(Root, arguments).Stdout.Trim();

    /// <summary>Add a file and commit it, so the history moves on by one.</summary>
    public void Commit(string file)
    {
        File.WriteAllText(Path.Combine(Root, file), $"# {file}\n");
        Git($"{Identity} add -A");
        Git($"{Identity} commit -q -m \"{file}\"");
    }

    public void Dispose()
    {
        // Git leaves read-only objects on Windows; a failed cleanup is not a failed test.
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
