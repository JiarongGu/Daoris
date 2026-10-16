namespace Daoris.Driver;

/// <summary>
/// What a plugin did (PLUGUI1d, D119 §3.2 and §4.1): the machine log's <c>plugin.*</c> lines for one plugin over a
/// period, summarised, with the sessions started on its agents (<c>session.started</c>'s <c>adapter</c>) and the
/// branches it pushed (the landing record, D102). One reader for both doors: the page's <c>PLUGIN_ACTIVITY</c> and
/// <c>daoris-driver plugins activity</c>.
/// </summary>
/// <remarks>
/// <para><b>Never the plugin's words.</b> The log holds none (D94 §5), and the landing record's pull request is an
/// address the plugin answered, shown as the link it is. What the plugin said this run is the console's ring, which
/// the page reads apart (<c>TAIL_SESSION</c>).</para>
///
/// <para>A line that cannot be read is skipped and counted, as <see cref="MachineLogReader"/> does; a day's file
/// older than the period is not opened.</para>
///
/// <para>The use case (STRUCT3a): the plugin found, the log and the landing record read; what they say is summarised by
/// the Domain's <see cref="PluginActivitySummary"/>.</para>
/// </remarks>
public static class PluginActivity
{
    /// <summary>How many recent events are answered.</summary>
    public const int RecentLines = PluginActivitySummary.RecentLines;

    /// <summary>The period a page opens on, and the terminal's without <c>--since</c>: the last 7 days.</summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromDays(7);

    /// <exception cref="DriverException">This machine holds no such plugin (<see cref="PluginPage.Unknown"/>).</exception>
    public static PluginActivityRead Read(string home, string id, DateTimeOffset since)
    {
        var (entry, manifest, _) = PluginPage.Installed(home, id);
        var plugin = entry.Manifest.Id;
        var folder = DaorisHome.Entry(home, HomeEntry.Logs);
        var read = MachineLogReader.Read(folder, new LogFilter(Since: since));

        var pushes = new LandedBranches(home).PushedBy(plugin)
            .Where(entry => entry.LandedAt >= since)
            .Reverse()
            .Select(entry => new PluginPush(entry.Repository, entry.Branch, entry.PullRequest, entry.LandedAt))
            .ToList();

        return PluginActivitySummary.Of(plugin, manifest, since, PluginFolders.There(folder), read.Lines, read.Skipped, pushes);
    }
}
