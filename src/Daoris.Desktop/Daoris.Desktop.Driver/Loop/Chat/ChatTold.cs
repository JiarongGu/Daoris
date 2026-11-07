using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What a new conversation is told with its first message, held from its start until then: its process (the commit rule, its landing and
/// its maker), the checks its execution holds, and what its standing block is composed from. The maps are its own, never the browser's,
/// which a hand-over's briefing appends: a conversation handed over is told Daoris's record instead.
/// </summary>
internal sealed class ChatTold(ChatContext context, ChatRunBinds chatRuns)
{
    private readonly ConcurrentDictionary<string, StandingFacts> _block = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ProcessBriefing> _process = new(StringComparer.OrdinalIgnoreCase);

    // The checks each live execution keeps by its id for `run-heavy`, let go as it ends.
    private readonly ConcurrentDictionary<string, string> _checksKept = new(StringComparer.Ordinal);

    /// <summary>The process a new conversation is told with its first message.</summary>
    public void HoldProcess(string sessionId, ProcessBriefing told) => _process[sessionId] = told;

    /// <summary>What a new conversation's standing block is composed from, taken by its first message like its process.</summary>
    public void HoldBlock(string sessionId, StandingFacts block) => _block[sessionId] = block;

    /// <summary>A start that fails, or is cancelled, has no first message to tell it with.</summary>
    public void Forget(string sessionId)
    {
        _process.TryRemove(sessionId, out _);
        _block.TryRemove(sessionId, out _);
    }

    /// <summary>The conversation ended: nothing is told now, and its execution's checks are let go.</summary>
    public void Ended(string sessionId)
    {
        _process.TryRemove(sessionId, out _);
        LetGoChecks(sessionId);
        _block.TryRemove(sessionId, out _);
    }

    /// <summary>An execution ended: the checks it kept are let go.</summary>
    public void LetGoChecks(string sessionId)
    {
        if (_checksKept.TryRemove(sessionId, out var kept)) SessionChecks.LetGo(kept);
    }

    /// <summary>
    /// The start's process, maker and standing block, consumed by the first ordinary or handed-over message: a hand-over's block says the
    /// memory its brief hands on.
    /// </summary>
    public ChatMessage With(string sessionId, ChatMessage message)
    {
        if (_block.TryRemove(sessionId, out var block))
        {
            message = message with { Block = StandingBlock.Compose(block with { Memory = message.Brief?.Memory ?? [] }) };
        }

        return _process.TryRemove(sessionId, out var told) ? message with { Process = told } : message;
    }

    /// <summary>
    /// What a conversation in a tree of its own is told of its process: the commit rule standing for its repository and its landing, in
    /// <see cref="ProcessWords.Conversation"/>'s words with the rule's facts.
    /// </summary>
    public static ProcessBriefing Process(ResolvedCommits commits, LandingPlan? plan, string? dependencies = null, ChatAgent? agent = null)
    {
        // A copy of the tree's dependencies is told with the process, so the agent does not run the install that would delete it; no new
        // note code, it rides the process's own.
        var words = ProcessWords.Conversation(commits, plan);
        if (agent is not null) words = $"{words}\n\n{agent.Trailers()}";
        if (!string.IsNullOrEmpty(dependencies)) words = $"{words}\n\n{dependencies}";
        return new(words, commits.Rule.Mode, commits.Source);
    }

    /// <summary>
    /// What a conversation in a tree of its own is handed of its run's checks at one execution: its tree locked in its id, its checks kept
    /// by it for the execution's life, and the deny rules and words they compose; none for a place that is no repository's tree. The words
    /// join what the process told it, once, so the conversation hears the checks only as they are enforced.
    /// </summary>
    public async Task<HeavyHanded> HeldChecksAsync(
        string sessionId, string workTree, ChatPlace place, DriverConfig config, ChatAgent agent, CancellationToken ct)
    {
        var home = context.Home;
        if (!place.Keeps || !new SessionTrees(home).Holds(workTree)) return HeavyHanded.None;
        if (await SessionLocks.HoldAsync(workTree, sessionId, ct).ConfigureAwait(false) is { } unlocked)
        {
            context.Output?.Append(sessionId, $"— its tree could not be locked in its name: {unlocked}");
        }

        if (await chatRuns.ChecksAsync(sessionId, workTree, config).ConfigureAwait(false) is not { } checks) return HeavyHanded.None;
        if (SessionChecks.Hold(home, sessionId, checks.Checks, checks.Minutes) is { } kept) _checksKept[sessionId] = kept;
        var heavy = HeavyTold.Hand(workTree, home, checks.Repository, checks.Checks, agent.TakesSettings);
        if (heavy.Words is { } words && _process.TryGetValue(sessionId, out var told))
        {
            _process[sessionId] = told with { Words = $"{told.Words}\n\n{words}" };
        }

        return heavy;
    }

    /// <summary>
    /// <see cref="Process"/> for the tree a new conversation just grew: its landing read as Review's press would read it. A landing that
    /// cannot be read leaves its lead unsaid, never the start: the record is open, so the run that follows concludes it, cancelled or not,
    /// and the commit rule is said either way.
    /// </summary>
    /// <param name="process">The chat run's bound process, which plans its landing as its press's gate does; null reads the rule.</param>
    public async Task<ProcessBriefing> ProcessAsync(
        string tree, string sessionId, DriverConfig config, RepoView known, string? dependencies, ChatAgent agent, CancellationToken ct,
        IWorkplaceProcess? process = null)
    {
        LandingPlan? plan;
        try
        {
            plan = await new SessionTrees(context.Home).PlanAsync(tree, new LandingSubject(sessionId, null, null), ct, process).ConfigureAwait(false);
        }
        catch (Exception)
        {
            plan = null;
        }

        return Process(CommitRules.Resolve(config, known.Repository, known.Workspace), plan, dependencies, agent);
    }
}
