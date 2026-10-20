using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The marker a shell Daoris started carries (PUSH1f2a, D194 point 5, the alone-path design §2): its session's record id,
/// <c>DAORIS_SESSION_ID</c>, by which <c>daoris-driver driver</c> refuses a person's press acting in its own process. Every start that
/// hands it is listed here, so a new one is seen; the window's terminal panel removes it from the shells it starts, which are the person's.
/// </summary>
public sealed partial class SessionMarkerTests
{
    /// <summary>Each source that hands a started process the marker, and how many times: the sessions and their connectors alone.</summary>
    private static readonly IReadOnlyDictionary<string, (int Count, string Why)> Starts = new Dictionary<string, (int, string)>(StringComparer.Ordinal)
    {
        ["Daoris.Desktop.Driver/Agents/Adapters/Adapters.cs"] =
            (5, "a driven session, an intake, a reviewer, a helper or brainstorm pass, and a conversation, where the session has a record"),
        ["Daoris.Desktop.Driver/Quests/IntakeRoom.cs"] = (1, "an intake's connector, the room its tools answer"),
        ["Daoris.Desktop.Driver/Service/KnowledgeConnector.cs"] = (1, "a session's knowledge connector"),
    };

    [Fact]
    public void Every_start_that_hands_the_marker_is_listed_with_why()
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in new[] { SourceFiles.DriverProject, "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App" })
        {
            foreach (var file in SourceFiles.All(project, "*.cs"))
            {
                var count = Handed().Matches(File.ReadAllText(file)).Count;
                if (count > 0) found[$"{project}/{Path.GetRelativePath(SourceFiles.Project(project), file).Replace('\\', '/')}"] = count;
            }
        }

        Assert.Equal(Starts.ToDictionary(each => each.Key, each => each.Value.Count).OrderBy(each => each.Key), found.OrderBy(each => each.Key));
        Assert.All(Starts.Values, start => Assert.False(string.IsNullOrWhiteSpace(start.Why)));
    }

    /// <summary>A terminal the window opens is the person's own: its shell is not handed the marker, though this process carries one.</summary>
    [Fact]
    public void The_window_s_terminal_starts_the_person_s_shell_without_the_marker()
    {
        var inherited = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [IntakeRoom.SessionVariable] = "s1a2b3c4",
            ["DAORIS_KEPT"] = "kept",
        };

        var block = PseudoConsole.EnvironmentBlock(new Dictionary<string, string>(), inherited).Split('\0');

        Assert.DoesNotContain(block, line => line.StartsWith(IntakeRoom.SessionVariable + "=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("DAORIS_KEPT=kept", block);
    }

    [GeneratedRegex(@"\[(?:IntakeRoom\.)?SessionVariable\]\s*=")]
    private static partial Regex Handed();
}
