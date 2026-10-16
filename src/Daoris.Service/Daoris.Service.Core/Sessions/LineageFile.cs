using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>
/// Where a session's connector writes the lineage each call's harness carried (FABRIC-C1a, D173): one line a call, under the
/// session's own folder in the home the driver named, for the driver to bind the session's native children to its part.
/// </summary>
/// <remarks>
/// <para><b>The file is the contract</b> (<c>.claude/knowledge/twins.md</c>): <c>&lt;home&gt;/sessions/&lt;session&gt;/lineage.jsonl</c>,
/// one JSON object a line, LF, UTF-8 with no BOM. This connector writes <c>"by":"call"</c> lines; the hook the driver hands
/// Claude Code (<c>lineage-hook.mjs</c>) writes <c>"by":"hook"</c> lines and the driver its own <c>"by":"stream"</c> and
/// <c>"by":"end"</c> lines; the driver's <c>ChildLineage</c> reads them all. 🔴 Its <c>FileName</c> and folder are this
/// file's, copied there.</para>
///
/// <para><b>Only for a session the driver started.</b> The session is the connector's <c>DAORIS_SESSION_ID</c> and the home
/// its <c>DAORIS_RULES_HOME</c>, else the Daoris home, as a proposal's (PERM2). A connector a person's own agent started
/// names no session and writes nothing; neither does one whose session is not shaped as an id, so no variable makes it write
/// anywhere else.</para>
///
/// <para><b>Several writers at once.</b> Codex starts a connector per thread from the same entry, and the hook runs as its
/// own process. Each line is appended holding the file alone, and tried again while another writer holds it, so no line
/// overwrites another's. A file that will not take a line costs the line, never the call.</para>
/// </remarks>
public sealed class LineageFile
{
    /// <summary>The file's name in the session's folder. 🔴 The driver's <c>ChildLineage.FileName</c> is the same name.</summary>
    public const string FileName = "lineage.jsonl";

    /// <summary>What a session id is here: the driver's are letters and digits; a separator or a dot never.</summary>
    private static readonly Regex SessionShape = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <param name="home">The home the driver named; null for none.</param>
    /// <param name="session">The session the connector speaks for; null for none.</param>
    public LineageFile(string? home, string? session)
    {
        var id = session?.Trim();
        Path = !string.IsNullOrWhiteSpace(home) && id is not null && SessionShape.IsMatch(id)
            ? System.IO.Path.Combine(home.Trim(), "sessions", id, FileName)
            : null;
    }

    /// <summary>The file this connector appends to, or null when it records nothing.</summary>
    public string? Path { get; }

    /// <summary>What this process was spawned with: the rules home, else the Daoris home, and the session.</summary>
    public static LineageFile FromEnvironment(Func<string, string?> environment) =>
        new(RuleProposalBox.FromEnvironment(environment).Home, environment(IntakeScope.SessionVariable));

    /// <summary>What this process was spawned with.</summary>
    public static LineageFile FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

    /// <summary>One call, with what its harness carried of its lineage, or none. Never throws.</summary>
    public void Call(string? tool, CallLineage? lineage, DateTimeOffset at)
    {
        if (Path is null) return;
        try
        {
            Append(Path, Line(tool, lineage, at));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A line lost is a lineage unknown, which the driver says as unknown; the call goes on.
        }
    }

    /// <summary>The line for one call: its moment, its tool where that is shaped as a name, and the ids its harness wrote.</summary>
    public static string Line(string? tool, CallLineage? lineage, DateTimeOffset at)
    {
        var line = new JsonObject
        {
            ["at"] = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["by"] = "call",
        };
        if (CallLineage.Id(tool) is { } name) line["tool"] = name;
        if (lineage is not null)
        {
            line["harness"] = lineage.Harness;
            Put(line, "toolUse", lineage.ToolUse);
            Put(line, "thread", lineage.Thread);
            Put(line, "parent", lineage.Parent);
            Put(line, "kind", lineage.Kind);
            Put(line, "source", lineage.Source);
            Put(line, "root", lineage.Root);
        }

        return line.ToJsonString();
    }

    private static void Put(JsonObject line, string name, string? value)
    {
        if (value is not null) line[name] = value;
    }

    /// <summary>
    /// One line appended holding the file alone, tried again for about two seconds while another writer holds it. Readers are
    /// let in; writers are kept out, so the end this writer appends at is the file's end.
    /// </summary>
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
            catch (IOException) when (attempt < 80 && !Directory.Exists(path))
            {
                Thread.Sleep(25);
            }
        }
    }
}
