namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver register [--repository &lt;name&gt;]</c> (WSSETUP5, D124 §3.1, §4.5): the terminal's door to following a
/// line when the person asks, every repository with a checkout here or the one named. The row's <i>Refresh</i> is the
/// screen's door to the same follow (D50). In the library rather than the host, so its words are held by a test, as the
/// set-up press's are.
/// </summary>
/// <remarks>
/// <para>Each repository is one line: the outcome's word, its name, and the sentence its row says. A line the person
/// moved outside Daoris is read here, as at a start.</para>
/// <para>Exit codes keep the family contract: 0 each followed, whatever it came to · 1 a repository named that was
/// refused · 2 a tool error, the usage among them. Every repository followed at once is a report, and a refusal among
/// them is said and is not a failure: a workspace nobody has set up yet is not set up, which is no error.</para>
/// </remarks>
public static class RegisterCommand
{
    public const string Usage = "usage: daoris-driver register [--repository <name>]";

    public static Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, IRegistrationWorld world, CancellationToken ct = default) =>
        RunAsync(args, output, world, instance: null, ct);

    /// <summary>
    /// The verb, through the running Daoris where one answers (RPCOPEN1, RPC1e, the terminal RPC design §7.2): each repository is one
    /// <c>REGISTRY_REFRESH</c>, whose answer is read back into the record the alone path prints from, so the words are the same;
    /// alone where none runs. An instance that lives and does not answer refuses the verb (exit 2), and nothing is registered here.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args, TextWriter output, IRegistrationWorld world, InstanceWorld? instance, CancellationToken ct = default)
    {
        if (Problem(args) is { } problem)
        {
            output.WriteLine($"register: {problem}");
            output.WriteLine(Usage);
            return 2;
        }

        var named = Named(args);
        FollowReport report;
        if (instance is not null && await InstanceDoor.PeekAsync(instance, ct).ConfigureAwait(false) is var look
            && look.Found is InstanceFound.Answers or InstanceFound.Silent or InstanceFound.Refused)
        {
            if (look.Found != InstanceFound.Answers) return await InstanceDoor.RefusedAsync(look, instance).ConfigureAwait(false);
            if (await ThroughAsync(named, world, instance, ct).ConfigureAwait(false) is not { } through) return 2;
            report = through;
        }
        else
        {
            report = await RegistrationFollow.FollowAsync(world, named is null ? null : [named], ct).ConfigureAwait(false);
        }

        if (report.Followed.Count == 0)
        {
            output.WriteLine("register: no repository has a checkout here, so there is nothing to register.");
            return 0;
        }

        foreach (var followed in report.Followed)
        {
            output.WriteLine($"  {followed.Outcome,-16} {followed.Repository}  {followed.Said}");
        }

        if (report.Refresh is { } refresh) output.WriteLine($"register: the index was not read again after registering: {refresh}");
        var sent = report.Followed.Count(followed => followed.Sent);
        var refused = report.Followed.Count(followed => RegistryOutcome.IsRefusal(followed.Outcome));
        output.WriteLine($"register: {sent} registered, {report.Followed.Count - sent - refused} already as their lines say, "
            + $"{refused} not registered{(refused > 0 ? ", each saying why above" : "")}.");
        return named is not null && refused > 0 ? 1 : 0;
    }

    /// <summary>
    /// Every repository with a checkout here, or the one named, each followed by one <c>REGISTRY_REFRESH</c> to the running Daoris; null
    /// where a press was refused or its answer did not read, which the door has said. The repositories are this machine's registry's,
    /// read as the alone path reads them, so the same ones are followed in the same order.
    /// </summary>
    private static async Task<FollowReport?> ThroughAsync(string? named, IRegistrationWorld world, InstanceWorld instance, CancellationToken ct)
    {
        IReadOnlyList<string> names = named is not null
            ? [named]
            : [.. (await world.RegistrationsAsync(ct).ConfigureAwait(false))
                .Where(row => !string.IsNullOrWhiteSpace(row.Root)).Select(row => row.Repository).Order(StringComparer.Ordinal)];
        var followed = new List<RegistrationFollowed>();
        string? refresh = null;
        foreach (var name in names)
        {
            var exit = await InstanceDoor.PressAsync(
                OpenRoutes.RegistryRefresh, new { repository = name },
                alone: async () =>
                {
                    // The instance went between the look and this press: what none running does, for this repository.
                    var one = await RegistrationFollow.FollowAsync(world, [name], ct).ConfigureAwait(false);
                    followed.AddRange(one.Followed);
                    refresh ??= one.Refresh;
                    return 0;
                },
                words: answer =>
                {
                    if (RegistrationFollowed.Read(answer) is not { } read)
                    {
                        instance.Error.WriteLine($"{instance.Verb}: Daoris answered in a form this terminal does not read.");
                        return 2;
                    }

                    followed.Add(read.Followed);
                    refresh ??= read.Refresh;
                    return 0;
                },
                instance, ct).ConfigureAwait(false);
            if (exit != 0) return null;
        }

        return new FollowReport(followed, refresh);
    }

    /// <summary>What is wrong with the words, or null for none or <c>--repository &lt;name&gt;</c>.</summary>
    public static string? Problem(IReadOnlyList<string> args) => args switch
    {
        [] => null,
        ["--repository", var name] when !name.StartsWith('-') && name.Trim().Length > 0 => null,
        ["--repository", ..] => "`--repository` takes a repository's name.",
        _ => $"`{args[0]}` is not a word register takes.",
    };

    private static string? Named(IReadOnlyList<string> args) => args is ["--repository", var name] ? name.Trim() : null;
}
