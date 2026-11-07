namespace Daoris.Driver;

/// <summary>
/// Claude Code's way of being asked an account's windows: the native <c>claude</c> in stream-json mode answering a
/// <c>get_usage</c> control request, a reading standing a minute because its read is the only whole-account one a walk ranks on.
/// </summary>
public sealed record ClaudeUsageQuestion(string Harness, IReadOnlyList<string> Command, IReadOnlyList<RecordedFrame> Recorded)
    : UsageQuestion(Harness, Command, Recorded)
{
    public override TimeSpan Fresh => ClaudeUsage.Floor;

    public override string Source => ClaudeUsage.Source;

    public override async Task<IReadOnlyList<WindowReading>?> AskAsync(UsageAsk ask, CancellationToken ct)
    {
        // Run in an empty folder of the home's own, never the repository or OS temp: the CLI keys its project state by its
        // working directory, and a read is no project's.
        var folder = Path.Combine(ask.ProfileHome, "usage-read");
        Directory.CreateDirectory(folder);
        var info = CodexUsage.Prepare(ask.Run, ask.Toolchain, ask.ProfileHome, ask.Managed, folder);
        return await CodexUsage.ReadAsync(
            info, ClaudeUsage.Patience, ct,
            (incoming, outgoing, patience, token) => ClaudeUsage.AskWithinAsync(incoming, outgoing, patience, ask.Now, token))
            .ConfigureAwait(false);
    }
}
