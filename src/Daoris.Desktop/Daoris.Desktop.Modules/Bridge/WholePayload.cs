using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// A route's whole payload, as sent, for a judge that holds the form exact (every field named once and nothing else, or a proof over
/// the bytes), so a record of fields could not tell what it was sent. A payload record names it as its only field; the page sees
/// that record as an object of anything.
/// </summary>
public readonly record struct WholePayload(JsonElement? Json);
