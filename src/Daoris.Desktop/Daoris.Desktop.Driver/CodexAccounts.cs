namespace Daoris.Driver;

/// <summary>
/// Codex as the holder of the accounts <c>codex-acp</c> runs as (CODEXACCT1, D125's CODEXACCT1 note): its own binary, its
/// sign-in, its status question and its account seam, declared for the agent <c>codex</c>, since this build carries no
/// <c>codex</c> door to declare them (D23) and the door runs <c>codex</c> with no sign-in of its own (ACP3).
/// </summary>
/// <remarks>
/// <para><b>The CLI's <c>codex</c> entry is the twin</b> (<c>toolchain.ts</c>, <c>.claude/knowledge/twins.md</c>):
/// <c>CodexAccountTests</c> reads it and holds this to it, so <c>daoris agent login codex --new</c> and the window's
/// <i>Add an account…</i> run one sign-in into one folder and ask one question.</para>
/// <para><b>Measured on the install with Codex 0.160.0, 2026-10-08</b>: <c>codex login status</c> prints <c>Logged in
/// using ChatGPT</c> or, under an empty <c>CODEX_HOME</c>, <c>Not logged in</c>, exiting 0 for both, so the words are the
/// answer, anchored at a line's start because the second contains the first. It names nobody, so an account is named by
/// the person (ACCT2).</para>
/// <para><b>The sign-in is by device code</b> (CODEXACCT2, D125's note): plain <c>codex login</c> waits for its browser's
/// callback on a local port, which Windows reserved on the owner's machine (os error 10013, exit 1), while
/// <c>codex login --device-auth</c> prints a link and a one-time code and listens on nothing. The console shows both, its
/// colours stripped as every action's are (<see cref="HarnessActions.Clean"/>).</para>
/// <para><b>No key</b> (D67 §1, AGT3): <c>codex login --with-api-key</c> stores a key in Codex's own home, where D67 keeps
/// a key in <c>keys.json</c> and hands it at spawn through a variable the tool reads, and no such variable is measured for
/// Codex. Its key accounts wait on that measurement.</para>
/// <para>Its pin stays the CLI's to install (<c>codex-releases</c>): a status question and a sign-in here run the pinned
/// <c>codex</c> where <c>harnesses.json</c> pins one and <c>driver.json</c> names no command (<see cref="HarnessProbe.CommandOf"/>),
/// as the usage question does (<see cref="CodexUsage.Question"/>).</para>
/// </remarks>
public static class CodexAccounts
{
    /// <summary>The agent whose accounts <c>codex-acp</c> runs as: its <c>AccountOf</c>.</summary>
    public const string Agent = "codex";

    /// <summary>Codex's own mechanisms for its accounts, read by the roster and the modules for its doors (AGT7).</summary>
    public static HarnessToolchain Toolchain { get; } = new(
        Product: "Codex",
        Maker: "OpenAI",
        Binary: ["codex"],
        VersionArguments: ["--version"],
        ProfileVariable: "CODEX_HOME",
        LoginArguments: ["login", "--device-auth"],
        LoginCheck: new LoginQuestion(["login", "status"], LoggedIn: @"(?m)^\s*logged in", LoggedOut: @"(?m)^\s*not logged in"));
}
