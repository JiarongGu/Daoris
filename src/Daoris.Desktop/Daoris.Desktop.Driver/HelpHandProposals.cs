namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>hand</c> proposal (WSR5b): a branch a landing made and recorded, handed to a landing
/// plugin afterwards — the review's own <i>hand it to</i>, and <c>daoris-driver trees hand</c> at a terminal.
/// </summary>
/// <remarks>
/// <para><b>Judged by what the door judges first</b>: a branch the record holds under the session or branch
/// the card names (in the repository it names, where a name alone is in several), and a plugin — the one
/// the card names, else the repository's landing rule's — that can land work here (D100's refusals). What
/// only the checkout can answer, the branch still standing, moved since its push, already on the line, is
/// the door's at the press, in its own words.</para>
///
/// <para><b>It pushes</b>, through the plugin, signed in as the person: the card says so before Apply, and a
/// press that does not push changes nothing.</para>
/// </remarks>
public static partial class HelpProposals
{
    private static HelpPlan Hand(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        static HelpPlan Refused(string why, string terminal = "") => new(why, "", terminal, null);
        if (proposal.Door != "hand") return Refused($"`{proposal.Door}` is not a hand-off — its door is `hand`.");

        var named = proposal.Target?.Trim() ?? "";
        var repository = proposal.Repository?.Trim() is { Length: > 0 } where ? where : null;
        var plugin = proposal.Value?.Trim() is { Length: > 0 } chosen ? chosen : null;
        if (named.Length == 0) return Refused("a hand-off names the session that landed the branch, or the branch itself.");

        // As the record's own lookup answers the terminal's `trees hand`: the session's newest landing, or the branch.
        var found = facts.Landed
            .Where(entry => repository is null || string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.Equals(entry.Session, named, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(entry.Branch, named, StringComparison.Ordinal))
            .Reverse()
            .ToList();
        if (found.Count == 0) return Refused(SessionTrees.NotLanded(named));
        var repositories = found.Select(entry => entry.Repository).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (repositories.Count > 1)
        {
            return Refused($"`{named}` names a landed branch in {Names(repositories)} — name the repository it is in.");
        }

        var entry = found[0];
        var terminal = $"daoris-driver trees hand {entry.Branch} --repository {entry.Repository}" + (plugin is null ? "" : $" --plugin {plugin}");
        var rule = LandingRules.Choose(config, entry.Repository, entry.Workspace).Rule;
        var to = plugin ?? (rule.Form == LandingForm.Branch ? rule.Plugin : null);
        if (to is null)
        {
            return Refused($"no plugin is named to hand `{entry.Branch}` to: `{entry.Repository}`'s landing rule names none. "
                + "Name one on the card, or on the rule (`daoris driver landing … --plugin <id>`).", terminal);
        }

        if (LandingRules.PluginProblem(to, facts.Plugins) is { } unready) return Refused(unready, terminal);

        var what = entry.Title is { Length: > 0 } title ? $" “{title}”" : "";
        var since = entry.PullRequest is { } pr ? $" Its last pull request: {pr}" : "";
        return new HelpPlan(null,
            $"Hand `{entry.Branch}` (in `{entry.Repository}`) to plugin `{to}`: it pushes the branch and opens the pull request "
            + $"against the line, signed in as you, for the work landed from session `{entry.Session}`{what}. Nothing is pushed "
            + $"unless it answers that it pushed.{since}",
            terminal, null) { Hand = new HelpHandOff(entry.Repository, entry.Branch, plugin) };
    }
}
