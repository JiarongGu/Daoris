namespace Daoris.Driver;

/// <summary>
/// The terminal's words about an agent's accounts (TOOL6a, TOOL6c; D130 §3.2, §6, §16.6): how a scope's list is used, the step the next
/// start would follow, and what each account last said, shared by <c>agent account use</c> and <c>agent list</c>.
/// </summary>
internal static class AgentAccountLines
{
    /// <summary>What the terminal says for each <i>use accounts</i> choice (D130 §16.6): its name, and what it costs where the screen says so.</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string? Cost)> UseWords = new Dictionary<string, (string, string?)>
    {
        ["goal"] = ("make the most of them", null),
        ["order"] = ("one by one, in order", "one limit stops every session on that account"),
    };

    /// <summary>The switch's values as the page names them (<c>harness.use.switch.*</c>), for the terminal to say the same.</summary>
    private static readonly IReadOnlyDictionary<string, string> SwitchWords = new Dictionary<string, string>
    {
        ["smart"] = "Smart", ["curve"] = "My own curve", ["off"] = "Off",
    };

    /// <summary>
    /// Whether an agent's accounts say how much of each window is used (TOOL6c, CODEXUSE1b): a toolchain whose accounts are its own,
    /// its door's or a door onto it (AGT7), carries the frame's words (<c>Windows</c>) or asks its own server (<c>Usage</c>), as
    /// <c>codex-acp</c> asks for <c>codex</c>'s. The CLI's <c>windows</c>.
    /// </summary>
    public static bool Says(AdapterSet set, string owner)
    {
        IEnumerable<HarnessToolchain?> theirs = [set.ToolchainOf(owner), .. set.Names
            .Select(name => (Name: name, Toolchain: set.Resolve(name).Toolchain))
            .Where(each => each.Toolchain is not null && string.Equals(set.OwnerOf(each.Name), owner, StringComparison.OrdinalIgnoreCase))
            .Select(each => each.Toolchain)];
        return theirs.Any(toolchain => toolchain?.Windows is not null || toolchain?.Usage is not null);
    }

    /// <summary>
    /// How a scope's list is used, in the terminal's words: each setting's label and value, and a note
    /// for each thing the person should know: a default outside the list, a kept account that leaves driven work none, and every value
    /// or setting this build does not know, said rather than dropped. <i>Switch accounts</i> says what passes an account at a start
    /// (D165 point 2, the switch design §3), only where the agent says how much of each window is used.
    /// </summary>
    public static (List<(string Label, string Value)> Rows, List<string> Notes) UseLines(
        RotationScope scope, string product, bool says, Func<string, string> shown)
    {
        var use = scope.Use;
        var mode = UseWords[use.Use];
        var rows = new List<(string, string)>
        {
            ("use accounts", mode.Cost is { } cost ? $"{mode.Name} — {cost}" : mode.Name),
            ("kept for conversations", use.Keep is null ? "none" : shown(use.Keep)),
            ("switch accounts", !says
                ? $"{SwitchWords[use.Switch]} — {product}'s sessions here do not say how much of each window is used, so a start passes no account by its use"
                : $"{SwitchWords[use.Switch]} — {(use.Switch == "off" ? "no session moves at a turn's end; " : "a session moves at a turn's end when its use is over the curve; ")}"
                    + $"a start {(use.Switch == "off" ? "still " : "")}passes an account whose limit is reached, that draws on usage "
                    + $"credits, or whose use is over {(use.Switch == "curve" ? "your curve, Daoris's smart curve on a window yours leaves out" : "Daoris's smart curve")}"),
        };
        if (CurveLine(use) is { } curve) rows.Add(("curve", curve));

        var notes = new List<string>();
        switch (ScopeProblem.Of(scope.Default, scope.List, use.Keep))
        {
            case { Kind: ScopeProblemKind.Default } problem:
                notes.Add($"its default, `{shown(problem.Account)}`, is not in this list: name it in the list, or make one of the list the default");
                break;
            case { Kind: ScopeProblemKind.Alone } problem:
                notes.Add($"`{shown(problem.Account)}` is kept for conversations, and it is the one account of Daoris's, so driven work has none");
                break;
        }

        foreach (var name in scope.Unknown)
        {
            notes.Add(RotationUse.Fields.Contains(name, StringComparer.Ordinal)
                ? $"`{name}` holds a value this build does not know, so it reads as today's default; it is kept as written"
                : RotationUse.OldNames.Contains(name, StringComparer.Ordinal)
                    ? $"`{name}` is an old name of the switch, and holds a value this build does not read: nothing reads it, and the next write takes it out"
                    : $"`{name}` is a setting this build does not know: nothing here reads it, and it is kept as written");
        }

        return (rows, notes);
    }

    /// <summary>
    /// The person's curve in the terminal's words (D165 point 2): each window it names, its points as <c>elapsed:used</c>, and where the
    /// switch is not <c>curve</c>, that it is kept and read only there. Null where there is none to show: none set, under another switch.
    /// </summary>
    private static string? CurveLine(RotationUse use)
    {
        string[] windows = [.. UseCurve.Windows.Where(window => use.Curve[window] is not null)];
        if (windows.Length == 0) return use.Switch == "curve" ? "none of your own — each window takes Daoris's smart curve" : null;
        var said = string.Join("; ", windows.Select(window =>
            $"{window} {string.Join(", ", use.Curve[window]!.Select(point => $"{point.Elapsed}:{point.Used}"))}"));
        return use.Switch == "curve" ? said : $"{said} — kept, and read only under curve";
    }

    /// <summary>
    /// The step the next start of a scope would follow (TOOL6b, TOOL6c; D130 §16.3, §16.4, §16.6), in the terminal's words: the goal's
    /// steps as the driver's walk takes them, or the list's order under <c>order</c>, and the kept account driven work passes; where the
    /// agent says how much of each window is used, the curve's steps (D165 point 2, the switch design §3). Prose about
    /// <see cref="AccountRotation.Order"/>: this reads no session record, so it names the steps rather than the account they would choose.
    /// </summary>
    public static (string Row, string? Note) NextStartLines(RotationScope scope, bool says, bool anySaid, Func<string, string> shown)
    {
        if (scope.List.Count == 1) return ($"`{shown(scope.List[0])}`, the one account of Daoris's", null);

        var begins = scope.Begins is null ? null : shown(scope.Begins);
        var from = $"from `{begins}`{(scope.Default is not null && scope.Default == scope.Begins ? ", its default" : "")}";
        var keep = scope.Use.Keep;
        var passes = keep is not null && ScopeProblem.Of(null, scope.List, keep) is null
            ? $"; driven work passes `{shown(keep)}`, kept for conversations"
            : "";
        var off = scope.Use.Switch == "off";
        var last = says
            ? $"; one whose limit is reached, that draws on usage credits or whose use is over {(off ? "Daoris's smart curve" : "its curve")} goes last"
            : "";
        if (scope.Use.Use != "goal") return ($"the first ready account of the order, {from}{passes}{last}", null);

        // Step 4 weighs what would lapse (D165's USAGERESET1 note): only a read week's room, so an agent that says nothing has none.
        var lapses = !says ? "" : "then the one whose week would lapse the most: within its last day, its room under "
            + $"{(off ? "Daoris's smart curve" : "its curve")} per hour to its reset; ";
        var room = !says ? "" : off ? "then the one furthest behind its week's pace; " : "then the one with the most room under its curve; ";
        return (
            $"the ready account running the fewest of Daoris's sessions; {lapses}{room}then the one Daoris started on least recently; "
                + $"then the order, {from}{passes}{last}{(says ? ", the least over first" : "")}",
            anySaid ? null : "no account has said what it has left yet: Daoris spreads starts across them by its own sessions");
    }

    /// <summary>The line a cooling account is given (D125 §2.4): until when, how long that is, why, and how to try it now.</summary>
    public static string CoolingLine(CoolingEntry entry, DateTimeOffset now, TimeZoneInfo zone)
    {
        var ready = $"`daoris agent account ready {entry.Agent} {(entry.Account is null ? "--own" : ShellWord.Of(entry.Account, ShellWord.Account))}`";
        return $"cooling until {CoolingWords.When(entry.Until, zone)}, in {Span(entry.Until - now)} — {CoolingWords.Why(entry)}; {ready} tries it now";
    }

    /// <summary>How long until then, to the minute and rounded up: <c>2 d 2 h</c>, <c>3 h 10 min</c>, <c>1 min</c>.</summary>
    private static string Span(TimeSpan left)
    {
        var minutes = Math.Max(1, (long)Math.Ceiling(left.TotalMinutes));
        var (days, hours) = (minutes / 1440, minutes % 1440 / 60);
        if (days > 0) return hours > 0 ? $"{days} d {hours} h" : $"{days} d";
        return hours > 0 ? $"{hours} h {minutes % 60} min" : $"{minutes} min";
    }

    /// <summary>
    /// What an account last said, as one line a person reads (TOOL6c, D130 §3.2, §9): how long ago, its own word where it gave one,
    /// credits, each window's use and reset in the machine's zone, and its tightest window over the scope's curve with the curve's value
    /// there now, as the start's line says it (SWITCH1b-iii), where a scope's <paramref name="use"/> is given (<c>agent list</c> gives
    /// none). Its pace warning is said in its own words, never as near (USAGEREAD3a).
    /// </summary>
    public static string SaidLine(AccountSaid said, DateTimeOffset now, TimeZoneInfo zone, RotationUse? use)
    {
        var parts = new List<string>();
        if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.RefusedWord) is { } reached)
        {
            parts.Add($"its {reached.Window} limit reached, by its own word");
        }
        else if (said.Windows.FirstOrDefault(each => each.Standing == AccountReadings.WarnedWord) is { } warned)
        {
            parts.Add($"ahead of its pace on its {warned.Window} window, by its own word");
        }

        if (said.Windows.Any(each => each.Credits)) parts.Add("drawing on usage credits");
        // The lead is the newest reading's source, a read winning a tie; a window from the other source says so, with its own age.
        var newest = said.Windows.Aggregate((best, each) =>
            each.Seen > best.Seen || (each.Seen == best.Seen && each.Source == AccountWindows.SourceRead) ? each : best);
        foreach (var each in said.Windows.OrderBy(each => each.Window.ToLowerInvariant() switch { "session" => 0, AccountWindows.Weekly => 1, _ => 2 }))
        {
            if (each.Used is not { } used) continue;
            var own = each.Source == newest.Source ? "" : $" ({each.Source} {RotationWords.Age(each.Seen, now)})";
            parts.Add($"{RotationWords.Percent(used)} of its {each.Window} limit used{own}, resetting {CoolingWords.When(each.Reset, zone)}");
        }

        if (use is not null && UsageCurves.TightestSaid(use, said, now) is { Room: < 0 } over)
        {
            parts.Add($"over its {over.Window.Window} curve's {UsageCurves.Points(over.Curve)}%");
        }

        // A reading with no number, no warning and no credits is its agent's clear word alone.
        if (parts.Count == 0) parts.Add("clear, by its own word");
        return $"{newest.Source} {RotationWords.Age(newest.Seen, now)}: {string.Join("; ", parts)}";
    }
}
