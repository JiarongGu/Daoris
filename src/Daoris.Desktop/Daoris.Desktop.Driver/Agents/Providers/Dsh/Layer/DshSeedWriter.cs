using System.Runtime.CompilerServices;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// dsh's word on a written conversation: not yet measured, so its moves are handed Daoris's brief. Read from its packages (format v4): a
/// session is a directory under its home holding a <c>session.v4.jsonl</c> log, a header (<c>version</c>, <c>id</c>, <c>createdAt</c>,
/// <c>cwd</c>, <c>isSeeded</c> with the length of the prefix it inherited) and contiguous events, each turn opened and closed
/// (<c>turn/start</c>, <c>step/start</c>, …), the model's view rebuilt from <c>user/message</c>, <c>assistant/message</c>, <c>tool/call</c> and
/// <c>tool/result</c> and the <c>request/header</c> snapshot each request appends. Its reader refuses a log with an event type it does not
/// know, and its own seeding is a fork of one of its sessions, so a log written from outside is unproven until one is loaded; Daoris also
/// declares no conversation home for it.
/// </summary>
internal sealed class DshSeedWriter : IConversationSeedWriter
{
    public static DshSeedWriter Writer { get; } = new();

    /// <inheritdoc/>
    public AgentSupport SupportFor(string doorId) => doorId == "dsh" ? AgentSupport.Unknown : AgentSupport.Unsupported;

    /// <inheritdoc/>
    public SeededConversation Write(SeedConversation conversation, string doorId, string sessionId) =>
        throw new NotSupportedException("dsh has not been measured to load a conversation written from outside it, so none is written.");

    /// <inheritdoc/>
    public SeedConversation Read(SeededConversation seeded) => throw new FormatException("No conversation is written in dsh's format.");

    [ModuleInitializer]
    internal static void Install() => ConversationSeedings.Bind(new ProviderId("dsh"), new LayerSeeding(
        new AgentProviderDescriptor { Id = "dsh", Doors = [LayerSeeding.Door("dsh", AgentDoorKinds.Acp, "the protocol's resume")] }, (_, _) => Writer));
}
