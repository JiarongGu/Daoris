using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGHOOK1a (D148 point 2, the plugin hooks design §2.1): a plugin is asked about a pull request only at an occasion that may
/// remove something, never at each look of the loop, on a timer, at a press that removes, or at a read. A network call nobody
/// waits on is the one D147 rejected for a fetch, and TOOL6g found the same shape costing an account. A source scan rather than
/// a unit test, because the property is one every new caller must keep and the next one written will not know to.
/// </summary>
public sealed class PullRequestOccasionTests
{
    private static readonly string[] Projects =
        ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    /// <summary>
    /// The occasions that may ask, each by the method it is asked from: LAND3's tidy, and the clean-up's look. PLUGHOOK1c adds
    /// bringing up to date's look after its fetch and <i>Ask again</i> here, and nothing else joins them.
    /// </summary>
    private static readonly string[] Occasions = ["TidyCarriedAsync", "CleanPlanAsync"];

    [Fact]
    public void The_loop_keeps_no_process_for_the_query_and_has_nothing_to_ask_it()
    {
        Assert.DoesNotContain(HookPoints.State, HookPoints.Loop);
        // The loop's set asks only what its points say; a plugin that speaks only the query is never kept running beside it.
        Assert.DoesNotContain("AskStatesAsync", Source("Daoris.Desktop.Driver", "Hooks.cs"));
    }

    /// <summary>Every ask is made from one of the occasions, and only the occasion's helper asks the plugins.</summary>
    [Fact]
    public void A_plugin_is_asked_only_at_an_occasion_that_may_remove()
    {
        var calls = Calls(@"\bAskStatesAsync\s*\(").ToList();

        Assert.True(calls.Count >= 3, $"expected to find the asks, found {calls.Count}");
        // The plugins are asked by the one helper the occasions share, and it by the occasions alone.
        Assert.All(calls.Where(call => call.Expression.Contains("_plugins.", StringComparison.Ordinal)),
            call => Assert.Equal("AskStatesAsync", call.Method));
        var occasions = calls.Where(call => !call.Expression.Contains("_plugins.", StringComparison.Ordinal)).ToList();
        Assert.All(occasions, call => Assert.True(Occasions.Contains(call.Method), $"{call.File} asks from `{call.Method}`, which is no occasion"));
        Assert.Equal(Occasions.Order(StringComparer.Ordinal), occasions.Select(call => call.Method).Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>A channel is asked at the query only by the occasions' helper and the kit's trial, which a person starts.</summary>
    [Fact]
    public void Only_the_occasions_helper_and_the_kits_trial_speak_the_query()
    {
        var speakers = Calls(@"\.StateAsync\s*\(")
            .Where(call => !call.File.EndsWith("Hooks.cs", StringComparison.Ordinal))
            .Select(call => (Path.GetFileName(call.File), call.Method))
            .Distinct()
            .Order()
            .ToList();

        Assert.Equal([("LandingPlugins.cs", "AskOneAsync"), ("PluginTrial.cs", "AskAsync")], speakers);
    }

    /// <summary>The clean-up's look, which asks, is started by a person's door alone: the page's list and `trees clean`.</summary>
    [Fact]
    public void The_clean_ups_look_is_a_persons_door_never_the_loops()
    {
        var doors = Calls(@"\bCleanPlanAsync\s*\(")
            .Where(call => call.Method != "CleanPlanAsync")
            .Select(call => Path.GetFileName(call.File))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["DriverModule.Lines.cs", "TreesConsole.cs"], doors);
    }

    /// <summary>Each call of a pattern in the desktop's source that is not its declaration, with the method it is made from.</summary>
    private static IEnumerable<(string File, string Method, string Expression)> Calls(string pattern)
    {
        var root = SourceRoot();
        foreach (var project in Projects)
        {
            var folder = Path.Combine(root, project);
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                var declarations = Regex.Matches(source,
                    @"^[ \t]*(?:public|private|internal|protected)[^\n;=(]*?\b(\w+)\s*\(", RegexOptions.Multiline);
                foreach (Match call in Regex.Matches(source, pattern))
                {
                    var lineStart = source.LastIndexOf('\n', call.Index) + 1;
                    var line = source[lineStart..(source.IndexOf('\n', call.Index) is var end and >= 0 ? end : source.Length)];
                    // A declaration, or a comment or a doc reference, is not a call.
                    if (Regex.IsMatch(line, @"^\s*(?:public|private|internal|protected)\b") || line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                        || line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var method = declarations.LastOrDefault(each => each.Index < call.Index)?.Groups[1].Value ?? "";
                    yield return (file, method, line.Trim());
                }
            }
        }
    }

    private static string Source(string project, string file) => File.ReadAllText(Path.Combine(SourceRoot(), project, file));

    private static string SourceRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("the desktop source tree was not found above the test binary");
    }
}
