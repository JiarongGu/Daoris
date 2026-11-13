using System.Diagnostics;
using System.Text;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>
/// The machine's processes: a start is admitted by the application's policy, placed by the machine's places for its class, made on
/// the thread pool, handed to the owner and bound to the place the policy answered, and read on both streams as it writes them.
/// </summary>
public sealed class MachineProcessHost(
    IMachineAdmission places, StartPolicy policy, ProcessOwner? owner = null, Func<ProcessStartInfo, Process?>? start = null) : IProcessHost
{
    private readonly Func<ProcessStartInfo, Process?> _start = start ?? Process.Start;

    /// <summary>Told each start's wait for a place and its creation inside it, in ticks of <see cref="Stopwatch"/>, from the flow that asked.</summary>
    public Action<long, long>? Timed { get; init; }

    public async ValueTask<IChildRun> StartAsync(ProcessSpec specification, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var placed = policy(specification.Program, specification.Purpose);
        var asked = Stopwatch.GetTimestamp();
        await using var place = await places.PlaceAsync(placed?.AdmissionClass ?? specification.AdmissionClass, ct).ConfigureAwait(false);
        var entered = Stopwatch.GetTimestamp();
        // A start cancelled while it waited for its place, and given the place as it was, starts nothing.
        ct.ThrowIfCancellationRequested();
        var info = InfoOf(specification);
        try
        {
            var process = await Task.Run(() => _start(info), CancellationToken.None).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{specification.Program} did not start");
            owner?.Take(process, specification.Purpose);
            placed?.Bind(process.Id);
            return new MachineRun(process, specification.Input, specification.RawOutput, owner?.End);
        }
        finally
        {
            Timed?.Invoke(entered - asked, Stopwatch.GetTimestamp() - entered);
        }
    }

    private static ProcessStartInfo InfoOf(ProcessSpec specification)
    {
        var info = new ProcessStartInfo(specification.Program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = specification.RawOutput ? null : Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = specification.WorkingFolder ?? "",
        };
        foreach (var argument in specification.Arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in specification.Environment)
        {
            if (value is null) info.Environment.Remove(name);
            else info.Environment[name] = value;
        }

        return info;
    }
}
