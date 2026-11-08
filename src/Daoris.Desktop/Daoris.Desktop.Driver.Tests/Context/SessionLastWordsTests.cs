using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A session's last words are read from its transcript while a writer still holds the file open, as Windows lets a reader that shares with it.</summary>
public sealed class SessionLastWordsTests : IDisposable
{
    private readonly string _directory = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "last-words", Guid.NewGuid().ToString("N")[..8]);

    public SessionLastWordsTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => GitSeed.Discard(_directory);

    [Fact]
    public void A_transcript_a_writer_holds_open_is_read_for_its_last_words()
    {
        var transcript = Path.Combine(_directory, "s1.log");
        using var writer = new FileStream(transcript, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write("→ a tool\nWhich port should it use?\n"u8);
        writer.Flush();

        Assert.Equal("Which port should it use?", SessionLastWords.LastWords(transcript));
    }
}
