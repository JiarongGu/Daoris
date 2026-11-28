using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris trees ledger [--days &lt;1-90&gt;] [--record]</c>: the tree ledger by day and repository, read from the home's store. Each day says
/// the trees and their bytes (a hard link counted once, those set aside apart), the trees made, removed and restored, the bytes written
/// and freed, the saves pending, and the drive's free-space change beside the trees' own and its floor, which is how the ledger is
/// reconciled. <c>--record</c> takes a reading first (<see cref="TreeSaveDoors"/>); without it this reads and changes nothing.
/// </summary>
/// <remarks>Exit codes keep the family contract: 0 said, or no reading yet · 2 a word it does not take.</remarks>
public static class TreeLedgerPrint
{
    public const string Usage = "usage: daoris trees ledger [--days <1-90>] [--record]";

    /// <summary>The days read where none are asked for.</summary>
    public const int Days = 7;

    /// <summary>The days <paramref name="args"/> asks for, and whether a reading goes first; null where a word is not one it takes.</summary>
    public static (int Days, bool Record)? Read(IReadOnlyList<string> args)
    {
        var (days, record) = (Days, false);
        for (var at = 0; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--days" when at + 1 < args.Count && int.TryParse(args[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) && asked is >= 1 and <= 90:
                    days = asked;
                    at++;
                    break;
                case "--record":
                    record = true;
                    break;
                default:
                    return null;
            }
        }

        return (days, record);
    }

    /// <summary>Prints the days <paramref name="args"/> asks for; a reading, where asked, is the caller's, before this.</summary>
    public static int Run(IReadOnlyList<string> args, string home, Func<DateTimeOffset> clock, TextWriter output)
    {
        if (Read(args) is not { } asked)
        {
            output.WriteLine($"trees ledger: `{string.Join(' ', args)}` is not what it takes.");
            output.WriteLine(Usage);
            return 2;
        }

        var from = DateOnly.FromDateTime(clock().UtcDateTime).AddDays(1 - asked.Days);
        var rows = new TreeLedger(home).Days(from);
        if (rows.Count == 0)
        {
            output.WriteLine($"trees ledger: no reading and no move in the last {asked.Days} day(s) (UTC); `daoris trees ledger --record` takes one.");
            return 0;
        }

        output.Write(Text(rows));
        return 0;
    }

    /// <summary>The days as a person reads them in a terminal, each repository's day on its own lines.</summary>
    public static string Text(IReadOnlyList<LedgerDay> rows)
    {
        var text = new StringBuilder();
        foreach (var row in rows)
        {
            var net = row.Freed - row.Written;
            text.Append(row.Day).Append("  ").Append(row.Workspace).Append('/').Append(row.Repository).Append(": ")
                .Append(row.Trees).Append(" trees, ").Append(SpaceCommand.Size(row.SlotBytes))
                .Append(row.LinkedBytes > 0 ? $" ({SpaceCommand.Size(row.LinkedBytes)} of it linked, counted once)" : "")
                .Append(row.Aside > 0 ? $"; {row.Aside} set aside until purged, {SpaceCommand.Size(row.AsideBytes)} of it" : "").Append('\n');
            text.Append("  made ").Append(row.Made).Append(", removed ").Append(row.Removed).Append(", restored ").Append(row.Restored)
                .Append("; written ").Append(SpaceCommand.Size(row.Written)).Append(", freed ").Append(SpaceCommand.Size(row.Freed)).Append('\n');
            text.Append("  saves pending ").Append(row.SavesPending).Append(row.SavesPending > 0 ? $" ({SpaceCommand.Size(row.SavesBytes)})" : "").Append('\n');
            if (row.FreeFirst is { } first && row.FreeLast is { } last)
            {
                var change = last - first;
                text.Append("  drive ").Append(row.Volume).Append(": free ").Append(SpaceCommand.Size(first)).Append(" to ").Append(SpaceCommand.Size(last))
                    .Append(row.Floor is { } floor ? $" (floor {SpaceCommand.Size(floor)}{(last < floor ? ", under it" : "")})" : "")
                    .Append("; ").Append(Signed(change)).Append(", the trees ").Append(Signed(net))
                    .Append(change == net ? ", the same" : $", the rest {Signed(change - net)} is not the trees'").Append('\n');
            }
        }

        return text.ToString();
    }

    private static string Signed(long bytes) => (bytes < 0 ? "-" : "+") + SpaceCommand.Size(Math.Abs(bytes));
}
