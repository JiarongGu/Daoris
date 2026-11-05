namespace Daoris.Driver;

/// <summary>A provider's program and its home variable as its registration declares them, for a start Daoris builds itself (the holder's terminal).</summary>
/// <param name="Provider">The provider's id, which Daoris's managed install names the program by.</param>
/// <param name="Binary">The program's file name without its extension (<c>claude</c> for Claude Code).</param>
/// <param name="HomeVariable">The variable naming the configuration home a start runs in, or null where it names none.</param>
public sealed record ManagedProgram(string Provider, string Binary, string? HomeVariable)
{
    /// <summary>The file a managed install holds, with the extension this system gives an executable.</summary>
    public string FileName => OperatingSystem.IsWindows() ? $"{Binary}.exe" : Binary;

    /// <summary>The program of a registered provider, read from the registration (<see cref="Providers.Built"/>).</summary>
    /// <exception cref="DriverException">No provider of that id declares a program.</exception>
    public static ManagedProgram Of(string provider)
    {
        var found = Providers.Built().FirstOrDefault(each => Ids.Same(each.Id.Value, provider))
            ?? throw new DriverException($"no provider '{provider}' is registered");
        if (!found.Program.IsKnown || found.Program.Required.Binary is not { Count: > 0 } binary)
        {
            throw new DriverException($"provider '{provider}' declares no program");
        }

        var door = found.Doors.FirstOrDefault(each => Ids.Same(each.Id.Value, provider));
        return new ManagedProgram(found.Id.Value, binary[0], door?.Home.Variable);
    }
}
