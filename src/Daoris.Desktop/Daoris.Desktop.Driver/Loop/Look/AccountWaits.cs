namespace Daoris.Driver;

/// <summary>
/// The waits a look's starts came to: those held on a cooling account, one wait per account, each written to the machine log the
/// first look it appears in and never again until a new cool-off; then those held on accounts not signed in with none cooling, one
/// wait per agent with no time, written once per set of accounts.
/// </summary>
internal static class AccountWaits
{
    public static IReadOnlyList<AccountWait> Of(IEnumerable<StartCame> begun, HarnessRoster harnesses, ServiceClient service)
    {
        var waits = begun
            .Where(each => each.Came?.Cooling is not null)
            .GroupBy(each => $"{each.Came!.Cooling!.Agent}/{each.Came.Cooling.Account ?? ""}", StringComparer.OrdinalIgnoreCase)
            .Select(account =>
            {
                var held = account.ToList();
                var cooling = held[0].Came!.Cooling!;
                var workspaces = held.Select(each => each.Started.Workspace).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                // The person's name for the account, read as the look says the wait: the page says it, the log the id.
                var names = AccountNames.Of(harnesses.Home, cooling.Agent);
                // The agent the start asked, as its plan answered it, where its hold says one; else the one it began on.
                return new AccountWait(
                    held[0].Came!.Agent ?? held[0].Started.Adapter, cooling.Agent, cooling.Account, workspaces.Count == 1 ? workspaces[0] : null,
                    cooling.Until, cooling.Stated, held[0].Came!.Held ?? CoolingWords.Hold(cooling, harnesses.Zone, names))
                {
                    Quests = [.. held.Select(each => each.Started.Quest).OfType<string>()],
                    Asks = [.. held.Select(each => each.Started.Ask).OfType<string>()],
                    Repositories = [.. held.Select(each => each.Started.Where).Distinct(StringComparer.OrdinalIgnoreCase)],
                    SignedOut = SignedOutOf(held),
                    Name = cooling.Account is { } id && names.TryGetValue(id, out var name) ? name : null,
                };
            })
            .ToList();

        foreach (var wait in waits.Where(wait => harnesses.NewWait(wait.Agent, wait.Account, wait.Until, wait.SignedOut)))
        {
            service.AccountSaid(AccountLine.Waiting(
                wait.Adapter, wait.Account, wait.Workspace, wait.Until, wait.Quests.Count + wait.Asks.Count, wait.SignedOut));
        }

        // A start held on accounts not signed in with none cooling waits for a person, not a time: a wait with no time and no
        // account, naming the accounts a sign-in frees, its quests and its asks' intakes among what it holds. The tick hands it to
        // the page as it hands a cool-off's, since a consideration names quests alone, and an intake held so was the log's line and
        // nothing else. One per agent, as the line is.
        var signedOut = begun
            .Where(each => each.Came is { Cooling: null, SignedOut: not null })
            .GroupBy(each => each.Came!.SignedOut!.Agent, StringComparer.OrdinalIgnoreCase)
            .Select(agent =>
            {
                var held = agent.ToList();
                var workspaces = held.Select(each => each.Started.Workspace).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return new AccountWait(
                    held[0].Came!.Agent ?? held[0].Started.Adapter, held[0].Came!.SignedOut!.Agent, null, workspaces.Count == 1 ? workspaces[0] : null,
                    Until: null, Stated: false, held[0].Came!.Held ?? "")
                {
                    Quests = [.. held.Select(each => each.Started.Quest).OfType<string>()],
                    Asks = [.. held.Select(each => each.Started.Ask).OfType<string>()],
                    Repositories = [.. held.Select(each => each.Started.Where).Distinct(StringComparer.OrdinalIgnoreCase)],
                    SignedOut = SignedOutOf(held),
                    NoAccount = held.Any(each => each.Came!.SignedOut!.NoAccount),
                };
            })
            .ToList();

        // Kept apart from a cool-off of the tool's own sign-in, whose account is null too: no profile name has a colon.
        foreach (var wait in signedOut.Where(wait => harnesses.NewWait(wait.Agent, ":signed-out", null, wait.SignedOut)))
        {
            service.AccountSaid(AccountLine.Waiting(
                wait.Adapter, null, wait.Workspace, until: null, wait.Quests.Count + wait.Asks.Count, wait.SignedOut));
        }

        return [.. waits, .. signedOut];
    }

    private static IReadOnlyList<string> SignedOutOf(IEnumerable<StartCame> held) =>
        [.. held.SelectMany(each => each.Came?.SignedOut?.Accounts ?? []).Distinct(StringComparer.OrdinalIgnoreCase)];
}
