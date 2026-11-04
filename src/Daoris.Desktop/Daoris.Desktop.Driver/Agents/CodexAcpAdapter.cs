using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// <b>Codex over the protocol door</b> (ACP3/D53, closing HARNESS2): the ACP project's Codex adapter,
/// which drives the machine's `codex` exactly as the Claude one drives `claude`.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The posture is `agent`, and it was established rather than guessed</b> — the one thing
/// HARNESS2 said the ACP route does not settle for free. Read from codex-acp@1.12.0's own bundle, the
/// wire offers three modes: <c>read-only</c> (asks for everything, so a driven session stalls on its
/// first edit), <c>agent</c> (workspace-write sandbox, approvals on request) and
/// <c>agent-full-access</c> (<c>dangerFullAccess</c> with approvals <c>never</c>, which is precisely
/// what D37 forbids). There is no third reading.</para>
///
/// <para>🔴 <b>It is stricter than `acceptEdits`, not equivalent.</b> `agent` runs with
/// <c>networkAccess: false</c>, which Claude Code's posture does not — and a session that cannot
/// reach the network fails in ways that look like something else entirely. Recorded here because the
/// surprise belongs next to the constant.</para>
///
/// <para>🔴 <b>`CODEX_HOME` must already exist</b>, where the Claude adapter creates its own config
/// directory. Pointed at a path that is not there, this adapter exits 1 before <c>initialize</c>
/// completes, naming the directory — which is why the toolchain carries <c>ProfileMustExist</c>.</para>
///
/// <para><b>No model is named</b> (D24), and the account belongs to `codex`: this adapter runs the
/// harness and reads the home the harness logged into, so it has no login flow of its own.</para>
/// </remarks>
public sealed class CodexAcpAdapter : ISessionAdapter
{
    public string Name => "codex-acp";

    public SessionWire Wire => SessionWire.Acp;

    public bool Interactive => true;

    /// <summary>
    /// D37 in Codex's vocabulary. Never <c>agent-full-access</c>, which is approvals off, but by the person's own setting: Ask Daoris's
    /// full access, a repository's unrestricted permission prompts.
    /// </summary>
    public string? AcpPosture => "agent";

    /// <summary>
    /// <c>agent-full-access</c>, Codex's full access: the <c>dangerFullAccess</c> sandbox with approvals <c>never</c>, offered on
    /// every session (codex-acp 2.1.1's bundle, <c>AgentMode.AgentFullAccess</c>), and only by the person's own setting: Ask Daoris's
    /// full access (HELPMODE1a1, D183), a repository's unrestricted permission prompts (AUTONOMY1b, D184). Codex takes no rules file,
    /// so the person's denies do not reach it in any mode.
    /// </summary>
    public string? FullPosture => "agent-full-access";

    /// <summary>
    /// <c>workspace-write</c>, Codex's asking mode for a repository's asking permission prompts (CODEXPERM1, D184): approvals on request
    /// reviewed by the user, the sandbox bounded to the session's own tree, so an edit inside it is done unasked and anything past it
    /// (a write beyond the tree, the network) waits for the person (AUTONOMY1b3a). Not <c>read-only</c>, which asks for every edit inside
    /// the tree too (codex-acp 2.1.1's bundle, <c>AgentMode</c>). An agent that does not offer it fails the open.
    /// </summary>
    public string? AskingPosture => "workspace-write";

    /// <summary>
    /// <c>read-only</c> too, for a brainstorm's pass (BRAINSTORM1c, D176 point 2): Codex takes no rules file, so the mode is the whole of its
    /// posture, its sandbox read-only so nothing is written in any folder, required so an agent that does not offer it fails the pass's open
    /// rather than running it in <c>agent</c>'s workspace-write sandbox. Every request it still sends is refused by the driver, as every
    /// driven session's is. Read from codex-acp's bundle (the ACP3 probe's §2), not yet measured on a pass (D176's BRAINSTORM1c note).
    /// </summary>
    public string? ReadingPosture => "read-only";

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(target, resolved[0], resolved.Skip(1));
    }

    public HarnessToolchain? Toolchain => new(
        Product: "Codex",
        Mark: "Cx",
        Maker: "OpenAI",
        // 🔴 The BINARY is `codex-acp` — the adapter's own bin, not this adapter's Daoris name and
        // not `codex`. Verified against the installed package's `bin` map, the same check that
        // caught the `claude-agent-acp` guess.
        Binary: ["codex-acp"],
        VersionArguments: ["--version"],
        ProfileVariable: "CODEX_HOME",
        Install: ["npm", "install", "-g", "@agentclientprotocol/codex-acp"],
        Package: "@agentclientprotocol/codex-acp",
        // No login of its own: it runs `codex` and reads the home `codex` logged into, whose sign-in and status question
        // are declared for `codex` itself (`CodexAccounts`, CODEXACCT1), since no `codex` door exists to carry them.
        AccountOf: CodexAccounts.Agent,
        ProfileMustExist: true,
        // What Codex says when an account's limit refuses a turn (TOOL4k), read in its source: declared here, since
        // no `codex` adapter exists to own it, and reaching this door only as the error's data (ACPDATA1).
        Limits: CodexLimits.Words,
        // How an account's windows are asked, since this door forwards none of them (CODEXUSE1): Codex's own app server under
        // the account's home, declared here for the same reason as `Limits`.
        Usage: CodexUsage.Question,
        // Its typed failure categories, marked only for a client that asks for them (CODEXSTATUS1).
        ErrorWords: CodexErrors.Words);

    /// <summary>
    /// Codex reads the tree's <c>AGENTS.md</c> up to its <c>project_doc_max_bytes</c>, 32 KiB unless set (what <c>check</c> reports past,
    /// <c>drift.ts</c>), and loads no skills of the tree's (CONTEXTONE1c).
    /// </summary>
    public int? InstructionFileLimit => 32_768;

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : Toolchain!.Binary;
}
