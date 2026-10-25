namespace Daoris.Driver;

/// <summary>
/// The terminal <c>daoris-driver tool</c> is said on and pressed from (FOUNDATION1 mod-v, the design §14.2: each verb stays in its own
/// subdomain and reaches the instance through the instance's door): the install declares this port, the instance's world
/// (<see cref="InstanceWorld"/>) implements it with the door, and every host and test that builds a <see cref="ToolsWorld"/> hands it one,
/// so the tools' command names neither the world nor the door. It is the settings' terminal and the folder the program runs from, which says
/// whether an install carries the built-in resource list.
/// </summary>
public interface IToolTerminal : IChoiceTerminal
{
    /// <summary>Where the program runs from, which says whether an install carries it.</summary>
    string BaseDirectory { get; }
}
