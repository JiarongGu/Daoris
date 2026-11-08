namespace Daoris.Driver;

/// <summary>The landing supervisor's decisions as the node pass asks them: whether one is live in this run, and what became of its word.</summary>
public interface ILandingWords
{
    /// <summary>Whether the decision is released and live in this run: its window past, its word kept, nothing since closing it.</summary>
    bool Live(string decision);

    /// <summary>The decision's word is applied (<c>went-on</c>), dropped (<c>stale</c>), or replaced by the person's (<c>superseded</c>).</summary>
    void Settled(string decision, SupervisorState to, string? why);
}
