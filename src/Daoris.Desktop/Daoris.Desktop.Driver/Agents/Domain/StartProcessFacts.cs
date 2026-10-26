namespace Daoris.Driver;

/// <summary>A start reads the landing's public facts; Workplace retains the plan and its replay graph.</summary>
public interface IStartLanding
{
    string Form { get; }
    string Target { get; }
    string Source { get; }
    string? Plugin { get; }
    string? Problem { get; }
    bool AutoAccept { get; }
    bool Own { get; }
    bool Stands { get; }
    // Opaque reads preserve the public serialized properties without moving or interpreting the landing graph.
    object? LeftOut { get; }
    object? Lands { get; }
    object? Conflict { get; }
}

/// <summary>The resolved commit rule a start reads, with the rule still owned by Settings.</summary>
public interface IStartCommits
{
    CommitRule Rule { get; }
    string Source { get; }
    void Deconstruct(out CommitRule rule, out string source) => (rule, source) = (Rule, Source);
}
