using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1b's census (D212's note of 2026-10-25, the foundation design §10; X4-1.2): every say to the service's say door outside
/// the tests passes the producer's key, or is named here as a legacy case with why it has none. A producer's retry is kept as one
/// word only where the word carries a key, so a new keyless caller is a second word waiting to happen.
/// </summary>
/// <remarks>
/// Read from the sources of the driver, its host and the modules, by name wherever they lie (STRUCT5a). A call is the door's when its
/// receiver is a service client by the name the code gives it; one reached under another name is not seen, which the floor on the
/// calls seen holds against a scan that stopped reading.
/// </remarks>
public sealed partial class SayKeysCensusTests
{
    /// <summary>The names the code gives a service client the say door is called on.</summary>
    private static readonly HashSet<string> Receivers = ["service", "_service", "client", "context.Service", "_context.Service", "world.Service"];

    /// <summary>
    /// Each legacy case, by the file that holds it: a say that carries no producer's key, with what stands for one. A file here is
    /// read by the census as holding exactly the keyless calls it names, so a second one fails.
    /// </summary>
    private static readonly Dictionary<string, (int Calls, string Why)> Legacy = new()
    {
        ["ChannelReach.cs"] = (1, "a channel message's words, kept under the message the ask already holds (SESSONE2e, COWORK1d): the channel is their identity and their queue"),
        ["SessionsCommand.cs"] = (1, "the terminal's verb where no loop drives the home: it keeps the words on the record for the next loop and holds none itself, so nothing restarts under it"),
    };

    [GeneratedRegex(@"(?<receiver>[\w.]+)\s*\.SayAsync\(")]
    private static partial Regex Call();

    [Fact]
    public void Every_say_to_the_say_door_passes_a_key_or_is_a_named_legacy_case()
    {
        var seen = 0;
        var keyless = new List<string>();
        foreach (var project in new[] { SourceFiles.DriverProject, "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules" })
        {
            foreach (var path in SourceFiles.All(project, "*.cs"))
            {
                var source = File.ReadAllText(path);
                foreach (Match call in Call().Matches(source))
                {
                    if (!Receivers.Contains(call.Groups["receiver"].Value)) continue;
                    seen++;
                    if (!Arguments(source, call.Index + call.Length).Contains("key:", StringComparison.Ordinal)) keyless.Add(Path.GetFileName(path));
                }
            }
        }

        Assert.True(seen >= 14, $"the scan saw {seen} says to the door: it stopped reading, or the callers were renamed");
        var named = keyless.GroupBy(file => file).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(
            Legacy.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value.Calls)),
            named.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value)));
    }

    /// <summary>
    /// The shell's own words to the door carry the key a held word kept: the held record path and the pump pass <c>word.Key</c>, and the
    /// forward to the conversation a record went on in passes the word's level, key and display choice as the first say did.
    /// </summary>
    [Fact]
    public void The_shells_held_word_is_said_with_its_key_on_the_record_path_and_in_the_forward()
    {
        var source = File.ReadAllText(SourceFiles.Find("Daoris.Desktop.Modules", "SessionWords.cs"));

        Assert.Equal(2, Regex.Matches(source, Regex.Escape("service.SayAsync(id, word.Text, word.Files, ct, key: word.Key)")).Count);
        Assert.Contains("SayAsync(next, word.Text, Uploads(id, word.Files), preface: null, word.Door, ct, word.Level, word.Key, word.Choice)", source);
        Assert.DoesNotContain("service.SayAsync(id, word.Text, word.Files, ct)", source);
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
}
