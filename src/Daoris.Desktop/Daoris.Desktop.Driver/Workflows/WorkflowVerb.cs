namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver driver workflow</c> (PUSH1f2b, D194; FOUNDATION1 mod-iii): the workflows' own verb, the settings' port
/// (<see cref="ChoiceVerb"/>) implemented beside the terminal's door to workflows (<see cref="DriverWorkflowsCommand"/>). Its words run whole,
/// never planned.
/// </summary>
public sealed class WorkflowVerb : ChoiceVerb
{
    public override string Word => "workflow";

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world) =>
        throw new ChoiceWords("`workflow` is not a standing choice this program changes.");

    public override Task<int>? RunAsync(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world, CancellationToken ct) =>
        Run(args, before, world, ct);

    /// <summary>
    /// <c>workflow …</c> (PUSH1f2b): a read said, and a write acted on the terminal's word, since no route takes one (D171's PUSH1f note),
    /// refused from a session's terminal before anything is read (D194 point 5). <see cref="DriverWorkflowsCommand"/> is the verb.
    /// </summary>
    private static async Task<int> Run(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world, CancellationToken ct)
    {
        var error = world.Instance.Error;
        if (DriverWorkflowsCommand.Changes([.. args.Skip(1)]) && world.Instance.Environment(DriverChoicesCommand.SessionVariable) is { Length: > 0 })
        {
            await error.WriteLineAsync($"{DriverChoicesCommand.Prefix}: {DriverChoicesCommand.SessionsTerminal}").ConfigureAwait(false);
            return 1;
        }

        try
        {
            return await DriverWorkflowsCommand.RunAsync(args, before, world, ct).ConfigureAwait(false);
        }
        catch (ChoiceWords refused)
        {
            await error.WriteLineAsync($"{DriverChoicesCommand.Prefix}: {refused.Message}").ConfigureAwait(false);
            return refused.Exit;
        }
        catch (DriverConfigUnreadableException unreadable)
        {
            await error.WriteLineAsync($"{DriverChoicesCommand.Prefix}: {unreadable.Message}").ConfigureAwait(false);
            return 2;
        }
        catch (DriverException refused)
        {
            await error.WriteLineAsync($"{DriverChoicesCommand.Prefix}: {refused.Message} ({ChoiceCodes.Of(refused)})").ConfigureAwait(false);
            return 2;
        }
    }
}
