using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1d-ii part 1's census of the say door's callers (the foundation design §3.4 rule 2, §10 row d-ii): a host with the <c>head</c> mark
/// refuses a word said to a record that is not its line's head as <c>continued</c>, naming the head, and every caller of the say door is named here
/// with what it does then. <i>Follows</i>: the same say again at the head, whole, by <c>WordsHead.FollowAsync</c>, which ends a walk that comes back
/// to a record it asked. <i>Own</i>: the caller reads the head itself and says the word there through its own judgement. <i>Not on</i>: the word is
/// not passed on, and why. A new call is a failing line until it is named, and each named call must still be made as it says.
/// </summary>
/// <remarks>
/// Read from the sources of the driver, its host and the modules, by name wherever they lie (STRUCT5a), as <see cref="SayKeysCensusTests"/> and
/// <see cref="SettledReadersCensusTests"/> are, a call being the door's when its receiver is a service client by the name the code gives it. The
/// dispatchers' own keeps are named too (<see cref="Dispatchers"/>): a fresh dispatcher answered <c>continued</c> must hold, queue, steer, attempt and
/// write nothing on its old door, never fall back to holding the word in this process as it does for a refusal from a host before COWORK1c.
/// </remarks>
public sealed partial class ContinuedReadersCensusTests
{
    private enum How
    {
        Follows,
        Own,
        NotOn,
    }

    /// <summary>Each file's calls to the say door, in the order they stand, with how each meets <c>continued</c>.</summary>
    private static readonly Dictionary<string, (How How, string Why)[]> Sites = new()
    {
        ["SaidDoor.cs"] =
        [
            (How.Own, "the dispatchers' door: the answer, its head with it, goes to the dispatcher that asked, which holds nothing on it (the dispatchers below)"),
        ],
        ["SessionSay.cs"] =
        [
            (How.Own, "the headless door: the request said again at the head, by the head's inbox where this loop runs it, else at its record, under the request's key, at its level, its files carried to the head's folder"),
        ],
        ["SessionWords.cs"] =
        [
            (How.Own, "the shell's record path: the word said again at the head through the shell's whole judge (the head's inbox, its turns or its record), with its key, level, files and display choice"),
            (How.Own, "the pump: a held word said again at the head once, under its key, off the held file once the head took it; a second `continued` follows the next head, and a walk back to a record asked is said lost"),
        ],
        ["SessionsCommand.cs"] =
        [
            (How.Follows, "the terminal's verb where no loop drives the home: the same say at the head, its files carried to the head's folder, the head named in its line"),
        ],
        ["WordsLeft.cs"] =
        [
            (How.Follows, "a failed run's words kept again: each said at the head whole, its files carried, shown in the conversation of the record that kept it"),
        ],
        ["ReopenTelling.cs"] =
        [
            (How.Follows, "Daoris's reopen and go-on words through the reopen's doors: said at the head under the fact's key, the mark kept by the session the reopen names"),
        ],
        ["DriverWatch.cs"] =
        [
            (How.Follows, "the integration transport's word to a session about its line's conflict (INTEGRATE1g): said at the head under the integration's key"),
        ],
        ["AutoLandingWorld.cs"] =
        [
            (How.Follows, "the landing's word to a session, said through the lander's world: said at its head under the landing's key"),
        ],
        ["TreesConsole.cs"] =
        [
            (How.Follows, "the terminal's carry-on word to a session its Accept carried on (LANDBACK1 part 1): said at its head under its key"),
        ],
        ["TreesRoutes.cs"] =
        [
            (How.Follows, "the window's Accept carrying a session on (LANDBACK1 part 1): Daoris's word said at its head under its key, never the person's"),
        ],
        ["ChatRunner.cs"] =
        [
            (How.Follows, "a refused start's moved words (MODELROLE1d2), the person's: kept at the head of the record they wait on, their files carried"),
            (How.NotOn, "Daoris's restart word to a conversation this shell runs, at its close: the word is this run's, and refused it is logged unkept as any refusal is"),
        ],
        ["ChatGoOn.cs"] =
        [
            (How.NotOn, "Daoris's limit word before this record's own go-on: it exists to wake this record, whose go-on stands aside for a record that went on; at the head it would wait with nothing to take it off, so the wake ends with the service's sentence"),
        ],
        ["LimitWaits.cs"] =
        [
            (How.NotOn, "the live wake's limit word, kept for the run this process holds (`running`): it goes into that run's own turn in this process alone, as a word a host before the door would not keep"),
        ],
        ["AgentCourseWiring.cs"] =
        [
            (How.NotOn, "the transient episode's go-on, kept for the run this process holds (`running`): not kept, the try goes on with the plain block, as for any refusal"),
        ],
        ["DriverContinuations.cs"] =
        [
            (How.NotOn, "the limit word of a cool-off, for this record's own go-on, whose conclusion takes it off this record: refused, the line goes on as the carry-on it was planned"),
        ],
        ["Driver.cs"] =
        [
            (How.NotOn, "the supervisor's release, whose door stays today's (O7-F12): it answers the park this record holds, and reads the refusal's word as it reads any, held for the owner"),
        ],
        ["ChannelReach.cs"] =
        [
            (How.NotOn, "a channel message's words: the channel is their queue (COWORK1d), so refused they wait for the entry's next part, which the channel reaches"),
        ],
    };

    /// <summary>
    /// The dispatchers' keeps and the callers of a dispatcher's keep: each reads the head before it holds a word, and none falls back to holding
    /// a word answered <c>continued</c> in this process.
    /// </summary>
    private static readonly Dictionary<string, string> Dispatchers = new()
    {
        ["DrivenInbox.cs"] = "a keep answered `continued` holds, sends, steers, attempts and takes nothing and answers InboxKeep.Continued with the head; KeepOrHoldAsync never holds it here instead; a reword so answered changes nothing here",
        ["ChatTurns.cs"] = "a keep answered `continued` queues, steers, attempts and writes nothing on its door and answers TurnKept.Head; a reword so answered changes nothing here",
        ["ChatRunner.cs"] = "the turns' head answered as ChatSaid.Head, with no reach and no word",
        ["SessionSay.cs"] = "the headless door: the inbox's head is where the request is said again",
        ["SessionWords.cs"] = "the inbox's and the turns' head is where the shell says the word again, through its whole judge",
        ["ReopenTelling.cs"] = "the running session's inbox answered `continued`: the reopen's words are said at the head through the reopen's doors",
        ["LongCalls.cs"] = "Daoris's word about this run's call: a record that went on is told nothing, as a closed inbox is",
    };

    /// <summary>Callers that reach the say door only through another named here: what they call.</summary>
    private static readonly Dictionary<string, (string Calls, string Why)> Through = new()
    {
        ["ConversationRoutes.cs"] = ("_loop.Words", "the conversation's box: its words go to the shell's SessionWords.SayAsync, whose record path, inbox and turns follow the head"),
    };

    [GeneratedRegex(@"(?<receiver>[\w.]+)\s*\.SayAsync\(")]
    private static partial Regex Say();

    [GeneratedRegex(@"\bWordsHead\.FollowAsync\(")]
    private static partial Regex Follow();

    [GeneratedRegex(@"\.KeepOrHoldAsync\(|\bdoor\.KeepAsync\(|\bTurns\.KeepAsync\(|\.SayKeptAsync\(")]
    private static partial Regex Keep();

    /// <summary>The names the code gives a service client the say door is called on.</summary>
    private static readonly HashSet<string> Receivers = ["service", "_service", "client", "context.Service", "_context.Service", "world.Service"];

    private static IEnumerable<(string File, string Source)> Sources()
    {
        foreach (var project in new[] { SourceFiles.DriverProject, "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules" })
        {
            foreach (var path in SourceFiles.All(project, "*.cs")) yield return (Path.GetFileName(path), File.ReadAllText(path));
        }
    }

    /// <summary>The call's argument text's end, to its closing parenthesis, a string literal's own parentheses passed over.</summary>
    private static int End(string source, int from)
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
                    return at;
            }
        }

        return source.Length;
    }

    [Fact]
    public void Every_say_to_the_say_door_is_named_with_how_it_meets_continued_and_is_made_as_it_says()
    {
        var seen = 0;
        var found = new Dictionary<string, List<bool>>(StringComparer.Ordinal);
        foreach (var (file, source) in Sources())
        {
            var follows = Follow().Matches(source).Select(call => (From: call.Index, To: End(source, call.Index + call.Length))).ToList();
            foreach (Match call in Say().Matches(source))
            {
                if (!Receivers.Contains(call.Groups["receiver"].Value)) continue;
                seen++;
                if (!found.TryGetValue(file, out var calls)) found[file] = calls = [];
                calls.Add(follows.Any(span => call.Index > span.From && call.Index < span.To));
            }
        }

        Assert.True(seen >= 18, $"the scan saw {seen} says to the door: it stopped reading, or the callers were renamed");
        Assert.Equal(Sites.Keys.Order(StringComparer.Ordinal), found.Keys.Order(StringComparer.Ordinal));
        foreach (var (file, named) in Sites)
        {
            var calls = found[file];
            Assert.True(named.Length == calls.Count, $"{file} makes {calls.Count} says to the door and the census names {named.Length}");
            for (var at = 0; at < named.Length; at++)
            {
                Assert.False(string.IsNullOrWhiteSpace(named[at].Why));
                Assert.True(
                    calls[at] == (named[at].How == How.Follows),
                    $"{file}'s say {at + 1} is named {named[at].How} ({named[at].Why}) but is {(calls[at] ? "" : "not ")}made through WordsHead.FollowAsync");
            }
        }
    }

    [Fact]
    public void Each_caller_that_reads_the_head_itself_names_it_in_its_own_code()
    {
        var sources = Sources().GroupBy(each => each.File).ToDictionary(group => group.Key, group => string.Join("\n", group.Select(each => each.Source)));
        foreach (var (file, named) in Sites.Where(pair => pair.Value.Any(site => site.How == How.Own)))
        {
            Assert.Matches(@"\bHead\b", sources[file]);
        }

        var keeps = sources.Where(pair => Keep().IsMatch(pair.Value)).Select(pair => pair.Key).Order(StringComparer.Ordinal);
        Assert.Equal(Dispatchers.Keys.Order(StringComparer.Ordinal), keeps);
        foreach (var (file, how) in Dispatchers)
        {
            Assert.True(Regex.IsMatch(sources[file], @"\bHead\b|InboxKeep\.Continued"), $"{file} says it reads the head ({how}) and names none");
        }

        foreach (var (file, (calls, why)) in Through)
        {
            Assert.True(sources.TryGetValue(file, out var source), $"{file} is named as calling through {calls} ({why}) but is not a source");
            Assert.Matches(Regex.Escape(calls) + @"\s*\.SayAsync\(", source);
        }
    }
}
