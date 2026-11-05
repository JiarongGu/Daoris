namespace Daoris.Driver;

/// <summary>
/// The harness's rules composed from a session's git access (GITAUTH1f, D198 point 8; the git-auth design §6.8): <c>no-push</c> keeps its
/// id, fixed on, and this is its one writer (ONEWAY1q). Write <i>deny</i> (and Daoris's default, which is deny for a session) leaves the four push
/// forms denied, so a start reads exactly as before; Write <i>ask</i> or <i>allow</i> allows the plain push forms, so the harness lets
/// the command start and the door asks (§6.6), and denies the force, delete and tag forms as before; the doors refuse a force and a
/// delete themselves, for a harness that takes no rules (D233 choice 3). The option-first forms stay denied: an allow so written is the
/// harness's own warning.
/// </summary>
public static class GitAccessPolicy
{
    private static readonly string[] Plain = ["Bash(git push)", "Bash(git push:*)"];

    private static readonly string[] Dangerous =
    [
        "Bash(git push --force:*)", "Bash(git push -f:*)", "Bash(git push --force-with-lease:*)", "Bash(git push --delete:*)",
        "Bash(git push -d:*)", "Bash(git push --tags:*)", "Bash(git push --mirror:*)", "Bash(git push --prune:*)",
    ];

    /// <summary>The classifier's sentence for a push once git access judges it; publishing is <c>no-publish</c>'s own sentence.</summary>
    public const string Judged =
        "A push is judged by Daoris's git access, which the person set, and Daoris's door asks the person where it says ask.";

    /// <summary>Whether a Write value lets the plain push forms start (the door then judges): <i>ask</i> and <i>allow</i>.</summary>
    public static bool Opens(string write) => write is GitAccessKeys.Ask or GitAccessKeys.Allow;

    /// <summary>The rules for a session whose Write resolved to <paramref name="write"/>; unchanged where it is deny or Daoris's default.</summary>
    public static RuleLists Compose(RuleLists rules, string write)
    {
        if (!Opens(write) || !Plain.Any(rules.Deny.Contains)) return rules;
        return rules with
        {
            Allow = [.. rules.Allow.Union(Plain, StringComparer.Ordinal)],
            Deny = [.. rules.Deny.Where(rule => !Plain.Contains(rule)).Union(Dangerous, StringComparer.Ordinal)],
        };
    }

    /// <summary>The classifier's sentences, with a push's sentence said as git access says it where Write opens.</summary>
    public static IReadOnlyList<string> HardDeny(IReadOnlyList<string> told, string write) =>
        !Opens(write) ? told : [.. told.Select(sentence => sentence.StartsWith("Pushing to any remote", StringComparison.Ordinal) ? Judged : sentence)];
}
