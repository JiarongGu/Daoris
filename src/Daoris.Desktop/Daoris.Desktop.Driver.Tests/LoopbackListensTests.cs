using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A test listens on loopback alone (FIREWALL1): a listener on every interface makes Windows ask its firewall about the test
/// host, once for each path a test host runs from, so every new worktree and gate tree asked again. `TcpListener.Create`
/// listens on every interface, as `IPAddress.Any` and `IPAddress.IPv6Any` do; the suites' own servers listen on
/// `IPAddress.Loopback` or `127.0.0.1`. The rule is held on sources made in memory, then over every test project here.
/// </summary>
public sealed class LoopbackListensTests
{
    [Fact]
    public void A_listen_on_every_interface_is_named_and_a_loopback_one_is_not()
    {
        const string source = """
            namespace N;
            public sealed class ATests
            {
                // TcpListener.Create(0) in a comment is no listen.
                private readonly TcpListener _all = TcpListener.Create(0);
                private readonly TcpListener _any = new(IPAddress.Any, 0);
                private readonly TcpListener _six = new(IPAddress.IPv6Any, 0);
                private readonly TcpListener _here = new(IPAddress.Loopback, 0);
            }
            """;

        Assert.Equal(
            [
                "A.cs:5: TcpListener.Create( listens on every interface: listen on IPAddress.Loopback",
                "A.cs:6: IPAddress.Any listens on every interface: listen on IPAddress.Loopback",
                "A.cs:7: IPAddress.IPv6Any listens on every interface: listen on IPAddress.Loopback",
            ],
            LoopbackListens.Check([("A.cs", source)]));
    }

    [Fact]
    public void Every_test_project_here_listens_on_loopback_alone()
    {
        var root = WorkspaceRoot.Folder;
        var projects = new[] { Path.Combine(root, "src", "Daoris.Desktop"), Path.Combine(root, "src", "Daoris.Service") }
            .SelectMany(folder => Directory.EnumerateDirectories(folder, "*Tests"))
            .ToList();
        var sources = projects
            .SelectMany(project => Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(path => Path.GetRelativePath(project, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] is not ("bin" or "obj"))
                .Select(path => (Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path))))
            // This file names the patterns it refuses, in its cases and its sentences.
            .Where(source => !source.Item1.EndsWith("/LoopbackListensTests.cs", StringComparison.Ordinal))
            .ToList();

        Assert.True(projects.Count >= 4 && sources.Count > 500, $"the walk found {projects.Count} test projects and {sources.Count} sources: it no longer reaches them");
        var breaches = LoopbackListens.Check(sources);
        Assert.True(breaches.Count == 0, "each a listen on every interface, which makes Windows ask its firewall about the test host:\n" + string.Join('\n', breaches));
    }
}

/// <summary>The listens on every interface in sources, a line each, comments aside.</summary>
internal static partial class LoopbackListens
{
    [GeneratedRegex(@"TcpListener\.Create\(|IPAddress\.IPv6Any\b|IPAddress\.Any\b")]
    private static partial Regex Wide();

    public static IReadOnlyList<string> Check(IEnumerable<(string Path, string Text)> sources)
    {
        var found = new List<string>();
        foreach (var (path, text) in sources)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i];
                var comment = code.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) code = code[..comment];
                foreach (Match match in Wide().Matches(code))
                {
                    found.Add($"{path}:{i + 1}: {match.Value} listens on every interface: listen on IPAddress.Loopback");
                }
            }
        }

        return found;
    }
}
