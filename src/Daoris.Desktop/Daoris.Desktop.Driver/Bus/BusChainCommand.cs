using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris bus chain &lt;correlation&gt;</c>: one chain of the bus's log, read from the home's store in one read and changing nothing: every
/// entry that carries the correlation, in order, each with its time, type, subject, own id and the entries it follows from. The correlation
/// is a chain's first entry's id, which <c>bus stalls</c> prints beside each stalled entry; an entry's own id finds its chain too where
/// the row is older than the column. Exit codes: 0 read, or no entry of that correlation, or no log yet, said · 2 a word it does not answer.
/// </summary>
public static class BusChainCommand
{
    public const string Usage =
        "usage: daoris bus chain <correlation>   one chain of the log in one read: each entry sharing the correlation, in order, with its type,\n"
        + "                                        subject, id and causes; the correlation is what `bus stalls` prints beside a stall; changes nothing";

    /// <summary>The entries listed of one chain.</summary>
    public const int Listed = 200;

    /// <param name="args">The words after <c>bus</c>, <c>chain</c> first, then the correlation.</param>
    /// <param name="home">The Daoris home.</param>
    /// <param name="said">What the verb said, for the caller to print.</param>
    public static int Run(string[] args, string home, out string said)
    {
        var output = new StringBuilder();
        var code = Run(args, home, output);
        said = output.ToString();
        return code;
    }

    private static int Run(string[] args, string home, StringBuilder output)
    {
        if (args is not ["chain", { Length: > 0 } correlation])
        {
            output.AppendLine("bus chain: name the one correlation to read.");
            output.AppendLine(Usage);
            return 2;
        }

        using var store = DesktopBusLogStore.Reading(home);
        if (store is null)
        {
            output.AppendLine("bus chain: this home keeps no bus log yet: its store has not been opened by a build that keeps one, and nothing was read.");
            return 0;
        }

        var chain = store.Chain(correlation, Listed + 1);
        if (chain.Count == 0)
        {
            output.AppendLine(Invariant($"bus chain: no entry carries the correlation {correlation} (the newest {DesktopBusLogStore.Cap} entries are kept)."));
            return 0;
        }

        var shown = chain.Take(Listed).ToList();
        output.AppendLine(Invariant(
            $"bus chain {correlation}: {shown.Count}{(chain.Count > Listed ? "+" : "")} entr{(shown.Count == 1 && chain.Count <= Listed ? "y" : "ies")}, in order:"));
        foreach (var entry in shown)
        {
            var follows = entry.Causes.Count == 0 ? "" : " follows " + string.Join(", ", entry.Causes);
            output.AppendLine(Invariant(
                $"  {entry.At.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z {entry.Kind} {entry.Subject} id {entry.Id}{follows}"));
        }

        return 0;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
