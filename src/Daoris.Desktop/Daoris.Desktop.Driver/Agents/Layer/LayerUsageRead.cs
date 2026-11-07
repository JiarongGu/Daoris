using System.Collections;
using System.Diagnostics;
using Lyntai;
using Lyntai.Agents;
using Lyntai.Processes;

namespace Daoris.Driver;

/// <summary>
/// The usage port's layer implementation: one read through the layer's
/// <see cref="IAgentUsageSource"/>, in a spawn's container of its own over <see cref="LauncherRunner"/>'s live stdin. It states the
/// question (the harness, the command, the recorded answers, the freshness) and names itself in the history's source. The spawn is the roster's:
/// the command and toolchain the roster resolved, composed by the one shell into the program, its prefix and its environment; the version
/// read on any binary as the usage reader reads it, with no probe; the patience the reader's.
/// </summary>
/// <param name="runner">The runner a read's container spawns through; a test's stands in. Null is Daoris's launcher.</param>
internal sealed class LayerUsageRead(UsageQuestion today, LayerUsageVendor vendor, Func<LauncherSeat, TimeSpan, IProcessRunner>? runner = null)
    : IUsageRead
{
    /// <summary>The version of the mapping into the walk and the history, which each reading it keeps carries.</summary>
    public const int Reader = 1;

    public string Harness => today.Harness;

    public IReadOnlyList<string> Command => today.Command;

    public IReadOnlyList<RecordedFrame> Recorded => today.Recorded;

    public TimeSpan Fresh => today.Fresh;

    /// <summary>The history's source: the question's, named as the layer's read.</summary>
    public string Source => "layer:" + today.Source;

    public async Task<IReadOnlyList<WindowReading>?> AskAsync(UsageAsk ask, CancellationToken ct)
    {
        var info = Composed(ask);
        if (CommandOf(info) is not { } command) return null;
        var seat = new LauncherSeat(ContainedKind.Agent, StartClass.Background, Home: null, Log: null) { Environment = EnvironmentOf(info) };
        var spawner = runner?.Invoke(seat, vendor.Patience) ?? new LauncherRunner(seat, vendor.Patience);
        await using var container = LayerContainer.ForSpawn(Registration(command), spawner);
        if (container.Registry.Get(vendor.Door) is not IAgentUsageSource source) return null;

        var account = new AgentAccount { Provider = vendor.Door, Name = ask.Profile ?? Path.GetFileName(ask.ProfileHome), Home = ask.ProfileHome };
        var reading = await source.ReadAllowanceAsync(account, ct).ConfigureAwait(false);
        if (!reading.IsKnown) return null;
        var (walk, seen, plan) = vendor.Answer(reading, ask.Now);
        return new UsageAnswer(walk, seen, plan, Source, Reader);
    }

    /// <summary>A step the server's written digits show, as the usage reader writes a precision: of a whole, a percent being a hundredth.</summary>
    internal static double? Precision(double? step) =>
        step is { } shown && shown > 0 ? 1 / (Math.Pow(10, Math.Round(-Math.Log10(shown))) * 100) : null;

    private LayerRegistration Registration(string command) => new(vendor.Door, builder =>
    {
        vendor.Register(builder, command);
        builder.AddAgentUsageReading(options =>
        {
            options.Timeout = vendor.Patience;
            options.VersionPolicy = AgentUsageVersionPolicies.AnyVersion;
            vendor.Options(options);
        });
    });

    /// <summary>The spawn the read starts: the resolved command, its tools and the account's home, through the one shell.</summary>
    private static ProcessStartInfo Composed(UsageAsk ask)
    {
        var info = new ProcessStartInfo { FileName = ask.Run[0], UseShellExecute = false, CreateNoWindow = true };
        foreach (var part in ask.Run.Skip(1)) info.ArgumentList.Add(part);
        Tools.Hand(info);
        Shell.Equip(info, ask.Toolchain, ask.ProfileHome, binary: ask.Managed);
        return info;
    }

    /// <summary>
    /// The program and what the shell put before the question's own arguments, as the layer's command line; null where the shell's start
    /// does not end in them, or a word cannot be quoted, since the layer appends its own server arguments.
    /// </summary>
    private string? CommandOf(ProcessStartInfo info)
    {
        var own = Command.Count - 1;
        var args = info.ArgumentList.ToList();
        if (args.Count < own || !args.Skip(args.Count - own).SequenceEqual(Command.Skip(1), StringComparer.Ordinal)) return null;
        string[] words = [info.FileName, .. args.Take(args.Count - own)];
        return words.Any(word => word.Length == 0 || word.Contains('"', StringComparison.Ordinal))
            ? null
            : string.Join(' ', words.Select(word => $"\"{word}\""));
    }

    /// <summary>The start's environment as the shell composed it, a variable the shell removed handed as null.</summary>
    private static Dictionary<string, string?> EnvironmentOf(ProcessStartInfo info)
    {
        var composed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in info.Environment) composed[name] = value;
        foreach (DictionaryEntry inherited in System.Environment.GetEnvironmentVariables())
        {
            if (inherited.Key is string name && !info.Environment.ContainsKey(name)) composed[name] = null;
        }

        return composed;
    }
}
