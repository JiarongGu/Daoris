using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 row p's census of the readers of a keyed answer (D212's note of the row; the foundation design §3.4 rule 5, round 8's
/// <i>C7-1.1</i>, <i>O7-F12</i>): a keyed say's or hand's answer may be <b>settled</b>, its own terminal kind with no word, and every reader of
/// one handles it before it reads a word. Each file outside the tests that says a word to the service under a key, hands a helper's or a partner's
/// answer, or keeps a word through the dispatchers' door is named here with how it reads a settled answer: <i>first</i>, naming it in its own
/// code before any word is read, or <i>as kept</i>, asking only whether the words are on the line and never dereferencing a word. A new reader
/// is a failing line until it is named, and a file named here must still read it as it says.
/// </summary>
/// <remarks>
/// Read from the sources of the driver, its host and the modules, by name wherever they lie (STRUCT5a), as <see cref="SayKeysCensusTests"/> is.
/// The supervisor's release says with <c>supervised</c> and no settled request: its door stays today's (O7-F12), which the client's tests hold.
/// </remarks>
public sealed partial class SettledReadersCensusTests
{
    /// <summary>Readers that handle a settled answer in their own code, before any word: what they do with it.</summary>
    private static readonly Dictionary<string, string> First = new()
    {
        ["ServiceClient.cs"] = "reads `settled` before `said` and carries it as SayAnswer.Settled, kept, with no word",
        ["HelperDoors.cs"] = "reads `settled` before `said` and answers HelperHanded.Settled, with no word",
        ["PartnerDoors.cs"] = "reads `settled` before `said` and answers it as the fifth member of a hand, with no word",
        ["ChatTurns.cs"] = "answers a retry of a word it holds already: holds, queues, steers, attempts and takes nothing",
        ["DrivenInbox.cs"] = "answers a retry of a word it holds already: holds, sends, steers, attempts and takes nothing",
        ["ChatRunner.cs"] = "maps the turns' settled answer to ChatSaid.Settled, with no reach and no word",
        ["SessionWords.cs"] = "the pump takes its held word off and shows nothing; the record path, the inbox and the chat answer sent, no word, nothing kept on the ask",
        ["SessionSay.cs"] = "the headless door answers sent with no word, shows nothing and keeps nothing on the ask",
        ["DriverHelpers.cs"] = "the helper pass dispatches and records nothing, the ask marked handed",
        ["DriverPartners.cs"] = "the partner's hand dispatches and records nothing, the piece marked handed",
    };

    /// <summary>Readers that ask only whether the words are on the line: a settled answer reads as kept, with no word to take an id from.</summary>
    private static readonly Dictionary<string, string> AsKept = new()
    {
        ["ChatGoOn.cs"] = "Daoris's limit word before a go-on: kept, the go-on goes on",
        ["LimitWaits.cs"] = "the live wake's limit word: kept without a word, it goes in this process alone as a word a host before the door would not keep",
        ["DriverContinuations.cs"] = "the limit word of a cool-off: kept with no word, so no word id is handed on and the line goes on as the carry-on it was planned",
        ["AgentCourseWiring.cs"] = "the episode's go-on: no word to keep, so the try's go-on is the plain block",
        ["WordsLeft.cs"] = "a word kept again on the record: kept, with no view to show",
        ["ReopenTelling.cs"] = "Daoris's reopen and go-on words: kept, the mark written",
        ["DriverWatch.cs"] = "the integration transport's word about a conflict: kept, the conflict told to the session",
        ["AutoLandingWorld.cs"] = "the landing's word to a session, said through the lander's world: kept",
        ["TreesConsole.cs"] = "the terminal's tree word to a session: kept",
        ["TreesRoutes.cs"] = "the window's Accept carrying a session on (LANDBACK1 part 1): kept, and the person's yes is kept with it",
        ["SaidDoor.cs"] = "the door hands the answer to the dispatcher, which reads it first",
        ["LongCalls.cs"] = "Daoris's word to a session whose call ran long, through its inbox: held or settled, it reads only that the inbox was not closed",
    };

    /// <summary>The doors that stay today's: they ask for no settled answer and read what they always read.</summary>
    private static readonly Dictionary<string, string> Today = new()
    {
        ["Driver.cs"] = "the supervisor's release says with `supervised` and asks no settled answer (O7-F12): its door answers its key only while its word waits, and it reads that word's id as it always did",
    };

    [GeneratedRegex(@"(?<receiver>[\w.]+)\s*\.SayAsync\(")]
    private static partial Regex Say();

    [GeneratedRegex(@"\.(Helpers|Partners)\s*\.HandAsync\(|\bdoor\.KeepAsync\(|\.KeepOrHoldAsync\(")]
    private static partial Regex Other();

    /// <summary>The names the code gives a service client the say door is called on.</summary>
    private static readonly HashSet<string> Receivers = ["service", "_service", "client", "context.Service", "_context.Service", "world.Service"];

    private static IEnumerable<(string File, string Source)> Sources()
    {
        foreach (var project in new[] { SourceFiles.DriverProject, "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules" })
        {
            foreach (var path in SourceFiles.All(project, "*.cs")) yield return (Path.GetFileName(path), File.ReadAllText(path));
        }
    }

    /// <summary>The call's argument text, to its closing parenthesis, a string literal's own parentheses passed over.</summary>
    private static string Arguments(string source, int from)
    {
        var depth = 1;
        for (var at = from; at < source.Length; at++)
        {
            switch (source[at])
            {
                case '"':
                    for (at++; at < source.Length && source[at] != '"'; at++)
                    {
                        if (source[at] == '\\') at++;
                    }

                    break;
                case '(':
                    depth++;
                    break;
                case ')' when --depth == 0:
                    return source[from..at];
            }
        }

        return source[from..];
    }

    [Fact]
    public void Every_reader_of_a_keyed_answer_is_named_with_how_it_reads_a_settled_one()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (file, source) in Sources())
        {
            var keyed = Say().Matches(source).Any(call =>
                Receivers.Contains(call.Groups["receiver"].Value) && Arguments(source, call.Index + call.Length).Contains("key:", StringComparison.Ordinal));
            if (keyed || Other().IsMatch(source)) found.Add(file);
        }

        // The doors and the classes that hold the answer's type declare it rather than read a keyed call; they are named for being read by name.
        var named = new SortedSet<string>([.. First.Keys, .. AsKept.Keys, .. Today.Keys], StringComparer.Ordinal);
        var readers = new SortedSet<string>(found.Where(file => file is not "ServiceClient.cs"), StringComparer.Ordinal);
        Assert.Empty(readers.Except(named));
        Assert.Empty(named.Except(readers.Concat(["ServiceClient.cs", "HelperDoors.cs", "PartnerDoors.cs", "ChatRunner.cs", "SaidDoor.cs", "SessionSay.cs"])));
    }

    [Fact]
    public void Each_reader_named_first_names_settled_in_its_own_code_and_each_read_as_kept_reads_no_word_unguarded()
    {
        var sources = Sources().GroupBy(each => each.File).ToDictionary(group => group.Key, group => string.Join("\n", group.Select(each => each.Source)));
        foreach (var (file, how) in First)
        {
            Assert.True(sources.TryGetValue(file, out var source), $"{file} is named first but is not a source");
            Assert.True(Regex.IsMatch(source, @"\bSettled\b|settled"), $"{file} says it reads a settled answer first ({how}) and names none");
        }

        foreach (var (file, how) in AsKept)
        {
            Assert.True(sources.TryGetValue(file, out var source), $"{file} is named as kept but is not a source");
            Assert.DoesNotMatch(@"\b(said|answer)\.Word\.\w", source);
            Assert.DoesNotMatch(@"\b(said|answer)\.Word!", source);
            Assert.False(string.IsNullOrWhiteSpace(how));
        }
    }
}
