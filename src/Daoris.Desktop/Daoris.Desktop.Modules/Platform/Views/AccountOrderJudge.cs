using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The <c>order</c> of <see cref="AccountUseView"/>: the accounts <c>daoris agent account order</c> puts first, each an account here and once,
/// the rest after in the set's order; or <c>--clear</c>, which the verb sends as an empty list: the ids' order again, which clears no setting.
/// </summary>
internal static class AccountOrderJudge
{
    public static PressViewed View(JsonElement? payload, string owner, IReadOnlyList<string> accounts, JsonElement set)
    {
        IReadOnlyList<string> named = payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("accounts", out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!.Trim()).Where(each => each.Length > 0)]
            : [];
        var clear = PressPayload.Flag(payload is { } sent ? sent : default, "clear");
        if (named.Count > 0 && clear) return PressViewed.Refused("`order` puts accounts first or returns to the ids' order, never both.");

        clear |= named.Count == 0;

        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["agent"] = owner };
        if (clear)
        {
            var ids = accounts.Order(StringComparer.Ordinal).Select(each => $"`{each}`").ToList();
            facts["order"] = ids.Count > 0 ? string.Join(", then ", ids) : "(none)";
            return PressViewed.Shown(new PressView("account-order-clear", facts, $"daoris agent account order {owner} --clear"));
        }

        if (HarnessSettings.OrderProblem(accounts, named) is { } problem) return PressViewed.Refused(problem.Sentence(owner, accounts));

        var inSet = PressPayload.Rows(set, "scopes").FirstOrDefault() is { ValueKind: JsonValueKind.Object } scope
            ? PressPayload.Rows(scope, "list").Select(each => each.GetString() ?? "")
            : [];
        var whole = named.Concat(inSet.Concat(accounts.Order(StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).Where(each => accounts.Contains(each, StringComparer.Ordinal) && !named.Contains(each, StringComparer.Ordinal)));
        facts["order"] = string.Join(", then ", whole.Select(each => $"`{each}`"));
        return PressViewed.Shown(new PressView("account-order", facts, $"daoris agent account order {owner} {ShellWord.Words(named, ShellWord.Account)}"));
    }
}
