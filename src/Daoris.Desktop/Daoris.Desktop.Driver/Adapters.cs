using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>A driver error a person can act on. Exit code 2 territory: tool error, not policy.</summary>
public sealed class DriverException(string message) : Exception(message);

/// <param name="QuestId">The quest the session exists to serve.</param>
/// <param name="Title">One line: what is wanted.</param>
/// <param name="Body">Why, and the evidence — the asker's words, verbatim.</param>
/// <param name="Asker">Who asked.</param>
/// <param name="Repository">Whose agent the session is.</param>
/// <param name="Root">The working tree it runs in.</param>
/// <param name="ServiceUrl">Where the service is, so the session can claim and close its own quest.</param>
public sealed record SessionTarget(
    string QuestId,
    string Title,
    string Body,
    string Asker,
    string Repository,
    string Root,
    string ServiceUrl);

/// <summary>
/// The claiming instruction (D46 §3), composed once and delivered per harness. Project-agnostic on
/// purpose: it travels to repositories that know nothing of this one's decision numbering, so it
/// speaks in the canon's names, never in D-numbers.
/// </summary>
public static class TargetPrompt
{
    public static string Compose(SessionTarget target) =>
        $"""
        You are the agent for `{target.Repository}`, working inside its own repository and nowhere else.

        Your target is quest `#{target.QuestId}`, asked by `{target.Asker}`:

        # {target.Title}

        {target.Body}

        First take the quest (respond to `#{target.QuestId}` with `take`), then do the work inside this
        repository under its own doctrine and gates, then close it: `done` when it has landed, or
        `decline` with the reason — the reason is the part the asker can act on. If the quest is already
        taken or closed, stand down and finish without changing anything.

        Never write outside this repository. Work another repository needs is a quest published to it,
        never an edit — that is the rule the whole arrangement rests on. Anything that cannot be taken
        back or that leaves the repository — a push, a publish, a release — is not yours to do; surface
        it and finish.
        """;
}

/// <summary>
/// One harness adapter: how a session is spawned, and how the target reaches it. An adapter names a
/// harness, never a model (D24) — which model answers is that harness's own configuration in that
/// repository.
/// </summary>
public interface ISessionAdapter
{
    string Name { get; }

    /// <summary>
    /// The process that would be the session: spawned in the root, target delivered, output
    /// redirected so the driver can keep the transcript. Preparation only — the driver owns the
    /// process lifetime, because observing it is the driver's half of the contract (D46 §5).
    /// </summary>
    ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command);
}

/// <summary>
/// The stub: runs whatever command the configuration names, in the repository root, with the target
/// in the environment. A test double with real mechanics — real spawn, real cwd, real delivery, real
/// exit — which is what lets the family rehearsal gate the whole loop with no model in it (D46 §8).
/// </summary>
public sealed class StubAdapter : ISessionAdapter
{
    public string Name => "stub";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        if (command is null || command.Count == 0)
        {
            throw new DriverException(
                "the stub adapter needs a command — name one in driver.json: "
                + """{ "commands": { "stub": ["node", "path/to/agent.mjs"] } }""");
        }

        var info = new ProcessStartInfo
        {
            FileName = command[0],
            WorkingDirectory = target.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in command.Skip(1)) info.ArgumentList.Add(argument);

        // The environment, not arguments: any script shape can read it without parsing, and nothing
        // quest-sized ever hits a shell's quoting rules.
        info.Environment["DAORIS_QUEST_ID"] = target.QuestId;
        info.Environment["DAORIS_QUEST_TITLE"] = target.Title;
        info.Environment["DAORIS_QUEST_BODY"] = target.Body;
        info.Environment["DAORIS_QUEST_ASKER"] = target.Asker;
        info.Environment["DAORIS_REPOSITORY"] = target.Repository;
        info.Environment["DAORIS_SERVICE_URL"] = target.ServiceUrl;
        info.Environment["DAORIS_TARGET"] = TargetPrompt.Compose(target);

        return info;
    }
}

/// <summary>
/// The adapters that exist, by name. D23 one layer up: `claude-code` first and supported, `codex`
/// second and explicit — and an unknown name is an error naming what exists, never a silent fallback,
/// because a driver that quietly spawned a different harness than the person configured is the same
/// failure as a repository that asked for one layout and received another.
/// </summary>
public sealed class AdapterSet(IReadOnlyDictionary<string, ISessionAdapter> adapters)
{
    public ISessionAdapter Resolve(string name)
    {
        if (adapters.TryGetValue(name, out var adapter)) return adapter;

        throw new DriverException(
            $"unknown adapter '{name}' — one of: {string.Join(", ", adapters.Keys.OrderBy(k => k, StringComparer.Ordinal))}. "
            + "An adapter is added deliberately, never guessed.");
    }

    public static AdapterSet Built() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        // The stub ships first so the mechanism is gate-verified before any real harness runs on it
        // (D46 §8). `claude-code` lands next as the supported adapter; `codex` after it, explicitly.
        ["stub"] = new StubAdapter(),
    });
}
