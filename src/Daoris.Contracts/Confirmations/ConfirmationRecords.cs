namespace Daoris.Contracts.Confirmations;

// PERSONDOOR1b (D156 point 4, design §4.2; ONEWAY1n-i): the window's confirmation of what an asker asked. A terminal cannot be told from
// a session, so a keyless call to the person's or the driver's door is refused, and its asker asks here with the exact request.

/// <summary>
/// One value the request carries: an address's (a route's parameter, then the query's, by name) or the body's, by its JSON pointer
/// (<c>/setUp/machine</c>, <c>/units/0/id</c>; <c>~0</c> and <c>~1</c> escape a name's <c>~</c> and <c>/</c>). <c>Type</c> is its JSON type
/// (<c>string</c>, <c>number</c>, <c>boolean</c>, <c>null</c>, <c>object</c>, <c>array</c>), so a string "3" and a number 3 read apart;
/// <c>Value</c> is a string as it is and anything else as its JSON. Only names the door binds are ever shown.
/// </summary>
public sealed record ConfirmationFieldResponse(string Name, string Type, string Value);

/// <summary>
/// A confirmation as the window shows it and its asker polls it: <c>State</c> is <c>waiting</c>, <c>making</c>, <c>made</c>, <c>refused</c>,
/// <c>withdrawn</c>, <c>stale</c> or <c>failed</c>; <c>Route</c> the door's pattern and <c>Act</c> its own name in the host's table;
/// <c>Values</c> the address's values and <c>Fields</c> every value of the body; <c>Session</c> the session the ask named, absent for a
/// terminal's. No clock ends one. Once it ends, <c>Ended</c>; once made, the door's <c>Status</c> and its <c>Answer</c> as text (absent past
/// 64 KB), and where the door refused or failed it, its <c>Reason</c>. Never the asker's token.
/// </summary>
public sealed record ConfirmationResponse(
    string Id, string State, string Method, string Path, string Route, string Act,
    IReadOnlyList<ConfirmationFieldResponse> Values, IReadOnlyList<ConfirmationFieldResponse> Fields, string? Body,
    DateTimeOffset Asked, string? Session, DateTimeOffset? Ended, int? Status, string? Answer, string? Reason);

/// <summary>A confirm's, a refusal's or a withdrawal's answer: the confirmation as it now stands, and a sentence the caller says.</summary>
public sealed record ConfirmationActionResponse(ConfirmationResponse Confirmation, string Message);

/// <summary>An ask's answer: the confirmation, the sentence its asker says while it waits, and the token that withdraws it, answered here alone.</summary>
public sealed record ConfirmationAskedResponse(ConfirmationResponse Confirmation, string Message, string Token);

/// <summary>
/// What waits for the person, as a held wait answers it (NOTIFY2j). <c>Since</c> is the cursor to ask from next (<c>&lt;epoch&gt;.&lt;n&gt;</c>),
/// and <c>Waiting</c> every confirmation waiting now, whole, oldest first.
/// </summary>
public sealed record ConfirmationsWaitingResponse(string Since, IReadOnlyList<ConfirmationResponse> Waiting);
