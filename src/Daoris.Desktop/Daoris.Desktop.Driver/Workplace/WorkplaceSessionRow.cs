namespace Daoris.Driver;

/// <summary>
/// What the workplace reads of a session's row in the list: which session it is, the group it is placed in (one of
/// <see cref="SessionGroup"/>) and the landing its own tree offers. Work orders places the rows and <see cref="SessionGrouping"/>
/// answers this directly, so the archive's judgement and an entry's Accept name no more of the row than they read.
/// </summary>
public interface IWorkplaceSessionRow
{
    string Session { get; }

    string Group { get; }

    /// <summary>What its own tree offers to land, or null (<see cref="LandOffer"/>).</summary>
    LandOffer? Lands { get; }
}
