using System.IO.Pipes;
using Daoris.Knowledge;
using Daoris.Knowledge.Http;

namespace Daoris.Service.Http.Tests;

/// <summary>
/// The person key (PERSONDOOR1a, D156 point 1; the person-door design §2.1, §2.3): 32 random bytes in base64url, read from
/// the first line of the host's standard input when its starter asks, compared in constant time, and proved by an HMAC of
/// a nonce. The rows here are the twin's (<c>.claude/knowledge/twins.md</c>): the shell that mints and hands the key
/// (PERSONDOOR1g) spells the variable, the header, the key's form and the proof again, and its tests hold these rows.
/// </summary>
public sealed class PersonKeyTests
{
    /// <summary>32 bytes of 0..31: the key the keyed host is handed.</summary>
    public const string Counting = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";

    /// <summary>32 zero bytes.</summary>
    public const string Zeros = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void The_spellings_the_shell_twins()
    {
        Assert.Equal("DAORIS_PERSON_KEY_ON_INPUT", PersonKey.InputVariable);
        Assert.Equal("Daoris-Person", PersonKey.Header);
        Assert.Equal(32, PersonKey.Size);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData(" 1", false)]
    public void Only_a_1_asks_for_the_key(string? value, bool asks) => Assert.Equal(asks, PersonKey.Asks(value));

    /// <summary>
    /// A key is the one canonical base64url spelling of 32 bytes, 43 characters, without padding; the spaces around it are
    /// not part of it, a Windows line's <c>\r</c> among them.
    /// </summary>
    [Theory]
    [InlineData(Counting, true)]
    [InlineData(Zeros, true)]
    [InlineData(Counting + "\r", true)]
    [InlineData("  " + Counting + " ", true)]
    [InlineData("_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-_-4", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    // 42 characters, 44, and the same bytes with base64's padding.
    [InlineData("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh", false)]
    [InlineData(Counting + "A", false)]
    [InlineData(Counting + "=", false)]
    // Base64's own alphabet, which base64url spells `-` and `_`.
    [InlineData("+/+/+/+/+/+/+/+/+/+/+/+/+/+/+/+/+/+/+/+/+/4", false)]
    // The same 32 zero bytes with the last character's spare bits set: a second spelling of one key.
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA AAA", false)]
    [InlineData("a key the person would type, which is no key", false)]
    public void A_key_is_the_canonical_base64url_of_32_bytes(string? line, bool reads) =>
        Assert.Equal(reads, PersonKey.Parse(line) is not null);

    [Fact]
    public void A_key_matches_its_own_text_and_nothing_else()
    {
        var key = PersonKey.Parse(Counting)!;

        Assert.True(key.Matches(Counting));
        Assert.False(key.Matches(Zeros));
        Assert.False(key.Matches(Counting[..42]));
        Assert.False(key.Matches(Counting + "A"));
        Assert.False(key.Matches(Counting.ToLowerInvariant()));
        Assert.False(key.Matches(""));
        Assert.False(key.Matches(null));
    }

    /// <summary>
    /// The proof (design §2.3): HMAC-SHA-256 under the key's 32 bytes, of the nonce's UTF-8 bytes, in base64url without
    /// padding. The vectors were made outside .NET (Node's <c>crypto.createHmac</c>), so this side and the shell's are
    /// each held to the same answers rather than to each other's code.
    /// </summary>
    [Theory]
    [InlineData(Zeros, "daoris-prove", "MGRkjOR5VZ2oQWQ11ToICsIzQjg_1oDAH-ok_0Lm3A0")]
    [InlineData(Counting, "a nonce, 一个", "gQMDWolsIQRnw-_d19k3A1c9E6ey2aP9-8RL_n_pJ50")]
    public void The_proof_is_an_hmac_of_the_nonce_under_the_key(string key, string nonce, string proof) =>
        Assert.Equal(proof, PersonKey.Parse(key)!.Prove(nonce));

    /// <summary>Not asked, a host has no key and refuses nothing, and its input is never so much as opened.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public void A_host_not_asked_reads_no_key_and_never_opens_its_input(string? asked)
    {
        var (key, refusal) = PersonKey.FromInput(
            ServiceMode.Local, asked, () => throw new InvalidOperationException("the input was opened"), TimeSpan.FromSeconds(1));

        Assert.Null(key);
        Assert.Null(refusal);
    }

    [Fact]
    public void Asked_it_reads_the_key_from_the_first_line_and_leaves_the_rest()
    {
        var input = new StringReader(Counting + "\nwhat follows is the stop's to watch\n");

        var (key, refusal) = PersonKey.FromInput(ServiceMode.Local, "1", () => input, TimeSpan.FromSeconds(5));

        Assert.Null(refusal);
        Assert.True(key!.Matches(Counting));
        Assert.Equal("what follows is the stop's to watch", input.ReadLine());
    }

    /// <summary>
    /// A starter that asked and handed no key, or a line that is not one, does not start the host: a starter that meant to
    /// gate the doors and failed must not leave them open by its mistake.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("\n" + Counting + "\n")]
    [InlineData("not a key\n")]
    [InlineData("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh\n")]
    public void Asked_a_line_that_is_no_key_refuses_the_start(string written)
    {
        var (key, refusal) = PersonKey.FromInput(ServiceMode.Local, "1", () => new StringReader(written), TimeSpan.FromSeconds(5));

        Assert.Null(key);
        Assert.Contains("the first line of standard input is not a person key", refusal);
        Assert.Contains("The host did not start, so no door was left open.", refusal);
        Assert.DoesNotContain(Counting, refusal);
    }

    /// <summary>A starter that never writes is waited for, then refused, rather than leaving a host that never answers.</summary>
    [Fact]
    public void Asked_and_never_written_to_it_refuses_once_the_wait_passes()
    {
        var writer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        var reader = new AnonymousPipeClientStream(PipeDirection.In, writer.ClientSafePipeHandle);
        try
        {
            var (key, refusal) = PersonKey.FromInput(
                ServiceMode.Local, "1", () => new StreamReader(reader), TimeSpan.FromMilliseconds(300));

            Assert.Null(key);
            Assert.Contains("no key arrived on standard input within", refusal);
        }
        finally
        {
            // The writer first: its close ends the read still waiting, and then the reader goes with nothing pending on it.
            writer.Dispose();
            reader.Dispose();
        }
    }

    /// <summary>A shared host reads no person key (design §6): asking one for it is refused, and its input is never read.</summary>
    [Fact]
    public void A_shared_host_asked_for_a_key_refuses_and_reads_nothing()
    {
        var (key, refusal) = PersonKey.FromInput(
            ServiceMode.Shared, "1", () => throw new InvalidOperationException("the input was opened"), TimeSpan.FromSeconds(1));

        Assert.Null(key);
        Assert.Contains("a shared host reads none (D156 point 6)", refusal);
    }
}
