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
    /// The occasions that may ask, each by the method it is asked from: LAND3's tidy, the clean-up's look, bringing up to date's
    /// look after its fetch, and <i>Ask again</i> (PLUGHOOK1c). Nothing else joins them.
    /// </summary>
    private static readonly string[] Occasions = ["TidyCarriedAsync", "CleanPlanAsync", "SyncPlanAsync", "AskAgainAsync"];

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

    /// <summary>
    /// PLUGHOOK1c (design §2.1 occasion 3): bringing up to date's look asks, after its fetch, where a merge commit first reaches
    /// this machine, and it is started by a person's door alone: the page's *Look for updates*, Ask Daoris's card's first press
    /// and `trees sync`. Its press, which removes, asks nothing.
    /// </summary>
    [Fact]
    public void Bringing_up_to_dates_look_asks_after_its_fetch_and_only_a_persons_door_opens_it()
    {
        var doors = Calls(@"\bSyncPlanAsync\s*\(")
            .Where(call => call.Method != "SyncPlanAsync")
            .Select(call => Path.GetFileName(call.File))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["DriverModule.Lines.cs", "HelpSyncProposals.cs", "TreesConsole.cs"], doors);
        var look = Body(Source("Daoris.Desktop.Driver", "SessionTrees.Sync.cs"), "SyncPlanAsync");
        var fetched = look.IndexOf("FetchedAsync(", StringComparison.Ordinal);
        var asked = look.IndexOf("AskStatesAsync(", StringComparison.Ordinal);
        Assert.True(fetched >= 0 && asked > fetched, "the look asks after its fetch, which is where a merge commit first arrives");
        Assert.DoesNotContain("AskStatesAsync(", Body(Source("Daoris.Desktop.Driver", "SessionTrees.Sync.cs"), "SyncAsync"));
    }

    /// <summary>PLUGHOOK1c (design §2.1 occasion 4): <i>Ask again</i> is a person's press: `trees state` today, the review's press with PLUGHOOK1d.</summary>
    [Fact]
    public void Ask_again_is_a_persons_door()
    {
        var doors = Calls(@"\bAskAgainAsync\s*\(")
            .Where(call => call.Method != "AskAgainAsync")
            .Select(call => Path.GetFileName(call.File))
            .Distinct()
            .ToList();

        Assert.Equal(["TreesConsole.cs"], doors);
    }

    /// <summary>
    /// No look of the loop, and nothing on a timer, opens an occasion: the loop's files and the modules' loop name none of the
    /// looks that ask, so a plugin is never asked a question nobody is waiting on (D147, D148 point 2).
    /// </summary>
    [Fact]
    public void No_look_of_the_loop_opens_an_occasion()
    {
        var looks = new[] { "CleanPlanAsync", "SyncPlanAsync", "AskAgainAsync" };
        var loop = Directory.EnumerateFiles(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver"), "Driver*.cs")
            .Append(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver", "AutoLander.cs"))
            .Append(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "DriverLoop.cs"))
            .Where(File.Exists)
            .ToList();

        Assert.True(loop.Count >= 3, $"expected the loop's files, found {loop.Count}");
        foreach (var file in loop)
        {
            var source = File.ReadAllText(file);
            foreach (var look in looks) Assert.DoesNotContain($"{look}(", source);
        }
    }

    /// <summary>The body of the first method of that name in <paramref name="source"/>: from its declaration to the next declaration.</summary>
    private static string Body(string source, string method)
    {
        var start = Regex.Match(source, $@"^[ \t]*(?:public|private|internal)[^\n;=(]*?\b{method}\s*\(", RegexOptions.Multiline);
        Assert.True(start.Success, $"`{method}` is declared");
        var next = Regex.Match(source[(start.Index + start.Length)..], @"^[ \t]*(?:public|private|internal|protected)\b", RegexOptions.Multiline);
        return next.Success ? source.Substring(start.Index, start.Length + next.Index) : source[start.Index..];
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
