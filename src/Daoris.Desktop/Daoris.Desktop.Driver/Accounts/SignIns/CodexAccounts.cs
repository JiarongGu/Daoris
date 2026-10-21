namespace Daoris.Driver;

/// <summary>
/// Codex as the holder of the accounts <c>codex-acp</c> runs as (CODEXACCT1, D125's CODEXACCT1 note): its own binary, its
/// sign-in, its status question and its account seam, declared for the agent <c>codex</c>, since this build carries no
/// <c>codex</c> door to declare them (D23) and the door runs <c>codex</c> with no sign-in of its own (ACP3).
/// </summary>
/// <remarks>
/// <para><b>One declaration</b>: <c>daoris agent login codex --new</c> is <c>daoris-driver agent login</c> (PUSH1f2e), so it and the
/// window's <i>Add an account…</i> run one sign-in into one folder and ask one question. The CLI's <c>codex</c> entry in
/// <c>toolchain.ts</c> was its twin until <c>TOOLCHAINS</c> left (PUSH1f2f).</para>
/// <para><b>Measured on the install with Codex 0.160.0, 2026-10-08</b>: <c>codex login status</c> prints <c>Logged in
/// using ChatGPT</c> or, under an empty <c>CODEX_HOME</c>, <c>Not logged in</c>, exiting 0 for both, so the words are the
/// answer, anchored at a line's start because the second contains the first. It names nobody, so an account is named by
/// the person (ACCT2).</para>
/// <para><b>Two ways in, the browser first</b> (CODEXLOGIN1, D125's note). <c>codex login</c> opens the system's browser and
/// waits for its answer on 127.0.0.1:1455, else 1457 where another program holds 1455 (measured with 0.160.0, 2026-10-21), as
/// ChatGPT's desktop app signs in; <c>codex login --device-auth</c> prints a link and a one-time code and listens on nothing
/// (CODEXACCT2, which took it alone after a reserved range covered the port: os error 10013, exit 1). A workspace may refuse
/// either: device codes are an admin's setting. So the browser is offered where one of its ports can be taken
/// (<see cref="SignInPorts"/>) and the device code beside it, and the person may switch at any time. The console shows both,
/// colours stripped as every action's are (<see cref="HarnessActions.Clean"/>).</para>
/// <para><b>No key</b> (D67 §1, AGT3): <c>codex login --with-api-key</c> stores a key in Codex's own home, where D67 keeps
/// a key in <c>keys.json</c> and hands it at spawn through a variable the tool reads, and no such variable is measured for
/// Codex. Its key accounts wait on that measurement.</para>
/// <para>Its pin is installed from its maker's channel (<c>codex-releases</c>, <see cref="CodexReleases"/>; PUSH1f2e): a status question and a sign-in here run the pinned
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
        Mark: "Cx",
        Maker: "OpenAI",
        Binary: ["codex"],
        VersionArguments: ["--version"],
        ProfileVariable: "CODEX_HOME",
        // Its own installer, the CLI's entry's, which `agent list` names where Codex is absent (PUSH1f2e4); no door runs it here.
        Install: ["npm", "install", "-g", "@openai/codex"],
        // Its own updater, run unpinned; a pin comes from the release's own package, by both of its published hashes (AGT2b), with no
        // pinned environment: an executable outside Codex's own standalone layout gets no update action (the channel evidence, §2).
        UpdateArguments: ["update"],
        Channel: CodexReleases.Channel,
        LoginArguments: ["login"],
        LoginCheck: new LoginQuestion(["login", "status"], LoggedIn: @"(?m)^\s*logged in", LoggedOut: @"(?m)^\s*not logged in"),
        DeviceLoginArguments: ["login", "--device-auth"],
        LoginPorts: [1455, 1457]);
}
