using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A proposal to allow or ask a git push, refused where it is proposed: the fixed <c>no-push</c> deny always wins over a harness rule and
/// only git access's Write opens a push, so such a rule could never work. The refusal names the place's Write and the verb that sets it,
/// a loosening the person confirms. A deny naming a push narrows as any rule does.
/// </summary>
public static partial class PushProposals
{
    /// <summary>Why the change is refused, or null where it opens no push.</summary>
    public static string? Refusal(ProposedChange change)
    {
        if (change is not { Action: "add", List: RuleList.Allow or RuleList.Ask, Rule: { } rule } || !NamesPush().IsMatch(rule)) return null;
        var (place, verb) = change.Scope switch
        {
            RuleScope.Machine => ("this machine's", "--machine"),
            RuleScope.Workspace => ($"workspace `{change.Name}`'s", $"--workspace {change.Name}"),
            _ => ($"`{change.Name}`'s", change.Name),
        };
        return $"a rule can never open a push: the fixed `no-push` deny always wins, and only git access's Write opens one. Set {place} "
               + $"Write with `daoris git access {verb} write=ask` (or `write=allow`), which the person confirms.";
    }

    // A Bash rule running git whose subcommand, after any options, is `push`; `git stash push` keeps a change here and is no push.
    [GeneratedRegex(@"^\s*Bash\(\s*git(\s+-\S+(\s+(?!push\b)[^\s-]\S*)?)*\s+push(\b|$)", RegexOptions.IgnoreCase)]
    private static partial Regex NamesPush();
}
