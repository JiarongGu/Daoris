using System.Text;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris home account-ids</c>: the one-time move of every agent account id an earlier build named by a counter or a word to the hash
/// form (<see cref="AccountIdMoves"/>). <c>--plan</c> says each move, the account's name beside it, and every reference it would change,
/// changing nothing; <c>--yes</c> does it, only while nothing of Daoris runs on the home, and says what it changed. Run again, it finds
/// nothing. An account whose folder a process holds is said with that process (name and id), left whole and moved by the next <c>--yes</c>.
/// </summary>
/// <remarks>
/// <para><b>Refused while Daoris runs</b>, as <c>home purge</c> is (<see cref="HomeAtRest"/>): a running instance holds the stores open
/// and its sessions run in the folders that move. It takes the lease for its act. From a session's terminal it lists and never moves.</para>
/// <para>Exit codes: 0 listed, moved or nothing to move · 1 refused, a store that does not read, or an id whose folder did not move ·
/// 2 a word it does not answer.</para>
/// </remarks>
public static class AccountIdsCommand
{
    public const string Usage =
        "usage: daoris home account-ids --plan     each account id an earlier build named, the hash id it moves to and every reference\n"
        + "       daoris home account-ids --yes      move them, once Daoris is closed";

    private const string Verb = "home account-ids";

    /// <param name="args">The words after <c>account-ids</c>.</param>
    /// <param name="session">The terminal's <c>DAORIS_SESSION_ID</c>: a session's terminal lists and never moves.</param>
    /// <param name="knowledgeDb">Where the environment moved the knowledge store to (<c>DAORIS_KNOWLEDGE_DB</c>), or null for the home's own.</param>
    public static int Run(IReadOnlyList<string> args, string home, string? session, string? knowledgeDb, out string said)
    {
        var output = new StringBuilder();
        var code = Run(args, home, session, knowledgeDb, output);
        said = output.ToString();
        return code;
    }

    private static int Run(IReadOnlyList<string> args, string home, string? session, string? knowledgeDb, StringBuilder output)
    {
        if (args is not [var only] || only is not ("--plan" or "--yes"))
        {
            output.AppendLine("home: say --plan or --yes.");
            output.AppendLine(Usage);
            return 2;
        }

        try
        {
            if (only == "--plan") return Listed(AccountIdMoves.Plan(home, knowledgeDb), home, output);
            if (session is { Length: > 0 })
            {
                output.AppendLine("home: moving account ids moves the folders sessions run in, so it is the person's, from their own terminal; this one is "
                    + "a session's. `daoris home account-ids --plan` lists what it would move.");
                return 1;
            }

            if (HomeAtRest.Running(home, Verb) is { } why) return Refused(why, output);
            HomeLease? lease = null;
            if (!HomeAtRest.OwnLease(home, Verb))
            {
                lease = HomeLease.TryAcquire(home, LeaseKind.Verb, out var holder, Verb);
                if (lease is null) return Refused($"{home} is held by {holder?.Named ?? "another process"}.", output);
            }

            using var held = lease;
            if (HomeAtRest.Sessions(home) is { } running) return Refused(running, output);
            return Done(AccountIdMoves.Apply(home, knowledgeDb), home, output);
        }
        catch (DriverException refused)
        {
            output.AppendLine($"home: {refused.Message}");
            return 1;
        }
    }

    private static int Listed(AccountIdPlan plan, string home, StringBuilder output)
    {
        if (plan.Nothing)
        {
            output.AppendLine("home: every account id is the hash form already. Nothing was changed.");
            return 0;
        }

        output.AppendLine($"home: {Head(plan)} would move. Nothing was changed.");
        Lines(plan, home, output, "drop");
        output.AppendLine("  `daoris home account-ids --yes` does it, once Daoris is closed.");
        return 0;
    }

    private static int Done(AccountIdPlan done, string home, StringBuilder output)
    {
        if (done.Nothing)
        {
            output.AppendLine("home: every account id is the hash form already. Nothing was changed.");
            return 0;
        }

        output.AppendLine($"home: {Head(done)} moved.");
        Lines(done, home, output, "dropped");
        return done.Moves.Any(move => move.Failed is not null) ? 1 : 0;
    }

    private static string Head(AccountIdPlan plan) =>
        $"{AccountIdJson.Counted(plan.Moves.Count, "account id")} with {AccountIdJson.Counted(plan.Moves.Sum(move => move.References.Count), "reference")}"
        + $" and {AccountIdJson.Counted(plan.Strays.Count, "stray entry", "stray entries")}";

    /// <summary>Each move, with the account's name where it has one, and each reference under it; then each stray entry.</summary>
    private static void Lines(AccountIdPlan plan, string home, StringBuilder output, string drop)
    {
        foreach (var move in plan.Moves)
        {
            var (agent, from, to) = (move.Rename.Agent, move.Rename.From, move.Rename.To);
            var name = AccountNames.NameOf(home, agent, from) ?? AccountNames.NameOf(home, agent, to);
            output.AppendLine($"  {agent}  {from} → {to}{(name is null ? "" : $"  ({name})")}");
            if (move.Failed is { } why) output.AppendLine($"    not moved: {why}.");
            foreach (var reference in move.References) output.AppendLine($"    {reference.Place}  {reference.Where}");
        }

        foreach (var stray in plan.Strays) output.AppendLine($"  {drop}  accounts.json  {stray.Agent}.{stray.Entry}: no account's");
    }

    private static int Refused(string why, StringBuilder output)
    {
        output.AppendLine($"home: {why} Close Daoris (its window's Quit, or Ctrl+C in a `daoris drive`), then run this again: nothing was moved.");
        return 1;
    }
}
