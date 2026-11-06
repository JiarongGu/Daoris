namespace Daoris.Driver;

/// <summary>How a pin's version comes to be installed (<see cref="PinInstall.Plan"/>).</summary>
internal enum PinWay
{
    /// <summary>It is installed where Daoris keeps it already: nothing is fetched.</summary>
    Present,

    /// <summary>It is fetched from the maker's release channel, verified, and staged.</summary>
    Channel,

    /// <summary>It is installed by the package manager's command under the folder Daoris keeps it in.</summary>
    Package,
}

/// <summary>What installing one version comes to, decided once for both surfaces: the way, the folder, and what each way needs.</summary>
/// <param name="Where">The folder Daoris keeps this version in.</param>
/// <param name="Present">The installed program, for <see cref="PinWay.Present"/>.</param>
/// <param name="Channel">The release channel's name, for <see cref="PinWay.Channel"/>.</param>
/// <param name="Command">The package manager's whole command, for <see cref="PinWay.Package"/>.</param>
internal sealed record PinPlan(PinWay Way, string Where, string? Present, string? Channel, IReadOnlyList<string>? Command);

/// <summary>
/// The one decision of a pin's install (AGENTPROVIDER1 row f, s-ix f4): from the maker's channel where the program declares one, else the
/// package manager's, an install already in place being the proof it verified. The window's actions and the terminal's <c>agent pin</c> and
/// <c>agent update</c> each ask it, then run what it names their own way: the terminal at its console and under the home's lease, the window
/// redirected with its home held already, each in its own words.
/// </summary>
internal static class PinInstall
{
    /// <summary>
    /// How <paramref name="version"/> of <paramref name="harness"/> is installed. A version a channel cannot verify is refused before a byte
    /// is fetched, and never handed to the package manager instead, which would be the same trust by another road.
    /// </summary>
    /// <param name="npm">The package manager a package pin runs.</param>
    /// <exception cref="DriverException">The version is not one the channel installs, the tools' way cannot run, or the program declares no package.</exception>
    public static PinPlan Plan(AccountAgent toolchain, string home, string harness, string version, IReadOnlyList<string> npm)
    {
        var where = HarnessSettings.ManagedHome(home, harness, version);
        if (toolchain.Program.Channel is { Length: > 0 } channel)
        {
            ReleaseChannels.RefuseVersion(channel, version);
            return HarnessSettings.ManagedBinary(home, harness, version, toolchain.Program.Binary) is { } present
                ? new PinPlan(PinWay.Present, where, present, channel, null)
                : new PinPlan(PinWay.Channel, where, null, channel, null);
        }

        return toolchain.Program.Package is { Length: > 0 } package
            ? new PinPlan(PinWay.Package, where, null, null, PinMove.PackageCommand(home, harness, package, version, npm))
            : throw new DriverException(
                "that agent declares no package, so Daoris has no sanctioned way to fetch a version "
                + "of it. Install it with its own tooling and Daoris will find it on PATH.");
    }
}
