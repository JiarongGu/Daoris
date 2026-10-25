namespace Daoris.Driver;

/// <summary>
/// The authors a word on a record names by its <c>from</c>, other than the person (COWORK1b, SUPERVISOR1a-i): the spelling the service keeps it by,
/// which a word read off a record (<see cref="SaidWordView"/>) tells its author from. <see cref="DaorisWords.From"/> and
/// <see cref="SupervisorHanded.From"/> are these, for the words they make.
/// </summary>
public static class SaidFrom
{
    /// <summary>Daoris's own words: a reopen, a step's go-on.</summary>
    public const string Daoris = "daoris";

    /// <summary>A supervisor's word, a claim on the record and never an authority.</summary>
    public const string Supervisor = "supervisor";
}
