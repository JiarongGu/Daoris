namespace Daoris.Ports.Processes;

/// <summary>What a process is started as: its program and arguments, the folder it works in, the environment it takes, and why.</summary>
/// <param name="Program">The program, as the caller resolved it.</param>
/// <param name="Arguments">Its argument vector, in order.</param>
/// <param name="Purpose">What the start is for, named in the machine's log and in a refusal.</param>
/// <param name="AdmissionClass">The class the machine admits the start under.</param>
public sealed record ProcessSpec(string Program, IReadOnlyList<string> Arguments, string Purpose, string AdmissionClass)
{
    /// <summary>The folder it works in, or null for the host's own.</summary>
    public string? WorkingFolder { get; init; }

    /// <summary>The variables it takes beside the host's; a null value removes the variable.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();

    /// <summary>Whether its standard input stays open for <see cref="IChildRun.WriteAsync"/>.</summary>
    public bool Input { get; init; }

    public bool RawOutput { get; init; }
}

/// <summary>Which of a process's two output streams a piece came from.</summary>
public enum ProcessStream
{
    /// <summary>The standard output.</summary>
    Output,

    /// <summary>The standard error.</summary>
    Error,
}

/// <summary>A piece of what a process wrote.</summary>
public sealed record ProcessOutput(ProcessStream Stream, string Text);

/// <summary>How a process ended: its exit, and whether the application stopped it.</summary>
public sealed record ProcessEnd(int Exit, bool Stopped);
