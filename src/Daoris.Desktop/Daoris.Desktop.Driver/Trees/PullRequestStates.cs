using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>The words a pull request's state takes at <see cref="HookPoints.State"/> (PLUGHOOK1a, D148 point 3, design §2.2).</summary>
public static class PullRequestStates
{
    public const string Open = "open";

    /// <summary>Final: a completed pull request is never asked about again.</summary>
    public const string Completed = "completed";

    public const string Abandoned = "abandoned";

    /// <summary>The platform answered without a state: no pull request from that branch, or a status Daoris has no word for.</summary>
    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [Open, Completed, Abandoned, Unknown];
}

/// <summary>How a completed pull request merged, where its platform says (design §2.2). Any other word is read as none.</summary>
public static class MergeHow
{
    public const string Merge = "merge";
    public const string Squash = "squash";
    public const string Rebase = "rebase";
    public const string RebaseMerge = "rebase-merge";

    public static readonly IReadOnlyList<string> All = [Merge, Squash, Rebase, RebaseMerge];
}

/// <summary>
/// A plugin's answer at <see cref="HookPoints.State"/> (PLUGHOOK1a, D148 point 3), and, kept on the landing entry as
/// <c>pullRequestState</c>, the plugin that gave it and when it was asked (D143).
/// </summary>
/// <param name="State">One of <see cref="PullRequestStates"/>.</param>
public sealed record PullRequestState(string State)
{
    /// <summary>The pull request it answered for, as an absolute web address, or null.</summary>
    public string? PullRequest { get; init; }

    /// <summary>The full id of the commit the completion put on its target; set for every completed answer.</summary>
    public string? MergeCommit { get; init; }

    /// <summary>The full id of the source commit the completion merged; set for every completed answer.</summary>
    public string? SourceCommit { get; init; }

    /// <summary>The branch it merged into, without <c>refs/heads/</c>, or null.</summary>
    public string? Target { get; init; }

    /// <summary>One of <see cref="MergeHow"/>, or null.</summary>
    public string? How { get; init; }

    /// <summary>When it completed or was abandoned, or null.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The plugin's own sentence, kept to <see cref="PullRequestAnswers.MessageLimit"/> characters, or null.</summary>
    public string? Message { get; init; }

    /// <summary>The plugin that answered.</summary>
    public string? Plugin { get; init; }

    /// <summary>When Daoris asked; <see cref="DateTimeOffset.MinValue"/> where a record did not keep it.</summary>
    public DateTimeOffset AskedAt { get; init; }
}

/// <summary>
/// The latest ask that failed (design §2.4), kept as <c>pullRequestAskFailed</c> while no answer is newer: one of
/// <see cref="PluginEvents"/>' failure kinds, the plugin, and when. A failure never overwrites an answer.
/// </summary>
public sealed record PullRequestAskFailed(string Code, string Plugin, DateTimeOffset At);

/// <summary>A session branch removed on a completed answer (design §2.5): its tip, when, and which occasion removed it.</summary>
/// <param name="By"><see cref="CarriedBy.Tidy"/> or <see cref="CarriedBy.CleanUp"/>.</param>
public sealed record CarriedBranch(string Branch, string Tip, DateTimeOffset At, string By);

/// <summary>Which occasion removed a session branch on a pull request's answer.</summary>
public static class CarriedBy
{
    /// <summary>LAND3's tidy, after a landing's plugin step.</summary>
    public const string Tidy = "tidy";

    /// <summary>The clean-up's press.</summary>
    public const string CleanUp = "clean-up";
}

/// <summary>
/// What a row says where a kept answer does not clear a branch (design §2.3), and why nothing was asked (§2.1, §2.4). Nothing
/// goes on any of them.
/// </summary>
public static class PullRequestCodes
{
    /// <summary>It completed into a branch other than the line.</summary>
    public const string OtherTarget = "other-target";

    /// <summary>The merge commit is not on the line here; bringing the repository up to date fetches it.</summary>
    public const string MergeNotHere = "merge-not-here";

    /// <summary>The branch holds commits the pull request did not carry, as after LAND2c advanced it.</summary>
    public const string Beyond = "beyond";

    /// <summary>The pull request merged a commit this machine never had.</summary>
    public const string SourceNotHere = "source-not-here";

    /// <summary>Nothing was asked: no plugin pushed it, and no landing rule names one.</summary>
    public const string NoPlugin = "no-plugin";

    /// <summary>Nothing was asked: the plugin is not installed, switched off, unsound, or speaks on no <c>work/state</c> point.</summary>
    public const string Unready = "plugin-unready";

    /// <summary>Nothing was asked this time: the occasion's bound came first.</summary>
    public const string NotAsked = "not-asked";
}

/// <summary>
/// The answer read by shape (PLUGHOOK1a, design §2.2): what <see cref="HookPeer"/> takes from the wire. The plugin kit's wire
/// test holds the same rules in its own code, and <c>PluginKitTests</c> holds the two to one table.
/// </summary>
public static partial class PullRequestAnswers
{
    /// <summary>How much of a plugin's sentence is kept.</summary>
    public const int MessageLimit = 300;

    [GeneratedRegex("^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$", RegexOptions.CultureInvariant)]
    private static partial Regex FullId();

    /// <summary>The answer, or null where it is not one: the caller says so, naming the plugin.</summary>
    public static PullRequestState? Read(JsonElement answer, string plugin)
    {
        if (answer.ValueKind != JsonValueKind.Object) return null;
        if (!answer.TryGetProperty("state", out var said) || said.ValueKind != JsonValueKind.String) return null;
        var state = said.GetString()!;
        if (!PullRequestStates.All.Contains(state, StringComparer.Ordinal)) return null;

        if (!Optional(answer, "pullRequest", out var pullRequest)
            || (pullRequest is not null && !(Uri.TryCreate(pullRequest, UriKind.Absolute, out var address) && address.Scheme is "https" or "http")))
        {
            return null;
        }

        if (!Optional(answer, "mergeCommit", out var merge) || (merge is not null && !FullId().IsMatch(merge))) return null;
        if (!Optional(answer, "sourceCommit", out var source) || (source is not null && !FullId().IsMatch(source))) return null;
        // A completed pull request names the commit it put on its target and the one it merged, or git has nothing to confirm.
        if (state == PullRequestStates.Completed && (merge is null || source is null)) return null;

        if (!Optional(answer, "target", out var target) || target is { Length: 0 }) return null;
        if (!Optional(answer, "how", out var how)) return null;
        if (!Optional(answer, "at", out var at)) return null;
        DateTimeOffset? when = null;
        if (at is not null)
        {
            if (!DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) return null;
            when = parsed;
        }

        if (!Optional(answer, "message", out var message)) return null;
        message = message?.Trim();

        return new PullRequestState(state)
        {
            PullRequest = pullRequest,
            MergeCommit = merge?.ToLowerInvariant(),
            SourceCommit = source?.ToLowerInvariant(),
            Target = target is not null && target.StartsWith("refs/heads/", StringComparison.Ordinal) ? target["refs/heads/".Length..] : target,
            How = how is not null && MergeHow.All.Contains(how, StringComparer.Ordinal) ? how : null,
            At = when,
            Message = message is { Length: > 0 } words ? words[..Math.Min(MessageLimit, words.Length)] : null,
            Plugin = plugin,
        };
    }

    /// <summary>A field absent or null is none; text is its value; anything else is not an answer.</summary>
    private static bool Optional(JsonElement answer, string name, out string? value)
    {
        value = null;
        if (!answer.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null) return true;
        if (field.ValueKind != JsonValueKind.String) return false;
        value = field.GetString();
        return true;
    }
}

/// <summary>What git found here for one entry's kept answer (design §2.3), gathered by <see cref="SessionTrees"/>.</summary>
/// <param name="Line">The repository's line (D86), or null where none is set and git names none.</param>
/// <param name="MergeOn">The form of the line that holds the merge commit (<c>main</c>, <c>origin/main</c>), or null.</param>
/// <param name="SourceHeld">Whether this machine holds the source commit.</param>
/// <param name="Standing">Whether the landed branch stands, holding the commit the landing recorded.</param>
/// <param name="UnderSource">Whether the standing branch's tip is the source commit or an ancestor of it.</param>
public sealed record PullRequestFacts(string? Line, string? MergeOn, bool SourceHeld, bool Standing, bool UnderSource);

/// <summary>
/// What a kept answer proves here (design §2.3).
/// </summary>
/// <param name="Code">Null where nothing stands in the way; else the state (<c>open</c>, <c>abandoned</c>, <c>unknown</c>) or one of <see cref="PullRequestCodes"/>.</param>
/// <param name="Form">The form of the line that holds the merge commit, where it is held there.</param>
/// <param name="Carries">Whether the pull request carried work here: completed into the line, its merge commit on it, its source commit held. Session branches at or under that commit may go.</param>
/// <param name="Clears">Whether the landed branch itself goes: it carries, and the branch stands at or under the source commit.</param>
public sealed record PullRequestVerdict(string? Code, string? Form, bool Carries, bool Clears);

/// <summary>
/// The proof a completed answer gives (PLUGHOOK1a, D148 point 4, design §2.3): pure, so its table holds every code without git.
/// The platform's word is that the merge commit merged that source commit; git confirms the rest.
/// </summary>
public static class PullRequestProof
{
    public static PullRequestVerdict Judge(PullRequestState? kept, PullRequestFacts facts)
    {
        if (kept is null) return new(PullRequestStates.Unknown, null, false, false);
        if (kept.State != PullRequestStates.Completed) return new(kept.State, null, false, false);
        if (kept.Target is { } target && !string.Equals(target, facts.Line, StringComparison.Ordinal))
        {
            return new(PullRequestCodes.OtherTarget, null, false, false);
        }

        if (facts.MergeOn is not { } form) return new(PullRequestCodes.MergeNotHere, null, false, false);
        if (!facts.SourceHeld) return new(PullRequestCodes.SourceNotHere, form, false, false);
        if (facts.Standing && !facts.UnderSource) return new(PullRequestCodes.Beyond, form, true, false);
        return new(null, form, true, facts.Standing);
    }
}

/// <summary>
/// Who is asked about an entry, and whether it is due (PLUGHOOK1a, D148 points 1–2, design §2.1): pure, so the rule is held
/// without git. Whether its answer could remove something now is git's question, asked by <see cref="SessionTrees"/>.
/// </summary>
public static class PullRequestAsking
{
    /// <summary>An entry asked within this long is not asked again, so a page that lists twice asks once.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromMinutes(1);

    /// <summary>The plugin that pushed the branch, which opened its pull request; else the one the repository's landing rule names now.</summary>
    public static string? PluginFor(LandedBranch entry, LandingRule rule) =>
        entry.Plugin ?? (rule.Form == LandingForm.Branch ? rule.Plugin : null);

    /// <summary>When it was last asked, answered or not; <see cref="DateTimeOffset.MinValue"/> where it never was.</summary>
    public static DateTimeOffset LastAsked(LandedBranch entry)
    {
        var answered = entry.PullRequestState?.AskedAt ?? DateTimeOffset.MinValue;
        var failed = entry.PullRequestAskFailed?.At ?? DateTimeOffset.MinValue;
        return answered > failed ? answered : failed;
    }

    /// <summary>
    /// Whether an entry is due an ask, git's question aside: its kept answer is not completed, and it was not asked in the last
    /// minute. <paramref name="again"/> is <i>Ask again</i>, which asks whatever the kept answer's age.
    /// </summary>
    public static bool Due(LandedBranch entry, DateTimeOffset now, bool again = false) =>
        entry.PullRequestState?.State != PullRequestStates.Completed && (again || now - LastAsked(entry) >= Fresh);

    /// <summary>
    /// Why <paramref name="plugin"/> cannot be asked here, in a sentence, or null (D100's four reasons, each its own sentence):
    /// not installed, switched off, unsound, or speaking on no <see cref="HookPoints.State"/> point.
    /// </summary>
    public static string? Problem(string plugin, PluginCatalog catalog)
    {
        var entry = catalog.Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, plugin, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return $"plugin `{plugin}`, which pushed the branch, is not installed on this machine, so its pull request was not asked about.";
        if (!entry.Enabled) return $"plugin `{plugin}` is switched off on this machine, so its pull request was not asked about.";
        if (entry.Problem is { } problem) return $"plugin `{plugin}` contributes nothing: {problem}";
        return entry.Manifest.Hooks?.Points.Contains(HookPoints.State, StringComparer.Ordinal) == true
            ? null
            : $"plugin `{plugin}` does not answer for a pull request — its manifest speaks on no `{HookPoints.State}` point.";
    }
}

/// <summary>A landed branch asked about at an occasion: the entry, the plugin asked, and the frame it is sent.</summary>
public sealed record StateAsk(LandedBranch Entry, string Plugin, StateFrame Frame);

/// <summary>What <see cref="HookPoints.State"/> is sent (design §2.2).</summary>
/// <param name="Root">The repository's checkout here, where the plugin runs its platform's tool.</param>
/// <param name="Line">The repository's line (D86), or null.</param>
/// <param name="PullRequest">The pull request the record holds, or null.</param>
/// <param name="PushedTip">The commit the plugin pushed, or null.</param>
public sealed record StateFrame(
    string Repository, string Workspace, string Root, string Branch, string? Line, string? PullRequest, string? PushedTip);

/// <summary>What came of one ask: the answer, or the failure's code and Daoris's sentence, or not asked this time.</summary>
/// <param name="Failure">One of <see cref="PluginEvents"/>' failure kinds, or <see cref="PullRequestCodes.NotAsked"/>; null where it answered.</param>
public sealed record StateAnswer(StateAsk Ask, PullRequestState? Answer, string? Failure, string? Sentence = null);
