namespace Daoris.Driver;

/// <summary>
/// The weighing of one quest for a start: the reasons it sits, each in the order they are weighed, and the cap, tree and repository
/// it takes when it starts.
/// </summary>
internal sealed class RoundConsidering(PlanRound round)
{
    private Snapshot snapshot => round.Snapshot;

    private DriverConfig config => round.Config;

    /// <summary>
    /// <paramref name="into"/> is the earlier session whose tree a resume or a carry-on goes back into — the one tree a live session
    /// there would hold. Null for a first start, which grows a tree of its own. <paramref name="continuing"/>: <paramref name="into"/>
    /// is the record the person's words wait on, which goes on itself, so it holds nothing against itself.
    /// </summary>
    internal Consideration Consider(QuestView quest, PriorSession? into = null, bool continuing = false)
    {
        bool Itself(SessionView session) => continuing && string.Equals(session.Id, into?.Session, StringComparison.OrdinalIgnoreCase);

        var repo = snapshot.Repositories.FirstOrDefault(r =>
            string.Equals(r.Repository, quest.To, StringComparison.OrdinalIgnoreCase));

        if (repo is null)
        {
            return new(quest, StartVerdict.NotAdopted,
                $"`{quest.To}` is not registered on this machine, so there is nowhere to start it.")
            {
                Said = new(SittingFact.Unregistered),
            };
        }

        // Registered is drivable over the protocol door; adopted is disciplined. The pipe door's session reaches the knowledge tools
        // only through the repository's own `.mcp.json`, which adoption writes and the driver may never write for it — so there it sits.
        if (!repo.Adopted)
        {
            if (repo.Root is null)
            {
                return new(quest, StartVerdict.NotAdopted,
                    $"`{quest.To}` has not adopted Daoris and no root is known for it on this machine, "
                    + "so there is nothing to start and nothing there to see the quest.")
                {
                    Said = new(SittingFact.NoRoot),
                };
            }

            if (!round.Unadopted)
            {
                return new(quest, StartVerdict.NotAdopted,
                    $"`{quest.To}` has not adopted Daoris, so a session there would have no connector to "
                    + "take the quest with — only the protocol door hands it one. Drive with an ACP "
                    + "agent, or adopt it.")
                {
                    Said = new(SittingFact.NoConnector),
                };
            }
        }

        // A set-up admitted alone starts by its own id where its repository is not driven; nothing else there does.
        if (!config.Drivable.Contains(quest.To, StringComparer.OrdinalIgnoreCase) && !config.Admits(quest))
        {
            return new(quest, StartVerdict.NotDrivable,
                $"`{quest.To}` has not been opted into driving on this machine.");
        }

        if (config.Holds.Contains(quest.To, StringComparer.OrdinalIgnoreCase))
        {
            return new(quest, StartVerdict.Held, $"`{quest.To}` is held by the person.");
        }

        // A set-up step it cannot honestly start: no rule here names its environment, or a local one has nowhere to show it. It
        // sits saying why, and no session starts with nothing to show the work in.
        if (SetUpSits.Sits(config, quest, repo.Workspace, round.Window) is { } sits)
        {
            return new(quest, StartVerdict.CannotShow, sits.Reason) { CannotShow = sits.Fact };
        }

        // The second opinion: a set-up step sits while an agent is still at work on the opinion of the work it shows, and under
        // `steps` a chain's next step sits while the opinion on the step before is unsettled. The look reads each from its facts;
        // the planner holds no copy of the gate. No strike: nothing ran.
        if (snapshot.Opinions.TryGetValue(quest.Id, out var opinion))
        {
            return new(quest, StartVerdict.WaitsForOpinion, opinion.Reason) { Said = opinion.Fact };
        }

        if (repo.Root is null)
        {
            return new(quest, StartVerdict.NoRoot,
                $"no root is known for `{quest.To}` — run `daoris repository connect` from that repository.");
        }

        // Before busy and before capacity, because those are waits and this is a stop: a parked quest must not read as "queued" in a
        // surface that renders the reason.
        var strikes = snapshot.Strikes.TryGetValue(quest.Id, out var failures)
            ? failures - config.ForgivenAt(quest.Id)
            : 0;
        // The person's words to its session are their Try again: a failure they write to is forgiven as its record leaves `failed`,
        // and a quest the strikes parked goes on with them, as a retry would start it.
        var parks = snapshot.FailuresAt.GetValueOrDefault(quest.Id);
        if (parks > 0 && strikes >= parks && !(continuing && into is { WordsWaiting: true }))
        {
            return new(quest, StartVerdict.Exhausted,
                $"{strikes} session(s) have failed on `#{quest.Id}` without landing anything — "
                + $"parked, because trying again spends an account rather than making progress. "
                + $"`daoris quest retry {quest.Id}` starts it again once you know why.")
            {
                // What *Try again* marks: the records' count, not the count past the mark this sentence says.
                Failures = failures,
                FailedSince = strikes,
                ParksAt = parks,
                LastSession = snapshot.LastRun.GetValueOrDefault(quest.Id)?.Session,
            };
        }

        // A session outlives the look that started it, so its quest can still be open while it works — before it takes it, or when
        // it never will. That session holds the quest: a second one would double its work, and in a tree of its own nothing below
        // would stop it.
        if (snapshot.Active.FirstOrDefault(s => string.Equals(s.Quest, quest.Id, StringComparison.OrdinalIgnoreCase) && !Itself(s))
            is { } serving)
        {
            return new(quest, StartVerdict.RepositoryBusy,
                $"session `{serving.Id}` is already working on `#{quest.Id}`.")
            {
                Said = new(SittingFact.Serving) { Session = serving.Id },
            };
        }

        // A start of this machine's that has not yet opened its record holds the quest as its session will: the look that began it
        // does not wait for it, so a wake meanwhile plans the quest again, and would start it twice.
        if (round.Opening.Any(start => string.Equals(start.Quest, quest.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return new(quest, StartVerdict.RepositoryBusy,
                $"`#{quest.Id}` is starting on this machine already: its session opens once its tree and account are ready.")
            {
                Said = new(SittingFact.Opening),
            };
        }

        // A first start (not a retry or carry-on, whose work is already in its tree) whose declared paths meet an earlier unit's of the
        // repository, declared or changed on its branch, waits under `overlap: wait` until that unit's integration is settled; under
        // `warn` it starts saying so. A unit is earlier while it is taken, running, started at this look or still integrating.
        var overlap = into is not null || quest.Status != "Open" ? null : OverlapOf(quest);
        if (overlap is not null
            && config.LinePolicyOf(quest.To, repo.Workspace)?.Overlap == LandingIntegrate.Wait)
        {
            return new(quest, StartVerdict.RepositoryBusy, $"{overlap.Said} — it starts once that work is integrated.")
            {
                Said = new(SittingFact.Overlaps) { Quest = overlap.Quest, Paths = overlap.Paths },
            };
        }

        // The TREE is the lock, and where every session here opens its own there is no reason to run one at a time. What still
        // holds is the one tree a resume or a carry-on goes back into.
        if (config.OpensOwnTree(quest.To))
        {
            if (into is { Tree: { Length: > 0 } tree }
                && snapshot.Active.FirstOrDefault(s => Planner.SameTree(s.Tree, tree) && !Itself(s)) is { } holder)
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{holder.Id}` is active in the tree `#{quest.Id}` goes back into — one session per tree.")
                {
                    Said = new(SittingFact.Tree) { Session = holder.Id },
                };
            }

            // So does a session waiting on its own helper there, which goes on in it with the answer.
            if (into is { Tree: { Length: > 0 } kept }
                && Planner.WaitingInTree(snapshot, kept, except: continuing ? into.Session : null) is { } waiting)
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"session `{waiting}` waits on its own helper in the tree `#{quest.Id}` goes back into — one session per tree.")
                {
                    Said = new(SittingFact.Tree) { Session = waiting },
                };
            }

            // A start still opening holds the tree it goes back into, as its session will.
            if (into is { Tree: { Length: > 0 } reserved }
                && round.Opening.FirstOrDefault(start => Planner.SameTree(start.Tree, reserved)) is { Quest: { } going })
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"queued behind quest `#{going}`, which is starting in the tree `#{quest.Id}` goes back into — one session per tree.")
                {
                    Said = new(SittingFact.TreeOpening) { Quest = going },
                };
            }

            // A further part planned into that tree at this look holds it as a live session would.
            if (into is { Tree: { Length: > 0 } claimed } && round.TreesThisTick.FirstOrDefault(claim => Planner.SameTree(claim.Tree, claimed)) is { Quest: { } ahead })
            {
                return new(quest, StartVerdict.RepositoryBusy,
                    $"queued behind quest `#{ahead}`, which goes into the tree `#{quest.Id}` goes back into at this look — one session per tree.")
                {
                    Said = new(SittingFact.TreeThisLook) { Quest = ahead },
                };
            }
        }
        else if ((continuing
                     ? snapshot.Active.FirstOrDefault(s =>
                         string.Equals(s.Repository, quest.To, StringComparison.OrdinalIgnoreCase) && !Itself(s))?.Id
                     : round.BlockedBy.GetValueOrDefault(quest.To)) is { } session)
        {
            return new(quest, StartVerdict.RepositoryBusy,
                $"session `{session}` is active in `{quest.To}` — one session per repository, whose root "
                + "is its one tree. Sessions there run side by side once they open trees of their own.")
            {
                Said = new(SittingFact.InRepository) { Session = session },
            };
        }
        else if (round.StartedThisTick.TryGetValue(quest.To, out var ahead))
        {
            return new(quest, StartVerdict.RepositoryBusy,
                $"queued behind quest `#{ahead}` in `{quest.To}` — oldest first, one at a time.")
            {
                Said = new(SittingFact.Queued) { Quest = ahead },
            };
        }
        // A start still opening in the root holds it, as its session will once it opens.
        else if (round.Opening.FirstOrDefault(start => string.Equals(start.Repository, quest.To, StringComparison.OrdinalIgnoreCase)
                                                       && !string.Equals(start.Quest, quest.Id, StringComparison.OrdinalIgnoreCase))
                 is { Quest: { } before })
        {
            return new(quest, StartVerdict.RepositoryBusy,
                $"queued behind quest `#{before}`, which is starting in `{quest.To}` — one session per repository, whose root is its one tree.")
            {
                Said = new(SittingFact.RepositoryOpening) { Quest = before },
            };
        }

        // An answered park already holds its slot among the active: going on, it starts no second session. An ended record holds
        // none, so going on takes one, as a start does; it is planned first, so it is first to.
        if (!(continuing && snapshot.Active.Any(Itself)))
        {
            if (round.Slots <= 0)
            {
                return new(quest, StartVerdict.AtCapacity,
                    $"the concurrency cap ({config.Cap}) is spent — it frees as sessions finish.")
                {
                    Said = new(SittingFact.CapSpent) { Cap = config.Cap },
                };
            }

            round.Slots--;
        }

        round.StartedThisTick[quest.To] = quest.Id;
        round.StartsThisLook.Add(quest);
        if (config.OpensOwnTree(quest.To) && into is { Tree: { Length: > 0 } back }) round.TreesThisTick.Add((back, quest.Id));
        return new(quest, StartVerdict.Start,
            overlap is null ? $"starting in `{quest.To}`." : $"starting in `{quest.To}`; it {overlap.Said}.", repo.Root, repo.Workspace)
        {
            Overlaps = overlap,
        };
    }

    // The first earlier unit of the quest's repository whose declared or changed paths meet the quest's declared ones; null for none.
    private OverlapFact? OverlapOf(QuestView quest)
    {
        if (quest.Paths.Count == 0) return null;
        var earlier = snapshot.Quests
            .Where(other => !string.Equals(other.Id, quest.Id, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(other.To, quest.To, StringComparison.OrdinalIgnoreCase)
                            && (other.Status == "Taken" || snapshot.Integrating.Contains(other.Id)
                                || snapshot.Active.Any(s => string.Equals(s.Quest, other.Id, StringComparison.OrdinalIgnoreCase))))
            .Concat(snapshot.IntegratingQuests.Where(other => string.Equals(other.To, quest.To, StringComparison.OrdinalIgnoreCase)
                                                              && !string.Equals(other.Id, quest.Id, StringComparison.OrdinalIgnoreCase)))
            .Concat(round.StartsThisLook.Where(other => string.Equals(other.To, quest.To, StringComparison.OrdinalIgnoreCase)));
        foreach (var other in earlier)
        {
            var met = PathOverlap.Meet(quest.Paths, other.Paths, snapshot.Changed.GetValueOrDefault(other.Id) ?? []);
            if (met.Count > 0) return new OverlapFact(other.Id, met);
        }

        return null;
    }
}
