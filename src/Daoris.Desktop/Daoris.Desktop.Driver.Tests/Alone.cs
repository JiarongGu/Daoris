namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The Process classes that run alone, after the rest (PROCPAR1). The Process half runs its classes several at once
/// (<c>process.runsettings</c> says how many), and each class here holds something every class beside it would share.
/// </summary>
/// <remarks>
/// A class joins for one of three reasons, and its own remarks say which:
/// <list type="bullet">
/// <item>it sets a variable of the process's environment, which every class beside it reads and every child they start inherits
/// (<see cref="CategoryTests"/> finds it);</item>
/// <item>it measures the process as a whole, its handles, which every class beside it moves (<see cref="CategoryTests"/> finds it);</item>
/// <item>its claim is a bound of a few seconds on a real process's answer or a real watcher's event, which the load of the classes
/// beside it could break, and which no check can find, so the class says so.</item>
/// </list>
/// xUnit runs a collection whose definition disables parallelization after every parallel one has ended, and one at a time, so these
/// run with nothing beside them; <see cref="ProcessPath"/> is one too. Each is wall time the whole run waits on alone, so a class joins
/// only for a reason above, and a long one is split so that only the part with the reason runs here.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Alone
{
    public const string Name = "alone, after the rest";
}
