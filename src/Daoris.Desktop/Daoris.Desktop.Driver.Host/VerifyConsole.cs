namespace Daoris.Driver.Host;

/// <summary>
/// A person's verification from a terminal (FABRIC-PIPELINE1d): <c>daoris-driver verify &lt;repository&gt; [--branch &lt;name&gt; | --commit &lt;sha&gt;]</c>,
/// run in a checkout of that repository. The words and the binding are the library's (<see cref="VerifyCommand"/>); this hands it the home,
/// the checkout this process runs in and the repository's landed route from the driver's file.
/// </summary>
internal static class VerifyConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (VerifyCommand.Read(args, out var problem) is not { } ask)
        {
            Console.Error.WriteLine($"verify: {problem}");
            Console.Error.WriteLine(VerifyCommand.Usage);
            return 2;
        }

        if (DaorisHome.Resolve() is not { } home)
        {
            Console.Error.WriteLine("verify: a verification run is bound under Daoris's home, and none is named here: run the installed daoris-driver, or name the home with DAORIS_HOME.");
            return 2;
        }

        if (HeavySlot.CheckoutOf(Environment.CurrentDirectory) is not { } checkout
            || !string.Equals(HeavySlot.RepositoryOf(checkout), ask.Repository, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"verify: run it from a checkout of `{ask.Repository}`: the commit is read there, and its runner starts there.");
            return 2;
        }

        try
        {
            var config = DriverConfig.Load(DriverConfig.ResolvePath(Environment.GetEnvironmentVariable, home));
            var route = WorkflowSelection.ResolveLanded(config, ask.Repository, null);
            return await VerifyCommand.BindAsync(home, checkout, ask, route, null, Console.Out, CancellationToken.None).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            Console.Error.WriteLine($"verify: {error.Message}");
            return 2;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"verify: {error.Message}");
            return 2;
        }
    }
}
