using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's native door's dialect: its <c>stream-json</c> lines read by <see cref="ClaudeStreamJson"/>, a person's words framed as
/// one user line and a turn's stop as a <c>control_request</c>, each the shape the binary took in the probe the evidence records.
/// </summary>
internal sealed class ClaudeNativeDialect : INativeDialect
{
    public IStreamMapper Mapper() => new ClaudeStreamJson();

    /// <summary>A person's message as one <c>stream-json</c> user line, one turn per line.</summary>
    public string FrameMessage(string text) => JsonSerializer.Serialize(new
    {
        type = "user",
        message = new { role = "user", content = new[] { new { type = "text", text } } },
    });

    /// <summary>
    /// The turn's stop as a <c>control_request</c> of subtype <c>interrupt</c>: the binary answers with a <c>control_response</c>, the
    /// turn ends <c>aborted_streaming</c> or <c>aborted_tools</c>, and the session takes the next turn (stream-json evidence, § Stopping a
    /// turn).
    /// </summary>
    public string FrameInterrupt() => JsonSerializer.Serialize(new
    {
        type = "control_request",
        request_id = $"daoris-{Guid.NewGuid():N}",
        request = new { subtype = "interrupt" },
    });
}
