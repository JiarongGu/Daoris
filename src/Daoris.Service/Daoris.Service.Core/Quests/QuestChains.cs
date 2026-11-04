namespace Daoris.Knowledge;

/// <summary>Whether a chain's steps can each be published when their turn comes (D65 §4).</summary>
internal static class QuestChains
{
    /// <summary>
    /// Every step of a chain must be publishable when its turn comes — so each is judged as its own
    /// publish would be, asked on behalf of the chain's asker, and each must be shared exactly when the
    /// quest it follows is. Null when the chain is fit (or there is none).
    /// </summary>
    /// <remarks>
    /// <b>All shared or all local</b> (D65 §4, D68): a step is published by the close of the one before
    /// it, on whichever machine closed it. A shared quest may be closed on a teammate's machine, which
    /// cannot see a repository local to this one; and a local quest's step to a shared receiver would
    /// publish only here, where no teammate's driver can see it close. A chain that straddles the two is
    /// refused when composed rather than stranded later.
    /// </remarks>
    public static string? Judge(
        QuestAsk ask, string to, IReadOnlyList<Registration> registered, string home, bool circleWired, bool shared,
        IReadOnlyList<string> addressable)
    {
        if (ask.Then.Count > QuestExchange.MaxChain)
        {
            return $"A chain carries at most {QuestExchange.MaxChain} steps after its first quest — this one was given "
                   + $"{ask.Then.Count}. Publish the rest when the work gets there.";
        }

        foreach (var (step, index) in ask.Then.Select((step, index) => (step, index + 1)))
        {
            if (string.IsNullOrWhiteSpace(step.To) || string.IsNullOrWhiteSpace(step.Title) || string.IsNullOrWhiteSpace(step.Body))
            {
                return $"Step {index} of the chain needs whom to ask, a title and a body — the same three words any quest does.";
            }

            if (string.Equals(step.To, ask.From, StringComparison.OrdinalIgnoreCase))
            {
                return $"Step {index} asks `{step.To}`, which is the chain's own asker — every step is asked on its "
                       + "behalf, so that would be a quest to itself. Its own backlog is the place for that.";
            }

            var receiver = registered.Named(step.To);
            if (receiver is null || !receiver.Addressable || !Workspaces.Same(receiver.InWorkspace, home))
            {
                return $"Step {index} asks `{step.To}`, which cannot be asked from `{ask.From}` — nothing would see it "
                       + $"when its turn came. Addressable: {string.Join(", ", addressable)}.";
            }

            var stepShared = circleWired && receiver.Joined;
            if (stepShared != shared)
            {
                return $"Step {index} asks `{step.To}`, which is {(stepShared ? "shared with the team" : "local to this machine")}, "
                       + $"and the chain starts at `{ask.To}`, which is {(shared ? "shared with the team" : "local to this machine")}. "
                       + "Each step is published on the machine that closes the one before it, so a chain is all "
                       + "shared or all local. Publish the other half as its own quest when this one is done.";
            }
        }

        return JudgeSetUpSteps(ask, to);
    }

    /// <summary>
    /// A chain's set-up steps, each judged as its composition must leave it (D154 point 4, design §2.1): the
    /// environment's name, never production; the repository of the step before it, and none of that repository's steps after
    /// it, so its tree grows from the work it shows and holds all of it, once; and never on a chain whose choice is
    /// <c>off</c>. Null when every one is fit, or there is none.
    /// </summary>
    private static string? JudgeSetUpSteps(QuestAsk ask, string to)
    {
        var before = to;
        foreach (var (step, index) in ask.Then.Select((step, index) => (step, index + 1)))
        {
            if (step.SetUpIn is { } environment)
            {
                if (Reviews.JudgeEnvironment(environment) is { } unfit) return $"Step {index} is a set-up step, and {unfit}.";
                if (ask.Review is { Choice: Reviews.Off })
                {
                    return $"Step {index} sets the work up for review in `{environment}`, and this chain's review choice is `off`: "
                           + "a chain the person chose no review for composes no set-up step.";
                }

                if (!string.Equals(step.To, before, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Step {index} sets up `{step.To}`'s work in `{environment}`, and the step before it asks `{before}`: a "
                           + "set-up step follows the last step of the repository whose work it shows, so its tree holds that work.";
                }

                var after = ask.Then.Skip(index)
                    .Select((later, offset) => (Later: later, Number: index + offset + 1))
                    .FirstOrDefault(each => string.Equals(each.Later.To, step.To, StringComparison.OrdinalIgnoreCase));
                if (after.Later is not null)
                {
                    return $"Step {index} sets up `{step.To}`'s work in `{environment}`, and step {after.Number} asks `{step.To}` after "
                           + "it: a set-up step comes after that repository's last step, so what it shows is all its work, once.";
                }
            }

            before = step.To;
        }

        return null;
    }
}
