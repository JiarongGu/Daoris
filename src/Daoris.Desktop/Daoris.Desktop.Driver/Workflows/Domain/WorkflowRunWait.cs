namespace Daoris.Driver;

/// <summary>
/// What a run's step waits on while it stands active or ready, said under the step and read by the session's card: one of its kind's declared
/// waits (<see cref="BuiltInNode.Waits"/>), which the page words as <c>workflow.wait.&lt;kind&gt;.&lt;code&gt;</c>, and the facts its words name.
/// </summary>
/// <param name="Code">One of the step's kind's <see cref="BuiltInNode.Waits"/>.</param>
/// <param name="Values">The facts its words name (a session, a line, a candidate, files, a try and its bound), each as the record has it.</param>
/// <param name="Since">When it began to wait, where the record kept it.</param>
/// <param name="Until">When the wait's bound ends, where it has one.</param>
public sealed record WorkflowRunWait(string Code, IReadOnlyDictionary<string, string> Values, DateTimeOffset? Since = null, DateTimeOffset? Until = null);
