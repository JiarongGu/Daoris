using Daoris.Ports.Git;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every <see cref="IGit"/> must pass, written once against the port. A subclass hands the backend: the scripted fake
/// answers each case from the script it is given, and the real adapter ignores the script and answers as git does, so the same case
/// holds both to one behaviour.
/// </summary>
public abstract class GitContract
{
    /// <summary>The words a case wants git to answer, which the fake reads and the real backend has no use for.</summary>
    protected sealed record Script(string Arguments, GitAnswer Answer);

    /// <summary>A folder the real backend may run in; the fake never looks at it.</summary>
    protected abstract string Folder { get; }

    /// <summary>The backend under test, made ready to answer <paramref name="script"/>.</summary>
    protected abstract IGit Backend(params Script[] script);

    private GitAsk Ask(params string[] arguments) => new(Folder, arguments, GitWait.Read, Writes: false);

    private static readonly Script Version = new("--version", new GitAnswer(0, "git version 2.50.0\n", ""));
    private static readonly Script Unknown = new("definitely-not-a-command", new GitAnswer(1, "", "git: 'definitely-not-a-command' is not a git command.\n"));

    [Fact]
    public async Task A_run_answers_what_git_wrote_and_its_exit()
    {
        var answer = await Backend(Version).RunAsync(Ask("--version"), CancellationToken.None);

        Assert.True(answer.Ok);
        Assert.StartsWith("git version", answer.Output);
    }

    [Fact]
    public async Task A_command_git_does_not_know_fails_with_its_error()
    {
        var answer = await Backend(Unknown).RunAsync(Ask("definitely-not-a-command"), CancellationToken.None);

        Assert.False(answer.Ok);
        Assert.False(string.IsNullOrWhiteSpace(answer.Error));
    }

    [Fact]
    public async Task The_lines_are_the_output_split_at_its_line_ends()
    {
        var git = Backend(Version);
        var run = await git.RunAsync(Ask("--version"), CancellationToken.None);
        var lines = new List<string>();
        await foreach (var line in git.ReadLinesAsync(Ask("--version"), CancellationToken.None)) lines.Add(line);

        Assert.Equal(run.Output.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'), lines);
    }

    [Fact]
    public async Task The_bytes_are_the_output_as_it_was_written()
    {
        var git = Backend(Version);
        var run = await git.RunAsync(Ask("--version"), CancellationToken.None);
        var bytes = await git.ReadBytesAsync(Ask("--version"), CancellationToken.None);

        Assert.True(bytes.Ok);
        Assert.Equal(run.Output, System.Text.Encoding.UTF8.GetString(bytes.Content));
    }

    [Fact]
    public async Task An_ask_made_with_a_cancelled_token_is_not_run()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Backend(Version).RunAsync(Ask("--version"), cancelled.Token));
    }
}
