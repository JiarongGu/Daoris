namespace Daoris.Driver;

/// <summary>A piece of background work a session's harness says it runs, with when this process heard it announced (FABRIC-PIPELINE1c).</summary>
public sealed record RunningTask(string Id, string Name, DateTimeOffset Since);
