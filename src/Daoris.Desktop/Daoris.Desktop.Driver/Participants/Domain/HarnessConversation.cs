namespace Daoris.Driver;

/// <summary>The harness's own conversation a session ran, as its wire named it, and the adapter that opened it (ANSWER1a).</summary>
/// <param name="Adapter">The Daoris adapter whose door opened it: a conversation is only that door's to resume.</param>
/// <param name="Conversation">The id the harness gave it: ACP's <c>sessionId</c>, or the native door's <c>session_id</c>.</param>
public sealed record HarnessConversation(string Adapter, string Conversation);
