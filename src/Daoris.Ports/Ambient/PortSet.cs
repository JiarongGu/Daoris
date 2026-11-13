using Daoris.Ports.Agents;
using Daoris.Ports.Browser;
using Daoris.Ports.Git;
using Daoris.Ports.Processes;
using Daoris.Ports.Repositories;
using Daoris.Ports.Store;

namespace Daoris.Ports.Ambient;

/// <summary>
/// The ports a root chose, as one record. It is named for what it holds and not for the adapter that implements the processes
/// (<c>Daoris.Adapters.Machine</c>), which it would otherwise read as.
/// </summary>
public sealed record PortSet
{
    /// <summary>The clock.</summary>
    public required TimeProvider Clock { get; init; }

    /// <summary>Git at the level of its arguments.</summary>
    public IGit? Git { get; init; }

    /// <summary>A git repository's operations; null where the root chose none, which the application answers over its own git funnel.</summary>
    public IGitRepository? Repository { get; init; }

    /// <summary>Starts processes.</summary>
    public IProcessHost? Processes { get; init; }

    /// <summary>Places starts on the machine.</summary>
    public IMachineAdmission? Admission { get; init; }

    /// <summary>Says whether a process still runs.</summary>
    public IProcessLiveness? Liveness { get; init; }

    /// <summary>How the desktop's store is opened and its files connected to.</summary>
    public DesktopStoreEngines? Store { get; init; }

    /// <summary>The browser.</summary>
    public IInAppBrowser? Browser { get; init; }

    /// <summary>The application's admission of each start, asked before the machine places it.</summary>
    public StartPolicy? StartPolicy { get; init; }

    public SecretSeal? Seal { get; init; }
}
