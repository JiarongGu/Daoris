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

/// <summary>What every adapter shares: the process shell, and the target riding in the environment.</summary>
internal static class Spawning
{
    /// <summary>
    /// A redirected process in the repository root. The environment, not arguments, carries the
    /// target's pieces: any script shape can read it without parsing, and nothing quest-sized ever
    /// hits a shell's quoting rules.
    /// </summary>
    public static ProcessStartInfo InRoot(SessionTarget target, string fileName, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = target.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);

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

        return Spawning.InRoot(target, command[0], command.Skip(1));
    }
}

/// <summary>
/// The supported harness (D46 §5): Claude Code in its non-interactive mode, in the repository root,
/// with the composed target as the prompt.
/// </summary>
/// <remarks>
/// <para><b>The permission mapping is the D37 boundary, stated in the harness's own vocabulary.</b>
/// Edits auto-accept — reversible, in-repository, the automated middle — and every other tool runs
/// under the repository's own checked-in permission configuration, exactly as an interactive session
/// there would. Nothing at the outward boundary is auto-approved by the driver, ever: a session that
/// cannot proceed ends, and the observation concludes it honestly.</para>
///
/// <para><b>The connector is the session's voice.</b> An adopted repository's own `.mcp.json` wires
/// the knowledge tools the prompt tells the session to claim and close its quest with — that wiring
/// is the connector's job at adoption, not something the driver may reach in and write (that would be
/// the very edit this whole system exists to prevent).</para>
///
/// <para><b>No model is ever named</b> (D24): which model answers is the harness's own configuration
/// in that repository. The default command is `claude` off the PATH; a machine whose shim needs a
/// path names it in `commands` like any other adapter command.</para>
/// </remarks>
public sealed class ClaudeCodeAdapter : ISessionAdapter
{
    public string Name => "claude-code";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = command is { Count: > 0 } ? command : ["claude"];
        var arguments = resolved.Skip(1)
            .Concat(["-p", TargetPrompt.Compose(target), "--permission-mode", "acceptEdits"]);

        return Spawning.InRoot(target, resolved[0], arguments);
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
        // The stub gate-verifies the mechanism with no model in it (D46 §8); `claude-code` is the
        // supported harness on top of that proven loop. `codex` arrives explicitly, later — and until
        // it does, asking for it errors naming these two, which is the honest answer.
        ["stub"] = new StubAdapter(),
        ["claude-code"] = new ClaudeCodeAdapter(),
    });
}
