using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The browser's <see cref="Process"/> as its bring-up holds it, joined as it is taken to the <see cref="ProcessPurpose.Browser"/> job
/// of the one management, so it ends with this process however this one ends (rule 63), and ended through that management.
/// </summary>
public sealed class ProcessBrowserChild : IBrowserChild, IDisposable
{
    private readonly Process _process;

    public ProcessBrowserChild(Process process, OwnedProcesses owned)
    {
        _process = process;
        owned.Take(process, ProcessPurpose.Browser);
    }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public Task WaitForExitAsync(CancellationToken ct) => _process.WaitForExitAsync(ct);

    public void End() => OwnedProcesses.End(_process);

    public void Dispose() => _process.Dispose();
}
