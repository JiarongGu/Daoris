using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The dependency store's install run as a heavy step is (<see cref="IInstallRunner"/>): admitted by the machine's admission under the
/// kind <c>deps</c>, and started as an integration candidate's step, in exactly its own environment and writing to its own log.
/// </summary>
internal sealed class HeavyInstallRunner : IInstallRunner
{
    [ModuleInitializer]
    internal static void Install() => InstallRunner.Current = new HeavyInstallRunner();

    public async Task<IDisposable> AdmitAsync(string home, string what, string repository, string tree, CancellationToken ct) =>
        await MachineAdmission.TakeAsync(home, new AdmissionRequest(what) { Repository = repository, Tree = tree, Kind = "deps" }, TextWriter.Null, ct)
            .ConfigureAwait(false);

    public IRunningInstall Start(ProcessJob? job, string command, string directory, IReadOnlyDictionary<string, string> environment, string log) =>
        new Running(HeavyStep.Start(job, command, directory, new Dictionary<string, string>(), new StepOptions(environment, log)));

    private sealed class Running(HeavyStep step) : IRunningInstall
    {
        public Task<int> ExitAsync(CancellationToken ct) => step.ExitAsync(ct);

        public void Stop() => step.Stop();

        public void Dispose() => step.Dispose();
    }
}
