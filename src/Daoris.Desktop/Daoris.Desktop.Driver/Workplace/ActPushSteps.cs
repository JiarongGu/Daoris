using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The push step (GITAPP2g and n, D204, D241 point 2; the git-writes design §5, §9): its plan, its effect and its postconditions. The effect
/// writes the one grant its push carries, bound to origin's push address, the ref, the commit the ref stands at on origin and the commit it
/// moves to; runs one git push in the act's job with that grant handed to it alone, its <c>pre-push</c> hook and the hook's children ending
/// with it; and drops the grant once git ends, taken or not. Nothing runs it again.
/// </summary>
/// <remarks>
/// A push's end state is origin's, which no local read knows. A step that ended with exit 0 is done; one refused before anything was sent
/// (the door's refusal, git's own <c>rejected</c>, git's own stop before it sends such as a <c>pre-push</c> hook refusing, or a hold on origin)
/// is not done; any other end, a connection cut after origin was reached included, is unknown, never done and never retried.
/// </remarks>
internal static class ActPushSteps
{
    /// <summary>The start of the words a push step says where nothing was sent, which its postconditions read as not done.</summary>
    internal const string NothingSent = "nothing was pushed: ";

    /// <summary>A push step: the git words, the address it pushes to (redacted, as the plan showed it), and the one update its grant carries.</summary>
    public static ActStepPlan Push(IReadOnlyList<string> arguments, string repository, string? workspace, string address, GitGrantRef update)
    {
        var said = new JsonObject
        {
            ["arguments"] = new JsonArray([.. arguments.Select(word => (JsonNode)word)]),
            ["repository"] = repository, ["workspace"] = workspace, ["address"] = address,
            ["ref"] = update.Ref, ["new"] = update.New, ["old"] = update.Old,
        }.ToJsonString();
        return new ActStepPlan(ActStepKinds.Push, said, said);
    }

    public static async Task<ActStepEnd> EffectAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var plan = JsonNode.Parse(step.Arguments)!;
        var arguments = plan["arguments"]!.AsArray().Select(each => each!.GetValue<string>()).ToList();
        var (home, shown) = (launch.Home, plan["address"]!.GetValue<string>());
        var workspace = plan["workspace"] is JsonValue value && value.TryGetValue<string>(out var named) ? named : null;

        // The grant names the address git will push to now, which must be the one the plan showed.
        var address = await GitRemotes.AddressOf(launch.Root, "origin", push: true).ConfigureAwait(false);
        if (address is null || GitWords.Redact(address) != shown)
        {
            return new ActStepEnd(null, false, $"{NothingSent}origin's push address is no longer the one shown", true);
        }

        var update = new GitGrantRef(plan["ref"]!.GetValue<string>(), plan["new"]!.GetValue<string>(), (string?)plan["old"]);
        var grant = GitGrants.Write(home, address, [update], DateTimeOffset.UtcNow);
        if (grant is null) return new ActStepEnd(null, false, $"{NothingSent}Daoris could not bind a grant to origin's push address", true);
        GitLog.Granted(home, launch.Repository, "origin", session: null, "press");

        ActLaunch.Ran run;
        try
        {
            run = await launch.HeldGitAsync(launch.Root, step.Ordinal, arguments, null, new Dictionary<string, string> { [GitRouting.GrantVariable] = grant }).ConfigureAwait(false);
        }
        finally
        {
            GitGrants.Drop(home, grant);
        }

        if (run.Unended || run.Exit is null) return run.End("");
        if (run.Exit == 0)
        {
            GitHolds.Passed(home, launch.Root, "origin", GitAct.Write);
            return run.End($"git pushed `{GitRefActs.ShortName(update.Ref)}` to origin");
        }

        // The doors' own refusal is a coded line of theirs; git's `rejected` is refused before any ref moved on origin.
        var stderr = GitWords.Redact(run.Stderr) ?? "";
        if (DoorLine(stderr) is { } door) return run.End(NothingSent + door);
        if (stderr.Contains("[rejected]", StringComparison.Ordinal) || stderr.Contains("[remote rejected]", StringComparison.Ordinal))
        {
            return run.End($"{NothingSent}origin refused it ({ContentHolds.Said(TreeGit.Failure(stderr))})");
        }

        // Git's own stop (exit 1, `failed to push some refs`, no `fatal:` of a connection) comes before the pack is sent: the person's pre-push
        // hook refusing is one, run once origin's refs are listed. A connection cut after that dies with `fatal:` and stays unknown.
        if (run.Exit == 1 && stderr.Contains("failed to push some refs", StringComparison.Ordinal)
            && !stderr.Contains("fatal:", StringComparison.Ordinal) && !stderr.Contains("hung up", StringComparison.Ordinal))
        {
            return run.End($"{NothingSent}git stopped it before anything was sent, as a pre-push hook of the repository does ({ContentHolds.Said(TreeGit.Failure(stderr))})");
        }

        var git = WorkingTree.GitInUse(home);
        var failure = GitMove.Reclass(home, launch.Repository, workspace, "origin", address, GitFailures.Class(run.Stderr, GitAct.Write));
        if (GitHolds.Hold(home, launch.Root, launch.Repository, workspace, "origin", address, git, failure, run.Stderr, DateTimeOffset.UtcNow) is { } hold)
        {
            return run.End(NothingSent + GitHolds.Said(hold));
        }

        return run.End($"git did not say the push finished ({ContentHolds.Said(TreeGit.Failure(stderr))})");
    }

    /// <summary>Done where the step ended with exit 0, not done where it says nothing was sent, else unknown.</summary>
    public static string Read(ActStep step) =>
        step.ExitCode == 0 && !step.Unended ? ActOutcomes.Done
        : !step.Unended && step.Said?.StartsWith(NothingSent, StringComparison.Ordinal) == true ? ActOutcomes.NotDone
        : ActOutcomes.Unknown;

    /// <summary>The first line a door wrote where git prints it (<c>daoris: …</c>), or null where none refused.</summary>
    internal static string? DoorLine(string stderr) =>
        stderr.Split('\n').Select(line => line.Trim()).Select(line => line.StartsWith("remote: ", StringComparison.Ordinal) ? line["remote: ".Length..] : line)
            .FirstOrDefault(line => line.StartsWith("daoris: ", StringComparison.Ordinal));
}
