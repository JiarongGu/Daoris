namespace Daoris.Driver.Host;

/// <summary>
/// Asks, from a terminal: a sentence entered at a WORKSPACE rather than at a repository.
/// </summary>
/// <remarks>
/// <para>The words are read first (<see cref="AskArguments"/>), and a problem sends nothing: <c>--help</c> prints the usage, and a flag
/// the verb does not take is never the ask's words.</para>
/// <para>A new ask is the person's press (<see cref="AskNewCommand"/>, <c>ASK_NEW</c>), confirmed in the running Daoris's window, or Ask
/// Daoris's delegate press; alone where none runs. <c>--short</c> is sent as typed: the service judges it, and its sentence is printed.</para>
/// <para><c>delete</c> removes an ask made by mistake with every quest asked by it, or refuses whole when one of them must stay.
/// <c>go-ahead</c> answers a go-ahead a session asked on the ask, with the person's words if any: every session on the ask is handed
/// the answer, and a parked session none of whose go-aheads waits any more goes on with them.</para>
/// <para><c>--review</c> chooses whether a new ask's work is reviewed before it lands, and where, <c>rule</c> (its default) sending none;
/// <c>review</c> changes an ask's choice, or applies its intake's proposal (<see cref="AskReviewCommand"/>). <c>--kind</c> and
/// <c>--workflow</c> say what kind of task a new ask is and which workflow its work follows; <c>workflow</c> changes an ask's, or
/// clears it with <c>--clear</c> (<see cref="AskWorkflowCommand"/>).</para>
/// <para>Exit codes keep the family contract: 0 answered · 1 refused (a receiver that cannot be asked, a closed ask) · 2 tool error.</para>
/// </remarks>
internal static class AskConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (AskArguments.Read(args, out var problem) is not { } read) return Usage(problem!);
        if (read.Help)
        {
            foreach (var line in Forms()) Console.WriteLine(line);
            return 0;
        }

        try
        {
            using var service = ServiceClient.FromEnvironment();
            var reviews = new AskReviewWorld(service, () => DriverConfig.Load(DriverConfig.ResolvePath()));
            var workflows = new AskWorkflowWorld(
                service, () => DriverConfig.Load(DriverConfig.ResolvePath()), DriverConfig.HomeOf(DriverConfig.ResolvePath()));

            if (read.SetWorkflow is { } setWorkflow)
            {
                if (read.WorkflowWords is not null) return Usage("`ask workflow <id>` takes your words after it, not --workflow-words");
                if (AskWorkflowCommand.Read(setWorkflow, read.Kind, read.Workflow, read.ClearWorkflow, read.Words, out var workflowProblem) is not { } set)
                {
                    return Usage(workflowProblem!);
                }

                return await AskWorkflowCommand.PressAsync(set, workflows, Console.Out, InstanceHost.World("ask")).ConfigureAwait(false);
            }

            if (read.SetReview is { } setReview)
            {
                if (AskReviewCommand.Read(setReview, read.Words, out var setProblem) is not { } chosen) return Usage(setProblem!);
                // The ask page's twin: its *Set*, and *Apply* on an intake's proposal.
                return await AskReviewCommand.PressAsync(chosen, reviews, Console.Out, InstanceHost.World("ask")).ConfigureAwait(false);
            }

            if (read.Publish is { } publish)
            {
                var session = Environment.GetEnvironmentVariable(DriverChoicesCommand.SessionVariable);
                return Report(await service.PublishAskAsync(publish, read.To!, session, read.Requirements).ConfigureAwait(false));
            }

            if (read.Close is { } close) return Report(await service.CloseAskAsync(close, read.Reason ?? "").ConfigureAwait(false));
            if (read.Delete is { } delete) return await AskDeleteCommand.PressAsync(delete, service, Console.Out, InstanceHost.World("ask")).ConfigureAwait(false);

            if (read.GoAhead is { } goAhead)
            {
                if (GoAheadCommand.Read(read.Words, out var answerProblem) is not { } answer) return Usage(answerProblem!);
                // The ask page's twin: the answer that leaves none of a parked session's go-aheads waiting sends it on.
                var (ok, message) = await service.AnswerGoAheadAsync(goAhead, answer.Number, answer.Approved, answer.Words, goesOn: true)
                    .ConfigureAwait(false);
                Console.WriteLine(message);
                return ok ? 0 : 1;
            }

            // Full paths, as this terminal names them: the press is read where Daoris runs, whose folder is its own.
            var ask = new AskNew(read.Workspace ?? "default", read.Sentence, read.To, read.Links, [.. read.Files.Select(Path.GetFullPath)],
                read.Review, read.ReviewWords, read.Kind, read.Workflow, read.WorkflowWords, read.Short);
            var registry = (await service.RegistryAsync().ConfigureAwait(false)).Select(row => (row.Repository, (string?)row.Workspace));
            if (AskNewCommand.Unfit(ask, reviews.Config(), workflows.Home, registry, out var usage) is { } unfit)
            {
                if (usage) return Usage(unfit);
                Console.WriteLine(unfit);
                return 1;
            }

            return await AskNewCommand.PressAsync(ask, reviews, workflows, Console.Out, InstanceHost.World("ask")).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            Console.Error.WriteLine($"ask: {error.Message}");
            return 2;
        }
        catch (HttpRequestException error)
        {
            Console.Error.WriteLine($"ask: could not reach the service — {error.Message}");
            return 2;
        }
    }

    private static int Report(AskAnswer answer)
    {
        Console.WriteLine(answer.Message);
        if (answer.AskId is not null) Console.WriteLine($"  ask    #{answer.AskId}");
        if (answer.QuestId is not null) Console.WriteLine($"  quest  #{answer.QuestId}");
        return answer.Ok ? 0 : 1;
    }

    private static IEnumerable<string> Forms() => AskArguments.Usage.Append($"daoris {ChannelCommand.Usage}");

    private static int Usage(string why)
    {
        Console.Error.WriteLine($"ask: {why}");
        var first = true;
        foreach (var form in Forms())
        {
            Console.Error.WriteLine((first ? "usage: " : "       ") + form);
            first = false;
        }

        return 2;
    }
}
