using System.Diagnostics;
using System.Text;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Machine;

/// <summary>
/// The machine's processes: a start is admitted by the application's policy, placed by the machine's places for its class, made on
/// the thread pool, bound to the place the policy answered, and read on both streams as it writes them.
/// </summary>
public sealed class MachineProcessHost(IMachineAdmission places, StartPolicy policy) : IProcessHost
{
    public async ValueTask<IChildRun> StartAsync(ProcessSpec specification, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var placed = policy(specification.Program, specification.Purpose);
        await using var place = await places.PlaceAsync(placed?.AdmissionClass ?? specification.AdmissionClass, ct).ConfigureAwait(false);
        var info = InfoOf(specification);
        var process = await Task.Run(() => Process.Start(info), CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{specification.Program} did not start");
        placed?.Bind(process.Id);
        return new MachineRun(process, specification.Input);
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
            StandardOutputEncoding = Encoding.UTF8,
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
