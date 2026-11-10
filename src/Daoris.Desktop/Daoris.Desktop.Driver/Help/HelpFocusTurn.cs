namespace Daoris.Driver;

/// <summary>
/// A turn's change of focus: the place it ran under before, the one it runs under now, and the place that was gone where the
/// driver widened it (§5 of the ASKSCOPE2 design).
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
        ? $"Focus is now {HelpFocusing.Named(To)}: {(gone.Repository is { } repository ? $"repository `{repository}`" : HelpFocusing.Named(gone))} is no longer on this machine. {Reach}"
        : To.Kind == HelpFocus.MachineKind
            ? $"Focus is now this machine: every workspace (was {HelpFocusing.Named(From)})."
            : $"Focus is now {HelpFocusing.Named(To)} (was {HelpFocusing.Named(From)}). {Reach}";
}

/// <summary>
/// What the words that start an Ask Daoris turn take of its focus (ASKSCOPE2c): the one waiting, else the one applied, resolved
/// against the registry now, a gone repository widened to its workspace and a gone workspace to this machine; kept as the
/// applied focus, added to the foci, and the waiting one cleared. Its agent had ended for the change (<c>HelpAccessSwap.Refocused</c>),
/// so these words resume the conversation, and the change is told in the line ahead of them.
/// </summary>
public static class HelpFocusTurn
{
    /// <summary>Apply the focus the next words take; the change, or null where the turn runs under the focus it ran under.</summary>
    /// <param name="workspace">The conversation's home workspace.</param>
    /// <param name="registry">The registered repositories now; null where they could not be read, which widens nothing.</param>
    public static HelpFocusChange? Apply(string home, string session, string? workspace, IReadOnlyList<RepoView>? registry)
    {
        HelpFocusChange? change = null;
        new HelpConversations(home).Change(session, kept =>
        {
            var held = HelpFoci.Of(kept, workspace);
            var wanted = held.FocusNext ?? held.Focus;
            var (place, gone) = registry is null ? (wanted, null) : Resolved(wanted, registry);
            if (!place.Equals(held.Focus) || gone is not null) change = new(held.Focus, place, gone);
            return kept with
            {
                Focus = place,
                FocusNext = null,
                Foci = held.Foci.Contains(place) ? held.Foci : [.. held.Foci, place],
            };
        });
        return change;
    }

    /// <summary>The words with <paramref name="line"/> ahead of the first, which keeps its id, so the record takes the same words off.</summary>
    public static IReadOnlyList<SaidWordView> Ahead(IReadOnlyList<SaidWordView> words, string line) =>
        words.Count == 0 ? words : [words[0] with { Text = $"{line}\n\n{words[0].Text}" }, .. words.Skip(1)];

    /// <summary>The place as the registry holds it now, or the nearest wider one it holds with the place that was gone.</summary>
    private static (HelpFocus Place, HelpFocus? Gone) Resolved(HelpFocus wanted, IReadOnlyList<RepoView> registry)
    {
        if (HelpFocusing.Spelled(wanted, registry) is { } held) return (held, null);
        var wider = wanted.Kind == HelpFocus.RepositoryKind
            ? HelpFocusing.Spelled(HelpFocus.OfWorkspace(wanted.Workspace!), registry) ?? HelpFocus.Machine
            : HelpFocus.Machine;
        return (wider, wanted);
    }
}
