using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>profile use &lt;agent&gt; [goal|order] [--keep &lt;account&gt;|--no-keep] [--switch smart|curve|off] [--curve
/// &lt;window&gt;=&lt;elapsed&gt;:&lt;used&gt;,…]… [--no-curve]</c>, or <c>--clear</c>: how the set is used, its one entry of settings, the Accounts place's <c>ACCOUNT_USE</c> <c>use</c>. With
/// no flag it prints the settings, the step the next start would follow and what each account last said. An agent with no account of
/// Daoris's has nothing to use and is refused, naming the sign-in; a kept account is one of the set and leaves driven work another;
/// a curve is refused in the sentence the driver says. <c>--early</c> and <c>--near</c> are accepted aliases.
/// </summary>
internal static class AgentAccountUse
{
    private const string Prefix = AccountVerbContext.Prefix;

    /// <summary><c>profile use</c>'s flags; any other is refused rather than read as a choice.</summary>
    private static readonly HashSet<string> UseFlags = new(StringComparer.Ordinal)
    {
        "--keep", "--no-keep", "--switch", "--curve", "--no-curve", "--early", "--near", "--clear",
    };

    /// <summary><c>profile use</c>'s shape, as a refusal names it.</summary>
    private static readonly string UseShape =
        $"[{string.Join('|', RotationUse.Modes)}] [--keep <account>|--no-keep] [--switch {string.Join('|', RotationUse.Switches)}] "
        + "[--curve <window>=<elapsed>:<used>,…]… [--no-curve], or --clear (--early on|off and --near <percent> "
        + "are the old names)";

    /// <summary>How a curve is given at the terminal, as a refusal shows it.</summary>
    private const string CurveExample = "`--curve weekly=0:30,50:70,100:100`";

    /// <summary>Choices said as a person says them: <c>a, b or c</c>.</summary>
    private static string Either(IReadOnlyList<string> choices) =>
        choices.Count > 1 ? $"{string.Join(", ", choices.Take(choices.Count - 1))} or {choices[^1]}" : string.Concat(choices);

    /// <summary>The verb: the press it sends, or null where it printed and <paramref name="exit"/> is its code.</summary>
    public static AgentPress? Use(AccountVerbContext at, out int exit)
    {
        exit = 0;
        var owner = at.Owner();
        if (at.Args.Skip(2).FirstOrDefault(word => word.StartsWith("--", StringComparison.Ordinal) && !UseFlags.Contains(word)) is { } stray)
        {
            throw new AgentWords($"`{stray}` is not a flag of `agent account use` — it takes {UseShape}.");
        }

        string[] chosen = [.. AgentsCommand.Operands(at.Args, AgentsCommand.Valued).Skip(3)];
        string[] modes = [.. RotationUse.Modes.Select(mode => $"{mode} ({AgentAccountLines.UseWords[mode].Name})")];
        if (chosen.Length > 1)
        {
            throw new AgentWords($"`agent account use` takes one way to use accounts, and `{string.Join(' ', chosen)}` names more — {Either(modes)}.");
        }

        if (chosen.Length == 1 && !RotationUse.Modes.Contains(chosen[0], StringComparer.Ordinal))
        {
            throw new AgentWords($"`{chosen[0]}` is not a way to use accounts — {Either(modes)}. An account is kept for conversations with "
                + "`--keep <account>`.");
        }

        var (change, named) = UseChangeOf(at, owner);
        if (chosen.Length == 1)
        {
            change = change with { Use = chosen[0] };
            named = true;
        }

        JsonObject Payload() => new() { ["action"] = "use", ["door"] = owner };

        var clear = at.Args.Contains("--clear");
        if (clear && named)
        {
            throw new AgentWords($"`--clear` sets no setting beside it — `daoris agent account use {owner} --clear` returns the settings to "
                + "today's defaults, and the setting is set on its own.");
        }

        if (clear)
        {
            var cleared = Payload();
            cleared["clear"] = true;
            return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, cleared,
                _ =>
                {
                    HarnessSettings.Change(at.Path, read => read.WithUse(owner, null), agents: at.Agents);
                    return AgentAccountsCommand.Done();
                },
                _ => null,
                _ =>
                {
                    at.Say($"{Prefix}: `{owner}` sets nothing of its own about how its accounts are used: today's defaults apply.");
                    at.Say($"  Written to {at.Path} — {AgentAccountsCommand.Written}");
                    return 0;
                });
        }

        if (!named)
        {
            exit = UsePrinted(at, owner, at.Settings.Set(owner));
            return null;
        }

        var list = at.Settings.Set(owner).List;
        if (list.Count == 0)
        {
            throw new AgentWords($"`{owner}` has no account of Daoris's, so there is nothing to use yet — `daoris agent login {owner} --new` "
                + "signs in to one, and how its accounts are used is set beside them.");
        }

        if (change.Keep is { } keep)
        {
            switch (ScopeProblem.Of(null, list, keep))
            {
                case { Kind: ScopeProblemKind.Keep } problem:
                    throw new AgentWords($"`{problem.Account}` is not one of `{owner}`'s accounts ({string.Join(", then ", list)}) — the kept "
                        + "account is one of Daoris's accounts; keep one of them.");
                case { Kind: ScopeProblemKind.Alone } problem:
                    throw new AgentWords($"`{owner}` has no account of Daoris's but `{problem.Account}`, so keeping it for conversations would leave "
                        + $"driven work none — `daoris agent login {owner} --new` signs in to another.");
            }
        }

        // As the Accounts place sends a change (`ACCOUNT_USE`'s `UseChangeOf`): a kept account or a curve taken out is its own flag.
        var sent = Payload();
        if (change.Use is { } use) sent["use"] = use;
        if (change.NoKeep) sent["noKeep"] = true;
        else if (change.Keep is { } kept) sent["keep"] = kept;
        if (change.Switch is { } switched) sent["switch"] = switched;
        if (change.NoCurve) sent["noCurve"] = true;
        else if (change.Curve is { } curve) sent["curve"] = CurveNode(curve);
        if (change.Early is { } early) sent["early"] = early;
        if (change.Near is { } near) sent["near"] = near;
        return new AgentPress(at.Verb, AgentRoutes.Account, at.Isolated, sent,
            _ =>
            {
                HarnessSettings.Change(at.Path, read => read.WithUse(owner, change), agents: at.Agents);
                return AgentAccountsCommand.Done();
            },
            _ => null,
            _ =>
            {
                UsePrinted(at, owner, HarnessSettings.LoadSet(at.Path, [owner]).Set(owner));
                at.Say($"  Written to {at.Path} — {AgentAccountsCommand.Written}");
                return 0;
            });
    }

    /// <summary>A curve as the route reads one: each window it names, its points as <c>[elapsed, used]</c>.</summary>
    private static JsonObject CurveNode(UseCurve curve)
    {
        var node = new JsonObject();
        foreach (var window in UseCurve.Windows)
        {
            if (curve[window] is not { } points) continue;
            node[window] = new JsonArray([.. points.Select(point => (JsonNode?)new JsonArray(point.Elapsed, point.Used))]);
        }

        return node;
    }

    /// <summary>
    /// The settings <c>profile use</c>'s flags name, each refused unless it is one, and whether any is named: the switch, the curve,
    /// the aliases, then the kept account, in that order.
    /// </summary>
    private static (UseChange Change, bool Named) UseChangeOf(AccountVerbContext at, string owner)
    {
        var change = new UseChange();
        var named = false;
        IReadOnlyList<string> Values(string flag)
        {
            var values = new List<string>();
            for (var index = 0; index < at.Args.Count; index++)
            {
                if (at.Args[index] != flag) continue;
                var value = index + 1 < at.Args.Count ? at.Args[index + 1] : null;
                if (value is null || value.StartsWith("--", StringComparison.Ordinal) || value.Trim().Length == 0)
                {
                    throw new AgentWords($"`{flag}` needs a value — it takes {UseShape}.");
                }

                values.Add(value);
            }

            return values;
        }

        string? Value(string flag) => Values(flag) is [var first, ..] ? first : null;

        if (Value("--switch") is { } switched)
        {
            if (!RotationUse.Switches.Contains(switched, StringComparer.Ordinal))
            {
                throw new AgentWords("`--switch` is smart, curve or off — smart, Daoris's curve; curve, your own (`--curve`); off, no switch "
                    + "while a session runs.");
            }

            change = change with { Switch = switched };
            named = true;
        }

        var curves = Values("--curve");
        var noCurve = at.Args.Contains("--no-curve");
        if (curves.Count > 0 && noCurve)
        {
            throw new AgentWords("`--curve` and `--no-curve` together say two things — set a window of your curve, or take the curve out.");
        }

        if (curves.Count > 0) (change, named) = (change with { Curve = CurveOf(curves) }, true);
        if (noCurve) (change, named) = (change with { NoCurve = true }, true);

        if (Value("--early") is { } early)
        {
            if (early is not ("on" or "off")) throw new AgentWords("`--early` is on or off — switch before the limit, or not.");
            (change, named) = (change with { Early = early == "on" }, true);
        }

        if (Value("--near") is { } near)
        {
            var percent = near.Length > 0 && near.All(char.IsAsciiDigit) && int.TryParse(near, out var whole) ? whole : -1;
            if (percent is < RotationUse.NearLowest or > RotationUse.NearHighest)
            {
                throw new AgentWords($"`--near` is a whole percent from {RotationUse.NearLowest} to {RotationUse.NearHighest} — e.g. `--near 85`: "
                    + "the old name of your curve's five-hour window, flat at that share; a start passes an account whose use of that window "
                    + "is over it.");
            }

            (change, named) = (change with { Near = percent }, true);
        }

        var keep = Value("--keep");
        var noKeep = at.Args.Contains("--no-keep");
        if (keep is not null && noKeep)
        {
            throw new AgentWords("`--keep` and `--no-keep` together say two things — keep one account for conversations, or none.");
        }

        if (keep is not null) (change, named) = (change with { Keep = at.Account(owner, keep) }, true);
        if (noKeep) (change, named) = (change with { NoKeep = true }, true);
        return (change, named);
    }

    /// <summary>
    /// The curve <c>--curve &lt;window&gt;=&lt;elapsed&gt;:&lt;used&gt;,…</c> gives, once per window: each window's
    /// points as whole numbers, then held to the curve's rules in the sentence the driver says (<see cref="UseCurve.Problem"/>).
    /// </summary>
    private static UseCurve CurveOf(IReadOnlyList<string> given)
    {
        var windows = new JsonObject();
        foreach (var value in given)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value, @"^([^=]+)=([0-9]+:[0-9]+(?:,[0-9]+:[0-9]+)*)$");
            if (!match.Success)
            {
                throw new AgentWords($"`--curve {value}` is not a window and its points — it takes `<window>=<elapsed>:<used>,…`, each a whole "
                    + $"percent, e.g. {CurveExample}.");
            }

            var window = match.Groups[1].Value;
            if (windows.ContainsKey(window)) throw new AgentWords($"`--curve` names `{window}` twice — give each window once.");
            windows[window] = new JsonArray([.. match.Groups[2].Value.Split(',').Select(point =>
                (JsonNode?)new JsonArray([.. point.Split(':').Select(number => (JsonNode?)JsonValue.Create(double.Parse(number, System.Globalization.CultureInfo.InvariantCulture)))]))]);
        }

        var raw = JsonSerializer.SerializeToElement(windows);
        if (UseCurve.Problem(raw) is { } problem)
        {
            throw new AgentWords($"{problem} `--curve` takes <window>=<elapsed>:<used>,…, e.g. {CurveExample}.");
        }

        return UseCurve.Read(raw)!;
    }

    /// <summary>
    /// <c>profile use</c>'s print: the set's order, how it is used, the step the next start would follow, and what each of its accounts
    /// last said.
    /// </summary>
    private static int UsePrinted(AccountVerbContext at, string owner, RotationScope scope)
    {
        if (scope.List.Count == 0)
        {
            at.Say($"{Prefix}: `{owner}` has no account of Daoris's, so it has no settings — `daoris agent login {owner} --new` signs in to one.");
            return 0;
        }

        // Each account by the person's name for it, else its id.
        string Shown(string account) => AccountNames.Shown(at.Home, owner, account);
        at.Say($"{Prefix}: `{owner}`'s accounts, for every workspace, in order: {string.Join(", then ", scope.List.Select(Shown))}.");

        var toolchain = Toolchain(at, owner);
        var says = AgentAccountLines.Says(at.World.Adapters(at.Home), owner);
        var now = at.World.Now();
        var zone = at.World.Zone;
        var said = scope.List.ToDictionary(account => account, account => AccountWindows.SaidOf(at.Home, owner, account, now), StringComparer.Ordinal);

        var (rows, notes) = AgentAccountLines.UseLines(scope, toolchain?.Product ?? owner, says, Shown);
        foreach (var (label, value) in rows) at.Say($"  {label,-24} {value}");
        foreach (var note in notes) at.Say($"  {note}");
        var (row, next) = AgentAccountLines.NextStartLines(scope, says, said.Values.Any(windows => windows is not null), Shown);
        at.Say($"  {"next start",-24} {row}");
        if (next is not null) at.Say($"  {next}");

        // Each account's cool-off, else its last reading and its age, else that it has said nothing: unknown. A reading over the
        // scope's curve says so, as the start's line does.
        var cooling = AccountCooling.Read(at.Home, now);
        at.Say("  what each account last said:");
        foreach (var account in scope.List)
        {
            var cooled = AgentAccountLines.CoolingLinesOf(cooling, owner, account, now, zone);
            at.Say($"    {Shown(account),-14} {(cooled.Count > 0
                ? cooled[0]
                : said[account] is { } reading
                    ? AgentAccountLines.SaidLine(reading, now, zone, scope.Use)
                    : "nothing said about what it has left")}");
            foreach (var more in cooled.Skip(1)) at.Say($"    {"",-14} {more}");
        }

        return 0;
    }

    /// <summary>The toolchain an agent's accounts are said by: its door's, or the agent a door runs as.</summary>
    private static AccountAgent? Toolchain(AccountVerbContext at, string agent) => at.World.Adapters(at.Home).AgentNamed(agent);
}
