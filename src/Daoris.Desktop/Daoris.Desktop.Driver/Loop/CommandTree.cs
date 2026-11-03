namespace Daoris.Driver;

/// <summary>What the tree made of the words: which program runs them, the usage, or a refusal.</summary>
public enum TreeRouteKind
{
    /// <summary>The desktop's program runs them, as <see cref="TreeRoute.Words"/>: the host's own words for the group's command.</summary>
    Run,

    /// <summary>A doctrine command, the CLI's: handed to it whole.</summary>
    Doctrine,

    /// <summary>The usage, the tree's or a group's (<see cref="TreeRoute.Group"/>); exit 0 where it was asked for, else 2.</summary>
    Usage,

    /// <summary>A word handed back: refused, exit 2, saying <see cref="TreeRoute.Said"/>; nothing is done.</summary>
    Refused,
}

/// <summary>The words routed: <paramref name="Words"/> are what the host runs, or the doctrine command's words.</summary>
public sealed record TreeRoute(TreeRouteKind Kind, IReadOnlyList<string> Words, string? Said = null, string? Group = null, bool Asked = false);

/// <summary>
/// The one command tree's router (ONEWAY1ai-i, D228; the oneway design §11), by <see cref="CommandWords"/>' table: the host routes a
/// person's words once, at its entry, into the words its commands read.
/// </summary>
public static class CommandTree
{
    /// <summary>
    /// A person's words at <c>daoris</c>, routed once: the host's own words for a group's command, the CLI's for a doctrine command, the
    /// usage, or a refusal. <paramref name="handedBy"/> is the program that handed them here, if one did.
    /// </summary>
    public static TreeRoute Route(IReadOnlyList<string> args, string? handedBy = null)
    {
        if (args.Count == 0) return new(TreeRouteKind.Usage, args);
        var first = args[0];
        if (first is "--help" or "-h") return new(TreeRouteKind.Usage, args, Asked: true);
        if (first == "--version" || CommandWords.Doctrine.Contains(first, StringComparer.Ordinal))
        {
            // A word handed once is never handed back (the design §11): the CLI handed it, so it is no doctrine command of the CLI's.
            return handedBy == "cli"
                ? new(TreeRouteKind.Refused, args, $"`{first}` is no command the CLI that handed it here has; `daoris help` names each.")
                : new(TreeRouteKind.Doctrine, args);
        }

        if (first == "help")
        {
            if (args.Count == 1) return new(TreeRouteKind.Usage, args, Asked: true);
            return CommandWords.Group(args[1]) is { } asked
                ? new(TreeRouteKind.Usage, args, Group: asked.Name, Asked: true)
                : new(TreeRouteKind.Usage, args, $"no group `{args[1]}`.");
        }

        if (CommandWords.Group(first) is not { } group) return new(TreeRouteKind.Usage, args, $"no command `{first}`.");

        var rest = args.Skip(1).ToList();
        var verb = rest.Count > 0 ? rest[0] : null;
        if (!group.Whole && group.Takes is null && (verb is null || !group.Verbs.Contains(verb, StringComparer.Ordinal)))
        {
            return new(TreeRouteKind.Usage, args, verb is null ? null : $"no `{verb}` in `daoris {group.Name}`.", group.Name);
        }

        var acts = CommandWords.AskActs;
        return group.Name switch
        {
            // The ask command's own words for its acts are no words of the tree's.
            "ask" when rest.FirstOrDefault(word => acts.Values.Contains(word, StringComparer.Ordinal)) is { } act =>
                new(TreeRouteKind.Usage, args, $"no `{act}` in `daoris ask`.", group.Name),
            "brainstorm" => Brainstorm(args, rest),
            _ => new(TreeRouteKind.Run, Inward(group.Name, rest), Group: group.Name),
        };
    }

    /// <summary>The words the host's commands read for <paramref name="group"/>'s <paramref name="rest"/>.</summary>
    private static string[] Inward(string group, List<string> rest)
    {
        var verb = rest.Count > 0 ? rest[0] : null;
        var after = rest.Skip(1);
        return group switch
        {
            "agent" when verb == "account" => ["agent", "profile", .. after],
            "ask" when verb is not null && CommandWords.AskActs.TryGetValue(verb, out var act) => ["ask", act, .. after],
            "ask-daoris" => ["help", .. rest],
            "gate" => [verb == "run" ? "run-heavy" : "verify", .. after],
            "place" when verb is "access" or "heavy" or "checks" => [verb, .. after],
            "place" when verb is not null && CommandWords.Group("place")!.Verbs.Contains(verb, StringComparer.Ordinal) => ["driver", .. rest],
            "plugin" when verb is "list" or "add" or "update" or "remove" or "enable" or "disable" => ["plugin", .. rest],
            "plugin" => ["plugins", .. rest],
            "press" => [verb!, .. after],
            "quest" when verb == "retry" => ["driver", "retry", .. after],
            "remote" when verb == "sync" => ["sync", .. after],
            "repository" when verb == "register" => ["register", .. after],
            "role" when verb == "brief" => ["roles", "brief", .. after],
            "role" => ["driver", "role", .. rest],
            "session" when verb == "list" => ["sessions", .. after],
            "session" when verb == "opinion" => [verb, .. after],
            "session" => ["sessions", .. rest],
            "settings" => ["driver", .. rest],
            "tree" => ["trees", .. rest],
            "workflow" when verb is "run" or "keep" or "skip" or "stop" or "changes" => ["workflow", .. rest],
            "workflow" => ["driver", "workflow", .. rest],
            _ => [group, .. rest],
        };
    }

    /// <summary>A brainstorm, or a meeting where <c>--kind meeting</c> names one (the oneway design §6 X2): the meeting's own words.</summary>
    private static TreeRoute Brainstorm(IReadOnlyList<string> args, List<string> rest)
    {
        var at = rest.IndexOf("--kind");
        if (at < 0) return new(TreeRouteKind.Run, ["brainstorm", .. rest], Group: "brainstorm");
        var kind = at + 1 < rest.Count ? rest[at + 1] : null;
        if (kind is not ("meeting" or "brainstorm"))
        {
            return new(TreeRouteKind.Usage, args, "`--kind` is brainstorm (the default) or meeting.", "brainstorm");
        }

        rest.RemoveRange(at, 2);
        if (kind == "brainstorm") return new(TreeRouteKind.Run, ["brainstorm", .. rest], Group: "brainstorm");
        return rest.Count > 0 && rest[0] is "show" or "send" or "stop" or "clear" or "list"
            ? new(TreeRouteKind.Run, ["meeting", .. rest], Group: "brainstorm")
            : new(TreeRouteKind.Run, ["meeting", "ask", .. rest], Group: "brainstorm");
    }
}
