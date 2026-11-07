namespace Daoris.Knowledge;

/// <summary>Why a publish did not produce a quest — or <see cref="None"/> when it did.</summary>
public enum QuestPublishRefusal
{
    None,

    /// <summary>Addressed to the repository doing the asking. Its own backlog is the place for that.</summary>
    SelfAddressed,

    /// <summary>
    /// Nothing could answer it: the target is not registered, or it has neither adopted nor a root on
    /// this machine.
    /// </summary>
    NotAddressable,

    /// <summary>
    /// The two repositories are in different workspaces, and the workspace is the unit of sharing. Cross-workspace asking is deliberately out of scope — a person carries it, or a
    /// later, explicit door does, with its own disclosure argument.
    /// </summary>
    CrossWorkspace,

    /// <summary>A link that is not an absolute http or https address — shown as a link, it would be something else.</summary>
    BadLink,

    /// <summary>More files, or more bytes, than a quest carries — or a file named in a shape no record keeps.</summary>
    BadAttachment,

    /// <summary>Files arrived with their bytes and this host has no Daoris home to keep them under.</summary>
    NoHome,

    /// <summary>
    /// A chain with a step that cannot be published when its turn comes: nobody there can see
    /// it, it asks the asker, it has no words, it is shared where the chain is local or local where it
    /// is shared, or there are too many.
    /// </summary>
    BadChain,

    /// <summary>
    /// The address names a lane its repository does not declare, or any lane of a repository that
    /// declares none. The answer names the lanes there are.
    /// </summary>
    UnknownLane,

    /// <summary>
    /// A requirement that is not one: it lacks the person's words or its check, there
    /// are too many or one is too long, or the quest is asked on no ask whose words it could quote.
    /// </summary>
    BadRequirement,

    /// <summary>A requirement quotes words the person never said on the ask — the answer names them.</summary>
    NotQuoted,

    /// <summary>A short title past <see cref="QuestTitles.MaxShort"/> characters, or over a line break.</summary>
    BadShortTitle,

    /// <summary>A weight that is neither <c>design</c> nor <c>exact</c>.</summary>
    BadWeight,

    /// <summary>A declared path that is no repository-relative glob, or too many.</summary>
    BadPaths,

    /// <summary>A workflow request's key that is no <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c>.</summary>
    BadRequest,

    /// <summary>
    /// The same words as a quest this machine cleared after its remote numbered it: the remote holds
    /// it closed, and would refuse a fresh copy on every pass, so a new ask is a new title.
    /// </summary>
    Cleared,

    /// <summary>
    /// A review choice that is not one: not <c>off</c>, <c>on</c> or an environment's name, words
    /// past their bound, or a session setting one without the person's words, or on no ask whose words it could quote.
    /// </summary>
    BadReview,

    /// <summary>
    /// A kind proposal that is not one: a kind that is not a kind's id, a reason that is missing or past its bound, or
    /// one on no ask whose words it could quote. A reason that does not quote the ask's words is <see cref="NotQuoted"/>.
    /// </summary>
    BadWorkflow,
}
