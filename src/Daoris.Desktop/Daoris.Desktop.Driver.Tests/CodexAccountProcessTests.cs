using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXACCT1's spawns: Codex's own sign-in and status question, run through the door the driver has onto it
/// (<c>codex-acp</c>) with <c>codex</c>'s binary and its <c>CODEX_HOME</c>. <c>codex</c> is a stand-in named in
/// <c>driver.json</c>'s commands, as a person names one, answering in the words <c>codex login status</c> printed on the
/// install (0.160.0, 2026-10-08): signed in where its home holds <c>auth.json</c>, which its <c>login</c> writes.
/// </summary>
/// <remarks>
/// A real process, so the <c>Process</c> half (MOD8). The declaration, the words and the walk are <c>CodexAccountTests</c>.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CodexAccountProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-codexacct-run-" + Guid.NewGuid().ToString("N")[..8]);

    private string Settings => Path.Combine(_home, "harnesses.json");

    private string Script => Path.Combine(_home, "codex.mjs");

    // The tool's own home, where it keeps the person's own sign-in when no `CODEX_HOME` is set.
    private string Own => Path.Combine(_home, "own");

    public CodexAccountProcessTests()
    {
        Directory.CreateDirectory(Own);
        File.WriteAllText(Script, """
            import fs from 'node:fs';
            import path from 'node:path';
            import { fileURLToPath } from 'node:url';
            const home = process.env.CODEX_HOME ?? path.join(path.dirname(fileURLToPath(import.meta.url)), 'own');
            fs.appendFileSync(new URL('./asked.log', import.meta.url), path.basename(home) + ' ' + process.argv.slice(2).join(' ') + '\n');
            const [verb, sub] = process.argv.slice(2);
            if (verb === '--version') { console.log('codex-cli 0.160.0'); process.exit(0); }
            if (verb === 'login' && sub === 'status') {
              console.log(fs.existsSync(path.join(home, 'auth.json')) ? 'Logged in using ChatGPT' : 'Not logged in');
              process.exit(0);
            }
            if (verb === 'login' && sub === undefined) {
              fs.writeFileSync(path.join(home, 'auth.json'), '{}');
              console.log('Successfully logged in');
              process.exit(0);
            }
            process.exit(1);
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string[] Asked => File.Exists(Path.Combine(_home, "asked.log")) ? File.ReadAllLines(Path.Combine(_home, "asked.log")) : [];

    private HarnessRoster Roster() => new(AdapterSet.Built(), Settings);

    // Both named, so nothing on this machine's PATH is asked: the door's binary and the agent's.
    private DriverConfig Config => DriverConfig.Empty with
    {
        Adapter = "codex-acp",
        Commands = new Dictionary<string, IReadOnlyList<string>>
        {
            ["codex"] = ["node", Script],
            ["codex-acp"] = ["node", Script],
        },
    };

    private string Account(string name, bool signedIn)
    {
        var home = HarnessSettings.ProfileHome(_home, "codex", name);
        Directory.CreateDirectory(home);
        if (signedIn) File.WriteAllText(Path.Combine(home, "auth.json"), "{}");
        return home;
    }

    /// <summary>
    /// *Read again* on Agents → Codex reads each account and the tool's own sign-in by <c>codex login status</c>, under each
    /// account's <c>CODEX_HOME</c> and none for the own, and keeps each reading under <c>codex</c>: the own sign-in read
    /// *unknown · never read*, since the door has no status question and no press of <c>codex</c>'s exists.
    /// </summary>
    [Fact]
    public async Task A_press_through_codex_acp_reads_each_account_and_the_own_sign_in_by_codex_s_status_words()
    {
        Account("account-1", signedIn: true);
        Account("account-2", signedIn: false);
        File.WriteAllText(Path.Combine(Own, "auth.json"), "{}");

        var report = await Roster().ReportAsync("codex-acp", Config, refresh: true, own: true);

        Assert.Equal(LoginState.In, report!.Profiles.Single(p => p.Name == "account-1").Login);
        Assert.Equal(LoginState.Out, report.Profiles.Single(p => p.Name == "account-2").Login);
        Assert.Equal(LoginState.In, report.OwnLogin);
        Assert.NotNull(report.OwnRead);
        var kept = AccountReads.Of(_home, "codex");
        Assert.Equal(LoginState.In, kept.Accounts["account-1"].Login);
        Assert.Equal(LoginState.Out, kept.Accounts["account-2"].Login);
        Assert.Equal(LoginState.In, kept.Own!.Login);
        Assert.Contains("account-1 login status", Asked);
        Assert.Contains("account-2 login status", Asked);
        Assert.Contains("own login status", Asked);
    }

    /// <summary>
    /// *Add an account…*'s sign-in: <c>codex login</c> into a new folder Daoris owns, with that folder as its
    /// <c>CODEX_HOME</c>, and the end's question reads it signed in. Never the tool's own home.
    /// </summary>
    [Fact]
    public async Task A_sign_in_to_a_new_codex_account_runs_codex_login_into_its_own_folder()
    {
        var roster = Roster();
        var agent = roster.AccountAgentOf("codex-acp")!.Value;
        var fresh = AccountNames.NewId(_home, "codex");
        var home = HarnessSettings.ProfileHome(_home, "codex", fresh);
        var said = new List<string>();

        var code = await HarnessActions.LoginAsync(
            agent.Toolchain, Config.Commands[agent.Name], home, said.Add, fresh: true);
        var (login, _) = await roster.LoginOfAsync("codex-acp", Config, fresh);

        Assert.Equal(0, code);
        Assert.True(File.Exists(Path.Combine(home, "auth.json")));
        Assert.False(File.Exists(Path.Combine(Own, "auth.json")));
        Assert.Contains($"{fresh} login", Asked);
        Assert.Equal(LoginState.In, login);
        Assert.Equal(LoginState.In, AccountReads.Of(_home, "codex").Accounts[fresh].Login);
    }
}
