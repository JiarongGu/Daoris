using static Daoris.Driver.HelpProposals;
using static Daoris.Driver.HelpSettingWords;

namespace Daoris.Driver;

/// <summary>
/// <c>git-use</c> (GITAUTH1e, D198 point 15; the git-auth design §9), as <c>daoris git use</c> reads it: a repository's remote
/// (<c>origin</c> unless <c>--remote</c> names one) or, with <c>--host</c> and the target a workspace, a workspace's host pointed at an
/// identity that exists on this machine, or cleared with <c>--clear</c>. Never <c>own</c>: the person's own sign-ins are a widening
/// only the person's press makes; never an identity created, tested or removed.
/// </summary>
internal static class HelpGitUseSetting
{
    private const string Usage =
        "`git-use` takes an identity's label, or `--clear`: for a repository `[--remote <name>] <identity>|--clear`, and for a workspace `--host <host> <identity>|--clear`.";

    public static HelpPlan Plan(string? target, string? workspace, string value, HelpMachineFacts facts)
    {
        HelpPlan Refused(string why) => new(why, "", "", null);
        var words = Words(value);
        string? remote = null, host = null, chosen = null;
        var clear = false;
        for (var at = 0; words is not null && at < words.Count; at++)
        {
            if (words[at] == "--remote" && at + 1 < words.Count) remote = words[++at];
            else if (words[at] == "--host" && at + 1 < words.Count) host = words[++at];
            else if (words[at] == "--clear") clear = true;
            else if (chosen is null && !words[at].StartsWith("--", StringComparison.Ordinal)) chosen = words[at];
            else return Refused(Usage);
        }

        if (words is null || clear == (chosen is not null)) return Refused(Usage);
        if (chosen is not null && string.Equals(chosen, GitIdentityUse.Own, StringComparison.OrdinalIgnoreCase))
            return Refused("a proposal never chooses the person's own git sign-ins: that widens what Daoris's git may use, and is the person's press in Settings → Git.");

        HelpGitIdentity? identity = null;
        if (chosen is not null)
        {
            identity = facts.GitIdentities.FirstOrDefault(each => string.Equals(each.Label, chosen, StringComparison.OrdinalIgnoreCase) || each.Id == chosen);
            if (identity is null) return Refused($"there is no git identity `{chosen}` on this machine — one of {Names(facts.GitIdentities.Select(each => each.Label).ToList())}.");
        }

        var named = identity is null ? "" : $" {ShellWord.Of(identity.Label, ShellWord.Workspace)}";
        var point = identity is null ? "Take back" : $"Point";
        if (workspace is { Length: > 0 })
        {
            if (target is { Length: > 0 }) return Refused("a git identity is chosen for a repository's remote or a workspace's host — name exactly one.");
            if (host is not { Length: > 0 }) return Refused("a workspace chooses a git identity for a host: add `--host <host>`.");
            if (identity is not null && !string.Equals(identity.Host, GitIdentityActs.HostOf(host), StringComparison.OrdinalIgnoreCase))
                return Refused($"git identity `{identity.Label}` is for {identity.Host}, not {GitIdentityActs.HostOf(host)}.");
            return new HelpPlan(null,
                identity is null ? $"Take back workspace `{workspace}`'s choice of git identity for {host}." : $"Use git identity `{identity.Label}` for {host} in workspace `{workspace}`.",
                $"daoris git use --workspace {ShellWord.Of(workspace, ShellWord.Workspace)} --host {host}{(identity is null ? " --clear" : named)}", c => c)
            { GitUse = new HelpGitUse("workspace", null, workspace, remote, host, identity?.Id) };
        }

        if (target is not { Length: > 0 }) return Refused("a git identity is chosen for a repository's remote or a workspace's host — name exactly one.");
        remote ??= "origin";
        return new HelpPlan(null,
            identity is null ? $"Take back the git identity chosen for {remote} of `{target}`." : $"Use git identity `{identity.Label}` for {remote} of `{target}`.",
            $"daoris git use {target}{(remote == "origin" ? "" : $" --remote {remote}")}{(identity is null ? " --clear" : named)}", c => c)
        { GitUse = new HelpGitUse("repository", target, null, remote, null, identity?.Id) };
    }
}
