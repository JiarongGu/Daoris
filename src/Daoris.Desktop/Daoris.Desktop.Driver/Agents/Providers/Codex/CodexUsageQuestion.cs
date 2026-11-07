namespace Daoris.Driver;

/// <summary>Codex's way of being asked an account's windows: <c>codex app-server</c>'s JSON-RPC over stdio, a reading standing five minutes.</summary>
public sealed record CodexUsageQuestion(string Harness, IReadOnlyList<string> Command, IReadOnlyList<RecordedFrame> Recorded)
    : UsageQuestion(Harness, Command, Recorded)
{
    public override TimeSpan Fresh => CodexUsage.Fresh;

    public override string Source => CodexUsage.Method;

    public override Task<IReadOnlyList<WindowReading>?> AskAsync(UsageAsk ask, CancellationToken ct) =>
        CodexUsage.ReadAsync(CodexUsage.Prepare(ask.Run, ask.Toolchain, ask.ProfileHome, ask.Managed), CodexUsage.Patience, ct);
}
