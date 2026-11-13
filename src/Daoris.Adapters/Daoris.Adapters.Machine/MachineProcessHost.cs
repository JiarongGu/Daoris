using System.Diagnostics;
using System.Text;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>
/// Hands a started process to whatever owns the processes of its purpose, so it ends with the application however that ends: the one
/// management the root composes (a job object per purpose). True where it took the process.
/// </summary>
/// <param name="process">The process just started, before its caller holds it.</param>
/// <param name="purpose">What the start is for, as the specification named it.</param>
public delegate bool ProcessOwner(Process process, string purpose);

/// <summary>
/// The machine's processes: a start is admitted by the application's policy, placed by the machine's places for its class, made on
/// the thread pool, handed to the processes' owner and bound to the place the policy answered, and read on both streams as it writes them.
/// </summary>
/// <param name="places">The machine's places a start waits for.</param>
/// <param name="policy">The application's admission of each start.</param>
/// <param name="owner">Takes each process as it starts, which joins it to a job that ends it with the application; none leaves it uncontained.</param>
public sealed class MachineProcessHost(IMachineAdmission places, StartPolicy policy, ProcessOwner? owner = null) : IProcessHost
{
    /// <summary>
    /// Told, for each start, the ticks of <see cref="Stopwatch"/> it waited for its place and the creation inside it, from the flow
    /// that asked, so a caller times the two apart.
    /// </summary>
    public Action<long, long>? Timed { get; init; }

    public async ValueTask<IChildRun> StartAsync(ProcessSpec specification, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var placed = policy(specification.Program, specification.Purpose);
        var asked = Stopwatch.GetTimestamp();
        await using var place = await places.PlaceAsync(placed?.AdmissionClass ?? specification.AdmissionClass, ct).ConfigureAwait(false);
        var entered = Stopwatch.GetTimestamp();
        var info = InfoOf(specification);
        try
        {
            var process = await Task.Run(() => Process.Start(info), CancellationToken.None).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{specification.Program} did not start");
            owner?.Invoke(process, specification.Purpose);
            placed?.Bind(process.Id);
            return new MachineRun(process, specification.Input, specification.RawOutput);
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
