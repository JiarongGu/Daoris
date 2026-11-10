using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// Ask Daoris's focus, the page's `bridge/help.ts`: what a conversation is about, chosen in its composer's chip and kept on its
/// file (ASKSCOPE2b, D253 point 3's note), and what a start is handed of it. Its reach stays every workspace whatever it is.
/// </summary>
public sealed class HelpFocusRoutes(DriverContext context)
{
    /// <summary>What a focus changes: Ask Daoris's history, and the conversations the panel shows.</summary>
    private const StateScopes Kept = StateScopes.Sessions | StateScopes.Help;

    private readonly DriverContext _context = context;
    private readonly DriverLoop _loop = context.Loop;

    // Choose what an Ask Daoris conversation is about, sent at once so a reload or another window shows it: before any words its
    // first focus, after them the one its next words take, choosing the applied one again clearing what waited.
    // `{ session, focus }` answers `{ session, focus, focusNext }`, or `{ session, refused, said }` for `unknown-place`,
    // `not-help` or `ended`, the focus left as it was.
    [DriverRoute("HELP_FOCUS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("HELP_FOCUS", Session = "session")]
    [Timeless("HELP_FOCUS", "a conversation's focus")]
    [Publishes("HELP_FOCUS", Kept, Session = "session")]
    private async Task<object?> HelpFocusAsync(HelpFocusPress press, CancellationToken cancellationToken)
    {
        var asked = Place(press.Focus)
            ?? throw new DriverException("a focus is `machine`, a workspace, or a workspace's repository; it was not changed.");

        // `ask-daoris focus <id> <place>`, said in the verb's words.
        if (press.Said)
        {
            var focus = new HelpVerbAsk("focus") { Id = press.Session, Text = asked.Said };
            return await SessionsVerbs.HelpSaidAsync(_context, focus, cancellationToken).ConfigureAwait(false);
        }

        var service = _loop.Service ?? throw NotReady();
        var records = SessionRecords.Parse(await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false));
        var registry = await service.RegistryAsync(cancellationToken).ConfigureAwait(false);
        return new HelpFocusing(_loop.Home)
            .Choose(press.Session, records, registry, _loop.Events, adapter => SessionsVerbs.Resumes(_loop, adapter), asked)
            .Wire();
    }

    /// <summary>The place a press names, or null where it names none or a shape that is none.</summary>
    public static HelpFocus? Place(JsonElement? wire) => wire is { ValueKind: JsonValueKind.Object } place ? HelpFocus.Read(place) : null;

    /// <summary>
    /// The focus a conversation <c>START_HELP</c> placed starts with: the place the press names, spelled as the registry spells it,
    /// else its home's. A place the registry does not hold is refused before anything is placed.
    /// </summary>
    public static HelpFocus? Asked(JsonElement? wire, IReadOnlyList<RepoView> registry)
    {
        if (Place(wire) is not { } asked) return null;
        return HelpFocusing.Spelled(asked, registry)
            ?? throw new DriverException($"there is no {HelpFocusing.Named(asked)} on this machine, so no conversation was started.");
    }

    /// <summary>
    /// Keep the first focus of the conversation a start placed, where its press asked for one, and answer the focus it holds;
    /// one carried on keeps its own.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Started(string home, string session, string workspace, HelpFocus? asked)
    {
        var focusing = new HelpFocusing(home);
        if (asked is not null) focusing.Begin(session, asked);
        return focusing.Applied(session, workspace).Wire();
    }

    /// <summary>
    /// A conversation started from an earlier one takes that one's applied focus (§1's table), never the one waiting; answered as
    /// the focus it holds.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> CarriedAsync(
        DriverLoop loop, string from, string session, string workspace, CancellationToken cancellationToken)
    {
        var service = loop.Service ?? throw NotReady();
        var earlier = SessionRecords.Parse(await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, from, StringComparison.Ordinal));
        var focusing = new HelpFocusing(loop.Home);
        return Started(loop.Home, session, workspace, earlier is null ? null : focusing.Applied(from, earlier.Workspace));
    }
}
