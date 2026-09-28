using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One of Ask Daoris's proposals, as the connector wrote it (HELP1c, D89).</summary>
/// <param name="Kind">`setting` or `ask`.</param>
/// <param name="Door">The `daoris driver` verb for a setting; `ask` for an ask.</param>
/// <param name="Session">The conversation that proposed it.</param>
/// <param name="State">`proposed`, then `applied`, `dismissed` or `refused`.</param>
public sealed record HelpProposal(
    string Id, string Kind, string Door, string? Target, string? Workspace, string? Value, string? Sentence,
    string Why, string? Session, string State);

/// <summary>What the machine holds that a proposal is judged against: names only.</summary>
public sealed record HelpMachineFacts(
    IReadOnlyCollection<string> Repositories, IReadOnlyCollection<string> Workspaces, IReadOnlyCollection<string> Agents);

/// <summary>
/// A proposal judged: what it changes, the terminal command that does the same (D50), and the edit to
/// make — or the route's refusal, and nothing to make.
/// </summary>
/// <param name="Apply">The edit to the driver's file, for a setting the route takes; null for an ask or a refusal.</param>
public sealed record HelpPlan(string? Refusal, string Describe, string Terminal, Func<DriverConfig, DriverConfig>? Apply);

/// <summary>
/// Ask Daoris's proposals, the driver's half (HELP1c, D89): the files the connector's
/// <c>setting_propose</c> and <c>ask_propose</c> wrote under the home, judged with the route's own code
/// and settled by the person's press.
/// </summary>
/// <remarks>
/// <para><b>Judged by the route, not by the door that wrote it.</b> The service checks a proposal's
/// shape; whether the route takes it — a repository registered here, a line git accepts, a pattern
/// naming one branch per session, an agent this machine has — is the config's own edits and this
/// machine's names, run here. A proposal the route would refuse is never shown to the person.</para>
///
/// <para><b>THE FILE is the contract</b>, the twin of the service's <c>HelpProposalBox</c>: they share no
/// code, and each side's tests hold the same shape.</para>
/// </remarks>
public static class HelpProposals
{
    public static string FolderOf(string home) => Path.Combine(home, "help", "proposals");

    /// <summary>The proposals one conversation made that wait for the person, oldest first. A file that does not read is skipped.</summary>
    public static IReadOnlyList<HelpProposal> Pending(string home, string session)
    {
        var folder = FolderOf(home);
        if (!Directory.Exists(folder)) return [];

        var found = new List<(DateTimeOffset At, HelpProposal Proposal)>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var proposal = new HelpProposal(
                    Text(root, "id") ?? Path.GetFileNameWithoutExtension(path), Text(root, "kind") ?? "", Text(root, "door") ?? "",
                    Text(root, "target"), Text(root, "workspace"), Text(root, "value"), Text(root, "sentence"),
                    Text(root, "why") ?? "", root.TryGetProperty("by", out var by) ? Text(by, "session") : null,
                    Text(root, "state") ?? "");
                if (proposal.State != "proposed" || !string.Equals(proposal.Session, session, StringComparison.OrdinalIgnoreCase)) continue;
                var at = DateTimeOffset.TryParse(Text(root, "proposed"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var when) ? when : DateTimeOffset.MinValue;
                found.Add((at, proposal));
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                // Half-written or not a proposal: nothing a person could press on.
            }
        }

        return [.. found.OrderBy(entry => entry.At).Select(entry => entry.Proposal)];
    }

    /// <summary>Settle one: its state and why, written beside and renamed, every other field kept.</summary>
    public static void Settle(string home, string id, string state, string? note)
    {
        var path = Path.Combine(FolderOf(home), $"{id}.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        node["state"] = state;
        node["note"] = note;
        AtomicFile.WriteText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>One proposal, found by its id, whatever its state — what Apply and Not now read.</summary>
    public static HelpProposal? Find(string home, string id)
    {
        var path = Path.Combine(FolderOf(home), $"{Path.GetFileName(id)}.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        return new HelpProposal(
            Text(root, "id") ?? id, Text(root, "kind") ?? "", Text(root, "door") ?? "", Text(root, "target"),
            Text(root, "workspace"), Text(root, "value"), Text(root, "sentence"), Text(root, "why") ?? "",
            root.TryGetProperty("by", out var by) ? Text(by, "session") : null, Text(root, "state") ?? "");
    }

    /// <summary>Judge a proposal with the route's own code, against the machine as it stands.</summary>
    public static HelpPlan Plan(HelpProposal proposal, DriverConfig config, HelpMachineFacts facts)
    {
        if (proposal.Kind == "ask") return Ask(proposal, facts);

        var target = proposal.Target?.Trim();
        var workspace = proposal.Workspace?.Trim();
        var value = proposal.Value?.Trim() ?? "";
        HelpPlan Refused(string why, string describe, string terminal) => new(why, describe, terminal, null);

        // The names the route itself does not check, and a helper can invent: a repository, a circle, an agent.
        if (target is { Length: > 0 } && !facts.Repositories.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"`{target}` is not registered on this machine — use a repository's name as Projects lists it.", "", "");
        }

        if (workspace is { Length: > 0 } && !facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return Refused($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", "", "");
        }

        var scope = workspace is { Length: > 0 } ? $"--workspace {workspace}" : target ?? "";
        var whose = workspace is { Length: > 0 } ? $"workspace `{workspace}`" : $"`{target}`";
        (string Describe, string Terminal, Func<DriverConfig, DriverConfig> Edit) planned;
        switch (proposal.Door)
        {
            case "drive":
                planned = ($"Drive `{target}`: a quest addressed to it starts a session on this machine.", $"daoris driver drive {target}", c => c.WithDrivable(target!, true));
                break;
            case "undrive":
                planned = ($"Stop driving `{target}`.", $"daoris driver undrive {target}", c => c.WithDrivable(target!, false));
                break;
            case "hold":
                planned = ($"Hold `{target}`: nothing new starts there until it is resumed.", $"daoris driver hold {target}", c => c.WithHold(target!, true));
                break;
            case "resume":
                planned = ($"Resume `{target}`.", $"daoris driver resume {target}", c => c.WithHold(target!, false));
                break;
            case "trees":
                planned = value == "on"
                    ? ($"Sessions in `{target}` open their own tree.", $"daoris driver trees {target} on", c => c.WithTrees(target!, true))
                    : ($"Sessions in `{target}` work in its checkout.", $"daoris driver trees {target} off", c => c.WithTrees(target!, false));
                break;
            case "line":
            {
                var branch = value == "--clear" ? null : value;
                planned = (branch is null ? $"Clear {whose}'s line." : $"Set {whose}'s line to `{branch}`.",
                    $"daoris driver line {scope} {value}",
                    c => workspace is { Length: > 0 } ? c.WithWorkspaceLine(workspace, branch) : c.WithLine(target!, branch));
                break;
            }
            case "landing":
            {
                var tidy = value.EndsWith(" --tidy", StringComparison.Ordinal);
                var bare = tidy ? value[..^" --tidy".Length].Trim() : value;
                LandingRule? rule = bare switch
                {
                    "--clear" => null,
                    "merge" => new LandingRule("merge", null, tidy),
                    _ when bare.StartsWith("branch ", StringComparison.Ordinal) => new LandingRule("branch", bare["branch ".Length..].Trim(), tidy),
                    _ => new LandingRule(bare, null, tidy),
                };
                var lands = rule is null ? $"Clear {whose}'s landing rule."
                    : rule.Form == "branch" ? $"Land {whose}'s accepted work on a branch `{rule.Pattern}`{(tidy ? ", its tree removed once landed" : "")}."
                    : $"Land {whose}'s accepted work merged into its line{(tidy ? ", its tree removed once landed" : "")}.";
                planned = (lands, $"daoris driver landing {scope} {value}",
                    c => workspace is { Length: > 0 } ? c.WithWorkspaceLanding(workspace, rule) : c.WithLanding(target!, rule));
                break;
            }
            case "intake" or "helper":
            {
                var agent = value == "off" ? null : value;
                if (agent is not null && !facts.Agents.Contains(agent, StringComparer.Ordinal))
                {
                    return Refused($"there is no agent `{agent}` on this machine — one of {Names(facts.Agents)}.", "", "");
                }

                var intake = proposal.Door == "intake";
                planned = (intake
                        ? agent is null ? "Stop answering asks with an agent: the declarations alone propose." : $"Answer asks with `{agent}`."
                        : agent is null ? "Turn Ask Daoris's agent off: it offers its starters only." : $"Run Ask Daoris on `{agent}`.",
                    $"daoris driver {proposal.Door} {value}",
                    c => intake ? c.WithIntake(agent) : c.WithHelper(agent));
                break;
            }
            case "strikes" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var strikes):
                planned = (strikes == 0 ? "Never park a quest for its failed sessions." : $"Park a quest after {strikes} failed sessions.",
                    $"daoris driver strikes {strikes}", c => c.WithStrikes(strikes));
                break;
            case "timeout" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes >= 1:
                planned = ($"Let one session run up to {minutes} minutes.", $"daoris driver timeout {minutes}", c => c with { TimeoutMinutes = minutes });
                break;
            case "notify" when value is "on" or "off":
                planned = (value == "on" ? "Say so when a session parks, or ends without being asked." : "Stop saying so when a session parks.",
                    $"daoris driver notify {value}", c => c.WithNotify(value == "on"));
                break;
            default:
                return Refused($"`{proposal.Door} {value}` is not a change the driver makes.", "", "");
        }

        // The route's own edits judge the rest — a branch git would not take, a pattern that is not one
        // branch per session — in their own words.
        try
        {
            planned.Edit(config);
        }
        catch (DriverException refused)
        {
            return Refused(refused.Message, planned.Describe, planned.Terminal);
        }

        return new HelpPlan(null, planned.Describe, planned.Terminal, planned.Edit);
    }

    private static HelpPlan Ask(HelpProposal proposal, HelpMachineFacts facts)
    {
        var workspace = proposal.Workspace?.Trim() ?? "";
        var sentence = proposal.Sentence?.Trim() ?? "";
        var terminal = $"daoris-driver ask --workspace {workspace} \"{sentence.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        var describe = $"Ask at workspace `{workspace}`: “{sentence}”";
        if (sentence.Length == 0) return new HelpPlan("an ask needs its words.", describe, terminal, null);
        if (!facts.Workspaces.Contains(workspace, StringComparer.OrdinalIgnoreCase))
        {
            return new HelpPlan($"there is no workspace `{workspace}` on this machine — one of {Names(facts.Workspaces)}.", describe, terminal, null);
        }

        return new HelpPlan(null, describe, terminal, null);
    }

    private static string Names(IEnumerable<string> names) =>
        string.Join(", ", names.OrderBy(name => name, StringComparer.Ordinal).Select(name => $"`{name}`"));

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
