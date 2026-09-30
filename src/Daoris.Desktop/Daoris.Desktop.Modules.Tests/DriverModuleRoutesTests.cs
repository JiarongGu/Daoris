using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The route table's shape (MOD5): the page's bridge and <see cref="DriverModule"/>'s table are two lists of
/// one set of names, in two languages, with no compiler between them, so a route can be half-added: called
/// by the page and answered NO_ROUTE, or answered and called by nothing.
/// </summary>
/// <remarks>
/// <para>Read from the sources, as <see cref="RefusalCatalogueTests"/> reads the throw sites: the page's calls
/// from <c>src/Daoris.Web/src/bridge/*.ts</c> (and <c>shell.ts</c>, which held them all before the bridge was
/// split by domain, MOD3), and each route's partial from the <c>[DriverRoute]</c> marks.</para>
///
/// <para>A route the page sends is a literal in one of three places: the first argument of <c>call</c>, the
/// bridge's one call onto this module; the argument of a helper that forwards its own to <c>call</c>
/// (<see cref="Forwarders"/>); or the route after <c>'DAORIS.DRIVER'</c> in a direct <c>invoke</c>.</para>
/// </remarks>
public sealed partial class DriverModuleRoutesTests
{
    /// <summary>The bridge's helpers that send their route argument on to <c>call</c>, besides <c>call</c> itself.</summary>
    private static readonly string[] Forwarders = ["useDriverChange"];

    /// <summary>Where a send names its route: <c>call</c> or a forwarder, then its type arguments or its call.</summary>
    private static readonly Regex Caller = new($@"\b(?:{string.Join('|', Forwarders.Prepend("call"))})\s*(?=[<(])");

    /// <summary>Walk up to the workspace root — the tests run from `bin/Debug/net10.0`.</summary>
    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>One route the page sends, and the bridge file it is sent from.</summary>
    private sealed record PageCall(string Route, string File);

    /// <summary>
    /// Every route the page sends this module, and how many sends pass on a route the caller was given
    /// rather than naming one: each of those is a forwarding helper, whose own callers name the route.
    /// </summary>
    private static (List<PageCall> Calls, int Forwarded) PageCalls()
    {
        var web = Path.Combine(RepositoryRoot(), "src", "Daoris.Web", "src");
        var bridge = Path.Combine(web, "bridge");
        var files = (Directory.Exists(bridge)
                ? Directory.EnumerateFiles(bridge, "*.ts").Where(path => !path.EndsWith(".test.ts", StringComparison.Ordinal))
                : [])
            .Append(Path.Combine(web, "shell.ts"))
            .ToList();

        var calls = new List<PageCall>();
        var forwarded = 0;
        foreach (var path in files)
        {
            var text = File.ReadAllText(path);
            var file = Path.GetFileName(path);
            foreach (Match direct in DirectInvoke().Matches(text))
            {
                calls.Add(new PageCall(direct.Groups["route"].Value, file));
            }

            foreach (Match caller in Caller.Matches(text))
            {
                var argument = FirstArgument(text, caller.Index + caller.Length);
                if (argument is null) continue;
                var named = RouteLiteral().Matches(argument).Select(literal => literal.Groups["route"].Value).ToList();
                calls.AddRange(named.Select(route => new PageCall(route, file)));
                if (named.Count == 0 && BareName().IsMatch(argument.Trim())) forwarded += 1;
            }
        }

        return (calls, forwarded);
    }

    /// <summary>
    /// The first argument of a call whose name ends at <paramref name="at"/>: past its type arguments, to the
    /// first comma or close at depth zero. Null when what follows is not a call.
    /// </summary>
    private static string? FirstArgument(string text, int at)
    {
        if (at < text.Length && text[at] == '<')
        {
            var depth = 0;
            for (; at < text.Length; at++)
            {
                if (text[at] == '<') depth++;
                else if (text[at] == '>' && text[at - 1] != '=' && --depth == 0) break;
            }

            at++;
        }

        while (at < text.Length && char.IsWhiteSpace(text[at])) at++;
        if (at >= text.Length || text[at] != '(') return null;

        var start = ++at;
        var nesting = 0;
        char? quote = null;
        for (; at < text.Length; at++)
        {
            var c = text[at];
            if (quote is { } open)
            {
                if (c == open && text[at - 1] != '\\') quote = null;
            }
            else if (c is '\'' or '"' or '`') quote = c;
            else if (c is '(' or '[' or '{') nesting++;
            else if (c is ')' or ']' or '}' && nesting > 0) nesting--;
            else if (c is ',' or ')' && nesting == 0) return text[start..at];
        }

        return null;
    }

    /// <summary>Each route's mark, and the partial it is in, read off the module's sources.</summary>
    private static List<(string Route, string File)> Marks()
    {
        var modules = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        return Directory.EnumerateFiles(modules, "DriverModule*.cs")
            .SelectMany(path => Mark().Matches(File.ReadAllText(path))
                .Select(mark => (mark.Groups["route"].Value, Path.GetFileName(path))))
            .ToList();
    }

    /// <summary>The domain a partial is named for — `Trees` for `DriverModule.Trees.cs` — or null for the core.</summary>
    private static string? DomainOf(string file) =>
        Partial().Match(file) is { Success: true } named ? named.Groups["domain"].Value : null;

    [Fact]
    public void Every_route_the_page_calls_has_a_handler_in_the_table()
    {
        var (calls, forwarded) = PageCalls();

        // A scan that found nothing would pass on a renamed helper: it has to see the page's calls.
        Assert.True(calls.Count > 40, $"the scan found {calls.Count} call(s) onto DAORIS.DRIVER; it should see the page's");
        // A new helper that forwards its route to `call` hides every route its callers name from this scan.
        Assert.True(forwarded == Forwarders.Length,
            $"{forwarded} send(s) pass on a route they were given, and {Forwarders.Length} helper(s) are known to: "
            + "name the new helper in Forwarders, so the routes its callers give it are read.");

        var unanswered = calls.Where(call => !DriverModule.Routes.Contains(call.Route))
            .Select(call => $"{call.File} calls `{call.Route}`, which no handler answers");
        Assert.Empty(unanswered);
    }

    [Fact]
    public void Every_handler_in_the_table_is_called_by_the_page_or_a_test()
    {
        var called = PageCalls().Calls.Select(call => call.Route).ToHashSet(StringComparer.Ordinal);
        var tests = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules.Tests");
        var asked = string.Join('\n', Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && Path.GetFileName(path) != $"{nameof(DriverModuleRoutesTests)}.cs")
            .Select(File.ReadAllText));

        var idle = DriverModule.Routes
            .Where(route => !called.Contains(route) && !asked.Contains($"\"{route}\"", StringComparison.Ordinal))
            .Select(route => $"`{route}` is answered, and neither the page nor a test asks it");
        Assert.Empty(idle);
    }

    /// <summary>
    /// A feature's C# door and its page bridge carry one domain name: a route is marked in the partial named as
    /// the bridge file that sends it (`DriverModule.Trees.cs` for `bridge/trees.ts`), never in the core.
    /// </summary>
    [Fact]
    public void Each_route_is_marked_in_the_partial_named_as_the_bridge_file_that_sends_it()
    {
        var marks = Marks();
        // The source scan is the table: a mark it missed, or one the table does not hold, fails here first.
        Assert.Equal(DriverModule.Routes.Order(StringComparer.Ordinal), marks.Select(mark => mark.Route).Order(StringComparer.Ordinal));
        Assert.Empty(marks.Where(mark => DomainOf(mark.File) is null).Select(mark => $"`{mark.Route}` is marked in {mark.File}, the core"));

        var partialOf = marks.ToDictionary(mark => mark.Route, mark => DomainOf(mark.File)!, StringComparer.Ordinal);
        var astray = PageCalls().Calls
            .Where(call => call.File != "shell.ts")
            .Where(call => partialOf.TryGetValue(call.Route, out var domain)
                && !string.Equals($"{domain}.ts", call.File, StringComparison.OrdinalIgnoreCase))
            .Select(call => $"bridge/{call.File} sends `{call.Route}`, answered in DriverModule.{partialOf[call.Route]}.cs");
        Assert.Empty(astray);
    }

    /// <summary>
    /// The desktop README carries a row per domain, and a new door edits its own domain's row: each route is
    /// named in the row of the partial that answers it.
    /// </summary>
    [Fact]
    public void The_readme_names_each_route_in_its_domains_row()
    {
        var readme = File.ReadAllLines(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "README.md"));
        var problems = new List<string>();
        foreach (var domain in Marks().GroupBy(mark => DomainOf(mark.File)!))
        {
            var row = readme.FirstOrDefault(line => line.StartsWith($"| `{domain.Key.ToLowerInvariant()}` |", StringComparison.Ordinal));
            if (row is null)
            {
                problems.Add($"the README has no row for `{domain.Key.ToLowerInvariant()}`");
                continue;
            }

            problems.AddRange(domain.Where(mark => !row.Contains($"`{mark.Route}`", StringComparison.Ordinal))
                .Select(mark => $"the README's `{domain.Key.ToLowerInvariant()}` row does not name `{mark.Route}`"));
        }

        Assert.Empty(problems);
    }

    [GeneratedRegex(@"'DAORIS\.DRIVER'\s*,\s*'(?<route>[A-Z][A-Z_]*)'")]
    private static partial Regex DirectInvoke();

    [GeneratedRegex(@"'(?<route>[A-Z][A-Z_]*)'")]
    private static partial Regex RouteLiteral();

    [GeneratedRegex(@"^[A-Za-z_]\w*$")]
    private static partial Regex BareName();

    [GeneratedRegex(@"\[DriverRoute\(""(?<route>[A-Z][A-Z_]*)""\)\]")]
    private static partial Regex Mark();

    [GeneratedRegex(@"^DriverModule\.(?<domain>[A-Za-z]+)\.cs$")]
    private static partial Regex Partial();
}
