using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One sentence a door was seen saying when its agent refused a start for its sign-in (ROSTER1b): the words, when, and where.</summary>
/// <param name="Sentence">The door's failure as the driver recorded it, the door's preface included.</param>
/// <param name="Seen">The day it was seen, ISO 8601.</param>
/// <param name="Channel">Which door, which agent, and what was running.</param>
public sealed record RecordedSignIn(string Sentence, string Seen, string Channel, string? Version = null);

/// <summary>
/// The words one agent refuses a start for its sign-in in (ROSTER1b, D150 §5.3): its entry, declared on its toolchain as
/// <see cref="AccountAgent.SignIn"/>, beside AGT3b's <c>Refused</c> and TOOL4a's <c>Limits</c>, and read as a limit is (D125
/// point 1): from the door's failure, apart from the agent's words, and never from the transcript.
/// </summary>
/// <remarks>
/// <para><b>An entry grows only with a recorded sentence.</b> Every marker is matched by one of <see cref="Recorded"/>, and every
/// recorded sentence by a marker; <c>SignInRefusalsTests</c> holds it both ways.</para>
/// <para><b>Words, not the code.</b> The protocol names <c>-32000</c> its <i>authentication required</i>, and the door keeps it
/// (<see cref="AcpRefusal.Code"/>), but JSON-RPC leaves -32000 to -32099 to each server, and the protocol stub answers every
/// failed turn with -32000; nor does the code travel past the door, whose failure reaches the conclusion as its sentence.</para>
/// </remarks>
/// <param name="Markers">Patterns, each matched case-insensitively against the whole failure.</param>
/// <param name="Recorded">The sentences the markers stand on.</param>
public sealed record SignInWords(IReadOnlyList<string> Markers, IReadOnlyList<RecordedSignIn> Recorded);

/// <summary>Whether a door's failure is its agent refusing the start for its sign-in (ROSTER1b): the one reader, pure.</summary>
public static class SignInRefusals
{
    /// <summary>True where <paramref name="entry"/> recognises <paramref name="failure"/>; an agent with no entry reads every failure as a failure.</summary>
    public static bool Read(SignInWords? entry, string? failure) =>
        entry is not null && !string.IsNullOrWhiteSpace(failure) && entry.Markers.Any(marker => Matches(marker, failure));

    /// <summary>One pattern against one failure; a pattern this build cannot run matches nothing, as an answer it cannot read is no evidence.</summary>
    internal static bool Matches(string pattern, string text)
    {
        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (Exception error) when (error is ArgumentException or RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
