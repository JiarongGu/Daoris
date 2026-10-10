using System.Text.Json;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>go</c> proposal (HELP6): a place the window has — its target the view, and its own
/// <c>domain</c>, <c>part</c> and, since ENTRY1f1, <c>item</c> — judged by <see cref="HelpPlaces"/> and, for an item,
/// against the machine's quests and asks. It changes nothing: its Apply hands the page the place, which navigates as the
/// starters' doors do.
/// </summary>
internal sealed class HelpGoProposals : IHelpProposalKind
{
    public string Kind => "go";

    public string Tool => "go_propose";

    public IReadOnlyList<string> Doors { get; } = ["go"];

    public HelpProposal Read(HelpProposal proposal, JsonElement file) => proposal with
    {
        Domain = Text(file, "domain"),
        Part = Text(file, "part"),
        Item = Text(file, "item"),
    };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var view = proposal.Target?.Trim().ToLowerInvariant() ?? "";
        var domain = proposal.Domain?.Trim().ToLowerInvariant() is { Length: > 0 } d ? d : null;
        var part = proposal.Part?.Trim().ToLowerInvariant() is { Length: > 0 } p ? p : null;
        HelpPlan Refused(string why) => new(why, "", "", null);

        // UX6i2a: a go spelled as its place was before it moved is judged, said and handed on as the place it is now.
        var asked = new HelpPlace(view, domain, part);
        if (HelpPlaces.Kept.FirstOrDefault(each => each.Was == asked) is { Now: { } now })
        {
            (view, domain, part) = (now.View, now.Domain, now.Part);
        }

        // A tuple not found is its default, whose names are null.
        var shown = HelpPlaces.Views.FirstOrDefault(each => each.Id == view);
        if (shown.Id is null)
        {
            return Refused($"there is no view `{view}` — one of {Names(HelpPlaces.Views.Select(each => each.Id))}.");
        }

        var describe = $"Open {shown.Name}";
        if (domain is not null)
        {
            if (view != "settings") return Refused("a domain is a part of Settings — name `settings` as the view.");
            var named = HelpPlaces.Domains.FirstOrDefault(each => each.Id == domain);
            if (named.Id is null)
            {
                return Refused($"there is no Settings domain `{domain}` — one of {Names(HelpPlaces.Domains.Select(each => each.Id))}.");
            }

            describe += $" → {named.Name}";
        }

        if (part is not null)
        {
            var within = domain ?? view;
            var parts = HelpPlaces.Parts.Where(each => each.Within == within).ToList();
            var found = parts.FirstOrDefault(each => each.Id == part);
            if (found.Id is null)
            {
                return Refused(parts.Count > 0
                    ? $"there is no part `{part}` of `{within}` — one of {Names(parts.Select(each => each.Id))}."
                    : $"there is no part `{part}` of `{within}`" + (view == "settings" && domain is null ? " — name its Settings domain." : "."));
            }

            // A setup step is a place in the guide, not a card beneath a domain.
            describe += within == "start" ? $" at {found.Name}" : $" → {found.Name}";
        }

        var item = proposal.Item?.Trim() is { Length: > 0 } i ? i : null;
        if (item is not null)
        {
            // ENTRY1f1 (D161's ENTRY1f note): the one that waits is a record of the view's list, judged as the delete kind
            // judges an id, so a card never hands the page a place that is not there.
            if (!HelpPlaces.ItemViews.Contains(view)) return Refused(
                $"a go names an item only on {Names(HelpPlaces.ItemViews)} — a quest by its id, or an ask as `{HelpPlaces.AskItem}<id>`.");
            if (domain is not null || part is not null) return Refused(
                $"a go names a part of `{view}` or an item in it, not both — the item opens on its own.");
            var (named, refusal) = Item(item, facts);
            if (refusal is not null) return Refused(refusal);
            describe += named!.Value.Said;
            item = named.Value.Item;
        }

        return new HelpPlan(null, describe + ".", "", null) { Go = new HelpPlace(view, domain, part) { Item = item } };
    }

    /// <summary>
    /// ENTRY1f1: an item on Quests as the machine's records hold it — a quest by its id, an ask as <c>ask:&lt;id&gt;</c>, a
    /// <c>#</c> before the id stripped and the id matched without case, as the delete kind's — with the words the card says.
    /// The records are every quest and ask, closed ones included, so a closed one is found and said closed.
    /// </summary>
    private static ((string Item, string Said)? Named, string? Refusal) Item(string item, HelpMachineFacts facts)
    {
        var spelled = item.TrimStart('#');
        var asked = spelled.StartsWith(HelpPlaces.AskItem, StringComparison.OrdinalIgnoreCase);
        var id = (asked ? spelled[HelpPlaces.AskItem.Length..] : spelled).Trim().TrimStart('#');
        if (id.Length == 0 || id.Contains(':'))
        {
            return (null, $"`{item}` is no item of `quests` — a quest by its id, or an ask as `{HelpPlaces.AskItem}<id>`.");
        }

        static string Closed(string status) =>
            status.ToLowerInvariant() is var word && word is "done" or "declined" or "closed" ? $", {word}" : "";
        if (asked)
        {
            return facts.Asks.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is { } ask
                ? (($"{HelpPlaces.AskItem}{ask.Id}", $" → ask `#{ask.Id}` “{ask.Sentence}”{Closed(ask.State)}"), null)
                : (null, $"there is no ask `#{id}` on this machine.");
        }

        if (facts.Quests.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is { } quest)
        {
            return ((quest.Id, $" → quest `#{quest.Id}` “{quest.Title}”{Closed(quest.Status)}"), null);
        }

        // The room lists an ask as `#id` too: one named without its prefix is told how to name it.
        return (null, facts.Asks.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase)) is { } meant
            ? $"there is no quest `#{id}` on this machine — `#{meant.Id}` is an ask: name it as `{HelpPlaces.AskItem}{meant.Id}`."
            : $"there is no quest `#{id}` on this machine.");
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct) =>
        Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} Nothing else changed.", null)
            with { Go = applying.Plan.Go });
}

/// <summary>A place on the window a go takes the person to (HELP6): a view, a Settings domain, a part of it.</summary>
public sealed record HelpPlace(string View, string? Domain, string? Part)
{
    /// <summary>
    /// The one record of the view's list a go opens (ENTRY1f1), as the page's list names it: a quest's id, or an ask's as
    /// <c>ask:&lt;id&gt;</c>, spelled as the machine's record is; null for a go to the place alone.
    /// </summary>
    public string? Item { get; init; }
}

/// <summary>
/// The places on the window a go may name (HELP6): the views, Settings' domains, and the parts of them a
/// door already opens — the groups of Sessions' and Quests' lists that wait on the person, the setup guide's steps, a
/// domain's cards, Repositories' drawers, a repository's Setup and a workspace's page's tabs and sections, Knowledge's two
/// modes, an agent's page's sections — and the places a go named before they moved.
/// </summary>
/// <remarks>
/// A twin (`.claude/knowledge/twins.md`) of the page's <c>help/places.ts</c>, which navigates to them:
/// they share no code, each side's test holds the same table, and they change together. Every name is
/// the one the window's own label shows.
/// </remarks>
public static class HelpPlaces
{
    // UX6i2a (D150 §2): the bar's eight places and Settings, in the bar's order: Knowledge where Convergence and Search were
    // (UX6i), Plugins after Agents (PLUGUI1b).
    public static readonly IReadOnlyList<(string Id, string Name)> Views =
    [
        ("overview", "Overview"), ("sessions", "Sessions"), ("quests", "Quests"), ("projects", "Repositories"),
        ("map", "Map"), ("knowledge", "Knowledge"), ("agents", "Agents"), ("plugins", "Plugins"), ("settings", "Settings"),
    ];

    // UX6i2a: the guide is Get started since UX6j, and Plugins left Settings for its place. UX6g2b: Workspace and
    // Permissions left it for a workspace's page with UX6g, and are kept below.
    public static readonly IReadOnlyList<(string Id, string Name)> Domains =
    [
        ("start", "Get started"), ("appearance", "Appearance"), ("ai", "AI features"), ("driver", "Driver"),
        ("browser", "Browser"), ("logs", "Machine log"),
    ];

    /// <summary>The parts, each within a view (Sessions, Quests, Repositories, Knowledge, Agents) or a Settings domain.</summary>
    public static readonly IReadOnlyList<(string Within, string Id, string Name)> Parts =
    [
        // ENTRY1b (D161's ENTRY1 note): what waits on the person below Sessions and Quests, a group of the view's list the
        // page brings into view, named by its heading: Sessions' parked and to-review groups (the reader's `you` and
        // `review`), Quests' asks and the quests held for the person's yes or review. A part names no session or quest in it
        // (a go's item names one on Quests, ENTRY1f1); Overview has no part, since what waits on the person leads it.
        ("sessions", "waiting", "Waiting on you"), ("sessions", "review", "To review"),
        ("quests", "asks", "Asks"), ("quests", "held", "Waiting on you"),
        // HELPSETUP1: a repository's Setup (UX6f, D150 §4.2), where its own values are set; a go names no repository.
        ("projects", "add", "Add repository"), ("projects", "import", "Import a folder"), ("projects", "setup", "a repository's Setup"),
        // UX6g2b (D161 §3, D150 §4.3): a workspace's page's four tabs and its Setup's two sections, prefixed since a
        // repository's page has three of the tabs' names; a go names no workspace, so the one in view opens.
        ("projects", "workspace-details", "a workspace's Details"), ("projects", "workspace-branches", "a workspace's Branches"),
        ("projects", "workspace-workflow", "a workspace's Workflow"), ("projects", "workspace-setup", "a workspace's Setup"),
        ("projects", "workspace-defaults", "a workspace's Defaults"), ("projects", "workspace-remote", "a workspace's Remote and reach"),
        // UX6i2a: Knowledge's two modes (UX6i), by the names its list's choice shows.
        ("knowledge", "search", "Search"), ("knowledge", "convergence", "Convergence"),
        ("start", "agent", "step 1, an agent"), ("start", "helper", "step 2, Ask Daoris's agent"),
        ("start", "repositories", "step 3, a workspace and its repositories"), ("start", "driven", "step 4, what is driven"),
        ("start", "landing", "step 5, how work lands"), ("start", "rules", "step 6, what agents may do"),
        // UX6e2: the agent's page's sections a door opens (D150 §5.2); Permissions' Proposals are its What it may do.
        ("agents", "accounts", "Accounts"), ("agents", "rules", "What it may do"), ("agents", "usage", "Usage"),
    ];

    /// <summary>
    /// The places a go named before they moved, each with the place it is now (UX6i2a, D150 §2): Search and Convergence
    /// became Knowledge's modes with UX6i, and Settings → Plugins the Plugins place with UX6j. UX6g2b: Settings → Workspace
    /// and Permissions retired into a workspace's page with UX6g (D150 §3.1), each old spelling with its part a row of its
    /// own, and Permissions alone what agents may do; a part no row names is refused. The room lists none of them, and a go
    /// still spelled so, kept in an earlier conversation or sent by a service that lists it, lands where it went.
    /// </summary>
    public static readonly IReadOnlyList<(HelpPlace Was, HelpPlace Now)> Kept =
    [
        (new("search", null, null), new("knowledge", null, "search")),
        (new("convergence", null, null), new("knowledge", null, "convergence")),
        (new("settings", "plugins", null), new("plugins", null, null)),
        (new("settings", "workspace", null), new("projects", null, "workspace-details")),
        (new("settings", "workspace", "wiring"), new("projects", null, "workspace-remote")),
        (new("settings", "workspace", "lines"), new("projects", null, "workspace-defaults")),
        (new("settings", "workspace", "landing"), new("projects", null, "workspace-defaults")),
        (new("settings", "workspace", "sweep"), new("projects", null, "workspace-branches")),
        (new("settings", "permissions", null), new("agents", null, "rules")),
        (new("settings", "permissions", "across"), new("projects", null, "workspace-defaults")),
    ];

    /// <summary>
    /// The views a go may name one item in (ENTRY1f1, D161's ENTRY1f note): Quests, whose list holds quests and asks.
    /// </summary>
    public static readonly IReadOnlyList<string> ItemViews = ["quests"];

    /// <summary>
    /// How an ask's item is told from a quest's on Quests, the page's <c>askItem</c> (<c>opener.ts</c>): duplicated
    /// deliberately, as a twin's constant is, never imported across.
    /// </summary>
    public const string AskItem = "ask:";
}

public sealed partial record HelpProposal
{
    /// <summary>A go's Settings domain, or null.</summary>
    public string? Domain { get; init; }

    /// <summary>A go's part of its domain or view — a card, a setup step, a drawer — or null.</summary>
    public string? Part { get; init; }

    /// <summary>A go's one quest or ask (ENTRY1f1), as the service's box wrote it, or null.</summary>
    public string? Item { get; init; }
}

public sealed partial record HelpPlan
{
    /// <summary>Where a go takes the person, for one the window has; null for every other kind.</summary>
    public HelpPlace? Go { get; init; }
}

public sealed partial record HelpApplied
{
    /// <summary>Where a go takes the person: the page navigates there, as its starters' doors do.</summary>
    public HelpPlace? Go { get; init; }
}
