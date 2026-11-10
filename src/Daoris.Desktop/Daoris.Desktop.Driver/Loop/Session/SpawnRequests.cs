namespace Daoris.Driver;

/// <summary>
/// A start's <see cref="SpawnRequest"/>, composed by its executor: a driven start's and a native resume's from the target the executor
/// composed, a conversation's from its place, the one place whose work each serves is turned into its identity variables. A
/// door's spawn and the transports' shell read the request alone.
/// </summary>
internal static class SpawnRequests
{
    /// <summary>A driven start's request: the target's posture and asked choice, its home, and whose work it serves.</summary>
    public static SpawnRequest Of(SessionTarget target, IReadOnlyList<string>? command, SessionDelegates? delegates = null) =>
        new(target.Repository, target.Root, target.ServiceUrl)
        {
            Command = command, Asked = target.Asked, Mode = target.Access?.PermissionMode, Home = target.Home,
            Identity = Delegated(Identity(target), delegates, target.Brainstorm is null ? new SessionPlace(target.Repository, target.Access?.Workspace) : null),
        };

    /// <summary>
    /// A conversation's request, its command, choice, mode and own conversation set by its runner: no quest's variable at all,
    /// rather than an empty one, which would read as an id that failed to parse; its own record, so a connector its agent
    /// starts from the repository's server list marks this conversation.
    /// </summary>
    public static SpawnRequest Chat(
        string repository, string root, string serviceUrl, string? session, SessionDelegates? delegates = null, string? workspace = null) =>
        new(repository, root, serviceUrl)
        {
            Identity = Delegated(
                session is { } own
                    ? new Dictionary<string, string?> { [ChildMarks.SessionVariable] = own }
                    : new Dictionary<string, string?>(),
                delegates, new SessionPlace(repository, workspace)),
        };

    /// <summary>Every kind's identity handed a delegate key of its own in this one place, beside its record's id and its place.</summary>
    private static Dictionary<string, string?> Delegated(Dictionary<string, string?> identity, SessionDelegates? delegates, SessionPlace? place)
    {
        delegates?.Hand(identity, place);
        return identity;
    }

    /// <summary>Whose work a driven start serves, each set by the start for its role and the rest absent.</summary>
    private static Dictionary<string, string?> Identity(SessionTarget target)
    {
        var identity = new Dictionary<string, string?>(StringComparer.Ordinal);

        // An intake serves an ask, not a quest: the ask and its own session, the quest variables absent rather than blank.
        if (target.Ask is { } ask)
        {
            identity[AgentWorkVariables.Ask] = ask;
            if (target.Session is { } session) identity[ChildMarks.SessionVariable] = session;
            identity["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return identity;
        }

        // A reviewer, a helper or a piece serves another session's work and no quest: its own record, so its connector says
        // its opinion as that session, and its instruction.
        if (target.Opinion is not null || target.Helper is not null || target.Piece is not null)
        {
            if (target.Session is { } reviewer) identity[ChildMarks.SessionVariable] = reviewer;
            identity["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return identity;
        }

        // A brainstorm's pass serves no quest and no repository: the brainstorm, its turn and where its
        // result tool writes, and its instruction; no repository named.
        if (target.Brainstorm is { } brainstorm)
        {
            identity[AgentWorkVariables.Brainstorm] = brainstorm;
            if (target.BrainstormTurn is { } turn) identity[AgentWorkVariables.Turn] = turn;
            if (target.BrainstormAnswer is { } answer) identity[AgentWorkVariables.Answer] = answer;
            if (target.Session is { } pass) identity[ChildMarks.SessionVariable] = pass;
            identity["DAORIS_REPOSITORY"] = null;
            identity["DAORIS_SERVICE_URL"] = null;
            identity["DAORIS_TARGET"] = TargetPrompt.Compose(target);
            return identity;
        }

        identity["DAORIS_QUEST_ID"] = target.QuestId;
        identity["DAORIS_QUEST_TITLE"] = target.Title;
        identity["DAORIS_QUEST_BODY"] = target.Body;
        identity["DAORIS_QUEST_ASKER"] = target.Asker;
        identity["DAORIS_TARGET"] = TargetPrompt.Compose(target);
        // Its own record, as an intake's is named: a connector the agent starts itself on the pipe door inherits it.
        if (target.Session is { } own) identity[ChildMarks.SessionVariable] = own;

        // The quest's files, absent rather than empty where this machine holds none: a blank directory reads as one emptied.
        if (target.AttachmentsDirectory is { } attachments) identity["DAORIS_QUEST_ATTACHMENTS"] = attachments;
        return identity;
    }
}
