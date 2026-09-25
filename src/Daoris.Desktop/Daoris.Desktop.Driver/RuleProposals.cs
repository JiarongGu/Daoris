using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>What became of a proposal to change the rules (PERM2).</summary>
public enum ProposalState
{
    /// <summary>Written by a session's connector; the driver has not read it yet.</summary>
    Proposed,

    /// <summary>It narrowed what agents may do, so the driver applied it.</summary>
    Applied,

    /// <summary>It widens what agents may do, so it waits for the person.</summary>
    Waiting,

    /// <summary>The person said yes, and it was applied.</summary>
    Accepted,

    /// <summary>The person said no.</summary>
    Declined,

    /// <summary>The rules could not take it — a malformed rule, an unknown default, a scope with no name.</summary>
    Refused,

    /// <summary>The rules already said it.</summary>
    Unchanged,
}

/// <summary>Whether a change lets agents do less, more, or the same.</summary>
public enum ChangeEffect
{
    Narrows,
    Widens,
    Nothing,
}

/// <summary>A change as proposed — the file's <c>change</c> object.</summary>
public sealed record ProposedChange(
    string Action, RuleScope Scope, string? Name, RuleList? List, string? Rule, string? Default, bool? On);

/// <summary>One proposal as read from its file.</summary>
/// <param name="Session">The session that proposed it, when its connector named one.</param>
/// <param name="Ask">The ask that session answers, when it is an intake.</param>
public sealed record RuleProposal(
    string Id, DateTimeOffset Proposed, string? Session, string? Ask, ProposedChange Change, string Why,
    ProposalState State)
{
    /// <summary>When it was settled, by whom (`the driver` or `the person`), and the note that says why.</summary>
    public DateTimeOffset? Settled { get; init; }

    public string? SettledBy { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// An agent's proposals to change what agents may do (PERM2, D74), as the driver and the person settle
/// them.
/// </summary>
/// <remarks>
/// <para><b>🔴 A widening never applies without the person</b> — the owner's answer, 2026-09-24. A
/// narrowing needs no one (a better approval surface must not widen autonomy, D37/D52, and narrowing
/// cannot), so the tick applies it at once; a widening is held as <see cref="ProposalState.Waiting"/>
/// until the person answers from either door (D50).</para>
///
/// <para><b>Narrowing is judged against the rules as they stand.</b> Removing a rule narrows when it was
/// an allow and widens when it was an ask or a deny; making a denied rule an ask loosens it, because
/// another scope's allow is then asked rather than denied. A default that allows narrows when it goes off;
/// one that denies or guards widens.</para>
///
/// <para><b>THE FILE is the contract</b>: one file per proposal under <c>&lt;home&gt;/proposals/</c>, written
/// by the connector (the service's <c>RuleProposalBox</c>) and settled here and by the CLI's
/// <c>ruleproposals.ts</c>. Settling rewrites only <c>state</c> and <c>settled</c>; everything the
/// session wrote is kept.</para>
/// </remarks>
public static class RuleProposals
{
    public const string Folder = "proposals";

    private static readonly Regex IdShape = new("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);

    /// <summary>Every readable proposal under the home, newest first. An unreadable file is left out.</summary>
    public static IReadOnlyList<RuleProposal> Load(string home)
    {
        var folder = Path.Combine(home, Folder);
        if (!Directory.Exists(folder)) return [];

        var read = new List<RuleProposal>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            if (Parse(path) is { } proposal) read.Add(proposal);
        }

        return [.. read.OrderByDescending(p => p.Proposed).ThenByDescending(p => p.Id, StringComparer.Ordinal)];
    }

    /// <summary>What the change does to the rules as they stand — or a refusal in the driver's words.</summary>
    /// <exception cref="DriverException">The rules could not take it.</exception>
    public static ChangeEffect Effect(PermissionFile file, ProposedChange change)
    {
        switch (change.Action)
        {
            case "default":
            {
                var shipped = PermissionRules.Defaults.FirstOrDefault(d => d.Id == change.Default)
                    ?? throw new DriverException(
                        $"no default `{change.Default}` — Daoris ships {string.Join(", ", PermissionRules.Defaults.Select(d => $"`{d.Id}`"))}.");
                if (change.On is not { } on) throw new DriverException($"say whether the default `{shipped.Id}` goes on or off.");
                var isOn = !file.DefaultsOff.Contains(shipped.Id, StringComparer.Ordinal);
                if (isOn == on) return ChangeEffect.Nothing;
                var allows = shipped.List == RuleList.Allow && shipped.Hook is null;
                return on == allows ? ChangeEffect.Widens : ChangeEffect.Narrows;
            }

            case "add":
            {
                var rule = Rule(change);
                var list = change.List ?? throw new DriverException("a rule goes in `allow`, `ask` or `deny`.");
                var sits = Sits(file, change, rule);
                if (sits == list) return ChangeEffect.Nothing;
                return list switch
                {
                    RuleList.Allow => ChangeEffect.Widens,
                    RuleList.Ask when sits == RuleList.Deny => ChangeEffect.Widens,
                    _ => ChangeEffect.Narrows,
                };
            }

            case "remove":
            {
                var rule = Rule(change);
                return Sits(file, change, rule) switch
                {
                    null => ChangeEffect.Nothing,
                    RuleList.Allow => ChangeEffect.Narrows,
                    _ => ChangeEffect.Widens,
                };
            }

            default:
                throw new DriverException($"a proposal is to `add`, `remove` or `default`, not `{change.Action}`.");
        }
    }

    /// <summary>The rules with the change made — by the same edits `daoris agent rules` makes.</summary>
    public static PermissionFile Apply(PermissionFile file, ProposedChange change) => change.Action switch
    {
        "add" => PermissionRules.Add(file, change.Scope, change.Name, change.List!.Value, Rule(change)),
        "remove" => PermissionRules.Remove(file, change.Scope, change.Name, Rule(change)),
        _ => PermissionRules.SwitchDefault(file, change.Default!, change.On!.Value),
    };

    /// <summary>
    /// The tick's part: every proposal not yet read is applied if it narrows, held if it widens, and
    /// settled either way. Answered as the lines the tick reports.
    /// </summary>
    public static IReadOnlyList<string> Settle(string home, DateTimeOffset at)
    {
        var said = new List<string>();
        foreach (var proposal in Load(home).Where(p => p.State == ProposalState.Proposed).Reverse())
        {
            var path = PathOf(home, proposal.Id);
            ChangeEffect effect;
            // 🔴 Read afresh for each proposal, and saved at once (REV3): one copy read before the loop
            // and saved after each narrowing wrote back whatever the person had changed meanwhile —
            // a deny they had just added could be undone by the driver applying something else.
            var rules = PermissionRules.Load(home);
            try
            {
                effect = Effect(rules, proposal.Change);
                if (effect == ChangeEffect.Narrows) rules = Apply(rules, proposal.Change);
            }
            catch (DriverException refused)
            {
                Mark(path, ProposalState.Refused, "the driver", refused.Message, at);
                said.Add($"rules  refused #{proposal.Id} from {Author(proposal)}: {refused.Message}");
                continue;
            }

            switch (effect)
            {
                case ChangeEffect.Narrows:
                    PermissionRules.Save(home, rules);
                    Mark(path, ProposalState.Applied, "the driver", "it narrows what agents may do, so it applied at once.", at);
                    said.Add($"rules  applied #{proposal.Id} from {Author(proposal)}: {Describe(proposal.Change)} — it narrows.");
                    break;
                case ChangeEffect.Widens:
                    Mark(path, ProposalState.Waiting, "the driver", "it widens what agents may do, so it waits for the person.", at);
                    said.Add($"rules  #{proposal.Id} waits for you: {Author(proposal)} proposes {Describe(proposal.Change)} — it widens what agents may do.");
                    break;
                default:
                    Mark(path, ProposalState.Unchanged, "the driver", "the rules already say it.", at);
                    break;
            }
        }

        return said;
    }

    /// <summary>
    /// The person's answer (D50: `daoris agent rules accept|decline`, and the screen's): a yes applies
    /// the change whatever it does, a no changes nothing. Only a proposal not yet settled is answered.
    /// </summary>
    /// <exception cref="DriverException">No such proposal, one already settled, or one the rules cannot take.</exception>
    public static RuleProposal Answer(string home, string id, bool accept, string? note, DateTimeOffset at)
    {
        var key = id.Trim().TrimStart('#');
        var path = PathOf(home, key);
        // An id names a file in the folder, and a path is not an id.
        var proposal = IdShape.IsMatch(key) && File.Exists(path) ? Parse(path) : null;
        if (proposal is null) throw new DriverException($"no proposal `#{key}` on this machine — `daoris agent rules proposals` lists them.");
        if (proposal.State is not (ProposalState.Proposed or ProposalState.Waiting))
        {
            throw new DriverException($"proposal `#{key}` was already {proposal.State.ToString().ToLowerInvariant()} — it is history now.");
        }

        if (accept)
        {
            PermissionRules.Save(home, Apply(PermissionRules.Load(home), proposal.Change));
        }

        var state = accept ? ProposalState.Accepted : ProposalState.Declined;
        Mark(path, state, "the person", string.IsNullOrWhiteSpace(note) ? null : note.Trim(), at);
        return proposal with { State = state, Settled = at, SettledBy = "the person", Note = note };
    }

    /// <summary>The change in a line — the rule is the harness's own words, quoted.</summary>
    public static string Describe(ProposedChange change)
    {
        var where = change.Scope switch
        {
            RuleScope.Machine => "every session on this machine",
            RuleScope.Workspace => $"workspace `{change.Name}`",
            _ => $"repository `{change.Name}`",
        };
        return change.Action switch
        {
            "add" => $"{change.List?.ToString().ToLowerInvariant()} `{change.Rule}` for {where}",
            "remove" => $"remove `{change.Rule}` from {where}",
            _ => $"switch the default `{change.Default}` {(change.On == true ? "on" : "off")}",
        };
    }

    /// <summary>Who proposed it, as a person reads it.</summary>
    public static string Author(RuleProposal proposal) => proposal.Session switch
    {
        null => "a session the driver did not start",
        var session => proposal.Ask is { } ask ? $"session {session} (ask #{ask})" : $"session {session}",
    };

    private static string Rule(ProposedChange change) =>
        change.Rule is { Length: > 0 } rule && PermissionRules.Refusal(rule) is null
            ? rule
            : throw new DriverException(PermissionRules.Refusal(change.Rule ?? "") ?? "a rule is needed.");

    /// <summary>Which list of the change's own scope already holds the rule, if any.</summary>
    private static RuleList? Sits(PermissionFile file, ProposedChange change, string rule)
    {
        var lists = change.Scope switch
        {
            RuleScope.Machine => file.Machine,
            RuleScope.Workspace => Named(file.Workspaces, change),
            _ => Named(file.Repositories, change),
        };
        if (lists.Allow.Contains(rule)) return RuleList.Allow;
        if (lists.Ask.Contains(rule)) return RuleList.Ask;
        if (lists.Deny.Contains(rule)) return RuleList.Deny;
        return null;
    }

    private static RuleLists Named(IReadOnlyDictionary<string, RuleLists> scopes, ProposedChange change) =>
        change.Name is { } name && !string.IsNullOrWhiteSpace(name)
            ? scopes.GetValueOrDefault(name.Trim()) ?? RuleLists.Empty
            : throw new DriverException($"a {(change.Scope == RuleScope.Workspace ? "workspace" : "repository")} scope names the one it reaches.");

    private static string PathOf(string home, string id) => Path.Combine(home, Folder, $"{id}.json");

    private static RuleProposal? Parse(string path)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root) return null;
            if (Text(root["id"]) is not { } id || root["change"] is not JsonObject change) return null;

            var by = root["by"] as JsonObject;
            var settled = root["settled"] as JsonObject;
            return new RuleProposal(
                id,
                DateTimeOffset.TryParse(Text(root["proposed"]), out var proposed) ? proposed : File.GetLastWriteTimeUtc(path),
                Text(by?["session"]),
                Text(by?["ask"]),
                new ProposedChange(
                    Text(change["action"]) ?? "",
                    Text(change["scope"]) switch
                    {
                        "workspace" => RuleScope.Workspace,
                        "repository" => RuleScope.Repository,
                        _ => RuleScope.Machine,
                    },
                    Text(change["name"]),
                    Text(change["list"]) switch
                    {
                        "allow" => RuleList.Allow,
                        "ask" => RuleList.Ask,
                        "deny" => RuleList.Deny,
                        _ => null,
                    },
                    Text(change["rule"]),
                    Text(change["default"]),
                    change["on"] is JsonValue on && on.TryGetValue<bool>(out var value) ? value : null),
                Text(root["why"]) ?? "",
                Enum.TryParse<ProposalState>(Text(root["state"]), ignoreCase: true, out var state) ? state : ProposalState.Proposed)
            {
                Settled = DateTimeOffset.TryParse(Text(settled?["at"]), out var at) ? at : null,
                SettledBy = Text(settled?["by"]),
                Note = Text(settled?["note"]),
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Rewrite the file's state and settling, keeping every other key the session wrote.</summary>
    private static void Mark(string path, ProposalState state, string by, string? note, DateTimeOffset at)
    {
        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [];
        root["state"] = state.ToString().ToLowerInvariant();
        root["settled"] = new JsonObject { ["at"] = at.ToString("O"), ["by"] = by, ["note"] = note };

        AtomicFile.WriteText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;
}
