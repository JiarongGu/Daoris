using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What every process Daoris starts is handed for its git (GITAUTH1a and GITAUTH1b, D198 points 2 and 6; the git-auth design §4.1
/// and §4.6): no git in it asks anyone anything, and every remote act passes one of Daoris's two doors, the ssh door for SSH and the
/// remote helper for HTTP and HTTPS. Handed through <see cref="Tools.Hand(ProcessStartInfo, ToolsRead, string)"/>, the seam every
/// start the source scan holds already passes, so a start written later is held to it with no second scan; and never through
/// <see cref="Tools.ChildEnvironment"/>, which the terminal's shells take: those are the person's own, and theirs may prompt and
/// pass no door (§4.5).
/// </summary>
/// <remarks>
/// <para><b>An empty <c>GIT_ASKPASS</c></b> stops git's whole search, <c>core.askPass</c> and <c>SSH_ASKPASS</c> included: git reads
/// the variable first and asks a program only where the value it found is not empty. That the empty value survives Windows'
/// environment block into git is measured by <c>GitPromptsProcessTests</c>, against a program a lower step names.</para>
/// <para><b>The doors are handed where they are installed</b>, in the install's <c>app/bin/</c> beside the home
/// (<see cref="Tools.InstallBin"/>), each on its own: a home with no install beside it (a test's, a development run's that laid none)
/// routes nothing, as before. The ssh door is named in <c>GIT_SSH_COMMAND</c> by a bare path in forward slashes holding none of git's
/// shell characters (<see cref="Bare"/>), which git starts directly, with no <c>sh</c>; where the install's path holds one, by
/// <c>GIT_SSH</c>, which git starts whole but which a repository's own <c>core.sshCommand</c> outranks (§4.2). The remote helper is
/// reached by <c>insteadOf</c> in the command scope, appended after any the start already holds.</para>
/// <para>The configuration file of §4.1 (TOOLS6) and the identity's author are GITAUTH1c's; they join here.</para>
/// </remarks>
public static class GitWiring
{
    /// <summary>The variables every child is handed, each over what it inherited.</summary>
    public static readonly IReadOnlyDictionary<string, string> Prompts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_ASKPASS"] = "",
        ["GCM_INTERACTIVE"] = "never",
        ["GIT_TRACE_REDACT"] = "1",
    };

    /// <summary>Taken out of every child's environment: an inherited one could prompt, or trace a header or a secret.</summary>
    public static readonly IReadOnlyList<string> Removed =
        ["SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "GIT_TRACE_CURL", "GIT_CURL_VERBOSE", "GCM_TRACE", "GCM_TRACE_SECRETS"];

    /// <summary>
    /// Taken out of a session's start alone (<c>Spawning.Shell</c>, §4.1): a session's <c>gh</c> or <c>az</c> would otherwise act
    /// with a token the desktop inherited. They still act as the person's own sign-in in their own configuration, unjudged (§10).
    /// </summary>
    public static readonly IReadOnlyList<string> SessionTokens = ["GH_TOKEN", "GITHUB_TOKEN", "GH_ENTERPRISE_TOKEN", "AZURE_DEVOPS_EXT_PAT"];

    /// <summary>The ssh door's file in the install's <c>app/bin/</c> (§4.2).</summary>
    public const string SshDoor = "daoris-git-ssh.exe";

    /// <summary>The remote helper's file in the install's <c>app/bin/</c>, which git runs for a <c>daoris::</c> address (§4.3).</summary>
    public const string RemoteHelper = "git-remote-daoris.exe";

    /// <summary>How the ssh door was named for a start: <c>command</c> (<c>GIT_SSH_COMMAND</c>), <c>git-ssh</c>, or null for none.</summary>
    public static string? SshForm(string? home)
    {
        if (DoorPath(home, SshDoor) is not { } door) return null;
        return Bare(door) is null ? "git-ssh" : "command";
    }

    /// <summary>Hand a start the prompt variables, and take out the inherited ones that could prompt or trace.</summary>
    public static void Hand(ProcessStartInfo info) => Hand(info, home: null);

    /// <summary>
    /// Hand a start the prompt variables and, for a start with a home, the doors installed beside it, with the home they act for.
    /// </summary>
    public static void Hand(ProcessStartInfo info, string? home)
    {
        foreach (var name in Removed) info.Environment.Remove(name);
        foreach (var (name, value) in Prompts) info.Environment[name] = value;
        // A grant is handed by its one start alone (§6.7), never inherited.
        info.Environment.Remove(GitRouting.GrantVariable);
        if (home is null) return;

        info.Environment[GitRouting.HomeVariable] = Path.GetFullPath(home);
        if (DoorPath(home, SshDoor) is { } ssh)
        {
            info.Environment["GIT_SSH_VARIANT"] = "ssh";
            if (Bare(ssh) is { } bare)
            {
                info.Environment["GIT_SSH_COMMAND"] = bare;
            }
            else
            {
                // git reads GIT_SSH_COMMAND first, so an inherited one would pass the door by.
                info.Environment.Remove("GIT_SSH_COMMAND");
                info.Environment["GIT_SSH"] = ssh;
            }
        }

        if (DoorPath(home, RemoteHelper) is not null) GitRouting.Scope(info.Environment, GitRouting.Rewrites);
    }

    /// <summary>Take a session's start's inherited token variables out (§4.1): called by <c>Spawning.Shell</c> alone.</summary>
    public static void Untoken(IDictionary<string, string?> environment)
    {
        foreach (var name in SessionTokens) environment.Remove(name);
    }

    /// <summary>
    /// The door's path as git is handed it in <c>GIT_SSH_COMMAND</c>: forward slashes, and null where it holds one of git's shell
    /// characters (a space in a folder's name, which no short name cures, since a short name carries a <c>~</c>).
    /// </summary>
    public static string? Bare(string path)
    {
        var slashed = path.Replace('\\', '/');
        return slashed.IndexOfAny(GitRouting.ShellCharacters.ToCharArray()) >= 0 ? null : slashed;
    }

    /// <summary>A door's file in the install beside <paramref name="home"/>, or null where it is not laid out.</summary>
    public static string? DoorPath(string? home, string door) =>
        home is not null && Tools.InstallBin(home) is { } bin && File.Exists(Path.Combine(bin, door)) ? Path.Combine(bin, door) : null;
}
