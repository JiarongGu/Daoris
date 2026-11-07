namespace Daoris.Driver;

/// <summary>
/// Whether a limit's go-on carries its conversation to the new account, read from the door's own declaration and the context the part
/// left: whole at or under the provider's bound, compacted past it where the harness can compact, else Daoris's brief, each said by code.
/// </summary>
internal static class LimitCarry
{
    /// <summary>The door's provider declares no carry.</summary>
    public const string Unable = "unable";

    /// <summary>No conversation id was kept for the part it leaves.</summary>
    public const string Unkept = "unkept";

    /// <summary>An account it leaves or goes to is a home Daoris does not own, the tool's own.</summary>
    public const string Unhomed = "unhomed";

    /// <summary>Its context is past the bound and its harness cannot be asked to compact it.</summary>
    public const string Large = "too-large";

    /// <summary>The conversation's file is not in the home it ran in.</summary>
    public const string Missing = "missing";

    /// <summary>The copy did not complete.</summary>
    public const string Failed = "copy-failed";

    /// <summary>The carry the door's provider declares through its conversation home, or null.</summary>
    public static IConversationCarry? Declared(ProviderRegistry registry, DoorId door) =>
        registry.Provider(registry.ProviderOf(door)).Conversations.TryGet(out var home) ? home.Carry : null;

    /// <summary>The plan for the conversation, before anything is copied.</summary>
    /// <param name="used">The context the part left stood at, in tokens; null where none was said, which carries it whole.</param>
    /// <param name="size">That context's window, where said.</param>
    public static CarryPlan Plan(IConversationCarry? carry, string? conversation, string? from, string? to, long? used, long? size)
    {
        if (carry is null) return new CarryPlan.Briefed(Unable);
        if (conversation is not { Length: > 0 }) return new CarryPlan.Briefed(Unkept);
        if (from is not { Length: > 0 } || to is not { Length: > 0 }) return new CarryPlan.Briefed(Unhomed);
        if (used is not { } tokens || tokens <= carry.WholeBound) return new CarryPlan.Whole(conversation, used);
        return carry.Compacting(carry.WholeBound, size) is { Count: > 0 } variables
            ? new CarryPlan.Compacted(conversation, tokens, carry.WholeBound, variables)
            : new CarryPlan.Briefed(Large);
    }

    /// <summary>The plan once its copy is made: a carry whose copy fails is briefed, by the copy's code.</summary>
    public static CarryPlan Copied(CarryPlan plan, IConversationCarry? carry, string from, string to, string tree)
    {
        if (plan is not CarryPlan.Carried carried || carry is null) return plan;
        string? why;
        try
        {
            why = carry.Copy(from, to, tree, carried.Conversation);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            why = Failed;
        }

        return why is null ? plan : new CarryPlan.Briefed(why);
    }
}
