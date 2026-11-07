using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A choice refused at the open with nothing else to set on the same session: the open ends before its prompt.</summary>
public sealed class AcpChoiceNotOffered(ModelRefusal refusal)
    : DriverException($"the agent did not offer the {refusal.Category} `{refusal.Value}` asked ({refusal.Code}): nothing was prompted.")
{
    /// <summary>What it refused.</summary>
    public ModelRefusal Refusal { get; } = refusal;
}

/// <summary>
/// A word a run is to write whose attempt its record does not acknowledge: it is not written, and
/// waits on its record for the session's next go-on, which sends it once. Thrown before the wire, never after a write.
/// </summary>
public sealed class AcpWordsUnmarked(string message) : DriverException(message)
{
    /// <summary>The sentence a word left waiting by an unacknowledged mark is said with, and a resumed run's failure carries.</summary>
    public const string Sentence = "the session's record did not mark the words attempted, so none was written: they wait on it for the session's next go-on";
}

/// <summary>
/// A word turned away at a run's door: the run has ended, or left a silent turn of a closed quest, before
/// it is written, so it never reaches the wire and goes back to the driver with the run's other words (<see cref="AcpSession.HandedBack"/>).
/// Thrown before the wire, never after a write, and never the door's failure: nothing more is said of it.
/// </summary>
public sealed class AcpTurnedAway() : DriverException(Sentence)
{
    /// <summary>What the words' delivery is told.</summary>
    public const string Sentence = "the session's run had ended before these words were written: they went back to wait for its next run";
}

/// <summary>
/// The agent's refusal of a call: a JSON-RPC error, said as a sentence, with what its <c>data</c> carried kept beside
/// it.
/// </summary>
/// <remarks>
/// 🔴 <b>An adapter may put the words that matter in <c>data</c>, not in the message.</b> <c>codex-acp</c> answers a usage
/// limit with the message <c>Internal error</c> and nothing after it, and the limit's sentence in <c>data.message</c>; Claude
/// Code's adapter puts its sentence in the message and the SDK's category in <c>data.errorKind</c>
/// So the sentence the data carries is said after the message, where
/// the conclusion and the limit reader read the door's failure, and the kind is kept for a reader that reads a field.
/// </remarks>
/// <param name="message">The refusal as a sentence: the error's message, then the data's, where it adds one.</param>
/// <param name="said">The data's <c>message</c>, or null where it carries none.</param>
/// <param name="errorKind">The data's <c>errorKind</c>, or null where it carries none.</param>
/// <param name="code">The error's JSON-RPC <c>code</c>, or null where it gives none.</param>
/// <param name="words">The agent's own sentence, without this client's preface; the message where unsaid.</param>
/// <param name="reason">The data's <c>reason</c>, or null where it carries none.</param>
public sealed class AcpRefusal(
    string message, string? said = null, string? errorKind = null, int? code = null, string? words = null, string? reason = null)
    : DriverException(message)
{
    /// <summary>What the error's <c>data.message</c>, or else its <c>data.details</c>, said; null where it said nothing.</summary>
    public string? Said => said;

    /// <summary>
    /// The error's <c>data.reason</c>: <c>codex-acp</c> says <c>thread_active_writer</c> there when another
    /// Codex client holds the conversation. A field a reader acts on, where the sentence beside it is only for a person.
    /// </summary>
    public string? Reason => reason;

    /// <summary>The error's <c>data.errorKind</c>, or null where it carried none.</summary>
    public string? ErrorKind => errorKind;

    /// <summary>
    /// The error's <c>code</c>: a resume refused <c>resource_not_found</c> is a conversation the agent no longer
    /// has, any other a refusal. Null where the error gives none.
    /// </summary>
    public int? Code => code;

    /// <summary>The agent's own sentence, as the error and its data said it, without this client's preface.</summary>
    public string Words => words ?? message;

    /// <summary>
    /// The refusal a JSON-RPC <c>error</c> member says, read without trusting its shape: a message that is not
    /// text is the error as written, and a <c>data</c> that is not an object, or fields in it that are not text, add
    /// nothing. The data's message is said after the error's only where it adds words the error's does not already say.
    /// <c>claude-agent-acp</c> puts its sentence in <c>data.details</c> instead (<c>Invalid value for config option model: …</c>),
    /// read where the data has no message.
    /// </summary>
    internal static AcpRefusal Of(JsonElement error)
    {
        var message = Json.Text(error, "message") ?? error.GetRawText();
        var data = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("data", out var d) ? d : default;
        var said = (Json.Text(data, "message") ?? Json.Text(data, "details")) is { } words && !string.IsNullOrWhiteSpace(words)
            ? words.Trim()
            : null;
        var sentence = said is null || message.Contains(said, StringComparison.Ordinal) ? message : $"{message}: {said}";
        var code = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var c)
                   && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var number)
            ? number
            : (int?)null;
        return new AcpRefusal(
            $"the ACP agent refused the call: {sentence}", said, Json.Text(data, "errorKind"), code, sentence,
            Json.Text(data, "reason"));
    }
}
