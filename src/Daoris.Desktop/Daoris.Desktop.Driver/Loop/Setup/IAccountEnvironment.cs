namespace Daoris.Driver;

/// <summary>
/// The person's account's own environment variables, as a terminal opened after a change reads them (WIRING1e): the one door every
/// setting-up press writes through, so a test hands a stand-in and the machine running it is never read or written.
/// </summary>
public interface IAccountEnvironment
{
    /// <summary>The variable's stored text, its <c>%NAME%</c> references unexpanded; null where it is not set. Throws where the account's store cannot be read.</summary>
    string? Read(string name);

    /// <summary>Stores the variable, or removes it for a null; programs already running are told the environment changed.</summary>
    void Write(string name, string? value);
}
