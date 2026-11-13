namespace Daoris.Knowledge;

/// <summary>An order between two parts, with its reason. <see cref="After"/> is a build dependency; <see cref="Orders"/> is only a landing order.</summary>
/// <param name="Part">The part that waits or lands second.</param>
/// <param name="On">The part it follows.</param>
/// <param name="Kind"><see cref="After"/> or <see cref="Orders"/>.</param>
/// <param name="Milestone">For <see cref="After"/>, what it waits for: <c>oriented</c>, <c>submitted</c> or <c>landed</c> (the default).</param>
/// <param name="Why">Why the order stands.</param>
public sealed record TeamEdge(string Part, string On, string Kind, string? Milestone, string Why)
{
    /// <summary>A build dependency: the other part's commits are this one's base.</summary>
    public const string After = "after";

    /// <summary>A landing order only: the two share a file and either order works, one at a time.</summary>
    public const string Orders = "orders";

    /// <summary>The milestones an <see cref="After"/> edge waits for.</summary>
    public static IReadOnlyList<string> Milestones { get; } = ["oriented", "submitted", "landed"];
}
