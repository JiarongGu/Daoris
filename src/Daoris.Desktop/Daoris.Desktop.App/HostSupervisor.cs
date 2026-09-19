using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Brings the local HTTP host up, and knows whether it owns it. Adopt, never double-start: a host
/// already answering belongs to whoever started it — another shell, a terminal, a rehearsal — and
/// killing someone else's server on exit would be the process version of writing into their tree.
/// </summary>
public sealed class HostSupervisor(string serviceUrl) : IDisposable
{
    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(2) };
    private Process? _owned;

    /// <summary>Why the last <see cref="EnsureAsync"/> answered false — a sentence for the person.</summary>
    public string? Trouble { get; private set; }

    /// <summary>True when the host answers — found running, or started here and now answering.</summary>
    public async Task<bool> EnsureAsync(CancellationToken ct = default)
    {
        if (await AnswersAsync(ct).ConfigureAwait(false)) return true;

        var location = ServiceHostLocator.Locate(
            Environment.GetEnvironmentVariable(ServiceHostLocator.PathVariable),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            AppContext.BaseDirectory);

        if (location is null)
        {
            Trouble =
                $"no service host is running at {serviceUrl}, and no {ServiceHostLocator.ExecutableName} was "
                + $"found — install one with `npm run publish:service -- --install`, or set "
                + $"{ServiceHostLocator.PathVariable}.";
            return false;
        }

        // The working directory is the location's, not the binary's — a dev host must run from its
        // project so the platform bundle in its wwwroot is what gets served (see HostLocation).
        _owned = Process.Start(new ProcessStartInfo
        {
            FileName = location.Executable,
            WorkingDirectory = location.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        for (var attempt = 0; attempt < 100; attempt += 1)
        {
            if (await AnswersAsync(ct).ConfigureAwait(false)) return true;
            if (_owned is null || _owned.HasExited)
            {
                Trouble = $"the service host at {location.Executable} exited before it answered.";
                return false;
            }

            await Task.Delay(300, ct).ConfigureAwait(false);
        }

        Trouble = $"the service host was started from {location.Executable} but never answered at {serviceUrl}.";
        return false;
    }

    /// <summary>Ends the host — but only one this supervisor started.</summary>
    public void Stop()
    {
        if (_owned is { HasExited: false })
        {
            _owned.Kill(entireProcessTree: true);
        }

        _owned = null;
    }

    private async Task<bool> AnswersAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _probe.GetAsync($"{serviceUrl.TrimEnd('/')}/api/status", ct)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        Stop();
        _probe.Dispose();
    }
}
