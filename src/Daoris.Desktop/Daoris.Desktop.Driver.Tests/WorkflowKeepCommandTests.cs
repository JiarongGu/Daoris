using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1f (the workflow design §4.4, D50): <c>daoris-driver workflow keep &lt;session&gt; ["…"]</c>, the person's <i>Keep</i> where
/// a kind's paths hold a session's work. Its words, its usage in the host's, and its routing. The press itself, over real git, is
/// <c>AutoLandingTests</c>' kind's paths row.
/// </summary>
public sealed class WorkflowKeepCommandTests
{
    public static TheoryData<string, string?> Lines => new()
    {
        { "keep s1", "s1 · " },
        { "keep s1 the report is the docs", "s1 · the report is the docs" },
        { "keep  s1 ", null },
        { "keep", null },
        { "keep --session s1", null },
        { "run --quest q1", null },
    };

    [Theory]
    [MemberData(nameof(Lines))]
    public void The_words_name_one_session_and_the_persons_words(string line, string? read)
    {
        var ask = WorkflowKeepCommand.Read(line.Split(' ', StringSplitOptions.None), out var problem);

        if (read is null)
        {
            Assert.Null(ask);
            Assert.Equal("`workflow keep` takes a session's id, then your words if you have any: `workflow keep <session> [\"…\"]`.", problem);
            return;
        }

        Assert.Null(problem);
        Assert.Equal(read, $"{ask!.Session} · {ask.Words}");
    }

    [Fact]
    public void The_verb_is_in_the_hosts_usage_and_routed_through_the_workflow_console()
    {
        Assert.StartsWith("usage: daoris-driver workflow keep <session> [\"…\"]", WorkflowKeepCommand.Usage);
        Assert.Contains("\n  workflow keep <session> [\"…\"]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));

        var host = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host");
        var console = File.ReadAllText(Path.Combine(host, "WorkflowConsole.cs"));
        Assert.Contains("if (args is [\"keep\", ..]) return await KeepAsync(args)", console);
        Assert.Contains("WorkflowKeepCommand.Read(args, out var problem)", console);
        Assert.Contains("Console.Error.WriteLine(WorkflowKeepCommand.Usage);", console);
        Assert.Contains("WorkflowKeepCommand.RunAsync(", console);
        Assert.Contains("//   workflow keep <session> [\"…\"]", File.ReadAllText(Path.Combine(host, "Program.cs")));
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
