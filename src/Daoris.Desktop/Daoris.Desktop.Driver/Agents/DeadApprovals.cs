using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A proposal to allow or ask a rule that a deny of the same mode would beat, refused where it is proposed so nothing is kept that
/// cannot take effect. It says which deny and where it is set: the person's own deny of that very rule (its scope named), or the
/// <c>no-push</c> default where the person switched it on and the rule names a git push.
/// </summary>
public static partial class DeadApprovals
{
    /// <summary>Why the change could never take effect, or null where it could.</summary>
    public static string? Refusal(PermissionFile file, ProposedChange change)
    {
        if (change is not { Action: "add", List: RuleList.Allow or RuleList.Ask, Rule: { } rule }) return null;

        foreach (var (where, lists) in Scopes(file))
        {
            if (lists.Deny.Contains(rule, StringComparer.Ordinal))
            {
                return $"this rule can never take effect: a deny of `{rule}` is set in {where}, and a deny beats an allow or an ask. "
                       + "Remove that deny first.";
            }
        }

        var noPush = PermissionRules.Defaults.First(shipped => shipped.Id == PermissionRules.NoPushId);
        if (PermissionRules.IsOn(file, noPush) && NamesPush().IsMatch(rule))
        {
            return "this rule can never take effect: the `no-push` default, which you switched on (Settings → the repository's agent rules), "
                   + "denies every push and a deny beats an allow or an ask. Switch `no-push` off, or set the place's git access Write "
                   + "(`daoris git access … write=ask|allow`), which opens a push while it is on.";
        }

        return null;
    }

    private static IEnumerable<(string Where, RuleLists Lists)> Scopes(PermissionFile file)
    {
        yield return ("this machine's rules", file.Machine);
        foreach (var (name, lists) in file.Workspaces) yield return ($"workspace `{name}`'s rules", lists);
        foreach (var (name, lists) in file.Repositories) yield return ($"`{name}`'s rules", lists);
    }

    // A Bash rule running git whose subcommand, after any options, is `push`; `git stash push` is no push.
    [GeneratedRegex(@"^\s*Bash\(\s*git(\s+-\S+(\s+(?!push\b)[^\s-]\S*)?)*\s+push(\b|$)", RegexOptions.IgnoreCase)]
    private static partial Regex NamesPush();
}
