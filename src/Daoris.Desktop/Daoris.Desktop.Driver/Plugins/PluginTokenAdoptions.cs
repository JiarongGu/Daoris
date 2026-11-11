namespace Daoris.Driver;

/// <summary>
/// When the identity's token adoption runs (<see cref="PluginTokenAdoption"/>): at the loop's start, and again whenever the plugins
/// change, one installed, updated, added or switched, so a plugin that arrives after the start takes the token without a restart.
/// One run at a time: an ask while one runs runs once more after it. A run with no identity holding a token does nothing.
/// </summary>
public sealed class PluginTokenAdoptions(string home, MachineLog log, PluginTokenAdoption.Round? round = null)
{
    /// <summary>The change's scope that names the plugins, in the page's words.</summary>
    public const string PluginsScope = "plugins";

    private readonly Lock _gate = new();
    private bool _asked;
    private bool _busy;
    private Task _running = Task.CompletedTask;

    /// <summary>Run it once more, after any run under way; answers the run that will cover this ask.</summary>
    public Task Ask()
    {
        lock (_gate)
        {
            _asked = true;
            if (_busy) return _running;
            _busy = true;
            return _running = Task.Run(RunAsync);
        }
    }

    /// <summary>A change the windows are told: asks a run where it names the plugins.</summary>
    public void Told(StateChangedFrame change)
    {
        if (change.Scopes.Contains(PluginsScope, StringComparer.Ordinal)) _ = Ask();
    }

    private async Task RunAsync()
    {
        var ended = false;
        try
        {
            while (true)
            {
                lock (_gate)
                {
                    if (!_asked)
                    {
                        _busy = false;
                        ended = true;
                        return;
                    }

                    _asked = false;
                }

                await PluginTokenAdoption.RunAsync(home, log, CancellationToken.None, round).ConfigureAwait(false);
            }
        }
        finally
        {
            if (!ended)
            {
                lock (_gate) _busy = false;
            }
        }
    }
}
