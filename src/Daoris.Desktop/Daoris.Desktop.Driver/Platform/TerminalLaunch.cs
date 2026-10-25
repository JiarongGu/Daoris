namespace Daoris.Driver;

/// <summary>A shell a terminal can run (CONSOLE4, D96): its id, the file that starts it, and what it is started with.</summary>
/// <param name="Id">The page's name for it: <c>pwsh</c>, <c>powershell</c>, <c>cmd</c> or <c>bash</c>.</param>
public sealed record TerminalShell(string Id, string Path, IReadOnlyList<string> Arguments);

/// <summary>What a terminal starts: the shell, the folder it starts in, and what its environment adds to this process's own.</summary>
/// <param name="Whole">
/// That <paramref name="Environment"/> is the whole environment, inheriting nothing of this process's and adding no tools'
/// (STEWARDROLE1a-i, D209 §6.1: the holder's harness, whose environment Daoris composes).
/// </param>
public sealed record TerminalLaunch(
    TerminalShell Shell, string Directory, IReadOnlyDictionary<string, string> Environment, int Columns = 80, int Rows = 24, bool Whole = false);

/// <summary>
/// A shell of the person's own, under a pseudo-console (CONSOLE4a, D96): what a terminal view drives.
/// Disposing it closes it, and everything the shell started ends with it.
/// </summary>
public interface ITerminal : IDisposable
{
    /// <summary>What the person typed, as the renderer encodes it — keys, escape sequences, pasted text.</summary>
    void Write(string data);

    /// <summary>The view's size in cells. Taken after the close as well, and ignored then.</summary>
    void Resize(int columns, int rows);

    /// <summary>The started process's id, 0 where unknown (STEWARDROLE1a-i: the holder's harness, which the judge's walk finds by it).</summary>
    int ProcessId => 0;

    /// <summary>The started process's creation time, UTC, as the system keeps it; default where unknown. With the id, it names one process.</summary>
    DateTime Created => default;
}
