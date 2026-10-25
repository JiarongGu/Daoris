using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Daoris.Driver.ChoiceArgs;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver review</c> (REVIEWENV1a, D154 point 2; FOUNDATION1 mod-iii): the review's own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the rule it edits (<see cref="ReviewRules"/>).
/// </summary>
public sealed class ReviewVerb : ChoiceVerb
{
    public override string Word => "review";

    /// <summary>
    /// <c>review</c> (REVIEWENV1a, D154 point 2): the person's words made one edit, judged by the rules' table, then, for an environment
    /// put, its procedure looked for in the checkouts the registry names (<see cref="ReviewLooks.Of"/>) before anything is written.
    /// </summary>
    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world)
    {
        var path = world.ConfigPath;
        void Say(string line) => world.Instance.Out.WriteLine(line);
        var workspace = FlagValue(args, "--workspace");
        var words = Operands(args, ["--workspace", "--kind", "--procedure", "--address", "--run", "--drop"]).Skip(1).ToList();
        string? name = workspace;
        if (name is null && words.Count > 0)
        {
            name = words[0];
            words.RemoveAt(0);
        }

        var environment = words.ElementAtOrDefault(0);
        var clear = args.Contains("--clear");
        var drop = FlagValue(args, "--drop");
        if (args.Contains("--required") && args.Contains("--not-required"))
        {
            throw new ChoiceWords("a rule is required or not — say `--required` or `--not-required`, not both.");
        }

        bool? required = args.Contains("--required") ? true : args.Contains("--not-required") ? false : null;
        if (string.IsNullOrEmpty(name) || (environment is null && !clear && drop is null && required is null))
        {
            throw new ChoiceWords("`driver review` needs <repository>|--workspace <name>, then <environment> --kind local|deployed --procedure <path> "
                + "[--address <url>] [--run \"<command>\"] [--required|--not-required], or none, --drop <environment>, "
                + "--required|--not-required, or --clear — e.g. `daoris driver review storefront dev --kind local --procedure README.md "
                + "--address http://localhost:4200 --required`.");
        }

        var kind = FlagValue(args, "--kind");
        var procedure = FlagValue(args, "--procedure");
        var address = FlagValue(args, "--address");
        var run = FlagValue(args, "--run");
        // `none` is the word for a repository with no review environment, unless it is given an environment's parts.
        var none = environment == "none" && kind is null && procedure is null && address is null && run is null;
        var payload = Scoped(workspace, name);
        if (clear) payload["clear"] = true;
        if (drop is not null) payload["drop"] = drop;
        if (none)
        {
            payload["none"] = true;
        }
        else if (environment is not null)
        {
            var put = new JsonObject { ["name"] = environment };
            if (kind is not null) put["kind"] = kind;
            if (procedure is not null) put["procedure"] = procedure;
            if (address is not null) put["address"] = address;
            if (run is not null) put["run"] = run;
            payload["put"] = put;
        }

        if (required is not null) payload["required"] = required;
        var edit = ReviewRules.EditOf(JsonSerializer.SerializeToElement(payload));
        DriverConfig after;
        try
        {
            after = ReviewRules.Apply(before, edit);
        }
        catch (DriverException refused)
        {
            throw new ChoiceWords(refused.Message);
        }

        var map = workspace is not null ? after.WorkspaceReviews : after.Reviews;
        var key = FindName(map.Keys, name) ?? FindName((workspace is not null ? before.WorkspaceReviews : before.Reviews).Keys, name) ?? name;
        var whose = workspace is not null ? $"repositories in the workspace `{key}` that set none of their own" : $"`{key}`";

        void Said()
        {
            var rule = map.GetValueOrDefault(key);
            if (clear)
            {
                Say(workspace is not null
                    ? $"daoris: repositories in the workspace `{key}` keep their own review rule, else none."
                    : $"daoris: `{key}` takes its workspace's review rule again, else none.");
            }
            else if (none)
            {
                Say($"daoris: `{key}` declares no review environment.");
            }
            else if (drop is not null)
            {
                var first = rule is null || rule.IsNone ? null : rule.Environments[0].Name;
                var was = (workspace is not null ? before.WorkspaceReviews : before.Reviews).GetValueOrDefault(key);
                var wasDefault = was is { IsNone: false } && was.Environments[0].Name == drop;
                Say($"daoris: {whose} no longer declare{(workspace is not null ? "" : "s")} `{drop}`"
                    + $"{(wasDefault && first is not null ? $"; `{first}` is {(workspace is not null ? "their" : "its")} default now" : "")}.");
            }
            else if (environment is null)
            {
                Say($"daoris: {(workspace is not null ? $"the workspace `{key}`'s" : $"`{key}`'s")} review rule is {(required == true ? "" : "not ")}required now.");
            }
            else
            {
                var first = rule is null || rule.IsNone ? null : rule.Environments[0].Name;
                Say($"daoris: {whose} declare{(workspace is not null ? "" : "s")} the review environment `{environment}`"
                    + (first == environment ? $", {(workspace is not null ? "their" : "its")} default" : $"; `{first}` stays {(workspace is not null ? "their" : "its")} default") + ".");
            }

            if (rule is not null)
            {
                foreach (var sentence in ReviewRules.Says(rule).Concat(ReviewRules.Gate(rule))) Say($"  {sentence}");
            }

            if (rule is { IsNone: false } && workspace is not null)
            {
                Say("  A repository with a rule of its own keeps it — `daoris driver review <repository> --clear` hands it back.");
            }
        }

        if (edit.Put is not { Procedure: { } looked })
        {
            return new("review", ChoiceRoutes.Review, payload, _ =>
            {
                StandingChoices.Write(path, config => ReviewRules.Apply(config, edit));
                return Task.FromResult<object?>(null);
            }, _ => null, _ =>
            {
                Said();
                Say($"  Written to {path}.");
                return 0;
            });
        }

        IEnumerable<string> Found(Reviewed reviewed)
        {
            if (reviewed.Unread is { } unread)
            {
                yield return $"Not checked: the registry could not be read — {unread}; a set-up step sits until its tree holds `{looked}`.";
                yield break;
            }

            if (workspace is null)
            {
                yield return reviewed.Unchecked
                    ? $"Not checked: `{key}` has no checkout on this machine; a set-up step sits until its tree holds `{looked}`."
                    : $"`{looked}` is in `{key}`'s checkout here.";
                yield break;
            }

            if (reviewed.Unchecked) yield return $"Not checked: no repository of the workspace `{key}` has a checkout on this machine.";
            else if (reviewed.Lacking.Count == 0) yield return $"Every repository of the workspace `{key}` with a checkout here holds `{looked}`.";
            foreach (var each in reviewed.Lacking) yield return ReviewRules.SitsUntil(each, looked);
        }

        // An environment put: its procedure is looked for in each checkout the rule reaches, before anything is written: by this verb
        // alone, and by the instance's own route while Daoris runs, whose answer says what it found (`reviewed`).
        return new("review", ChoiceRoutes.Review, payload, async ct =>
        {
            var registry = await world.Registry(ct).ConfigureAwait(false);
            Reviewed reviewed;
            if (registry.Checkouts is not { } checkouts)
            {
                reviewed = new([], true, registry.Unread ?? "it answered nothing");
            }
            else
            {
                var look = ReviewLooks.Of(checkouts, edit, looked);
                reviewed = new(look.Lacking, look.Unchecked, null);
            }

            StandingChoices.Write(path, config => ReviewRules.Apply(config, edit));
            return reviewed;
        }, answer =>
        {
            var said = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("reviewed", out var found) && found.ValueKind == JsonValueKind.Object
                ? found
                : default;
            IReadOnlyList<string> lacking = said.ValueKind == JsonValueKind.Object && said.TryGetProperty("lacking", out var listed) && listed.ValueKind == JsonValueKind.Array
                ? [.. listed.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                : [];
            return new Reviewed(lacking, said.ValueKind == JsonValueKind.Object && Json.Flag(said, "unchecked"), null);
        }, fields =>
        {
            Said();
            foreach (var note in Found((Reviewed)fields!)) Say($"  {note}");
            Say($"  Written to {path}.");
            return 0;
        });
    }

    /// <summary>The lines this verb's keys add to <c>list</c>.</summary>
    public override void Listed(DriverConfig choices, DriverChoicesWorld world)
    {
        void Say(string line) => world.Instance.Out.WriteLine(line);
        foreach (var (repository, rule) in choices.Reviews) Say($"  review     {repository}  {ReviewListed(rule)}");
        foreach (var (workspace, rule) in choices.WorkspaceReviews) Say($"  review     workspace {workspace}  {ReviewListed(rule)}  (for each repository there that sets none)");
        if (choices.Reviews.Count == 0 && choices.WorkspaceReviews.Count == 0)
        {
            Say("  review     none set — work is offered to land once its quest is done; `daoris driver review <repository> "
                + "<environment> --kind local|deployed --procedure <path>` declares one");
        }
    }

    /// <summary>What a review's procedure look found, as the words read it.</summary>
    private sealed record Reviewed(IReadOnlyList<string> Lacking, bool Unchecked, string? Unread);

    /// <summary>A rule in a line of <c>list</c>: each environment with its kind, address, procedure and command, then whether it waits.</summary>
    private static string ReviewListed(ReviewRule rule)
    {
        if (rule.IsNone) return "none here, whatever its workspace says";
        var environments = rule.Environments.Select(each =>
        {
            var parts = new List<string> { each.Kind };
            if (each.Address is { } address) parts.Add(address);
            parts.Add($"by {each.Procedure}");
            if (each.Run is { } run) parts.Add($"runs `{run}`");
            return $"{each.Name} ({string.Join(", ", parts)})";
        });
        return $"{string.Join("; ", environments)}, {(rule.Required ? "required before landing" : "on a task's asking")}";
    }
}
