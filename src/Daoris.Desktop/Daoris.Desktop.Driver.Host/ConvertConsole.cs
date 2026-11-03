namespace Daoris.Driver.Host;

/// <summary>
/// <c>convert --plan | --apply &lt;digest&gt; [--leave &lt;part&gt;] | --undo</c> (ONEWAY1ae; the oneway design §1.1): the home's one conversion door at
/// the terminal, through the running Daoris alone, which plans it and asks the person to confirm the press; with none running it says so.
/// The words are the library's (<see cref="ConvertCommand"/>).
/// </summary>
internal static class ConvertConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (ConvertCommand.Read(args, out var problem) is not { } asked)
        {
            Console.Error.WriteLine($"convert: {problem}");
            Console.Error.WriteLine(ConvertCommand.Usage);
            return 2;
        }

        try
        {
            var home = DriverConfig.HomeOf(DriverConfig.ResolvePath());
            return await ConvertCommand.RunAsync(asked, Console.Out, InstanceHost.World(home, "convert")).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or System.Text.Json.JsonException or IOException)
        {
            Console.Error.WriteLine($"convert: {error.Message}");
            return 2;
        }
    }
}
