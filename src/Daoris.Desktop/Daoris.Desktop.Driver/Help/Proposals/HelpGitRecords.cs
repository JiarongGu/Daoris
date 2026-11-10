namespace Daoris.Driver;

/// <summary>A git identity as a rule view's judge knows it: its id, label, host and way. Never a key, a token or a user name.</summary>
public sealed record HelpGitIdentity(string Id, string Label, string Host, string Way);

public interface IHelpConfigDoors
{
    /// <summary>An edit to the driver's file, as the route it names makes it.</summary>
    void Change(Func<DriverConfig, DriverConfig> edit);
}
