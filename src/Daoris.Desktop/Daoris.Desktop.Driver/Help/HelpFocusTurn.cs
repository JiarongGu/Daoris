namespace Daoris.Driver;

/// <summary>
/// A turn's change of focus: the place it ran under before, the one it runs under now, and the place no longer here where the
/// driver widened it.
/// </summary>
/// <param name="Gone">The place chosen or held that is no longer on this machine, which <paramref name="To"/> widens; null for a change the person chose.</param>
public sealed record HelpFocusChange(HelpFocus From, HelpFocus To, HelpFocus? Gone)
{
    private const string Reach = "Your reach is unchanged: name a workspace, or `all`, to look beyond it.";

    /// <summary>Whether the driver widened a gone place, which the transcript says as <i>Focus widened</i>.</summary>
    public bool Widened => Gone is not null;

    /// <summary>
    /// The one line the agent is told ahead of the person's words and the preface: agent-facing, in English, never translated.
    /// </summary>
    public string Line => Gone is { } gone
        ? $"Focus is now {To.Named}: {(gone.Repository is { } repository ? $"repository `{repository}`" : gone.Named)} is no longer on this machine. {Reach}"
        : To.Kind == HelpFocus.MachineKind
            ? $"Focus is now this machine: every workspace (was {From.Named})."
            : $"Focus is now {To.Named} (was {From.Named}). {Reach}";
}

/// <summary>
/// What the words that start an Ask Daoris turn take of its focus: the one waiting, else the one applied, resolved
/// against the registry now, a gone repository widened to its workspace and a gone workspace to this machine; kept as the
/// applied focus, added to the foci, and the waiting one cleared. Its agent had ended for the change (<c>HelpAccessSwap.Refocused</c>),
/// so these words resume the conversation, and the change is told in the line ahead of them.
/// </summary>
public static class HelpFocusTurn
{
    /// <summary>Apply the focus the next words take; the change, or null where the turn runs under the focus it ran under.</summary>
    /// <param name="workspace">The conversation's home workspace.</param>
    /// <param name="registry">The registered repositories now; null where they could not be read, which widens nothing.</param>
    /// <param name="first">The person's message these words start with, which keeps the change (<see cref="HelpFocusMark"/>); null for none.</param>
    public static HelpFocusChange? Apply(
        string home, string session, string? workspace, IReadOnlyList<RepoView>? registry, SaidWordView? first = null)
    {
        HelpFocusChange? change = null;
        new ConversationFacts(home).Change(session, kept =>
        {
            var held = HelpFoci.Of(kept, workspace);
            var wanted = held.FocusNext ?? held.Focus;
            // Its home is never gone while it speaks there, though a workspace with no repository is in no registry row.
            var (place, gone) = registry is null || wanted.Equals(HelpFocus.Home(workspace)) ? (wanted, null) : Resolved(wanted, registry);
            if (!place.Equals(held.Focus) || gone is not null) change = new(held.Focus, place, gone);
            return kept with
            {
                Focus = place,
                FocusNext = null,
                Foci = held.Foci.Contains(place) ? held.Foci : [.. held.Foci, place],
                Marks = change is null || first is null
                    ? kept.Marks
                    : [.. kept.Marks, new HelpFocusMark(first.Id, first.At, change.From, change.To, change.Widened)],
            };
        });
        return change;
    }

    /// <summary>The words with <paramref name="line"/> ahead of the first, which keeps its id, so the record takes the same words off.</summary>
    public static IReadOnlyList<SaidWordView> Ahead(IReadOnlyList<SaidWordView> words, string line) =>
        words.Count == 0 ? words : [words[0] with { Text = $"{line}\n\n{words[0].Text}" }, .. words.Skip(1)];

    /// <summary>The place as the registry holds it now, or the nearest wider one it holds beside the place no longer here.</summary>
    private static (HelpFocus Place, HelpFocus? Gone) Resolved(HelpFocus wanted, IReadOnlyList<RepoView> registry)
    {
        if (HelpFocusing.Spelled(wanted, registry) is { } held) return (held, null);
        var wider = wanted.Kind == HelpFocus.RepositoryKind
            ? HelpFocusing.Spelled(HelpFocus.OfWorkspace(wanted.Workspace!), registry) ?? HelpFocus.Machine
            : HelpFocus.Machine;
        return (wider, wanted);
    }
}
