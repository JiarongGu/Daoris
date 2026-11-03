using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class ParticipantsWorkflowsOperations : IParticipantsWorkflows
{
    [ModuleInitializer]
    internal static void Install() => ParticipantsWorkflows.Current = new ParticipantsWorkflowsOperations();

    public bool RunBound(string home, string run) => WorkflowRunBindings.Bound(home, run);

    public string? ChatOf(TracedSession session, IReadOnlyList<TracedSession> sessions, Func<string, bool> holds) =>
        ChatRuns.ChatOf(session, sessions, holds);

    public IParticipantsChatBound Bind(
        DriverConfig config, string home, string run, string repository, string? workspace, IWorkplacePluginCatalog plugins, DateTimeOffset at) =>
        new ChatBound(ChatRuns.Bind(config, home, run, repository, workspace, WorkflowSteps.PluginsOf(plugins), at));

    public IWorkplaceProcess ReadProcess(string home, DriverConfig config, string repository, string? workspace, string? run) =>
        WorkflowProcesses.Read(home, config, repository, workspace, run);

    public (string Checks, int? Minutes)? ChecksOf(DriverConfig config, string repository, IWorkplaceProcess process) =>
        SessionChecks.Of(config, repository, process as ICheckedProcess) is { } checks ? (checks, LongCalls.MinutesOf(process as ICheckedProcess)) : null;

    private sealed class ChatBound(ChatRunBound bound) : IParticipantsChatBound
    {
        public string? Failed => bound.Failed;

        public bool Wrote => bound.Wrote;

        public Noted? Told() => bound.Wrote && bound.Binding is { } binding ? ChatRuns.Line(binding) : null;

        public Noted FailedLine(string problem) => ChatRuns.FailedLine(problem);

        public IWorkplaceProcess Process(string home, DriverConfig config, string repository, string? workspace, string run) =>
            WorkflowProcesses.Bound(home, config, repository, workspace, run, bound.Binding);
    }
}
