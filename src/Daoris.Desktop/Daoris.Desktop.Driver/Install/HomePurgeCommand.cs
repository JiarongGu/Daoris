using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris home purge --history</c>: a home's history moved out once, its configuration kept (D322). <c>--plan</c> lists every path and
/// table it would move or empty, with sizes and counts, and changes nothing; <c>--yes</c> does it, only while nothing of Daoris runs on the home.
/// </summary>
/// <remarks>
/// <para><b>Refused while Daoris runs</b>, as a staged build waits for the desktop to close: a desktop, a <c>daoris drive</c> or another
/// verb holding the home's lease, or the service the desktop left its address for, still living. It takes the lease for its act, so
/// nothing starts on the home meanwhile. From a session's terminal it lists and never purges: a purge ends every session's record.</para>
///
/// <para>The printing lives in the library so a test runs the whole door in-process. Exit codes: 0 listed or purged · 1 refused, or a
/// purge stopped part-way, said with what moved · 2 a word it does not answer.</para>
/// </remarks>
[CommandGroup("home", "purge")]
public static class HomePurgeCommand
{
    public const string Usage =
        "usage: daoris home purge --history --plan     what a purge would move out of the home, with sizes and counts\n"
        + "       daoris home purge --history --yes      move the home's history into purged/<UTC stamp>/ beside it; configuration stays";

    /// <param name="args">The words after <c>home</c>.</param>
    /// <param name="home">The Daoris home.</param>
    /// <param name="session">The terminal's <c>DAORIS_SESSION_ID</c>: a session's terminal lists and never purges.</param>
    /// <param name="said">Everything the door said, for the caller to print.</param>
    public static int Run(string[] args, string home, Func<DateTimeOffset> clock, MachineLog? log, string? session, out string said)
    {
        var output = new StringBuilder();
        var code = Run(args, home, clock, log, session, output);
        said = output.ToString();
        return code;
    }

    private static int Run(string[] args, string home, Func<DateTimeOffset> clock, MachineLog? log, string? session, StringBuilder output)
    {
        if (args is not ["purge", .. var words] || !words.Contains("--history") || words.Any(word => word is not ("--history" or "--plan" or "--yes"))
            || words.Contains("--plan") == words.Contains("--yes"))
        {
            output.AppendLine("home: name what to purge (--history) and either --plan or --yes.");
            output.AppendLine(Usage);
            return 2;
        }

        var plan = HomePurge.Plan(home);
        if (words.Contains("--plan"))
        {
            Listed(plan, output);
            return 0;
        }

        if (session is { Length: > 0 })
        {
            output.AppendLine("home: a purge ends every session's record, so it is the person's, from their own terminal; this one is a session's. "
                + "`daoris home purge --history --plan` lists what it would move.");
            return 1;
        }

        if (Running(home) is { } why)
        {
            output.AppendLine($"home: {why} Close Daoris (its window's Quit, or Ctrl+C in a `daoris drive`), then run this again: nothing was moved.");
            return 1;
        }

        using var lease = HomeLease.TryAcquire(home, LeaseKind.Verb, out var holder, "home purge --history");
        if (lease is null)
        {
            output.AppendLine($"home: {home} is held by {holder?.Named ?? "another process"}. Close it, then run this again: nothing was moved.");
            return 1;
        }

        var done = HomePurge.Run(plan, clock(), log);
        output.AppendLine($"home: moved {done.Moved.Count} of {plan.Paths.Count} paths ({Size(done.Moved.Sum(path => path.Bytes))}) and "
            + $"{done.Emptied.Sum(table => table.Rows)} rows of {done.Emptied.Count} tables into {done.Into}; the configuration stays.");
        foreach (var path in done.Moved) output.AppendLine($"  moved   {path.Relative}  {Size(path.Bytes)}, {path.Files} files");
        foreach (var table in done.Emptied.Where(table => table.Rows > 0)) output.AppendLine($"  emptied {table.Relative} {table.Table}  {table.Rows} rows");
        if (done.Failed is null)
        {
            output.AppendLine($"  {HomePurge.SummaryFile} there lists it; delete that folder once the start is clean.");
            return 0;
        }

        output.AppendLine($"  stopped: {done.Failed} What moved is there; `daoris home purge --history --yes` moves the rest.");
        return 1;
    }

    /// <summary>What holds the home now, in a sentence, or null where nothing of Daoris runs on it.</summary>
    private static string? Running(string home)
    {
        if (HomeLease.HeldBy(home) is { } holder) return $"{home} is held by {holder.Named}.";
        return RunningAddress.Read(home) is { } entry && RunningService.Lives(entry.Pid, entry.Started)
            ? $"Daoris's service runs on {home} (process {entry.Pid.ToString(CultureInfo.InvariantCulture)})."
            : null;
    }

    private static void Listed(PurgePlan plan, StringBuilder output)
    {
        output.AppendLine($"home: a purge would move {plan.Paths.Count} paths ({Size(plan.Bytes)}) and empty {plan.Tables.Count} tables "
            + $"({plan.Rows} rows) into {plan.Backup}{Path.DirectorySeparatorChar}<UTC stamp>; the configuration stays. Nothing was changed.");
        foreach (var path in plan.Paths) output.AppendLine($"  move  {path.Relative}  {Size(path.Bytes)}, {path.Files} files");
        foreach (var table in plan.Tables) output.AppendLine($"  empty {table.Relative} {table.Table}  {table.Rows} rows");
        output.AppendLine("  `daoris home purge --history --yes` does it, once Daoris is closed.");
    }

    private static string Size(long bytes) => ByteSizes.Said(bytes);
}
