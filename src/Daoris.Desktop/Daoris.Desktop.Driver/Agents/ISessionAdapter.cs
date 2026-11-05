using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// One harness adapter: how a session is spawned, and how the target reaches it. An adapter names a
/// harness, never a model (D24) — which model answers is that harness's own configuration in that
/// repository.
/// </summary>
public interface ISessionAdapter
{
    string Name { get; }

    /// <summary>
    /// Which door the driver holds this harness's session over (D53). Default <see cref="SessionWire.Pipe"/>,
    /// so an adapter that says nothing behaves exactly as every adapter did before the door existed —
    /// the same silence-preserves rule the toolchain and the session trees follow.
    /// </summary>
    SessionWire Wire => SessionWire.Pipe;

    /// <summary>
    /// The permission posture D37 sanctions, in <b>this harness's own vocabulary</b>, for adapters
    /// whose wire carries one (ACP3/D53).
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>The posture lives in three different places across three harnesses</b>, which is
    /// why this is a property of the adapter and not a constant in the session. Claude Code names it
    /// <c>acceptEdits</c> and Codex names it <c>agent</c> — both ACP modes, both observed rather than
    /// guessed (`docs/2026-09-22-acp3-probe-evidence.md`). dsh's <c>session/new</c> carries no modes
    /// at all, so its posture is an environment variable set at spawn instead.</para>
    ///
    /// <para><b>Null means this wire carries no posture</b>, and the session then asks for none —
    /// leaving the agent at its own default. That is the safe direction: every default observed is
    /// equal to or stricter than the one Daoris would set, so a forgotten posture stalls a session
    /// rather than widening it. It is never a licence to guess a neighbouring mode.</para>
    /// </remarks>
    string? AcpPosture => null;

    /// <summary>
    /// The harness's own mode that asks nothing, in <b>its own vocabulary</b>, for Ask Daoris's full access (HELPMODE1a1, D183
    /// point 1) and a driven session whose repository's permission prompts are unrestricted (AUTONOMY1b, D184 point 2): a
    /// <c>--permission-mode</c> on the native door (<see cref="ChatTarget.PermissionMode"/>, <see cref="DrivenAccess.PermissionMode"/>),
    /// a mode on the protocol door, which the session must set or fail (<see cref="PostureAsked.Required"/>).
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>Only by the person's own setting</b>: Ask Daoris's full access, or a repository's setup set to unrestricted. A
    /// repository's conversation and a driven session under auto keep D81's postures.</para>
    ///
    /// <para><b>Null means none is mapped</b>, and a full start on this harness is refused naming it, never run in a narrower mode
    /// under the full label: dsh, whose wire carries no modes, and every plugin-declared harness, until one is measured.</para>
    /// </remarks>
    string? FullPosture => null;

    /// <summary>
    /// The harness's own asking mode, in <b>its own vocabulary</b>, for a driven session whose repository's permission prompts are
    /// asking (AUTONOMY1b, D184 point 3): a <c>--permission-mode</c> on the native door, a mode on the protocol door, which the session
    /// must set or fail (<see cref="PostureAsked.Required"/>). On the protocol door each request is held for the person to allow once or
    /// refuse (AUTONOMY1b3a, <see cref="HeldPermissions"/>); over the native door nothing can hold one, so a tool its rules do not allow
    /// is refused there.
    /// </summary>
    /// <remarks>
    /// <b>Null means none is mapped</b>, and an asking start on this harness is refused naming it, never run in the harness's own,
    /// looser mode under the asking label: dsh and every plugin-declared harness, until one is measured.
    /// </remarks>
    string? AskingPosture => null;

    /// <summary>
    /// The harness's own mode in which it changes nothing in the folder it runs in, on the protocol door, for a harness that takes no rules
    /// Daoris composes (<see cref="TakesSettings"/>): what a brainstorm's pass asks, required, so a pass is never run in the harness's
    /// writing mode under the posture's name (BRAINSTORM1c, D176 points 1 and 2).
    /// </summary>
    /// <remarks>
    /// <b>Null means none is mapped</b>: a harness that takes rules is narrowed by them (<see cref="BrainstormPermissions.Shape"/>), and a
    /// harness that takes neither keeps the room's contents as its floor until its posture is measured.
    /// </remarks>
    string? ReadingPosture => null;

    /// <summary>
    /// The process that would be the session: spawned in the root, target delivered, output
    /// redirected so the driver can keep the transcript. Preparation only — the driver owns the
    /// process lifetime, because observing it is the driver's half of the contract (D46 §5).
    /// </summary>
    ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command);

    /// <summary>
    /// Whether this harness can be wired for turn-taking (D49 §3) — stdin carrying the person's
    /// messages, stdout streaming the harness's.
    /// </summary>
    /// <remarks>
    /// Declared honestly and opted into, never assumed: an adapter that has not been wired for a
    /// conversation says so, and asking for one errors naming what the harness is — the same rule
    /// that governs an unknown adapter name (D23). Default false, so a new adapter is non-interactive
    /// until someone has actually done the work.
    /// </remarks>
    bool Interactive => false;

    /// <summary>
    /// Hand a pipe-door session the MCP servers Daoris offers it (D65 §1f), written to a file under
    /// Daoris's home — the harness's own way of taking servers at spawn, where it has one. The
    /// protocol door carries them on the wire instead (ACP4) and never comes here.
    /// </summary>
    /// <remarks>
    /// Default: nothing — a harness with no such flag is handed nothing, and says nothing, which
    /// is the silence-preserves rule every adapter default follows. An adapter opts in when the
    /// flag is real and verified against the binary, like every other claim about somebody else's
    /// tool.
    /// </remarks>
    void HandServers(ProcessStartInfo info, string configFile) { }

    /// <summary>
    /// Whether this harness takes the permission rules Daoris composes for a session (PERM1, D72) — a
    /// settings file under the home, in the harness's own rule language.
    /// </summary>
    /// <remarks>
    /// Default false, and then nothing is composed or handed: a Claude Code rule means nothing to
    /// Codex's approval policy or dsh's permission mode, and translating one is a claim about somebody
    /// else's program, made when asked for and measured when made. Silence preserves how the adapter
    /// spawned before the rules existed.
    /// </remarks>
    bool TakesSettings => false;

    /// <summary>Hand a pipe-door spawn its rules file, as the harness's own flag. Default: nothing.</summary>
    void HandSettings(ProcessStartInfo info, string settingsFile) { }

    /// <summary>
    /// What a protocol-door session carries on <c>session/new</c> to take its rules file, in the adapter's
    /// own <c>_meta</c> vocabulary — or null for none, and then no <c>_meta</c> is sent at all.
    /// </summary>
    object? AcpSessionMeta(string settingsFile) => null;

    /// <summary>
    /// What a protocol-door session carries on <c>session/new</c> to take its rules file and the harness plugins a spawn composed
    /// for it (HELPPLUG1b, <see cref="SpawnSkills"/>). Default: the rules alone, so a harness with no plugin door is handed none.
    /// </summary>
    object? AcpSessionMeta(string settingsFile, IReadOnlyList<string> plugins) => AcpSessionMeta(settingsFile);

    /// <summary>
    /// Hand a pipe-door spawn the harness plugins composed for it (HELPPLUG1b, D170 point 4): folders under Daoris's home, each a
    /// plugin of the harness's own shape holding skills. Default: nothing, the silence-preserves rule; an adapter opts in when
    /// its door is measured against the binary.
    /// </summary>
    void HandPlugins(ProcessStartInfo info, IReadOnlyList<string> plugins) { }

    /// <summary>
    /// What a spawn of this harness carries so a session's tools load with its first request, rather than
    /// behind a search step the agent must take first (HELP5). Empty where the harness has no such switch,
    /// or nobody has read one in its binary.
    /// </summary>
    /// <remarks>
    /// <para>Every session Daoris runs carries it, a driven one's and a conversation's of every kind
    /// (CONTEXTONE1d, D230 point 3), through <see cref="Driver.Equip"/>: its connector's tools are the ones it
    /// is told to use, so they are in hand from the first request.</para>
    ///
    /// <para>Default empty, the silence-preserves rule every adapter default follows: an adapter opts in
    /// when the switch is read in the binary it runs, like every other claim about somebody else's tool.</para>
    /// </remarks>
    IReadOnlyDictionary<string, string> ToolsUpFront => ReadOnlyDictionary<string, string>.Empty;

    /// <summary>
    /// Whether this harness loads the tree's own skills itself (CONTEXTONE1c, D230 §3 E): where it does not, the standing block names
    /// their folders so the session reads them by path. Default false, so a harness nobody measured is told where they are.
    /// </summary>
    bool LoadsTreeSkills => false;

    /// <summary>
    /// The most bytes of the tree's instruction file (<c>AGENTS.md</c>) this harness reads itself, or null where it reads it whole or
    /// reads another (CONTEXTONE1c): past it, the standing block carries the repository's rule index. Default null.
    /// </summary>
    int? InstructionFileLimit => null;

    /// <summary>The process that would be a CHAT: the same spawn, with stdin open.</summary>
    ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
        throw new DriverException(
            $"the `{Name}` adapter cannot hold a conversation — it spawns an agent that takes its "
            + "target once and runs to completion. Chat with an adapter that declares `interactive`.");

    /// <summary>
    /// This harness AS A TOOL (D49 §4): where its binary is, how it reports its version, which
    /// environment variable names its configuration home, and how to run its own install, update and
    /// login flows.
    /// </summary>
    /// <remarks>
    /// Null — the default — is an adapter Daoris manages nothing about. It spawns exactly as it did
    /// before the toolchain existed, which is what lets a new adapter arrive without first answering
    /// questions about an installer it may not have. An adapter opts in when the answers are real:
    /// every field here is a claim about somebody else's tool, and a guessed one is worse than none.
    /// </remarks>
    HarnessToolchain? Toolchain => null;

    /// <summary>
    /// A reader of this harness's own structured stdout on the pipe door (D76 §1), new per session — or
    /// null when the door carries text, which is every adapter until its harness's wire is checked.
    /// </summary>
    /// <remarks>
    /// The protocol door reads its structure in <see cref="AcpSession"/>; this is the native door's twin,
    /// and it is the ADAPTER's, because a wire belongs to a harness (D23). Nothing here is parsed out of
    /// prose: a mapper reads a documented format, checked against the binary before it was written.
    /// </remarks>
    IStreamMapper? StructuredOutput() => null;

    /// <summary>
    /// A person's message as this harness reads it on a conversation's stdin — the text as it is, unless
    /// the harness takes structured input.
    /// </summary>
    string FrameMessage(string text) => text;

    /// <summary>
    /// The line that stops a conversation's turn on this harness's stdin and keeps its session (CONV4a) —
    /// null where the harness has none, and a turn there cannot be stopped short of ending the session.
    /// </summary>
    string? FrameInterrupt() => null;

    /// <summary>
    /// Whether this adapter's door can resume a harness conversation an answer continues (ANSWER1a, D131 §1). The protocol
    /// door's agent says on its own wire whether it resumes, so it is asked there; a native door resumes only where its
    /// adapter knows the harness's own resume (<see cref="PrepareResume"/>).
    /// </summary>
    bool Resumes => Wire == SessionWire.Acp;

    /// <summary>
    /// The native door's resume (ANSWER1a, D131 §1): the harness run on the conversation it kept, under the same posture a start has;
    /// its next turn goes on its input, written by the driver (AGENTLYNTAI1s-i, <see cref="NativePrompt"/>). Null where this adapter knows none.
    /// </summary>
    ProcessStartInfo? PrepareResume(SessionTarget target, IReadOnlyList<string>? command, string conversation) => null;
}
