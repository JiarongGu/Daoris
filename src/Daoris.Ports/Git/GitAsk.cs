namespace Daoris.Ports.Git;

/// <summary>What a git's run waits on: this machine's files alone, or what is outside Daoris.</summary>
public enum GitWait
{
    /// <summary>A git that runs on the repository's files alone.</summary>
    Read,

    /// <summary>A git that waits outside Daoris: a remote act or an act that runs a hook.</summary>
    Outside,
}

/// <summary>One git asked of <see cref="IGit"/>: where it runs, its arguments, what it waits on and whether it writes.</summary>
/// <param name="Folder">The folder git runs in, as one full spelling.</param>
/// <param name="Arguments">Its arguments, in order, without the program.</param>
/// <param name="Wait">What its run waits on.</param>
/// <param name="Writes">Whether it may write: nothing queued behind it in its repository passes it.</param>
/// <param name="Input">What is written on its standard input, or null for none.</param>
/// <param name="Bound">How long it may run, or null for the adapter's own bound.</param>
/// <param name="Home">The Daoris home whose tool choices say which git, or null for the machine's.</param>
public sealed record GitAsk(
    string Folder,
    IReadOnlyList<string> Arguments,
    GitWait Wait,
    bool Writes,
    string? Input = null,
    TimeSpan? Bound = null,
    string? Home = null);

/// <summary>What one run of git answered.</summary>
/// <param name="Exit">Its exit code, or null where it did not start or was ended before it exited.</param>
/// <param name="Output">What it wrote to its standard output.</param>
/// <param name="Error">What it wrote to its standard error.</param>
public sealed record GitAnswer(int? Exit, string Output, string Error)
{
    /// <summary>Whether it exited with zero.</summary>
    public bool Ok => Exit == 0;
}

/// <summary>What one run of git wrote to its standard output, as bytes, with its exit and error.</summary>
public sealed record GitBytes(int? Exit, byte[] Content, string Error)
{
    /// <summary>Whether it exited with zero.</summary>
    public bool Ok => Exit == 0;
}
