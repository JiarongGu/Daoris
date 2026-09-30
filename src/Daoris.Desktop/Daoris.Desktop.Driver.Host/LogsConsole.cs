namespace Daoris.Driver.Host;

/// <summary>
/// The machine log, from a terminal: `daoris-driver logs [--since &lt;30m|2h|3d&gt;] [--source &lt;name&gt;]
/// [--event &lt;name&gt;] [--level &lt;warn|error&gt;] [--json]` (LOG1c, D94).
/// </summary>
/// <remarks>
/// <para><b>The terminal's door</b>; Settings → Logs is the screen's (D50). Both read through
/// <see cref="MachineLogReader"/>, so a filter means the same at each.</para>
///
/// <para>Every source's lines, merged by time and oldest first, as a log is read top to bottom; with
/// <c>--json</c>, the lines as written, for a pipe. What goes to standard error is about the reading —
/// nothing matched, lines skipped — so standard output stays the lines alone.</para>
///
/// <para>Exit codes keep the family contract: 0 read · 2 a flag it cannot use, or no home (the driver's
/// usual sentence, from the catch every door shares).</para>
/// </remarks>
internal static class LogsConsole
{
    public static int Run(string[] args)
    {
        var parsed = MachineLogReader.Arguments(args, DateTimeOffset.UtcNow);
        if (parsed.Problem is { } problem)
        {
            Console.Error.WriteLine($"logs: {problem}.");
            Console.Error.WriteLine(MachineLogReader.Usage);
            return 2;
        }

        var folder = DaorisHome.Require(MachineLog.Folder);
        var read = MachineLogReader.Read(folder, parsed.Filter);
        foreach (var line in read.Lines)
        {
            Console.WriteLine(parsed.Json ? line.Raw : MachineLogReader.Format(line));
        }

        foreach (var said in MachineLogReader.Closing(read, folder)) Console.Error.WriteLine(said);
        return 0;
    }
}
