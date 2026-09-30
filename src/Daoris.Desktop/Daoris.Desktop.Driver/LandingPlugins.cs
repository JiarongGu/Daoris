namespace Daoris.Driver;

/// <summary>
/// The landing's side of the plugin wire (WSR4, D100): the plugin a branch rule names, started for one
/// landing, told the branch Daoris made on <see cref="HookPoints.Land"/>, and stopped again.
/// </summary>
/// <remarks>
/// <para><b>Daoris pushes nothing and opens nothing</b> (D37, D87). The plugin's process does both, for
/// its own platform, with whatever that platform's tools are signed in as — the push is the plugin's act,
/// installed and named by the person, never Daoris's.</para>
///
/// <para><b>Started per landing, not kept with the loop.</b> A landing is a press, and it happens where
/// the press is: in the shell, or in a terminal's <c>daoris-driver trees land</c>, which has no loop. A
/// process that exists exactly as long as the one frame it answers is the registration-as-effect rule
/// (D64 §4) at its smallest.</para>
///
/// <para><b>It never throws for the plugin's sake.</b> Whatever the plugin does — not start, answer late,
/// answer wrongly, refuse — comes back as a <see cref="PluginLanding"/> marked failed, with the sentence,
/// because the branch is already made and the press is owed an answer about both.</para>
/// </remarks>
public sealed class LandingPlugins(
    string home,
    Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>>? start = null,
    // Where a plugin's word goes: the console under `plugin:<id>`, or a terminal's own lines.
    Action<string, string>? say = null,
    TimeSpan? patience = null)
{
    /// <summary>
    /// How long a plugin has to push and open the pull request. Longer than a decision's ten seconds: a
    /// push and a platform's API are network round trips, and the person pressed and is waiting.
    /// </summary>
    public static readonly TimeSpan DefaultPatience = TimeSpan.FromMinutes(2);

    private readonly Func<PluginEntry, Action<string>, CancellationToken, Task<IHookChannel>> _start =
        start ?? (async (plugin, onLine, ct) =>
            await HookProcess.StartAsync(plugin, home, onLine, ct, patience ?? DefaultPatience).ConfigureAwait(false));

    /// <summary>The home's plugins, read as the driver reads them — a harness name this build carries refused.</summary>
    public PluginCatalog Catalog() => PluginCatalog.Load(home, AdapterSet.Built().Names);

    /// <summary>Why <paramref name="plugin"/> cannot land work here now, or null (<see cref="LandingRules.PluginProblem"/>).</summary>
    public string? Problem(string plugin) => LandingRules.PluginProblem(plugin, Catalog());

    /// <summary>Speak the one frame and hear the answer — or the sentence saying why there is none.</summary>
    public async Task<PluginLanding> LandAsync(string plugin, LandingFrame frame, CancellationToken ct = default)
    {
        var catalog = Catalog();
        if (LandingRules.PluginProblem(plugin, catalog) is { } problem) return Failed(plugin, problem);
        var entry = catalog.Plugins.First(p => string.Equals(p.Manifest.Id, plugin, StringComparison.OrdinalIgnoreCase));
        var id = entry.Manifest.Id;

        IHookChannel? channel = null;
        try
        {
            channel = await _start(entry, line => Say(id, line), ct).ConfigureAwait(false);
            if (!channel.Points.Contains(HookPoints.Land, StringComparer.Ordinal))
            {
                return Failed(id, $"plugin `{id}` declares `{HookPoints.Land}` but its process does not listen there.");
            }

            var answer = await channel.LandAsync(Payload(frame), ct).ConfigureAwait(false);
            Say(id, $"landed `{frame.Branch}`: {(answer.Pushed ? "pushed" : "not pushed")}"
                + (answer.PullRequest is { } pr ? $", {pr}" : "") + $" — {answer.Message}");
            return answer with { Plugin = id };
        }
        catch (DriverException error)
        {
            return Failed(id, error.Message);
        }
        finally
        {
            if (channel is not null) await channel.DisposeAsync().ConfigureAwait(false);
        }
    }

    private PluginLanding Failed(string plugin, string why)
    {
        Say(plugin, why);
        return new PluginLanding(plugin, Pushed: false, PullRequest: null, why, Failed: true);
    }

    private void Say(string plugin, string line) => say?.Invoke(plugin, line);

    /// <summary>The frame as the wire carries it, every name spelled here rather than left to a serializer's policy.</summary>
    private static object Payload(LandingFrame frame) => new
    {
        repository = frame.Repository,
        workspace = frame.Workspace,
        root = frame.Root,
        branch = frame.Branch,
        @base = frame.Base,
        title = frame.Title,
        quest = frame.Quest is { } quest ? new { id = quest, title = frame.Title } : null,
        session = frame.Session,
        commits = frame.Commits.Select(commit => new { sha = commit.Sha, subject = commit.Subject }).ToArray(),
    };
}
