namespace Daoris.Driver;

/// <summary>Who settles a proposal: the person, or a supervisor's role with the decision that gave its yes.</summary>
public sealed record RuleSettler(string Who, string? Role = null, string? Decision = null)
{
    public static RuleSettler Person { get; } = new("the person");

    /// <summary>A supervisor's settlement, which an exclusion read under the lock can return to the person.</summary>
    public static RuleSettler Supervisor(string role, string decision) => new("the supervisor", role, decision);

    public bool IsSupervisor => Role is not null;
}

/// <summary>How a settlement ended: the proposal settled, or handed back to the person unwritten.</summary>
public enum SettleOutcome
{
    Settled,
    Returned,
    Pending,
}

/// <summary>A settlement's outcome and the proposal as it stands after it.</summary>
public sealed record RuleSettled(RuleProposal Proposal, SettleOutcome Outcome, string? Why = null);

/// <summary>
/// The one settlement of a proposal, whoever presses: the person's press and verb, a supervisor's keeping. Under an exclusive lock on the
/// proposal's file it refuses one already settled; a yes is one change of the rules (<see cref="PermissionChange"/>) that judges the
/// change against the rules as they stand, marks the proposal <c>settling</c> and saves, and once the rule file's lock is let go
/// marks it settled. A proposal found <c>settling</c> is never saved again: it is settled where the rules already say its change,
/// else returned to the person, so an edit made between the save and the mark stands.
/// </summary>
public static class ProposalSettlement
{
    /// <exception cref="DriverException">No such proposal, one already settled, or one the rules cannot take.</exception>
    public static RuleSettled Settle(string home, string id, RuleSettler by, bool yes, string? receipt, DateTimeOffset at)
    {
        var key = id.Trim().TrimStart('#');
        var path = RuleProposals.PathOf(home, key);
        // An id names a file in the folder, and a path is not an id.
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z0-9_-]+$") || !File.Exists(path))
        {
            throw new DriverException($"no proposal `#{key}` on this machine — `daoris agent rules proposals` lists them.");
        }

        using var held = KeepLock.Take(path);
        var proposal = RuleProposals.Parse(path) ?? throw new DriverException($"proposal `#{key}` could not be read.");
        if (proposal.State == ProposalState.Settling) return Recovered(home, path, proposal, at);
        if (proposal.State is not (ProposalState.Proposed or ProposalState.Waiting))
        {
            var who = proposal.SettledBy == "the supervisor" ? " by its supervisor" : "";
            throw new DriverException($"proposal `#{key}` was already {proposal.State.ToString().ToLowerInvariant()}{who} — it is history now.");
        }

        if (!yes)
        {
            RuleProposals.Mark(path, ProposalState.Declined, by.Who, receipt, at, by.Role, by.Decision);
            return new(proposal with { State = ProposalState.Declined, Settled = at, SettledBy = by.Who, Note = receipt }, SettleOutcome.Settled);
        }

        string? reserved = null;
        var marked = false;
        try
        {
            PermissionChange.Change(home, rules =>
            {
                if (by.IsSupervisor && WideningAllows.Reserved(rules, proposal.Change) is { } why)
                {
                    reserved = why;
                    return rules;
                }

                if (RuleProposals.Effect(rules, proposal.Change) == ChangeEffect.Nothing) return rules;
                RuleProposals.Mark(path, ProposalState.Settling, by.Who, receipt, at, by.Role, by.Decision);
                marked = true;
                return RuleProposals.Apply(rules, proposal.Change);
            });
        }
        catch (Exception) when (marked)
        {
            // A refused save leaves nothing for a settling mark to wait on.
            RuleProposals.Mark(path, proposal.State, "the driver", "the rules could not be written, so nothing was settled.", at);
            throw;
        }
        if (reserved is not null)
        {
            RuleProposals.Mark(path, ProposalState.Waiting, "the driver", "it stays for the person to decide.", at);
            return new(proposal with { State = ProposalState.Waiting }, SettleOutcome.Returned, reserved);
        }

        RuleProposals.Mark(path, ProposalState.Accepted, by.Who, receipt, at, by.Role, by.Decision);
        return new(proposal with { State = ProposalState.Accepted, Settled = at, SettledBy = by.Who, Note = receipt }, SettleOutcome.Settled);
    }

    /// <summary>A proposal found <c>settling</c>, settled or returned by what the rules hold; none where it is not settling.</summary>
    public static RuleSettled? Recover(string home, string id, DateTimeOffset at)
    {
        var path = RuleProposals.PathOf(home, id);
        if (!File.Exists(path)) return null;
        using var held = KeepLock.Take(path);
        return RuleProposals.Parse(path) is { State: ProposalState.Settling } proposal ? Recovered(home, path, proposal, at) : null;
    }

    private static RuleSettled Recovered(string home, string path, RuleProposal proposal, DateTimeOffset at)
    {
        var rules = PermissionRules.Load(home);
        // A rules file that does not read leaves it settling for the next look.
        if (rules.Problem is not null) return new(proposal, SettleOutcome.Pending, rules.Problem);
        if (RuleProposals.Effect(rules, proposal.Change) == ChangeEffect.Nothing)
        {
            RuleProposals.Mark(path, ProposalState.Accepted, proposal.SettledBy ?? "the driver", proposal.Note, proposal.Settled ?? at, proposal.SettledRole, proposal.SettledDecision);
            return new(proposal with { State = ProposalState.Accepted }, SettleOutcome.Settled);
        }

        RuleProposals.Mark(path, ProposalState.Waiting, "the driver", "the rules no longer say what a yes had saved, so it waits for the person.", at);
        return new(proposal with { State = ProposalState.Waiting }, SettleOutcome.Returned);
    }
}
