namespace Daoris.Knowledge;

/// <summary>
/// One session that serves the team of an ask: the team is these rows, and nothing else of it is stored. A lead is the session that wrote a plan
/// revision, of the ask's own plan or of the split of one part.
/// </summary>
/// <param name="Ask">The ask the team builds.</param>
/// <param name="Part">The part the session serves; null for the ask's own plan.</param>
/// <param name="Role">What the session is on the team: <see cref="Lead"/>.</param>
/// <param name="Session">The session.</param>
/// <param name="Revision">The newest plan revision the session wrote or served.</param>
/// <param name="At">When the row last moved.</param>
public sealed record TeamMember(string Ask, string? Part, string Role, string Session, int Revision, DateTimeOffset At)
{
    /// <summary>The session that writes the plan.</summary>
    public const string Lead = "lead";
}
