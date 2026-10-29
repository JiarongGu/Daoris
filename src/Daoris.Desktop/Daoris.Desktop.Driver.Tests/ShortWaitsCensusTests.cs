using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TESTPATIENCE2 (the FIX-LOG entry of that name): a desktop test that waits on a signal which fails to come only when the code is
/// wrong waits <c>Poll.Patience</c>, never a few seconds of its own, because a loaded machine reaches a few seconds on time alone
/// and a fast-half failure is never re-run. This census reads both desktop test projects' sources and counts each short fixed
/// <c>WaitAsync(TimeSpan.From…)</c> and each <c>TimeSpan Name = TimeSpan.From…</c> shorter than <c>Poll.Patience</c>. Each is listed
/// below with what it is that is not a wait on a signal (a bound that states the claim, a window in which nothing should happen,
/// a staging delay, or an input the product itself honours), and a new one, or one gone, fails until the list says so.
/// </summary>
public sealed class ShortWaitsCensusTests
{
    private const string Units = "Milliseconds|Seconds|Minutes|Hours";

    private static readonly string[] Projects = [SourceFiles.DriverTestsProject, "Daoris.Desktop.Modules.Tests"];

    private static readonly Regex Waits = new($@"WaitAsync\(\s*(?<bound>TimeSpan\.From(?<unit>{Units})\((?<n>\d+(?:\.\d+)?)\))");

    private static readonly Regex Fields = new($@"\bTimeSpan\s+(?<name>\w+)\s*=\s*TimeSpan\.From(?<unit>{Units})\((?<n>\d+(?:\.\d+)?)\)");

    private static readonly Regex Trailing = new(@"\s//.*$");

    private static TimeSpan Length(Match match)
    {
        var n = double.Parse(match.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return match.Groups["unit"].Value switch
        {
            "Milliseconds" => TimeSpan.FromMilliseconds(n),
            "Seconds" => TimeSpan.FromSeconds(n),
            "Minutes" => TimeSpan.FromMinutes(n),
            _ => TimeSpan.FromHours(n),
        };
    }

    /// <summary>
    /// The short bounds in a source, each as the text that names it, in order: a <c>WaitAsync</c> given a literal length, or a
    /// <c>TimeSpan</c> named for one, shorter than <see cref="Poll.Patience"/>. Comment lines and trailing comments are not read.
    /// </summary>
    internal static List<string> Shorts(string source)
    {
        // Read whole, not line by line, so a call whose length is on the next line is read too.
        var code = string.Join('\n', source.Split('\n').Where(raw => !raw.TrimStart().StartsWith("//", StringComparison.Ordinal)).Select(raw => Trailing.Replace(raw, "")));
        return
        [
            .. Waits.Matches(code).Where(match => Length(match) < Poll.Patience).Select(match => $"WaitAsync({match.Groups["bound"].Value})"),
            .. Fields.Matches(code).Where(match => Length(match) < Poll.Patience).Select(match => $"TimeSpan {match.Groups["name"].Value}"),
        ];
    }

    /// <summary>Every short bound of the two projects, by <c>project/path: bound</c>, the census's own file left out.</summary>
    private static SortedDictionary<string, int> Scan()
    {
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in Projects)
        {
            var folder = SourceFiles.Project(project);
            foreach (var path in SourceFiles.All(project, "*.cs").Where(path => Path.GetFileName(path) != nameof(ShortWaitsCensusTests) + ".cs"))
            {
                var relative = project + "/" + Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');
                foreach (var bound in Shorts(File.ReadAllText(path))) found[$"{relative}: {bound}"] = found.GetValueOrDefault($"{relative}: {bound}") + 1;
            }
        }

        return found;
    }

    private const string Driver = SourceFiles.DriverTestsProject;

    /// <summary>What each listed short bound is, by <c>project/path: bound</c>: how many there are, and why none of them is a wait on a signal.</summary>
    private static readonly (string Site, int Count, string Reason)[] Listed =
    [
        ($"{Driver}/Agents/AccountWalkGateTests.cs: TimeSpan LockWait", 1, "an input: the lock patience the roster is handed where a test sets none"),
        ($"{Driver}/Agents/AccountWalkGateTests.cs: TimeSpan Short", 1, "an input: a start's patience for its account's lock, short so that a held lock runs it out"),
        ($"{Driver}/Agents/AcpBackgroundTests.cs: TimeSpan Look", 1, "an input: how often the session reads its quest again while it waits on background work"),
        ($"{Driver}/Agents/AcpBackgroundTests.cs: TimeSpan Minute", 1, "an input: the background look's pace at the product's default, spent on the case's own moved clock"),
        ($"{Driver}/Agents/AcpBackgroundTests.cs: TimeSpan Soon", 1, "an input: the session's quiet wait before it goes on, which a case also states as its claim"),
        ($"{Driver}/Agents/OutputTailTests.cs: TimeSpan Fast", 1, "an input: the tail's own polling interval"),
        ($"{Driver}/Events/ChainTests.cs: TimeSpan LookDeadline", 1, "an input: a topic's deadline, spent on the test's own clock"),
        ($"{Driver}/Events/ChainTests.cs: TimeSpan RequestsDeadline", 1, "an input: a topic's deadline, spent on the test's own clock"),
        ($"{Driver}/Events/ChangeWatchTests.cs: TimeSpan Deadline", 1, "an input: a topic's deadline, spent on the test's own clock"),
        ($"{Driver}/Events/SessionWatchTests.cs: TimeSpan Pace", 1, "an input: the watch's pace, spent on the test's own clock"),
        ($"{Driver}/Loop/BrainstormBoundsTests.cs: TimeSpan Minute", 1, "an input: a brainstorm's minute shrunk, which the product spends"),
        ($"{Driver}/Loop/MeetingTests.cs: TimeSpan Second", 1, "an input: a meeting's minute shrunk, which the product spends"),
        ($"{Driver}/Loop/MeetingTests.cs: TimeSpan Tenth", 1, "an input: a meeting's minute shrunk, which the product spends"),
        ($"{Driver}/Workflows/GraphPressTests.cs: WaitAsync(TimeSpan.FromMilliseconds(20))", 1, "a window in which nothing happens: past the bound the ask is no longer waited on, and the claim is that it says false"),
        ($"{Driver}/Workflows/GraphPressTests.cs: WaitAsync(TimeSpan.FromMinutes(1))", 2, "the product's own timeout argument on its pass asks, not a wait of the test's: its answer is read after the pass is done"),
        ($"{Driver}/Workplace/RefWatchProcessTests.cs: TimeSpan Within", 1, "an elapsed-time bound that states the claim: a judgement begun within a second of git's exit"),
        ($"{Driver}/Workplace/SetAsideSlimTests.cs: TimeSpan Budget", 1, "an input: the budget the keep's set-aside is handed"),
    ];

    /// <summary>What differs between the short bounds a scan found and the ones listed, each as a sentence.</summary>
    internal static List<string> Differences(IReadOnlyDictionary<string, int> scanned, IReadOnlyDictionary<string, int> listed) =>
    [
        .. scanned.Where(each => !listed.ContainsKey(each.Key))
            .Select(each => $"{each.Key} ({each.Value}) is shorter than Poll.Patience and is not listed: wait on Poll.Patience, or list it with its reason"),
        .. listed.Where(each => !scanned.ContainsKey(each.Key)).Select(each => $"{each.Key} is listed ({each.Value}) and is not there now: take it off the list"),
        .. scanned.Where(each => listed.TryGetValue(each.Key, out var count) && count != each.Value)
            .Select(each => $"{each.Key} is there {each.Value} time(s) and the list says {listed[each.Key]}"),
    ];

    private static SortedDictionary<string, int> Table() =>
        new(Listed.ToDictionary(each => each.Site, each => each.Count), StringComparer.Ordinal);

    /// <summary>🔴 Every short fixed wait and every short private bound in the desktop tests is a listed one, or a wait on the shared patience.</summary>
    [Fact]
    public void Every_short_fixed_bound_in_the_desktop_tests_is_listed_with_its_reason()
    {
        var differences = Differences(Scan(), Table());
        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    /// <summary>The census fails on a planted short bound, in a listed file and in a new one, and passes a wait on the patience.</summary>
    [Fact]
    public void A_planted_short_bound_fails_the_census_and_a_wait_on_the_patience_does_not()
    {
        Assert.Equal(["WaitAsync(TimeSpan.FromSeconds(10))"], Shorts("await run.WaitAsync(TimeSpan.FromSeconds(10));"));
        Assert.Equal(["WaitAsync(TimeSpan.FromSeconds(10))"], Shorts("await run.WaitAsync(\n    TimeSpan.FromSeconds(10), ct);"));
        Assert.Equal(["TimeSpan Bound"], Shorts("    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);"));
        Assert.Equal(["TimeSpan Soon"], Shorts("    private static readonly TimeSpan Soon = TimeSpan.FromMilliseconds(300);"));
        Assert.Equal(["WaitAsync(TimeSpan.FromMinutes(1))"], Shorts("await run.WaitAsync(TimeSpan.FromMinutes(1));"));
        Assert.Empty(Shorts("await run.WaitAsync(Poll.Patience);"));
        Assert.Empty(Shorts("await run.WaitAsync(TimeSpan.FromMinutes(2));"));
        Assert.Empty(Shorts("await run.WaitAsync(TimeSpan.FromSeconds(180));"));
        Assert.Empty(Shorts("    private static readonly TimeSpan Never = TimeSpan.FromHours(1);"));
        Assert.Empty(Shorts("// await run.WaitAsync(TimeSpan.FromSeconds(10));\nvar quiet = 1; // TimeSpan Bound = TimeSpan.FromSeconds(10);"));

        var listed = Table();
        var scanned = new SortedDictionary<string, int>(listed, StringComparer.Ordinal);
        var known = listed.First().Key;
        Assert.Empty(Differences(scanned, listed));
        Assert.Equal([$"{known} is there {scanned[known] + 1} time(s) and the list says {scanned[known]}"],
            Differences(new SortedDictionary<string, int>(scanned) { [known] = scanned[known] + 1 }, listed));
        Assert.Single(Differences(new SortedDictionary<string, int>(scanned) { [$"{Driver}/Loop/Planted.cs: WaitAsync(TimeSpan.FromSeconds(5))"] = 1 }, listed),
            each => each.Contains("Planted.cs", StringComparison.Ordinal) && each.Contains("not listed", StringComparison.Ordinal));
        scanned.Remove(known);
        Assert.Single(Differences(scanned, listed), each => each.StartsWith(known, StringComparison.Ordinal) && each.Contains("take it off the list", StringComparison.Ordinal));
    }

    /// <summary>Each listed bound says why it is not a wait on a signal.</summary>
    [Fact]
    public void Each_listed_bound_says_why_it_is_not_a_wait_on_a_signal()
    {
        foreach (var (site, count, reason) in Listed)
        {
            Assert.True(count > 0, $"{site} is listed with no count");
            Assert.False(string.IsNullOrWhiteSpace(reason), $"{site} says nothing of why it stays");
        }

        Assert.Equal(Listed.Length, Table().Count);
    }
}
