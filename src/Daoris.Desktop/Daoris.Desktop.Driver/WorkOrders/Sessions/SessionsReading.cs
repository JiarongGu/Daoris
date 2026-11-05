namespace Daoris.Driver;

/// <summary>The words after <c>daoris session</c>, read into what they ask.</summary>
internal static class SessionsReading
{
    /// <summary>The ask a helper's or a partner's verb read of the words, which those verbs hand on as the facet they read.</summary>
    private static SessionsAsk MadeAsk(string verb, IReadOnlyList<string> ids, string? text, string? agent, IReadOnlyList<string> paths) =>
        new(verb) { Ids = ids, Text = text, Agent = agent, Paths = paths };

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    internal static SessionsAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case ["stop", var id] when Id(id):
                return new SessionsAsk("stop") { Ids = [id] };
            case ["stop", var id, "--turn"] when Id(id):
                return new SessionsAsk("stop-turn") { Ids = [id] };
            case ["stop", ..]:
                problem = "`stop` takes one session's id, and `--turn` to stop only its turn so what waits goes next.";
                return null;
            case ["finish", var id] when Id(id):
                return new SessionsAsk("finish") { Ids = [id] };
            case ["finish", var id, "--note", var note] when Id(id) && note.Trim().Length > 0:
                return new SessionsAsk("finish") { Ids = [id], Note = note.Trim() };
            case ["finish", ..]:
                problem = "`finish` takes one session's id, and `--note` the words its record keeps.";
                return null;
            case ["decline", var id, "--reason", var reason] when Id(id) && reason.Trim().Length > 0:
                return new SessionsAsk("decline") { Ids = [id], Note = reason.Trim() };
            case ["decline", ..]:
                problem = "`decline` takes one session's id and `--reason`: the reason is the part whoever reads its record can act on.";
                return null;
            case ["archive", "--ended"]:
                return new SessionsAsk("archive") { Ended = true };
            case ["archive", "--ended", "--yes"]:
                return new SessionsAsk("archive") { Ended = true, Yes = true };
            case ["archive", _, ..] when args.Skip(1).All(Id):
                return new SessionsAsk("archive") { Ids = [.. args.Skip(1).Distinct(StringComparer.Ordinal)] };
            case ["archive", ..]:
                problem = "`archive` takes sessions' ids, or `--ended` and then `--yes`.";
                return null;
            case ["unarchive", _, ..] when args.Skip(1).All(Id):
                return new SessionsAsk("unarchive") { Ids = [.. args.Skip(1).Distinct(StringComparer.Ordinal)] };
            case ["unarchive", ..]:
                problem = "`unarchive` takes sessions' ids.";
                return null;
            case ["delete", var id] when Id(id):
                return new SessionsAsk("delete") { Ids = [id] };
            case ["delete", ..]:
                problem = "`delete` takes one session's id.";
                return null;
            case ["say", ..]:
                return ReadSay(args, out problem);
            case ["take-back", var id] when Id(id):
                return new SessionsAsk("take-back") { Ids = [id] };
            case ["take-back", var id, var word] when Id(id) && Id(word):
                return new SessionsAsk("take-back") { Ids = [id], Word = word.Trim() };
            case ["take-back", ..]:
                problem = "`take-back` takes one session's id, then the word to take back; with no word it lists those waiting. "
                    + "To put new words in its place, `reword <id> <word> \"…\"`.";
                return null;
            case ["reword", var id, var word, _, ..] when Id(id) && Id(word) && string.Join(' ', args.Skip(3)).Trim().Length > 0:
                return new SessionsAsk("reword") { Ids = [id], Word = word.Trim(), Text = string.Join(' ', args.Skip(3)).Trim() };
            case ["reword", ..]:
                problem = "`reword` takes one session's id, the word to reword, then the words to put in its place.";
                return null;
            case ["start-from", var id] when Id(id):
                return new SessionsAsk("start-from") { Ids = [id] };
            case ["start-from", ..]:
                problem = "`start-from` takes one session's id.";
                return null;
            // No agent named: the words a cooling account holds go on in a new session on the walk's pick.
            case ["hand-over", var id] when Id(id):
                return new SessionsAsk("hand-over") { Ids = [id] };
            case ["hand-over", var id, "--agent", var agent] when Id(id) && Id(agent):
                return new SessionsAsk("hand-over") { Ids = [id], Agent = agent };
            case ["hand-over", var id, "--agent", var agent, "--account", var account] when Id(id) && Id(agent) && Id(account):
                return new SessionsAsk("hand-over") { Ids = [id], Agent = agent, Account = account };
            // The person's yes or no to the agent's own ask to hand its session over.
            case ["hand-over", var id, "--approve"] when Id(id):
                return new SessionsAsk("hand-over-approve") { Ids = [id] };
            case ["hand-over", var id, "--decline"] when Id(id):
                return new SessionsAsk("hand-over-decline") { Ids = [id] };
            case ["hand-over", ..]:
                problem = "`hand-over` takes one session's id, with `--agent <adapter>` to name the agent and, where one is wanted, `--account <name>`; "
                    + "or `--approve` or `--decline` for its agent's own ask to hand it over.";
                return null;
            case ["helper", ..]:
                return SessionsHelperCommand.Read(args, out problem, MadeAsk);
            case ["supervise", ..]:
                return SessionsSuperviseCommand.Read(args, out problem, (verb, ids, draft) => new SessionsAsk(verb) { Ids = ids, Supervised = draft });
            case ["partner", ..]:
                return SessionsPartnerCommand.Read(args, out problem, MadeAsk);
            case ["at-work", var id] when Id(id):
                return new SessionsAsk("at-work") { Ids = [id] };
            case ["at-work", var id, "--json"] when Id(id):
                return new SessionsAsk("at-work") { Ids = [id], Json = true };
            case ["at-work", ..]:
                problem = "`at-work` takes one session's id, and `--json` for the answer whole.";
                return null;
            // One quest's handback, by the quest's id or its session's.
            case ["handback", var id] when Id(id):
                return new SessionsAsk("handback") { Ids = [id.Trim().TrimStart('#')] };
            case ["handback", var id, "--json"] when Id(id):
                return new SessionsAsk("handback") { Ids = [id.Trim().TrimStart('#')], Json = true };
            case ["handback", ..]:
                problem = "`handback` takes one session's or quest's id, and `--json` for the service's answer as it gave it.";
                return null;
        }

        var ask = new SessionsAsk("list");
        for (var at = 0; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--json":
                    ask = ask with { Json = true };
                    break;
                case "--group" when at + 1 < args.Count && SessionGroup.Order.Contains(args[at + 1]):
                    ask = ask with { Group = args[++at] };
                    break;
                case "--group":
                    problem = $"`--group` takes one of {string.Join(", ", SessionGroup.Order)}.";
                    return null;
                case "--repository" when at + 1 < args.Count && Id(args[at + 1]):
                    ask = ask with { Repository = args[++at] };
                    break;
                case "--repository":
                    problem = "`--repository` takes a repository's name.";
                    return null;
                default:
                    problem = $"`{args[at]}` is not a word `sessions` takes.";
                    return null;
            }
        }

        return ask;
    }

    /// <summary>
    /// <c>say &lt;id&gt; "…" [--file &lt;path&gt;]… [--level ordinary|urgent|now]</c>: the words after the id joined by a space,
    /// each path after its flag, and the level after its own.
    /// </summary>
    private static SessionsAsk? ReadSay(IReadOnlyList<string> args, out string? problem)
    {
        problem = "`say` takes one session's id, then your words, `--file <path>` for each file you give with them, and "
            + "`--level ordinary|urgent|now` for when it reads them.";
        if (args.Count < 2 || !Id(args[1])) return null;

        var words = new List<string>();
        var files = new List<string>();
        string? level = null;
        for (var at = 2; at < args.Count; at++)
        {
            if (args[at] == "--level")
            {
                if (at + 1 >= args.Count || MessageLevels.Read(args[at + 1]) is null)
                {
                    problem = "`--level` is when it reads your words: ordinary, urgent or now.";
                    return null;
                }

                level = args[++at];
                continue;
            }

            if (args[at] != "--file")
            {
                words.Add(args[at]);
                continue;
            }

            if (at + 1 >= args.Count || args[at + 1].Trim().Length == 0) return null;
            files.Add(args[++at]);
        }

        var text = string.Join(' ', words).Trim();
        if (text.Length == 0) return null;

        problem = null;
        return new SessionsAsk("say") { Ids = [args[1]], Text = text, Files = files, Level = level };
    }

    /// <summary>A word that is an id: something, and not a flag.</summary>
    internal static bool Id(string word) => word.Trim().Length > 0 && !word.StartsWith('-');
}
