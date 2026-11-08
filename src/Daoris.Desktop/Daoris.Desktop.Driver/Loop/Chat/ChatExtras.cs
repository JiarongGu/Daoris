namespace Daoris.Driver;

/// <summary>
/// What a live conversation is handed once, ahead of the person's next words, as one message takes it: the earlier part's brief, the browser
/// line, the workflow draft with its palette, and the start's process and standing block. Taken as the message is composed and given back where no turn takes the message, so the
/// words said again still carry them.
/// </summary>
internal sealed class ChatExtras
{
    private readonly ChatContext _context;
    private readonly string _session;
    private Handing? _handed;
    private string? _browser;
    private WorkflowDraftTold? _drawn;
    private string? _digest;
    private Action? _alongside;

    private ChatExtras(ChatContext context, string session)
    {
        _context = context;
        _session = session;
    }

    /// <summary>The brief of the earlier part, handed with the first message.</summary>
    public ComposedBrief? Brief => _handed?.Brief;

    /// <summary>The line telling the agent of the browser it is handed.</summary>
    public string? Browser => _browser;

    /// <summary>The draft as the message carries it: its palette handed whole only where the conversation is not told it under the same digest.</summary>
    public WorkflowDraftTold? Draft { get; private set; }

    /// <summary>What the start's process and standing block took with the message, given back by <see cref="Restore"/> as the rest is.</summary>
    public void Alongside(Action restore) => _alongside += restore;

    /// <summary>Nothing taken: a conversation nothing here holds is handed none of it.</summary>
    public static ChatExtras None(ChatContext context, string session) => new(context, session);

    public static ChatExtras Take(ChatContext context, string session)
    {
        var taken = new ChatExtras(context, session);
        if (context.Handing.TryRemove(session, out var handed)) taken._handed = handed;
        if (context.Browsers.TryRemove(session, out var browser)) taken._browser = browser;
        if (!context.Drafts.TryRemove(session, out var draft)) return taken;

        taken._drawn = draft;
        taken.Draft = draft;
        if (draft.Palette is not { } palette) return taken;

        taken._digest = context.Palettes.TryGetValue(session, out var told) ? told : null;
        context.Palettes[session] = palette.Digest;
        if (taken._digest == palette.Digest) taken.Draft = draft with { Palette = palette with { Text = null } };
        return taken;
    }

    /// <summary>
    /// Give back what was taken, ahead of any a later message has been handed meanwhile: no turn took the message, so the words said again
    /// are the ones that carry it.
    /// </summary>
    public void Restore()
    {
        _alongside?.Invoke();
        if (_handed is not null) _context.Handing.TryAdd(_session, _handed);
        if (_browser is not null) _context.Browsers.TryAdd(_session, _browser);
        if (_drawn is null) return;

        if (_context.Drafts.TryAdd(_session, _drawn) && _drawn.Palette is not null)
        {
            if (_digest is null) _context.Palettes.TryRemove(_session, out _);
            else _context.Palettes[_session] = _digest;
        }
    }
}
