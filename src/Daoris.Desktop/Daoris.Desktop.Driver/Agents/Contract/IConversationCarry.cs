namespace Daoris.Driver;

/// <summary>
/// How an agent's conversation goes from one of its account homes to another, so work an account's limit cut off goes on in the same
/// conversation on the next account: the context it carries whole, how its harness compacts a context past that, and the copy of the
/// conversation's own files. Each provider declares its own; one that declares none is handed Daoris's brief.
/// </summary>
public interface IConversationCarry
{
    /// <summary>
    /// The context, in tokens, at or under which the conversation is carried whole. Past it the harness compacts it first where it can,
    /// else the new part is briefed: a carry starts with no prompt cache on the new account, so its first request writes the whole context.
    /// </summary>
    long WholeBound { get; }

    /// <summary>
    /// The variables a carried conversation's process is spawned with so its harness compacts it, past <paramref name="bound"/> tokens in a
    /// window of <paramref name="size"/>, before it goes on; null where the harness cannot be asked to.
    /// </summary>
    IReadOnlyDictionary<string, string>? Compacting(long bound, long? size);

    /// <summary>
    /// Copies the conversation's own files from one account's home to another's, for the tree it runs in. Null where it is copied, else
    /// why not: <c>missing</c> where the conversation's file is not in the home it ran in, <c>copy-failed</c> where the copy did not complete.
    /// </summary>
    string? Copy(string from, string to, string tree, string conversation);
}
