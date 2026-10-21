using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The words a main window's page hands the shell's notifier (NOTIFY2d, <c>SET_NOTICE_WORDS</c>, the notification design §3.3): kept
/// whole as last told, so a change of language is one telling, and bounded, since a page's words are a page's.
/// </summary>
public sealed class NoticeWordsTests
{
    private static JsonElement Told(object words) => JsonSerializer.SerializeToElement(words);

    [Fact]
    public void Nothing_is_handed_until_a_page_tells_it()
    {
        Assert.Null(new NoticeWords().Now);
    }

    [Fact]
    public void A_telling_replaces_the_last_whole()
    {
        var words = new NoticeWords();

        words.Told(Told(new Dictionary<string, string> { ["notifications.system.parked.title"] = "{{repository}}: a session waits on you", ["person.route.SWEEP"] = "clean up session branches" }));
        words.Told(Told(new Dictionary<string, string> { ["notifications.system.parked.title"] = "{{repository}}：一个会话等你处理" }));

        Assert.Equal(
            [new KeyValuePair<string, string>("notifications.system.parked.title", "{{repository}}：一个会话等你处理")],
            words.Now!.OrderBy(each => each.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Only_words_are_kept_and_a_blank_word_is_none()
    {
        var words = new NoticeWords();

        words.Told(JsonDocument.Parse("""{"a":"kept","b":3,"c":null,"d":{"e":"f"},"g":"  ","h":""}""").RootElement);

        Assert.Equal(["a"], words.Now!.Keys);
    }

    /// <summary>The page hands a few hundred; past the bound a word, or a key, is not kept, and none longer than the bound is.</summary>
    [Fact]
    public void Its_bounds_keep_no_more_than_a_page_hands()
    {
        var words = new NoticeWords();
        var many = Enumerable.Range(0, NoticeWords.MostWords + 5).ToDictionary(at => $"person.route.R{at:D5}", at => $"act {at}");
        many["long"] = new string('x', NoticeWords.Longest + 1);
        many[new string('k', NoticeWords.Longest + 1)] = "a key too long";

        words.Told(Told(many));

        Assert.Equal(NoticeWords.MostWords, words.Now!.Count);
        Assert.DoesNotContain("long", words.Now.Keys);
        Assert.DoesNotContain(words.Now.Keys, key => key.Length > NoticeWords.Longest);
    }

    [Fact]
    public void A_telling_that_holds_no_words_object_hands_none()
    {
        var words = new NoticeWords();
        words.Told(Told(new Dictionary<string, string> { ["a"] = "b" }));

        words.Told(JsonDocument.Parse("[]").RootElement);

        Assert.Null(words.Now);
    }
}
