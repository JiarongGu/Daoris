namespace Daoris.Driver;

/// <summary>What <c>daoris session</c> asks for.</summary>
/// <param name="Verb">
/// <c>list</c>, <c>stop</c>, <c>finish</c>, <c>decline</c>, <c>archive</c>, <c>unarchive</c>, <c>delete</c>, <c>say</c>,
/// <c>take-back</c>, <c>reword</c>, <c>start-from</c>, <c>hand-over</c>, <c>partner</c>, <c>partner-list</c>, <c>partner-stop</c>
/// or <c>handback</c>.
/// </param>
public sealed record SessionsAsk(string Verb) : ISupervisorAsk, IParticipantsAsk
{
    /// <summary>The sessions a verb names.</summary>
    public IReadOnlyList<string> Ids { get; init; } = [];

    /// <summary>The listing's one group, one of <see cref="SessionGroup"/>.</summary>
    public string? Group { get; init; }

    /// <summary>The listing's one repository.</summary>
    public string? Repository { get; init; }

    /// <summary>The listing as the screen's answer.</summary>
    public bool Json { get; init; }

    /// <summary>A finish's note or a decline's reason.</summary>
    public string? Note { get; init; }

    /// <summary><c>archive --ended</c>: every session in Ended, listed first.</summary>
    public bool Ended { get; init; }

    /// <summary><c>archive --ended --yes</c>: archive what the list holds.</summary>
    public bool Yes { get; init; }

    /// <summary>A say's words, joined by a space as `answer` joins them; a reword's new words.</summary>
    public string? Text { get; init; }

    /// <summary>A take-back's or a reword's word: the id it waits under on the record; null lists them.</summary>
    public string? Word { get; init; }

    /// <summary>A say's files, each as the person named its path.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>
    /// A say's <c>--level</c>: <c>ordinary</c>, <c>urgent</c> or <c>now</c>, which <c>SESSION_INPUT</c> carries as <c>level</c>. Null
    /// where none is given, the person's default, urgent.
    /// </summary>
    public string? Level { get; init; }

    /// <summary>A hand-over's agent: the adapter the conversation goes on with.</summary>
    public string? Agent { get; init; }

    /// <summary>A hand-over's account, or null for the one the roster picks.</summary>
    public string? Account { get; init; }

    /// <summary>A piece's paths (<c>sessions partner … --paths</c>), each repository-relative as the person named it.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>A helper ask's <c>--after</c>: the answered ask it asks again after.</summary>
    public string? After { get; init; }

    /// <summary>A helper ask's <c>--to-me</c>: its answer is the person's until <c>sessions pass</c>.</summary>
    public bool ToMe { get; init; }

    /// <summary>
    /// The supervisor's answer (<c>sessions supervise &lt;id&gt; --choice …</c>): its question, generation, label and reasons; or its
    /// leave (<c>--leave &lt;bound&gt;</c>), its bound in place of the label.
    /// </summary>
    public SupervisorDraft? Supervised { get; init; }
}
