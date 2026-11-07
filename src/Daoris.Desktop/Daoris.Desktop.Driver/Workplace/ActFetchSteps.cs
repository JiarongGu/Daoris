using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The fetch step: its plan, its effect and its postconditions. The effect is one git run once in
/// the act's job, so its <c>reference-transaction</c> hook and the hook's children end with it; nothing runs it again. A fetch's end state is
/// origin's, which no local read knows, so a step that ended with exit 0 is done, and a step with no end is read from the tracking refs against
/// the ones the plan kept: unchanged is not done, changed is unknown, never done.
/// </summary>
internal static class ActFetchSteps
{
    /// <summary>A fetch step: the tracking refs as they stood (kept as the recovery, since a fetch can be run again), and who is fetched from.</summary>
    public static ActStepPlan Fetch(IReadOnlyDictionary<string, string> before, string repository, string? workspace, string address)
    {
        var said = new JsonObject
        {
            ["arguments"] = new JsonArray([.. GitFetchActs.Arguments.Select(word => (JsonNode)word)]),
            ["repository"] = repository, ["workspace"] = workspace, ["address"] = address,
        }.ToJsonString();
        var read = new JsonObject { ["before"] = new JsonObject(before.Select(each => KeyValuePair.Create(each.Key, (JsonNode?)each.Value))) }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Fetch, said, read);
    }

    public static async Task<ActStepEnd> EffectAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var plan = JsonNode.Parse(step.Arguments)!;
        var arguments = plan["arguments"]!.AsArray().Select(each => each!.GetValue<string>()).ToList();
        var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, arguments, null, null).ConfigureAwait(false);
        if (run.Unended || run.Exit is null) return run.End("");

        var (home, address) = (launch.Home, plan["address"]!.GetValue<string>());
        var remote = await GitRemotes.AddressOf(launch.Root, "origin").ConfigureAwait(false) ?? address;
        var git = WorkingTree.GitInUse(home);
        // The hold is what the line fetch keeps: a refusal of the sign-in holds origin; a fetch that passes lifts a hold over reads.
        if (run.Exit == 0)
        {
            GitHolds.Passed(home, launch.Root, "origin", GitAct.Read);
            return run.End("git fetched origin's branches");
        }

        var workspace = plan["workspace"] is JsonValue value && value.TryGetValue<string>(out var named) ? named : null;
        var failure = GitMove.Reclass(home, launch.Repository, workspace, "origin", remote, GitFailures.Class(run.Stderr, GitAct.Read));
        if (GitHolds.Hold(home, launch.Root, launch.Repository, workspace, "origin", remote, git, failure, run.Stderr, DateTimeOffset.UtcNow) is { } hold)
        {
            return run.End(GitHolds.Said(hold));
        }

        return run.End($"git refused the fetch ({GitWords.Redact(ContentHolds.Said(TreeGit.Failure(run.Stderr)))})");
    }

    /// <summary>Done where the step ended with exit 0, else not done where the tracking refs stand as they did, else unknown; null where git could not say.</summary>
    public static async Task<string?> ReadAsync(string root, ActStep step, JsonNode plan, CancellationToken ct)
    {
        if (step.ExitCode == 0 && !step.Unended) return ActOutcomes.Done;
        if (await GitFetchActs.TrackingAsync(root, ct).ConfigureAwait(false) is not { } now) return null;
        var before = plan["before"]!.AsObject().ToDictionary(each => each.Key, each => each.Value!.GetValue<string>(), StringComparer.Ordinal);
        var same = before.Count == now.Count && before.All(each => now.TryGetValue(each.Key, out var id) && id == each.Value);
        return same ? ActOutcomes.NotDone : ActOutcomes.Unknown;
    }
}
