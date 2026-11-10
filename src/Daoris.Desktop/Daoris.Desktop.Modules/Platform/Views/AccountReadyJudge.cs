using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The <c>ready</c> of <see cref="AccountUseView"/>: <i>Try now</i>, an account's cool-off ended early as <c>daoris agent account ready</c> ends
/// it. An account of the owner's that is cooling, or <c>--own</c>, the tool's own sign-in, when that is; one not cooling has nothing to end.
/// </summary>
internal static class AccountReadyJudge
{
    public static PressViewed View(JsonElement? payload, string owner, IReadOnlyList<string> accounts, JsonElement set)
    {
        var own = payload is { } sent && PressPayload.Flag(sent, "own");
        var account = PressPayload.Text(payload, "profile");
        if (!own && account.Length == 0)
        {
            return PressViewed.Refused("`ready` names the account whose cool-off ends, or the tool's own sign-in (`--own`).");
        }

        if (!own && !accounts.Contains(account, StringComparer.Ordinal))
        {
            return PressViewed.Refused($"`{owner}` has no account `{account}` on this machine — accounts that exist: "
                + (accounts.Count > 0 ? string.Join(", ", accounts) : "(none)"));
        }

        var cooling = PressPayload.Rows(set, "accounts").Where(row => Cooling(row)).Select(row => PressPayload.Field(row, "name")).ToList();
        var ownCooling = set.ValueKind == JsonValueKind.Object && set.TryGetProperty("own", out var theirs) && Cooling(theirs);
        if (own ? !ownCooling : !cooling.Contains(account, StringComparer.Ordinal))
        {
            var who = own ? $"`{owner}`'s own sign-in" : $"`{account}`";
            var now = cooling.Select(each => $"`{each}`").Concat(ownCooling ? ["its own sign-in"] : []).ToList();
            return PressViewed.Refused($"{who} is not cooling, so there is nothing to end — "
                + (now.Count > 0 ? $"cooling now: {string.Join(", ", now)}." : $"no account of `{owner}` is cooling."));
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["agent"] = owner };
        if (own) return PressViewed.Shown(new PressView("account-ready-own", facts, $"daoris agent account ready {owner} --own"));
        facts["account"] = account;
        return PressViewed.Shown(new PressView("account-ready", facts, $"daoris agent account ready {owner} {ShellWord.Of(account, ShellWord.Account)}"));
    }

    private static bool Cooling(JsonElement row) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty("cooling", out var value) && value.ValueKind == JsonValueKind.Object;
}
