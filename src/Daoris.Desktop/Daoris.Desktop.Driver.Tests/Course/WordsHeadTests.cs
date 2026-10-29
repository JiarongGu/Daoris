using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1d-ii part 1 (the foundation design §3.4 rules 1 and 2): the walk a say caller makes where a host with the <c>head</c> mark refuses its
/// word <c>continued</c>, naming the line's head. The same say goes again at the head, whole, and a second <c>continued</c> follows the next head; a
/// walk that comes back to a record it asked ends, as does one past its bound, never looping; a word's files go to each head's folder before it is
/// said there.
/// </summary>
public sealed class WordsHeadTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-head-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static SayAnswer Continued(string head) =>
        new(false, $"went on in `{head}`, its line's head") { Refusal = SayAnswer.Continued, Head = head };

    private static SayAnswer Kept(string at) =>
        new(true, $"Kept for session `{at}`.") { Word = new SaidWordView($"w-{at}", "words", DateTimeOffset.UnixEpoch, [], true) };

    /// <summary>A say door answering each record from <paramref name="answers"/>, kept where it names none; every record asked, in order.</summary>
    private static Func<string, Task<SayAnswer>> Door(List<string> asked, Dictionary<string, string> answers) => at =>
    {
        asked.Add(at);
        return Task.FromResult(answers.TryGetValue(at, out var head) ? Continued(head) : Kept(at));
    };

    [Fact]
    public async Task A_word_refused_continued_is_said_again_at_the_head_and_a_second_continued_follows_the_next_head()
    {
        var asked = new List<string>();
        var carried = new List<(string From, string To)>();

        var (said, at) = await WordsHead.FollowAsync("a", Door(asked, new() { ["a"] = "b", ["b"] = "c" }), (from, to) => carried.Add((from, to)));

        Assert.Equal(["a", "b", "c"], asked);
        Assert.Equal([("a", "b"), ("b", "c")], carried);
        Assert.Equal(("c", true, "w-c"), (at, said.Kept, said.Word!.Id));
    }

    [Fact]
    public async Task A_record_that_is_its_lines_head_is_said_to_once_and_nothing_is_carried()
    {
        var asked = new List<string>();
        var carried = 0;

        var (said, at) = await WordsHead.FollowAsync("a", Door(asked, []), (_, _) => carried++);

        Assert.Equal(["a"], asked);
        Assert.Equal(("a", true, 0), (at, said.Kept, carried));
    }

    /// <summary>A planted cycle, a's head b and b's a: the walk ends at the record it would ask again, refused with the driver's sentence and no head.</summary>
    [Fact]
    public async Task A_walk_that_comes_back_to_a_record_it_asked_ends_and_never_loops()
    {
        var asked = new List<string>();

        var (said, at) = await WordsHead.FollowAsync("a", Door(asked, new() { ["a"] = "b", ["b"] = "a" }));

        Assert.Equal(["a", "b"], asked);
        Assert.Equal(("b", false, SayAnswer.Continued, (string?)null), (at, said.Kept, said.Refusal, said.Head));
        Assert.Contains("`a`", said.Message);
        Assert.Contains("came back", said.Message);
    }

    /// <summary>A host naming a new head at every say: the walk ends at its bound, refused, rather than following without end.</summary>
    [Fact]
    public async Task A_walk_past_its_bound_ends()
    {
        var asked = new List<string>();

        var (said, _) = await WordsHead.FollowAsync("r0", at =>
        {
            asked.Add(at);
            return Task.FromResult(Continued($"r{asked.Count}"));
        });

        Assert.Equal(HeadWalk.Most, asked.Count);
        Assert.Equal((false, SayAnswer.Continued, (string?)null), (said.Kept, said.Refusal, said.Head));
    }

    /// <summary>A word's files, kept for the record it was said to, kept again in the head's folder under the same names, so its go-on finds them.</summary>
    [Fact]
    public void A_words_files_are_carried_to_the_heads_folder_under_their_names()
    {
        ChatFiles.Keep(_home, "a", [new ChatUpload("notes.md", "the notes"u8.ToArray()), new ChatUpload("shot.png", [1, 2, 3])]);

        ChatFiles.Carry(_home, "a", "b", ["notes.md", "shot.png", "gone.txt"]);

        Assert.Equal("the notes", File.ReadAllText(ChatFiles.Find(_home, "b", "notes.md")!.Path));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(ChatFiles.Find(_home, "b", "shot.png")!.Path));
        Assert.Null(ChatFiles.Find(_home, "b", "gone.txt"));
        Assert.NotNull(ChatFiles.Find(_home, "a", "notes.md"));
    }
}
