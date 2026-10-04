using System.Text.Json;
using static Daoris.Driver.HelpProposals;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's <c>go</c> proposal (HELP6): a place the window has — its target the view, and its own
/// <c>domain</c> and <c>part</c> — judged by <see cref="HelpPlaces"/>. It changes nothing: its Apply hands the
/// page the place, which navigates as the starters' doors do.
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
    };

    public HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        var view = proposal.Target?.Trim().ToLowerInvariant() ?? "";
        var domain = proposal.Domain?.Trim().ToLowerInvariant() is { Length: > 0 } d ? d : null;
        var part = proposal.Part?.Trim().ToLowerInvariant() is { Length: > 0 } p ? p : null;
        HelpPlan Refused(string why) => new(why, "", "", null);

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

        return new HelpPlan(null, describe + ".", "", null) { Go = new HelpPlace(view, domain, part) };
    }

    public Task<HelpApplied> ApplyAsync(HelpApplying applying, CancellationToken ct) =>
        Task.FromResult(applying.Settled(true, $"Applied: `#{applying.Id}` — {applying.Plan.Describe} Nothing else changed.", null)
            with { Go = applying.Plan.Go });
}

/// <summary>A place on the window a go takes the person to (HELP6): a view, a Settings domain, a part of it.</summary>
public sealed record HelpPlace(string View, string? Domain, string? Part);

/// <summary>
/// The places on the window a go may name (HELP6): the views, Settings' domains, and the parts of them a
/// door already opens — the setup guide's steps, a domain's cards, Repositories' drawers and a repository's Setup,
/// an agent's page's sections.
/// </summary>
/// <remarks>
/// A twin (`.claude/knowledge/twins.md`) of the page's <c>help/places.ts</c>, which navigates to them:
/// they share no code, each side's test holds the same table, and they change together. Every name is
/// the one the window's own label shows.
/// </remarks>
public static class HelpPlaces
{
    // UX6e2: Agents is a place since UX6e (D150 §5), after Search on the bar.
    public static readonly IReadOnlyList<(string Id, string Name)> Views =
    [
        ("overview", "Overview"), ("sessions", "Sessions"), ("quests", "Quests"), ("projects", "Repositories"),
        ("map", "Map"), ("convergence", "Convergence"), ("search", "Search"), ("agents", "Agents"), ("settings", "Settings"),
    ];

    public static readonly IReadOnlyList<(string Id, string Name)> Domains =
    [
        ("start", "Setup"), ("appearance", "Appearance"), ("ai", "AI features"), ("workspace", "Workspace"),
        ("driver", "Driver"), ("permissions", "Permissions"), ("plugins", "Plugins"),
        ("browser", "Browser"), ("logs", "Machine log"),
    ];

    /// <summary>The parts, each within a view (Repositories, Agents) or a Settings domain.</summary>
    public static readonly IReadOnlyList<(string Within, string Id, string Name)> Parts =
    [
        // HELPSETUP1: a repository's Setup (UX6f, D150 §4.2), where its own values are set; a go names no repository.
        ("projects", "add", "Add repository"), ("projects", "import", "Import a folder"), ("projects", "setup", "a repository's Setup"),
        ("start", "agent", "step 1, an agent"), ("start", "helper", "step 2, Ask Daoris's agent"),
        ("start", "repositories", "step 3, a workspace and its repositories"), ("start", "driven", "step 4, what is driven"),
        ("start", "landing", "step 5, how work lands"), ("start", "rules", "step 6, what agents may do"),
        ("workspace", "wiring", "Wiring"), ("workspace", "lines", "Lines"), ("workspace", "landing", "How work lands"),
        ("workspace", "sweep", "Session branches"),
        // UX6e2: the agent's page's sections a door opens (D150 §5.2); Permissions' Proposals are its What it may do.
        ("agents", "accounts", "Accounts"), ("agents", "rules", "What it may do"), ("agents", "usage", "Usage"),
        // HELP10: the card READ1 built (D107), which the page finds by its own `settings-across`.
        ("permissions", "across", "Across repositories"),
    ];
}

public sealed partial record HelpProposal
{
    /// <summary>A go's Settings domain, or null.</summary>
    public string? Domain { get; init; }

    /// <summary>A go's part of its domain or view — a card, a setup step, a drawer — or null.</summary>
    public string? Part { get; init; }
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
