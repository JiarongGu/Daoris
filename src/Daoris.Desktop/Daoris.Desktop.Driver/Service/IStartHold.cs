namespace Daoris.Driver;

/// <summary>A start's hold as the service's opens ask it: an open in a tree is made only inside the hold on that tree's repository.</summary>
public interface IStartHold
{
    /// <summary>Throws where the hold is let go of or does not hold <paramref name="tree"/>'s repository.</summary>
    void Assert(string what, string? tree = null);
}
