using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The <c>use</c> of <see cref="AccountUseView"/>: the settings <c>daoris agent account use</c> takes, judged as it judges them — a way
/// to use accounts, a switch and a near this build knows, a curve that keeps its rules, a kept account of the set that leaves driven work
/// another — and spelled as that command.
/// </summary>
internal static class AccountUseJudge
{
    public static PressViewed View(JsonElement? payload, string owner, JsonElement set)
    {
        var body = payload is { ValueKind: JsonValueKind.Object } sent ? sent : default;
        var command = $"daoris agent account use {owner}";
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("workspace", out var workspace) && workspace.ValueKind == JsonValueKind.String)
        {
            return PressViewed.Refused($"every account signed in for Daoris serves every workspace (D197), so a workspace names nothing here — `daoris agent account order {owner} <account>` puts one first.");
        }

        if (PressPayload.Flag(body, "clear"))
        {
            return PressViewed.Shown(new PressView("account-use-clear", new Dictionary<string, string>(StringComparer.Ordinal) { ["agent"] = owner }, $"{command} --clear"));
        }

        string? use = Word(body, "use"), keep = Word(body, "keep"), switched = Word(body, "switch");
        var (noKeep, noCurve) = (PressPayload.Flag(body, "noKeep"), PressPayload.Flag(body, "noCurve"));
        bool? early = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("early", out var flag)
            ? flag.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;
        int? near = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("near", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var whole)
            ? whole
            : null;
        UseCurve? curve = null;
        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("curve", out var raw) && raw.ValueKind == JsonValueKind.Object)
        {
            if (UseCurve.Problem(raw) is { } broken) return PressViewed.Refused(broken);
            curve = UseCurve.Read(raw);
        }

        if (use is null && keep is null && !noKeep && early is null && near is null && switched is null && curve is null && !noCurve)
        {
            return PressViewed.Refused("a use names how the accounts are used: `use` (goal or order), `keep` (one of the accounts, or none), "
                + "`switch` (smart, curve or off) or `curve` (your own points, a window each).");
        }

        var list = PressPayload.Rows(set, "scopes").FirstOrDefault() is { ValueKind: JsonValueKind.Object } scope
            ? PressPayload.Rows(scope, "list").Select(each => each.GetString() ?? "").ToList()
            : [];
        if (list.Count == 0)
        {
            return PressViewed.Refused($"`{owner}` has no account of Daoris's, so there is nothing to use — `daoris agent login {owner} --new` signs in to "
                + "one, and how its accounts are used is set beside them.");
        }

        if (use is not null && !RotationUse.Modes.Contains(use, StringComparer.Ordinal))
        {
            return PressViewed.Refused($"`{use}` is not a way to use accounts — goal (make the most of them) or order (one by one, in order).");
        }

        if (switched is not null && !RotationUse.Switches.Contains(switched, StringComparer.Ordinal))
        {
            return PressViewed.Refused($"`{switched}` is not when to switch accounts — smart (Daoris's curve), curve (your own) or off.");
        }

        if (near is { } percent && percent is < RotationUse.NearLowest or > RotationUse.NearHighest)
        {
            return PressViewed.Refused($"near is a whole percent from {RotationUse.NearLowest} to {RotationUse.NearHighest}, not {percent}: it holds "
                + "your own curve's five-hour window at that share.");
        }

        if (keep is not null)
        {
            switch (ScopeProblem.Of(null, list, keep))
            {
                case { Kind: ScopeProblemKind.Keep }:
                    return PressViewed.Refused($"`{keep}` is not one of `{owner}`'s accounts ({string.Join(", then ", list)}) — the kept account is one "
                        + "of Daoris's accounts.");
                case { Kind: ScopeProblemKind.Alone }:
                    return PressViewed.Refused($"`{owner}` has no account of Daoris's but `{keep}`, so keeping it for conversations would leave driven "
                        + "work none.");
            }
        }

        var flags = new List<string>();
        if (use is not null) flags.Add(use);
        if (keep is not null) flags.Add($"--keep {ShellWord.Of(keep, ShellWord.Account)}");
        else if (noKeep) flags.Add("--no-keep");
        if (early is { } switchesEarly) flags.Add($"--early {(switchesEarly ? "on" : "off")}");
        if (near is { } at) flags.Add($"--near {at}");
        if (switched is not null) flags.Add($"--switch {switched}");
        if (noCurve) flags.Add("--no-curve");
        else if (curve is not null)
        {
            flags.AddRange(UseCurve.Windows.Where(window => curve[window] is not null)
                .Select(window => $"--curve {window}={string.Join(",", curve[window]!.Select(point => $"{point.Elapsed}:{point.Used}"))}"));
        }

        var tail = string.Join(" ", flags);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["agent"] = owner, ["changes"] = tail };
        return PressViewed.Shown(new PressView("account-use", facts, $"{command} {tail}"));
    }

    private static string? Word(JsonElement body, string name) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && value.GetString()!.Trim() is { Length: > 0 } text ? text : null;
}
