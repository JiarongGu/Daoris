namespace Daoris.Driver;

/// <summary>
/// What choosing a conversation's focus answered: its applied focus and the one waiting for its next words, or the refusal's
/// code and sentence, the focus left unchanged.
/// </summary>
public sealed record HelpFocusAnswer(string Session, HelpFocus? Focus, HelpFocus? FocusNext, string? Refused = null, string? Said = null)
{
    /// <summary>The answer as <c>HELP_FOCUS</c> gives it: <c>{ session, focus, focusNext }</c>, or <c>{ session, refused, said }</c>.</summary>
    public IReadOnlyDictionary<string, object?> Wire() => Refused is null
        ? new Dictionary<string, object?> { ["session"] = Session, ["focus"] = Focus?.Wire(), ["focusNext"] = FocusNext?.Wire(), ["reopens"] = Reopens }
        : new Dictionary<string, object?> { ["session"] = Session, ["refused"] = Refused, ["said"] = Said };

    /// <summary>The answer in the terminal's words.</summary>
    public string Line => Refused is not null ? $"help: {Said}"
        : FocusNext is { } next ? $"help: {Session}'s focus becomes {next.Named} with its next words; it is {Focus!.Named} until then."
        : $"help: {Session}'s focus is {Focus!.Named}.";

    /// <summary>Whether its agent, opened ahead under another place, ends for the choice, so the page opens it ahead again (<see cref="HelpFocusAhead"/>).</summary>
    public bool Reopens { get; init; }
}

/// <summary>
/// The person's choice of what an Ask Daoris conversation is about, the one way
/// <c>HELP_FOCUS</c> and <c>ask-daoris focus</c> take: kept on the conversation's file at once, so a reload or another window
/// shows it. Before any words it is the conversation's first focus; after them it waits for the next words, which apply it, and
/// choosing the applied focus again clears what waited. The place is spelled as the registry spells it now; a repository with no
/// checkout here is one, since a focus is about records as much as a checkout.
/// </summary>
public sealed class HelpFocusing(string home)
{
    /// <summary>A place this machine's registry does not hold.</summary>
    public const string UnknownPlace = "unknown-place";

    /// <summary>An id that names no Ask Daoris conversation of this machine's.</summary>
    public const string NotHelp = "not-help";

    /// <summary>A conversation that has ended and cannot go on, so no words would ever apply the change.</summary>
    public const string Ended = "ended";

    /// <summary>Choose <paramref name="asked"/> as the focus of <paramref name="session"/>, among <paramref name="records"/>.</summary>
    /// <param name="registry">The registered repositories, which say the workspaces and repositories there are.</param>
    /// <param name="resumes">Whether an adapter's door resumes a conversation by its id.</param>
    public HelpFocusAnswer Choose(
        string session, IEnumerable<SessionRecord> records, IReadOnlyList<RepoView> registry, SessionEvents events,
        Func<string, bool> resumes, HelpFocus asked)
    {
        var record = records.FirstOrDefault(each => string.Equals(each.Id, session, StringComparison.Ordinal));
        if (record is null || record.Teammate || !WordsNever.IsHelp(record))
        {
            return Refuse(session, NotHelp, record is null
                ? $"no conversation here is `{session}`."
                : $"`{session}` is no Ask Daoris conversation of this machine's.");
        }

        if (Spelled(asked, registry) is not { } place)
        {
            return Refuse(session, UnknownPlace, $"there is no {asked.Named} on this machine, so the focus stays as it is.");
        }

        var conversation = new HarnessConversations(home).Read(session);
        if (!record.Live && !(conversation is not null && resumes(conversation.Adapter)))
        {
            return Refuse(session, Ended, $"`{session}` has ended and cannot go on, so its focus stays as it is.");
        }

        var spoken = events.Openings([session]).GetValueOrDefault(session) is not null;
        var kept = new HelpConversations(home);
        kept.Change(session, now => spoken
            ? now with { FocusNext = place.Equals(HelpFoci.Of(now, record.Workspace).Focus) ? null : place }
            : now with { Focus = place, FocusNext = null, Foci = [place] });
        var after = HelpFoci.Of(kept.Read(session), record.Workspace);
        return new(session, after.Focus, after.FocusNext);
    }

    /// <summary>Keep a new conversation's first focus: the one its start asked for, or the earlier one's it starts from.</summary>
    public void Begin(string session, HelpFocus focus) =>
        new HelpConversations(home).Change(session, kept => kept with { Focus = focus, FocusNext = null, Foci = [focus] });

    /// <summary>The applied focus of <paramref name="session"/>, homed in <paramref name="workspace"/>.</summary>
    public HelpFocus Applied(string session, string? workspace) => HelpFoci.Of(new HelpConversations(home).Read(session), workspace).Focus;

    /// <summary>
    /// The place as the registry spells it now, or null where it holds none: a workspace is <c>default</c> or one a registered
    /// repository names, and a repository is registered in that workspace.
    /// </summary>
    public static HelpFocus? Spelled(HelpFocus asked, IReadOnlyList<RepoView> registry) => asked.Kind switch
    {
        HelpFocus.MachineKind => HelpFocus.Machine,
        HelpFocus.WorkspaceKind => registry.Select(known => known.Workspace).Append(RemoteTarget.DefaultWorkspace)
            .FirstOrDefault(name => string.Equals(name, asked.Workspace, StringComparison.OrdinalIgnoreCase)) is { } workspace
            ? HelpFocus.OfWorkspace(workspace)
            : null,
        _ => registry.FirstOrDefault(known => string.Equals(known.Workspace, asked.Workspace, StringComparison.OrdinalIgnoreCase)
                && string.Equals(known.Repository, asked.Repository, StringComparison.OrdinalIgnoreCase)) is { } repository
            ? HelpFocus.OfRepository(repository.Workspace, repository.Repository)
            : null,
    };

    private static HelpFocusAnswer Refuse(string session, string code, string said) => new(session, null, null, code, said);
}
