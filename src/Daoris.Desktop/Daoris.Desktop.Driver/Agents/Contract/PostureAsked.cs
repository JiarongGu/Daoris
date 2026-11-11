namespace Daoris.Driver;

/// <summary>
/// The posture a start asks of a protocol door: the modes it names in the agent's own words, whether the session must
/// set one or not open, and whether the person answers its permission requests. Help and Participants build
/// one; <see cref="Wire"/> is the one string the open carries, and <see cref="Parse"/> its one reader.
/// </summary>
/// <param name="Modes">The modes in order, <c>|</c> between them, the first the agent offers being set.</param>
/// <param name="Required">An agent that does not offer one fails the start rather than running in a narrower mode under the label.</param>
/// <param name="PersonAnswers">The session's permission requests are held for the person.</param>
public sealed record PostureAsked(string Modes, bool Required = false, bool PersonAnswers = false)
{
    private const char RequiredMark = '!';
    private const char PersonAnswersMark = '?';

    /// <summary>The string a session's open carries.</summary>
    public string Wire() => $"{(Required ? RequiredMark : "")}{(PersonAnswers ? PersonAnswersMark : "")}{Modes}";

    /// <summary>A posture as its string says it, marks in either order before the modes; the empty posture for none.</summary>
    public static PostureAsked Parse(string? posture)
    {
        var (required, held, at) = (false, false, 0);
        while (posture is not null && at < posture.Length && posture[at] is RequiredMark or PersonAnswersMark)
        {
            required |= posture[at] == RequiredMark;
            held |= posture[at] == PersonAnswersMark;
            at++;
        }

        return new(posture?[at..] ?? "", required, held);
    }

    public static string? Held(string? posture) => posture is { Length: > 0 } ? (Parse(posture) with { PersonAnswers = true }).Wire() : posture;

    /// <summary>The modes as a sentence says them: <c>auto|acceptEdits</c> is <c>`auto` or `acceptEdits`</c>.</summary>
    public string Spelled() => string.Join(
        " or ",
        Modes.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(mode => $"`{mode}`"));
}
