using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// `daoris-driver space [--json] [--measure]` (KEEP1c, the cleanup design §4.4, D50): the terminal's twin of This Daoris's <i>Disk
/// space</i> card, reading the same facts through <see cref="SystemSpace"/>: the free space, and each kind's size, rule, next and last
/// clean. <c>--json</c> prints <c>SYSTEM_SPACE</c>'s answer field for field, so the twins share one test table; <c>--measure</c> walks each
/// kind, within its own budget, before it answers. It removes nothing: <c>space free</c> is KEEP1e's.
/// </summary>
/// <remarks>
/// The printing lives in the library so a test runs the whole door in-process. Exit codes keep the family contract: 0 said ·
/// 2 a word it does not answer.
/// </remarks>
public static class SpaceCommand
{
    public const string Usage = "usage: daoris-driver space [--json] [--measure]";

    /// <param name="world">What the reader is handed: <see cref="SpaceWorld.OfThisTerminal"/>, or a test's.</param>
    /// <param name="said">Everything the door said, for the caller to print.</param>
    public static int Run(string[] args, SpaceWorld world, out string said)
    {
        bool json = false, measure = false;
        foreach (var word in args)
        {
            switch (word)
            {
                case "--json": json = true; break;
                case "--measure": measure = true; break;
                default:
                    said = $"space: no option `{word}`.\n{Usage}\n";
                    return 2;
            }
        }

        if (measure)
        {
            foreach (var kind in SpaceKinds.Table) world.Sizes.MeasureNow(world.Home, kind.Id, WiringBudget.Default, CancellationToken.None);
        }

        var space = SystemSpace.Read(world);
        said = json ? SystemSpace.Json(space) : Text(space);
        return 0;
    }

    /// <summary>The answer as a person reads it in a terminal: the free space, then each kind with its size, rule, next and last.</summary>
    public static string Text(JsonObject space)
    {
        var text = new StringBuilder();
        if (space["volume"] is JsonObject volume)
        {
            text.Append("free space: ").Append(Size((long)volume["free"]!)).Append(" of ").Append(Size((long)volume["total"]!))
                .Append(", on the volume of ").Append((string?)volume["path"]).Append('\n');
        }
        else
        {
            text.Append("free space: could not be read\n");
        }

        if (space["held"] is JsonObject held)
        {
            var who = $"from the {(string?)held["door"]}, process {(int)held["pid"]!}, since {Moment((string?)held["since"])}";
            text.Append((bool)held["gone"]! ? $"the last run, {who}, stopped before its end\n" : $"a run is under way, {who}\n");
        }

        foreach (var node in space["kinds"]!.AsArray())
        {
            var kind = node!.AsObject();
            text.Append((string?)kind["id"]).Append(": ").Append(Sized(kind)).Append('\n');
            text.Append("  rule: ").Append(Rule(kind["rule"]!.AsObject())).Append('\n');
            if (kind["next"] is JsonObject next)
            {
                text.Append("  next: ").Append((int)next["count"]!).Append(" on ").Append(Day((string?)next["at"]));
                if (next["bytes"] is { } bytes) text.Append(", ").Append(Size((long)bytes));
                text.Append('\n');
            }
            else if ((string?)kind["nextWhy"] is { } nextWhy)
            {
                text.Append("  next: not read (").Append(nextWhy).Append(")\n");
            }

            if (kind["last"] is JsonObject last)
            {
                var removed = last["removed"]!.AsObject();
                text.Append("  last: ").Append(Moment((string?)last["at"])).Append(" by the ").Append((string?)last["door"]).Append(", ")
                    .Append((int)removed["count"]!).Append(" removed, ").Append(Size((long)removed["bytes"]!));
                var kept = last["kept"]!.AsObject();
                if (kept.Count > 0) text.Append("; kept ").Append(string.Join(", ", kept.Select(pair => $"{(int)pair.Value!} ({pair.Key})")));
                text.Append('\n');
            }
            else if ((string?)kind["lastWhy"] is { } lastWhy)
            {
                text.Append("  last: not known (keeping.json ").Append(lastWhy == "unreadable" ? "could not be read" : lastWhy).Append(")\n");
            }
            else if (SpaceKinds.Named((string)kind["id"]!)?.Keeps is not null)
            {
                text.Append("  last: nothing removed yet\n");
            }
        }

        return text.ToString();
    }

    private static string Sized(JsonObject kind)
    {
        if ((bool)kind["counting"]! && kind["bytes"] is null) return "counting…";
        if (kind["bytes"] is not { } bytes) return "not counted yet (--measure counts it)";
        var size = (bool)kind["partial"]! ? $"at least {Size((long)bytes)} ({(string?)kind["partialWhy"]})" : Size((long)bytes);
        return $"{size} in {(int)kind["files"]!:N0} files, counted {Moment((string?)kind["measured"])}" + ((bool)kind["counting"]! ? ", counting again" : "");
    }

    /// <summary>A kind's rule in words, as the card says it.</summary>
    private static string Rule(JsonObject rule) => ((string?)rule["kind"], (string?)rule["whose"]) switch
    {
        ("days", _) when rule["whole"] is { } whole && (bool)whole => $"kept {(int)rule["days"]!} days, each folder whole",
        ("days", _) when rule["whole"] is not null => $"kept {(int)rule["days"]!} days; its package folders go an hour after the move",
        ("days", _) when rule["days"] is { } days => $"kept {(int)days} days",
        ("never", _) => "kept until you remove it",
        ("press", _) => "kept until you clear it",
        ("proof", _) => "cleaned up once its work is on the line",
        ("own", "browser") => "the browser's own limit",
        ("own", "agents") => "yours, and each agent's own rule",
        ("own", "person") => "removed only by your press (`space free --early left-over`)",
        ("own", _) => "each by its own rule",
        var (other, _) => other ?? "none",
    };

    /// <summary>A size in its unit, as a person reads it.</summary>
    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 30):0.0} GB"),
        >= 1L << 20 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 20):0.0} MB"),
        >= 1L << 10 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 10):0} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes} bytes"),
    };

    private static string Moment(string? at) =>
        DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC"
            : at ?? "";

    private static string Day(string? at) =>
        DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : at ?? "";
}
