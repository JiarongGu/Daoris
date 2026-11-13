using System.Text;
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
        Assert.NotNull(answer.Exit);
        Assert.False(string.IsNullOrWhiteSpace(answer.Error));
    }

    [Fact]
    public async Task A_read_hands_on_the_output_as_it_was_written_and_answers_the_exit()
    {
        var git = Backend(Version);
        var run = await git.RunAsync(Ask("--version"), CancellationToken.None);
        var read = new StringBuilder();
        var exit = await git.ReadAsync(Ask("--version"), piece =>
        {
            read.Append(piece.Span);
            return true;
        }, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(run.Output, read.ToString());
    }

    [Fact]
    public async Task A_read_whose_taker_has_enough_stops_git_and_answers_zero()
    {
        var exit = await Backend(Unknown).ReadAsync(Ask("definitely-not-a-command"), _ => false, CancellationToken.None);
        var stopped = await Backend(Version).ReadAsync(Ask("--version"), _ => false, CancellationToken.None);

        Assert.NotEqual(0, exit);
        Assert.Equal(0, stopped);
    }

    [Fact]
    public async Task The_bytes_are_the_output_as_it_was_written()
    {
        var git = Backend(Version);
        var run = await git.RunAsync(Ask("--version"), CancellationToken.None);
        var bytes = await git.ReadBytesAsync(Ask("--version"), 1 << 20, CancellationToken.None);

        Assert.True(bytes.Read);
        Assert.False(bytes.Cut);
        Assert.Equal(run.Output, Encoding.UTF8.GetString(bytes.Content));
    }

    [Fact]
    public async Task Bytes_past_the_limit_are_cut_and_what_was_read_stands()
    {
        var bytes = await Backend(Version).ReadBytesAsync(Ask("--version"), 3, CancellationToken.None);

        Assert.True(bytes.Cut);
        Assert.True(bytes.Read);
        Assert.Equal("git", Encoding.UTF8.GetString(bytes.Content));
    }

    [Fact]
    public async Task An_input_is_written_whole_to_git_as_UTF8_without_a_mark()
    {
        var hashed = new Script("hash-object --stdin", new GitAnswer(0, "ce013625030ba8dba906f756967f9e9ca394464a\n", ""));

        var answer = await Backend(hashed).RunAsync(Ask("hash-object", "--stdin") with { Input = "hello\n" }, CancellationToken.None);

        Assert.True(answer.Ok);
        Assert.Equal("ce013625030ba8dba906f756967f9e9ca394464a", answer.Output.Trim());
    }

    [Fact]
    public async Task An_ask_made_with_a_cancelled_token_is_not_run()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Backend(Version).RunAsync(Ask("--version"), cancelled.Token));
    }
}
