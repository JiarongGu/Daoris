using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Whether a Bash permission rule matches a command, as the harness's maker documents it (Claude Code,
/// permissions, "Wildcard patterns", read 2026-10-01): a <c>*</c> stands for any text, spaces included; a
/// trailing <c>:*</c> is a trailing <c> *</c>; a trailing <c> *</c> that is the rule's only wildcard also
/// matches the bare command; everything else is compared as written.
/// </summary>
/// <remarks>
/// 🔴 A MODEL, for the tables that ask which form of a push meets a deny rule (UNBLOCK4). It is not the
/// harness: it splits no compound command and strips no wrapper, and every row it is asked about is one
/// command. <see cref="BashRuleTests"/> holds it to the maker's own table. That the harness agrees on a
/// real turn is the owner's canary to show.
/// </remarks>
internal static class BashRule
{
    public static bool Matches(string rule, string command)
    {
        if (!rule.StartsWith("Bash(", StringComparison.Ordinal) || !rule.EndsWith(')')) return false;

        var pattern = rule[5..^1];
        if (pattern.EndsWith(":*", StringComparison.Ordinal)) pattern = pattern[..^2] + " *";

        var expression = pattern.Count(c => c == '*') == 1 && pattern.EndsWith(" *", StringComparison.Ordinal)
            ? Regex.Escape(pattern[..^2]) + "( .*)?"
            : string.Join(".*", pattern.Split('*').Select(Regex.Escape));
        return Regex.IsMatch(command, $"^{expression}$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }
}

/// <summary>The model above, held to the rows the harness's maker documents.</summary>
public sealed class BashRuleTests
{
    [Theory]
    [InlineData("Bash(npm run build)", "npm run build")]
    [InlineData("Bash(npm run *)", "npm run build")]
    [InlineData("Bash(npm run *)", "npm run test --watch")]
    [InlineData("Bash(npm run *)", "npm run")]
    [InlineData("Bash(git log * main)", "git log --oneline main")]
    [InlineData("Bash(git log * main)", "git log -5 main")]
    [InlineData("Bash(git * main)", "git merge main")]
    [InlineData("Bash(git * main)", "git push origin main")]
    [InlineData("Bash(git * main)", "git -c core.fsmonitor=<script> diff main")]
    [InlineData("Bash(* --version)", "node --version")]
    [InlineData("Bash(* --version)", "bash -c 'echo hi' --version")]
    [InlineData("Bash(ls *)", "ls -la")]
    [InlineData("Bash(ls *)", "ls")]
    [InlineData("Bash(ls*)", "ls -la")]
    [InlineData("Bash(ls*)", "lsof")]
    [InlineData("Bash(* --help *)", "npm --help x")]
    [InlineData("Bash(ls:*)", "ls -la")]
    [InlineData("Bash(ls:*)", "ls")]
    [InlineData("Bash(git push *)", "git push origin main")]
    public void A_documented_match_matches(string rule, string command) =>
        Assert.True(BashRule.Matches(rule, command), $"{rule} should match `{command}`");

    [Theory]
    [InlineData("Bash(npm run build)", "npm run build --watch")]
    [InlineData("Bash(npm run *)", "npm install")]
    [InlineData("Bash(git log * main)", "git log main")]
    [InlineData("Bash(git log * main)", "git push origin main")]
    [InlineData("Bash(git * main)", "git log")]
    [InlineData("Bash(* --version)", "node -v")]
    [InlineData("Bash(ls *)", "lsof")]
    [InlineData("Bash(* --help *)", "npm --help")]
    [InlineData("Bash(ls:*)", "lsof")]
    // What a `git push` rule does not stop, in the maker's own words (UNBLOCK4's finding).
    [InlineData("Bash(git push *)", "git -C . push origin main")]
    [InlineData("Bash(git push *)", "git -c push.default=current push origin main")]
    [InlineData("Bash(git push *)", "git 'push' origin main")]
    [InlineData("Read(//c/x/**)", "git push")]
    public void A_documented_miss_misses(string rule, string command) =>
        Assert.False(BashRule.Matches(rule, command), $"{rule} should not match `{command}`");
}
