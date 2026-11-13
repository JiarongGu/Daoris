using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census of the say door's callers: a host with the <c>head</c> mark
/// refuses a word said to a record that is not its line's head as <c>continued</c>, naming the head, and every caller of the say door is named here
/// with what it does then. <i>Follows</i>: the same say again at the head, whole, by <c>WordsHead.FollowAsync</c>, which ends a walk that comes back
/// to a record it asked. <i>Own</i>: the caller reads the head itself and says the word there through its own judgement. <i>Not on</i>: the word is
/// not passed on, and why. A new call is a failing line until it is named, and each named call must still be made as it says.
/// </summary>
/// <remarks>
/// Read from the sources of the driver, its host and the modules, by name wherever they lie, as <see cref="SayKeysCensusTests"/> and
/// <see cref="SettledReadersCensusTests"/> are, a call being the door's when its receiver is a service client by the name the code gives it. The
/// dispatchers' own keeps are named too (<see cref="Dispatchers"/>): a fresh dispatcher answered <c>continued</c> must hold, queue, steer, attempt and
/// write nothing on its old door, never fall back to holding the word in this process as it does for a refusal from an older host.
/// </remarks>
[Trait(Category.Name, Category.Census)]
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
        ],
        ["HeldWordsQueue.cs"] =
        [
            (How.Own, "the pump: a held word said again at the head once, under its key, off the held file once the head took it; a second `continued` follows the next head, and a walk back to a record asked is said lost"),
        ],
        ["SessionsKeeping.cs"] =
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
        ["WatchNodes.cs"] =
        [
            (How.Follows, "the workflow runtime's word to the work about a review's changes (DECIDE1c): said at the head under the changes' key"),
            (How.NotOn, "a run's `resolve` ask to the newest session in its tree (PROCSTEPS1d): the resolver watches that record for the ask's key and its turn, so refused `continued` the ask is untold and the tree put back, never said to a head whose turn it does not watch"),
            (How.Follows, "the integration transport's word to a session about its line's conflict (INTEGRATE1g): said at the head under the integration's key"),
        ],
        ["IntegrateRoutes.cs"] =
        [
            (How.Follows, "the owner's *rebase it* of a superseded commit (INTEGRATE1h): said at the head under the request's and the commit's key"),
        ],
        ["ReviewLandingWorld.cs"] =
        [
            (How.Follows, "the landing's word to a session, said through the lander's world: said at its head under the landing's key"),
        ],
        ["SessionsKeeping.cs"] =
        [
            (How.Follows, "the terminal's say to a conversation no driver here took: the same say again at the head of the record's line, its files carried to the head's folder first"),
        ],
        ["TogetherRoutes.cs"] =
        [
            (How.Follows, "Bring them together's word to a line's head (LINESTOGETHER1d): Daoris's word said at its head under the operation's key"),
        ],
        ["ChatClosing.cs"] =
        [
            (How.NotOn, "Daoris's restart word to a conversation this shell runs, at its close: the word is this run's, and refused it is logged unkept as any refusal is"),
        ],
        ["ChatReplacement.cs"] =
        [
            (How.Follows, "a refused start's moved words, the person's: kept at the head of the record they wait on, their files carried"),
        ],
        ["ChatGoOn.cs"] =
        [
            (How.NotOn, "Daoris's limit word before this record's own go-on: it exists to wake this record, whose go-on stands aside for a record that went on; at the head it would wait with nothing to take it off, so the wake ends with the service's sentence"),
        ],
        ["DriverChecks.cs"] =
        [
            (How.NotOn, "a check's verdict said to the session that asked, under the check's key: a record the line left refuses it and the next look says it again, so no head is walked to"),
        ],
        ["LimitWaits.cs"] =
        [
            (How.NotOn, "the live wake's limit word, kept for the run this process holds (`running`): it goes into that run's own turn in this process alone, as a word a host before the door would not keep"),
        ],
        ["AgentCourseWiring.cs"] =
        [
            (How.NotOn, "the transient episode's go-on, kept for the run this process holds (`running`): not kept, the try goes on with the plain block, as for any refusal"),
        ],
        ["ContinuationLimits.cs"] =
        [
            (How.NotOn, "the limit word of a cool-off, for this record's own go-on, whose conclusion takes it off this record: refused, the line goes on as the carry-on it was planned"),
        ],
        ["SupervisorLook.cs"] =
        [
            (How.NotOn, "the supervisor's release, whose door stays today's (O7-F12): it answers the park this record holds, and reads the refusal's word as it reads any, held for the owner"),
        ],
        ["OverlapsToldLook.cs"] =
        [
            (How.NotOn, "the overlap word to a running unit (INTEGRATE1l), kept for the run this process holds (`running`): it goes into that run's own turn in this process alone, and the pair is marked told only once the say returned"),
        ],
        ["ChannelReach.cs"] =
        [
            (How.NotOn, "a channel message's words: the channel is their queue (COWORK1d), so refused they wait for the entry's next part, which the channel reaches"),
        ],
    };

    /// <summary>The dispatchers' keeps and the callers of a dispatcher's keep: each reads the head before it holds a word, and none falls back to holding a word answered <c>continued</c> in this process.</summary>
    private static readonly Dictionary<string, string> Dispatchers = new()
    {
        ["InboxKeeps.cs"] = "a keep answered `continued` holds, sends, steers, attempts and takes nothing and answers InboxKeep.Continued with the head; KeepOrHoldAsync never holds it here instead; a reword so answered changes nothing here",
        ["TurnKeeps.cs"] = "a keep answered `continued` queues, steers, attempts and writes nothing on its door and answers TurnKept.Head; a reword so answered changes nothing here",
        ["ChatWords.cs"] = "the turns' head answered as ChatSaid.Head, with no reach and no word",
        ["SessionSay.cs"] = "the headless door: the inbox's head is where the request is said again",
        ["SessionWords.cs"] = "the inbox's and the turns' head is where the shell says the word again, through its whole judge",
        ["ReopenTelling.cs"] = "the running session's inbox answered `continued`: the reopen's words are said at the head through the reopen's doors",
        ["QuietWord.cs"] = "the watcher's word to a quiet session or about a long call: a record that went on is told nothing, as a closed inbox is, and a quiet episode is forgotten",
    };

    /// <summary>A keep made for the dispatcher that holds it, which is named above and reads the head itself.</summary>
    private static readonly Dictionary<string, (string Calls, string Why)> Forwards = new()
    {
        ["ChatRunner.cs"] = ("_words.SayKeptAsync(", "the conversation runner's door: its keep is ChatWords's, which answers the head as ChatSaid.Head"),
        ["KeepRetry.cs"] = ("door.KeepAsync(", "TurnKeeps's keep, asked again under its key: the answer goes back to TurnKeeps, which answers TurnKept.Head"),
    };

    /// <summary>Callers that reach the say door only through another named here: what they call.</summary>
    private static readonly Dictionary<string, (string Calls, string Why)> Through = new()
    {
        ["ConversationRoutes.cs"] = ("_loop.Words", "the conversation's box: its words go to the shell's SessionWords.SayAsync, whose record path, inbox and turns follow the head"),
    };

    [GeneratedRegex(@"\bWordsHead\.FollowAsync\(")]
    private static partial Regex Follow();

    [GeneratedRegex(@"\.KeepOrHoldAsync\(|\bdoor\.KeepAsync\(|\bKeepRetry\.KeepAsync\(|\bTurns\.KeepAsync\(|\.SayKeptAsync\(")]
    private static partial Regex Keep();

    /// <summary>The names the code gives a service client the say door is called on.</summary>
    private static readonly HashSet<string> Receivers = ["service", "_service", "client", "context.Service", "_context.Service", "Context.Service", "world.Service", "wiring.Service"];


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
        foreach (var (file, source, says) in SayDoorSources.All)
        {
            var follows = Follow().Matches(source).Select(call => (From: call.Index, To: End(source, call.Index + call.Length))).ToList();
            foreach (var call in says)
            {
                if (!Receivers.Contains(call.Receiver)) continue;
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
        var sources = SayDoorSources.All.GroupBy(each => each.File).ToDictionary(group => group.Key, group => string.Join("\n", group.Select(each => each.Text)));
        foreach (var (file, named) in Sites.Where(pair => pair.Value.Any(site => site.How == How.Own)))
        {
            Assert.Matches(@"\bHead\b", sources[file]);
        }

        var keeps = sources.Where(pair => Keep().IsMatch(pair.Value) && !Forwards.ContainsKey(pair.Key)).Select(pair => pair.Key).Order(StringComparer.Ordinal);
        Assert.Equal(Dispatchers.Keys.Order(StringComparer.Ordinal), keeps);
        foreach (var (file, (calls, why)) in Forwards)
        {
            Assert.True(sources.TryGetValue(file, out var forwarded), $"{file} is named as forwarding {calls} ({why}) but is not a source");
            Assert.Contains(calls, forwarded);
        }
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
