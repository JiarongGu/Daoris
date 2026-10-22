using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What every process Daoris starts is handed so that no git in it asks anyone anything (GITAUTH1a, D198 point 6; the git-auth
/// design §4.1 and §4.6): git asks no terminal, runs no askpass program, Git Credential Manager opens no window, and git's own
/// traces redact what authenticates. Handed through <see cref="Tools.Hand(ProcessStartInfo, ToolsRead, string)"/>, the seam
/// every start the source scan holds already passes, so a start written later is held to it with no second scan; and never
/// through <see cref="Tools.ChildEnvironment"/>, which the terminal's shells take: those are the person's own, and theirs may
/// prompt (§4.5).
/// </summary>
/// <remarks>
/// <para><b>An empty <c>GIT_ASKPASS</c></b> stops git's whole search, <c>core.askPass</c> and <c>SSH_ASKPASS</c> included: git reads
/// the variable first and asks a program only where the value it found is not empty. That the empty value survives Windows'
/// environment block into git is measured by <c>GitPromptsProcessTests</c>, against a program a lower step names.</para>
/// <para>The rest of §4.1 (the configuration file, the doors, the identity's author) is GITAUTH1b's and later rows'; they join
/// here.</para>
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

    /// <summary>Hand a start the prompt variables, and take out the inherited ones that could prompt or trace.</summary>
    public static void Hand(ProcessStartInfo info)
    {
        foreach (var name in Removed) info.Environment.Remove(name);
        foreach (var (name, value) in Prompts) info.Environment[name] = value;
    }
}
