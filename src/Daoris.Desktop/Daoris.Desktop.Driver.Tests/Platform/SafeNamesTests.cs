using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DEDUP6 (D166's note): one path rule and three id rules, where ten copies disagreed on <c>\</c>, <c>:</c>, <c>..</c> and
/// control characters. Each caller keeps its own refusal sentence; these tests hold only the yes or no.
/// </summary>
public sealed class SafeNamesTests
{
    [Theory]
    [InlineData("a", true)]
    [InlineData("a/b.c", true)]
    [InlineData("docs/x.md", true)]
    [InlineData(".claude/INDEX.md", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/../b", false)]
    [InlineData("a/./b", false)]
    [InlineData("a//b", false)]
    [InlineData("a/", false)]
    [InlineData("/a", false)]
    [InlineData("\\a", false)]
    [InlineData("a\\b", false)]
    [InlineData("a:b", false)]
    [InlineData("C:/x", false)]
    [InlineData("a\0b", false)]
    [InlineData("a\tb", false)]
    [InlineData("a\nb", false)]
    [InlineData("a\u007fb", false)]
    [InlineData("a.", false)]
    [InlineData("a ", false)]
    [InlineData(" a", true)]
    [InlineData("a./b", false)]
    [InlineData("a /b", false)]
    [InlineData("CON", false)]
    [InlineData("con.txt", false)]
    [InlineData("a/NUL", false)]
    [InlineData("COM1", false)]
    [InlineData("LPT9.log", false)]
    [InlineData("COM0", true)]
    [InlineData("console", true)]
    public void A_repository_path_joined_to_the_disk(string path, bool expected) =>
        Assert.Equal(expected, SafeNames.IsRepositoryPath(path));

    [Theory]
    [InlineData("abc", true)]
    [InlineData("a-b_c.d", true)]
    [InlineData("0123456789abcdef", true)]
    [InlineData("", false)]
    [InlineData("a:b", false)]
    [InlineData("..", false)]
    [InlineData("a..b", false)]
    [InlineData("a.", false)]
    [InlineData("a\n", false)]
    [InlineData("CON", false)]
    [InlineData("nul.txt", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    public void A_folder_id_named_under_the_home(string id, bool expected) =>
        Assert.Equal(expected, SafeNames.IsFolderId(id));

    /// <summary>A session id keeps <c>:</c> for a remote's <c>machine:id</c>; a local file name does not.</summary>
    [Theory]
    [InlineData("abc-123", true, true)]
    [InlineData("host:abc", true, false)]
    [InlineData("abc\n", false, false)]
    [InlineData("abc.", false, false)]
    [InlineData("a..b", false, false)]
    [InlineData("con", false, false)]
    [InlineData("host:con", false, false)]
    public void A_session_id_and_a_local_one(string id, bool session, bool local)
    {
        Assert.Equal(session, SessionEventText.IsId(id));
        Assert.Equal(local, SessionEventText.IsLocalId(id));
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("Run_1-b", true)]
    [InlineData("a\n", false)]
    [InlineData("", false)]
    [InlineData("a.b", false)]
    [InlineData("a:b", false)]
    public void A_plain_id(string id, bool expected) => Assert.Equal(expected, SafeNames.IsPlainId(id));

    [Fact]
    public void A_plain_id_is_at_most_64_characters()
    {
        Assert.True(SafeNames.IsPlainId(new string('a', 64)));
        Assert.False(SafeNames.IsPlainId(new string('a', 65)));
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("a-b", true)]
    [InlineData("a1-b2-c3", true)]
    [InlineData("", false)]
    [InlineData("-a", false)]
    [InlineData("a-", false)]
    [InlineData("a--b", false)]
    [InlineData("A", false)]
    [InlineData("a_b", false)]
    [InlineData("a\n", false)]
    public void A_kebab_id(string id, bool expected) => Assert.Equal(expected, SafeNames.IsKebabId(id));
}
