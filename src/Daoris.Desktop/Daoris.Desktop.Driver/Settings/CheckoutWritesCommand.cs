namespace Daoris.Driver;

/// <summary>What <c>daoris place checkout</c> asks: a repository, and whether Daoris may change its registered checkout's files.</summary>
public sealed record CheckoutWritesAsk(string Repository, bool Allow);

/// <summary>
/// <c>daoris place checkout &lt;repository&gt; on|off</c>: <c>SET_CHECKOUT_WRITES</c>'s terminal twin. It writes the driver's file alone,
/// through <see cref="DriverConfig.WithCheckoutWrites"/>, the edit the route's handler makes, so the two write alike.
/// </summary>
/// <remarks>Exit codes are the family's: 0 set · 2 could not, the usage among them.</remarks>
public static class CheckoutWritesCommand
{
    public const string Usage = "usage: daoris place checkout <repository> on|off";

    /// <summary>What the words after <c>checkout</c> ask, or null with what is wrong with them.</summary>
    public static CheckoutWritesAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count != 2 || args[0].StartsWith("--", StringComparison.Ordinal) || args[0].Trim().Length == 0)
        {
            problem = "`checkout` takes a repository's name, then `on` or `off`.";
            return null;
        }

        switch (args[1].Trim().ToLowerInvariant())
        {
            case "on": return new CheckoutWritesAsk(args[0].Trim(), true);
            case "off": return new CheckoutWritesAsk(args[0].Trim(), false);
            default:
                problem = $"`{args[1]}` is not `on` or `off`.";
                return null;
        }
    }

    /// <summary>Make the change in the driver's file at <paramref name="configPath"/> and say it.</summary>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing is written.</exception>
    public static int Run(CheckoutWritesAsk ask, string configPath, TextWriter output)
    {
        var changed = DriverConfig.Load(configPath).WithCheckoutWrites(ask.Repository, ask.Allow);
        changed.Save(configPath);
        output.WriteLine(ask.Allow
            ? $"checkout: Daoris may now change files in `{ask.Repository}`'s registered checkout."
            : $"checkout: Daoris may no longer change files in `{ask.Repository}`'s registered checkout.");
        return 0;
    }
}
