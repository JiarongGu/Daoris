using System.Text.Json;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>browser</c> proposal (HELP10, D110): Daoris's browser's settings, as Settings → Browser and
/// <c>daoris browser</c> set them — its door the terminal's verb (<c>use</c>, <c>links</c>, <c>extensions</c>,
/// <c>favorite</c>), its value what it is set to, and for a favorite its target the address and its own <c>title</c>.
/// </summary>
/// <remarks>
/// <para><b>Judged by the screen's routes</b> (<c>BrowserModule</c>, the desktop modules'): the values each route
/// takes, a favorite that is a web page by the route's own reader, and a file the screen could not read never written
/// over — each refused in the route's words. The files are the modules' own, so the facts carry what the desktop
/// read of them (<see cref="HelpMachineFacts.Browser"/>), and none read is no proposal.</para>
///
/// <para>A favorite removed must be one kept: the route removes nothing silently, and a helper can invent an
/// address. Applied through <see cref="IHelpDoors.ChangeBrowser"/>, the route's own edit.</para>
/// </remarks>
internal sealed class HelpBrowserProposals : IHelpProposalKind
{
    public string Kind => "browser";

    public string Tool => "browser_propose";

    /// <summary>The `daoris browser` verbs that change something, as the service's twin spells them.</summary>
    public IReadOnlyList<string> Doors { get; } = ["use", "links", "extensions", "favorite"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal with { Title = Text(file, "title") };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var value = proposal.Value?.Trim() ?? "";
        HelpPlan Refused(string why) => new(why, "", "", null);
        if (!Doors.Contains(proposal.Door))
        {
            return Refused($"`{proposal.Door}` is not a setting of Daoris's browser — one of {Names(Doors)}.");
        }

        if (facts.Browser is not { } files)
        {
            return Refused("Daoris's browser's files were not read here, so nothing about them is proposed — Settings → Browser sets them.");
        }

        if (proposal.Door == "favorite") return Favorite(proposal, value, files);

        // The route's own refusal for a file it could not read: it will not write over it.
        if (files.Problem is { } problem) return Refused(Unreadable(problem));
        switch (proposal.Door)
        {
            case "use" when value is Daoris or Edge:
                return new HelpPlan(null,
                    value == Edge
                        ? "Sessions and you use your Edge, on a profile of Daoris's, from the next time the browser is opened. "
                          + "Favorites and the extensions setting are Daoris's own browser's; Edge keeps its own."
                          + (files.EdgeFound ? "" : " No Edge is installed on this machine, so choosing it would open nothing.")
                        : "Sessions and you use Daoris's own browser, on the engine Daoris ships, from the next time the browser is opened.",
                    $"daoris browser use {value}", null);
            case "use":
                return Refused($"The browser is `daoris` or `edge`, not `{value}`.");
            case "links" when value is Daoris or SystemBrowser:
                return new HelpPlan(null,
                    value == Daoris
                        ? "Links on the page open in Daoris's browser, from the next click. A sign-in link always opens in the system's browser."
                        : "Links on the page open in the system's browser, from the next click.",
                    $"daoris browser links {value}", null);
            case "links":
                return Refused($"Links open in `system` or `daoris`, not `{value}`.");
            case "extensions" when value is Offer or Refuse:
                return new HelpPlan(null,
                    $"Other software's Chrome extensions will be {(value == Offer ? "offered for your approval" : "refused")} from the "
                    + "browser's next start, in Daoris's own browser.",
                    $"daoris browser extensions {value}", null);
            default:
                return Refused($"The extensions setting is `offer` or `refuse`, not `{value}`.");
        }
    }

    /// <summary>A favorite kept or dropped, as the screen's two routes take it: a web page by the route's reader, one kept to drop.</summary>
    private static HelpPlan Favorite(HelpProposal proposal, string value, HelpBrowserFacts files)
    {
        HelpPlan Refused(string why) => new(why, "", "", null);
        if (value is not ("add" or "remove")) return Refused($"a favorite is `add` or `remove`, not `{value}`.");
        var typed = proposal.Target?.Trim() ?? "";
        if (typed.Length == 0) return Refused("a favorite names its address — the page to keep, or the one to stop keeping.");
        if (files.FavoritesProblem is { } problem) return Refused(Unreadable(problem));
        if (files.Page(typed) is not { } page) return Refused($"`{typed}` is not a web page, so it cannot be a favorite.");

        if (value == "remove")
        {
            if (!files.Favorites.Contains(page, StringComparer.Ordinal))
            {
                return Refused($"`{typed}` is not a favorite — the ones kept are "
                    + (files.Favorites.Count > 0 ? string.Join(", ", files.Favorites) : "none") + ".");
            }

            return new HelpPlan(null, $"Stop keeping {page} on the browser's bookmarks bar.", $"daoris browser favorite remove {page}", null);
        }

        var title = proposal.Title?.Trim() is { Length: > 0 } named ? named : null;
        return new HelpPlan(null,
            $"Keep {page} in the Daoris folder on the browser's bookmarks bar" + (title is null ? "." : $", as “{title}”.")
                + " It shows from the browser's next start.",
            $"daoris browser favorite add {page}" + (title is null ? "" : $" --title \"{title}\""), null);
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct)
    {
        var (proposal, plan, id) = (applying.Proposal, applying.Plan, applying.Id);
        try
        {
            applying.Doors.ChangeBrowser(
                proposal.Door, proposal.Value!.Trim(), proposal.Target?.Trim() is { Length: > 0 } address ? address : null,
                proposal.Title?.Trim() is { Length: > 0 } title ? title : null);
        }
        catch (DriverException error)
        {
            return Task.FromResult(applying.Settled(false, $"Not applied: `#{id}` (`{plan.Terminal}`) — {error.Message}", error.Message));
        }

        return Task.FromResult(applying.Settled(true, $"Applied: `#{id}` — {plan.Describe} (`{plan.Terminal}`)", null));
    }

    /// <summary>The Browser screen's refusal for a file it could not read (<c>BrowserModule</c>), in its words.</summary>
    private static string Unreadable(string problem) =>
        $"{problem}. Fix it, or delete it to start from nothing: Daoris will not write over a file it could not read.";

    // The values the screen's routes take, spelled as the modules' `BrowserChoice`, `LinksSetting` and
    // `ExtensionsSetting` spell them — a twin duplicated deliberately, since the driver cannot reference the modules.
    private const string Daoris = "daoris";
    private const string Edge = "edge";
    private const string SystemBrowser = "system";
    private const string Offer = "offer";
    private const string Refuse = "refuse";
}

/// <summary>
/// What Daoris's browser's two files hold, as the Browser screen reads them (HELP10): the settings and the favorites,
/// why either could not be read, whether an Edge is installed, and the route's own reader of an address.
/// </summary>
/// <param name="Browser">`daoris` or `edge`.</param>
/// <param name="Links">`system` or `daoris`.</param>
/// <param name="Extensions">`offer` or `refuse`.</param>
/// <param name="Favorites">The pages kept, each as the route's reader writes it.</param>
public sealed record HelpBrowserFacts(string Browser, string Links, string Extensions, IReadOnlyList<string> Favorites)
{
    /// <summary>Why the settings file could not be read, in the reader's words; null when it could.</summary>
    public string? Problem { get; init; }

    /// <summary>Why the favorites file could not be read; null when it could.</summary>
    public string? FavoritesProblem { get; init; }

    /// <summary>Whether an Edge is installed here, which the screen says before Edge is chosen.</summary>
    public bool EdgeFound { get; init; } = true;

    /// <summary>The route's own reading of an address as a web page (<c>BrowserFavorites.Address</c>): the page, or null for none.</summary>
    public Func<string, string?> Page { get; init; } = _ => null;
}

public sealed partial record HelpMachineFacts
{
    /// <summary>What Daoris's browser's files hold (HELP10), read by the desktop for a browser proposal; null where none was read.</summary>
    public HelpBrowserFacts? Browser { get; init; }
}

public sealed partial record HelpProposal
{
    /// <summary>A favorite's title, for a browser proposal that keeps one (HELP10), or null for the page's host.</summary>
    public string? Title { get; init; }
}

public partial interface IHelpDoors
{
    /// <summary>
    /// The Browser screen's own edit of Daoris's browser's files (HELP10, <c>BrowserModule</c>): which browser, where the
    /// page's links open, other software's extensions, or a favorite kept or dropped.
    /// </summary>
    /// <param name="setting">`use`, `links`, `extensions` or `favorite`.</param>
    /// <param name="value">What it is set to; for a favorite, `add` or `remove`.</param>
    void ChangeBrowser(string setting, string value, string? address, string? title);
}
