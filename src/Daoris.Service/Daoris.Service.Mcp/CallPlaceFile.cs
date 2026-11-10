using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Where an Ask Daoris connector notes each call that named a place (<see cref="CallPlace"/>), for the driver to compose each turn's
/// receipt of what each turn looked at beyond the conversation's focus.
/// </summary>
/// <remarks>
/// <para><b>The file is the contract</b> (<c>.claude/knowledge/twins.md</c>): <c>&lt;home&gt;/sessions/&lt;session&gt;/places.jsonl</c>,
/// one JSON object a line (<c>at</c>, <c>tool</c>, <c>act</c>, <c>workspace?</c>, <c>repository?</c>, <c>what?</c>), LF, UTF-8 with no
/// BOM. 🔴 The driver's <c>HelpPlaceNotes.FileName</c> is the same name.</para>
/// <para><b>Only for Ask Daoris bound to a workspace.</b> The session is the connector's <c>DAORIS_SESSION_ID</c>, the home its
/// rules home, and the workspace its <c>DAORIS_SESSION_WORKSPACE</c>, which the driver hands Ask Daoris's connector alone. One
/// focused on this machine is bound to none, and nothing is beyond the machine, so it notes nothing.</para>
/// </remarks>
public sealed class CallPlaceFile
{
    /// <summary>The file's name in the session's folder.</summary>
    public const string FileName = "places.jsonl";

    private static readonly Regex SessionShape = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <param name="home">The home the driver named; null for none.</param>
    /// <param name="session">The session the connector speaks for; null for none.</param>
    /// <param name="bound">The workspace the connector is bound to; null for none.</param>
    public CallPlaceFile(string? home, string? session, string? bound)
    {
        var id = session?.Trim();
        Path = !string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(bound) && id is not null && SessionShape.IsMatch(id)
            ? DaorisHome.Entry(home.Trim(), HomeEntry.Sessions, id, FileName)
            : null;
    }

    /// <summary>The file this connector appends to, or null when it notes nothing.</summary>
    public string? Path { get; }

    /// <summary>What this process is spawned with.</summary>
    public static CallPlaceFile FromEnvironment() =>
        new(RuleProposalBox.FromEnvironment(Environment.GetEnvironmentVariable).Home,
            Environment.GetEnvironmentVariable(IntakeScope.SessionVariable),
            AmbientWorkspace.BoundFrom(Environment.GetEnvironmentVariable));

    /// <summary>One call's place. Never throws: a line lost is a place the receipt does not claim.</summary>
    public void Note(CallPlace place, DateTimeOffset at)
    {
        if (Path is null) return;
        var line = new JsonObject
        {
            ["at"] = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["tool"] = place.Tool,
            ["act"] = place.Act,
        };
        if (place.Workspace is { } workspace) line["workspace"] = workspace;
        if (place.Repository is { } repository) line["repository"] = repository;
        if (place.What is { } what) line["what"] = what;
        try
        {
            Append(Path, line.ToJsonString());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The call goes on.
        }
    }

    /// <summary>One line appended holding the file alone, tried again briefly while another thread's connector holds it.</summary>
    private static void Append(string path, string line)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var bytes = Utf8.GetBytes(line + "\n");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
                return;
            }
            catch (IOException) when (attempt < 40 && !Directory.Exists(path))
            {
                Thread.Sleep(25);
            }
        }
    }
}
