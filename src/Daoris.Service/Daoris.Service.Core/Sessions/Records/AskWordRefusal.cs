namespace Daoris.Knowledge;

/// <summary>Why a person's word was not kept on an ask (DRIFT1a) — or <see cref="None"/> when it was.</summary>
public enum AskWordRefusal
{
    None,

    /// <summary>No words: nothing was said.</summary>
    Empty,

    /// <summary>No session under that id of this machine's.</summary>
    NotFound,

    /// <summary>
    /// The session is on no ask held here — a quest a repository asked, or a conversation on none — so
    /// there is no ask to keep the words on. Not an error: its own record holds what was said.
    /// </summary>
    NoAsk,
}
