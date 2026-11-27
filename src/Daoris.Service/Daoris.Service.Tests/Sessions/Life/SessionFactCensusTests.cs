using System.Text.RegularExpressions;

namespace Daoris.Service.Tests;

/// <summary>
/// The structure census of the sessions' journal: no source writes a session's stored status without appending its witnessed fact. A file whose
/// SQL writes the <c>sessions</c> table's state appends a fact (<c>Journal.AppendAsync</c>), and every door that moves or opens a record
/// names its witness, so a fact's witness is the caller's reason, never the store's own word.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class SessionFactCensusTests
{
    /// <summary>The service's production sources: its core, its hosts and its connector, the tests and the build's output aside.</summary>
    private static IEnumerable<string> Sources()
    {
        var service = Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Service");
        return Directory.EnumerateFiles(service, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(service, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) is var parts
                           && !parts[0].Contains("Tests", StringComparison.Ordinal) && !parts[0].Contains("Rehearsals", StringComparison.Ordinal)
                           && !parts.Contains("bin") && !parts.Contains("obj"));
    }

    [GeneratedRegex(@"UPDATE\s+sessions\s+SET\s[^""]*?\bstate\s*=|INSERT\s+INTO\s+sessions\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex StateWrite();

    [GeneratedRegex(@"\b(SetStateAsync|AdvanceAsync|CreateAsync)\(")]
    private static partial Regex Door();

    [Fact]
    public void A_file_that_writes_a_sessions_state_appends_its_fact()
    {
        var writers = Sources().Where(path => StateWrite().IsMatch(File.ReadAllText(path))).ToList();
        var bare = writers.Where(path => !File.ReadAllText(path).Contains("Journal.AppendAsync(", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(writers);
        Assert.True(bare.Count == 0, $"writes a session's state with no fact appended: {string.Join(", ", bare.Select(Path.GetFileName))}");
    }

    [Fact]
    public void Every_door_that_moves_or_opens_a_session_names_its_witness()
    {
        var unnamed = new List<string>();
        var calls = 0;
        foreach (var path in Sources())
        {
            var text = File.ReadAllText(path);
            foreach (Match door in Door().Matches(text))
            {
                var line = text[(text.LastIndexOf('\n', door.Index) + 1)..door.Index];
                // A declaration of a door is no call of one: its parameters are the census's next door's to name.
                if (Regex.IsMatch(line, @"\b(private|public|internal|protected)\b.*\bTask\b")) continue;
                var span = Arguments(text, door.Index + door.Length - 1);
                // A create is a session's where it names a kind of session: every door that opens one does, and no other store's create.
                if (door.Groups[1].Value == "CreateAsync" && !span.Contains("SessionKind", StringComparison.Ordinal)) continue;
                calls++;
                if (!Regex.IsMatch(span, @"\bwitness\b")) unnamed.Add($"{Path.GetFileName(path)}:{text[..door.Index].Count(c => c == '\n') + 1}");
            }
        }

        Assert.True(calls >= 15, $"the census found {calls} doors: it no longer reads them");
        Assert.True(unnamed.Count == 0, $"moves or opens a session with no witness named: {string.Join(", ", unnamed)}");
    }

    /// <summary>The text between the parenthesis at <paramref name="open"/> and the one that closes it.</summary>
    private static string Arguments(string text, int open)
    {
        var depth = 0;
        for (var at = open; at < text.Length; at++)
        {
            if (text[at] == '(') depth++;
            else if (text[at] == ')' && --depth == 0) return text[open..(at + 1)];
        }

        return text[open..];
    }
}
