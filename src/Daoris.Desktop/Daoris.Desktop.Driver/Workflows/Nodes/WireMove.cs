using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>One move the evaluation made through <see cref="NodeLifecycle.Apply"/>: the move, what it came to, and why.</summary>
/// <param name="Detail">The lapse's reason, or the receipt or input no longer current; null where the why says it all.</param>
public sealed record WireMove(string Node, NodeMove Move, NodeMoved Moved, string Why, string? Detail);
